# UnityDataTool

The UnityDataTool is a command line tool providing a set of commands related to Unity data files. It
is the main purpose of this repository.

## Running UnityDataTool

First you need to build, as described [here](../README.md#how-to-build).

Once built, the UnityDataTools executable can be found inside `UnityDataTool/bin/[target specific folders]`.

For example, on Windows the tool may be found a `[UnityDataTools repo path]\UnityDataTool\bin\Release\net9.0\UnityDataTool.exe`

>[!TIP]
>Consider adding the directory that contains the UnityDataTool executable to the "Path" environmental variable on your computer.  That makes it easy to invoke UnityDataTool from the command line and from other scripts, without specifying the full path every time.

### Instructions for running on Mac

Note that on Mac, you need to publish the UnityDataTool project to get an executable file. You
can do it from your IDE or execute this command in the UnityDataTool folder (not from the root
folder):

(On intel-based macs)  
`dotnet publish UnityDataTool -c Release -r osx-x64 -p:PublishSingleFile=true -p:UseAppHost=true`  

(On apple silicon macs)  
`dotnet publish UnityDataTool -c Release -r osx-arm64 -p:PublishSingleFile=true -p:UseAppHost=true`  

Also on Mac, you may be a warning that "UnityFileSystemApi.dylib" cannot be opened because the
developer cannot be verified.  In that case click "Cancel", then open the System Preferences -> Security &
Privacy window. You should be able to allow the file from there.


# Usage

The tool is invoked from the command line like this: `UnityDataTool [command] [command options]`

For a list of available commands run it like this: `UnityDataTool --help`

For help on a specific command use `--help` along with the command name, for example: `UnityDataTool analyze --help`


# Commands

## analyze/analyse

This command extracts information from Unity Archives (e.g. AssetBundles) and SerializedFiles and dumps the results into a SQLite database. 

The command takes the path of the folder containing the files to analyze as argument.

It also provides the following options:
* -o, --output-file \<database-filename\>: filename of the database that will be created, the
  default is database.db.  Any existing file by that name will be replaced by the data from running this command.
* -s, --skip-references: skip CRC and reference (PPtrs) extraction. Faster processing and smaller
  database, but inaccurate duplicate asset detection and no references table.
* -p, --search-pattern \<pattern\>: search pattern used to determine which files are AssetBundles.  The default is \*.  The * and ? characters are supported. Regular expressions are not supported.
* -v, --verbose: show more information during the analysis process, for example list any files that are ignored.
* --no-recurse: do not recurse into sub-directories.

Example: `UnityDataTool analyze /path/to/asset/bundles -o my_database.db -p "*.bundle"`

**Refer to this [documentation](../Analyzer/README.md#How-to-use-the-database) for more information
about the output database structure.**


### Example Input to the Analyze command

Example of directories that could be analyzed:

* The output path of an AssetBundle build.
* A folder inside the StreamingAssets folder of a Unity Player build. For example:
  * The "StreamingAssets/aa" folder, containing AssetBundles from an Addressables build.
  * The "StreamingAssets/ContentArchives" folder containing sub-scene content if your project uses [Entities](https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/content-management-intro.html).
* The "Data" folder of a Unity Player build.
  * By default, any AssetBundles or ContentArchives in the StreamingAssets folder would also be included. Use the "--no-recurse" option to avoid that.
  * Compressed Player Builds are supported. The data.unity3d file will be analyzed the same way AssetBundles are.
  * The structure and content of a Player varies based on the platform.  In some cases you may have to first extract the content out of a platform-specific container file prior to Analysis (for example .apk files on Android).

### Filtering Other File Types

Analyze works by trying to process all files in the provided path, assuming they are all Unity Archives or SerializedFiles.  Because there is no standard file extension for those files it is tricky to reliably distinguish these file types from the other files that may also be found in the build output.

This can lead to error messages in the UnityDataTool output like this: 

```
Failed to load 'C:\....\MyData.db'. File may be corrupted or was serialized with a newer version of Unity.
```

Typically these are not serious errors. The analyze process will continue, and can still produce a perfectly valid database file.  However if there are many messages like this it can obscure more important or unexpected failures.

To reduce the number of these warnings, UnityDataTool automatically ignores common filenames and file paths that are found in Player, AssetBundle, Addressable or Entities builds.  For example ".txt, .json, .manifest".  When the `--verbose` option is passed each ignored file will be listed.

If you use an extension or other naming convention for your AssetBundles, for example ".bundle", then you can avoid those warnings using the `-p .bundle` option to ignore other files.

For Player builds there is no single -p option that can catch all SerializedFiles (unless it is a compressed build with a single `data.unity3d` file).

The `--no-recurse` option can reduce the volume of these warnings.


### Errors when TypeTrees are missing

If a SerializedFile is built without TypeTrees, then the Analyze command will not be able to extract information about the contained objects.  It will print an error similar to this example, then skip to the next file:

```
Error processing file: C:\Src\TestProject\Build\Player\TestProject_Data\level0
System.ArgumentException: Invalid object id.
```

See [this topic](../Documentation/unity-content-format.md) for more information about TypeTrees.

### SQL Errors

The following SQL errors may occur when running the analyze command:

```
SQLite Error 19: 'UNIQUE constraint failed: objects.id'
```

or

```
SQLite Error 19: 'UNIQUE constraint failed: serialized_files.id'.
```

The likely cause of these errors is the same serialized file name appearing in more than Player or AssetBundle file that is processed by Analyze.

This may occur:

* If you analyze files from more than one version of the same build (e.g. if you run it on a directory that contains two different builds of the same project in separate sub-directories).
* If two scenes with the same filename (but different paths) are included in a build.
* In a build that used AssetBundle variants.

The conflicting name makes it impossible to uniquely identify the serialized file and its object in the database, and makes it ambiguous how to interpret dependencies from one file to another.

The [comparing builds](../Documentation/comparing-builds.md) topic gives some ideas about how to run Analyze more than once if you want to compare two different versions of the same build.

## dump

This command dumps the contents of a SerializedFile into a file of the selected format. It currently
only supports the 'text' format, which is similar to the binary2text output format.

The command takes the path of the file to dump as argument. It also provides the following options:
* -o, --output-path <output-path>  Output folder, default is the current folder.
* -f, --output-format <format>: output format, default is 'text'.
* -s, --skip-large-arrays: the contents of basic data type arrays with a large number of elements
  won't be dumped.
* -i, --objectid <objectid>: only dump the object with this local file id. If not specified or 0, all objects will be dumped.

Example: `UnityDataTool dump /path/to/file -o /path/to/output`

If you pass an Archive file to this command, it will dump the contents of all the SerializedFiles inside.

As an example, suppose you have a AssetBundle "scenes.bundle" that contains "SampleScene.unity" and "Scene2.unity".

Running this command:
```
UnityDataTool dump scenes.bundle
```

writes out these text dumps to the current directory:

```
BuildPlayer-SampleScene.sharedAssets.txt
BuildPlayer-SampleScene.txt
BuildPlayer-Scene2.sharedAssets.txt
BuildPlayer-Scene2.txt
```

**Refer to this [documentation](../TextDumper/README.md#How-to-interpret-the-output-files) for more
information about the contents of the output file.**

## archive

The archive command offers a set of sub-commands related to Unity archives (AssetBundles and web platform .data files).

**list** This sub-command lists the contents of an archive. It takes the archive path as argument.

**extract** This sub-command extracts the contents of an archive. This is similar to the WebExtract tool that is part of the Unity installation. 
It takes the archive path as argument and also accepts the following option:
* -o, --output-path \<path\>: Output directory of the extracted archive (default: archive)

As an example, suppose you have a AssetBundle "scenes.bundle" that contains "SampleScene.unity" and "Scene2.unity".

Running this command:
```
UnityDataTool archive extract scenes.bundle -o contents
```

write out these 4 files:

```
contents/BuildPlayer-SampleScene.sharedAssets
contents/BuildPlayer-SampleScene
contents/BuildPlayer-Scene2.sharedAssets
contents/BuildPlayer-Scene2
```

Note: When using this command the files are not transformed into text dumps, e.g. in this case they are the exact same binary SerializedFiles that are inside the AssetBundle.  See also the `dump` command.

## find-refs

> Note: this is an experimental command, it may not work as expected.

This command finds reference chains leading to specific objects. It requires a database that was
created by the 'analyze' command without the --skip-references option. It takes an object id or
name as input and will find reference chains originating from a root asset to the specified object
(s). A root asset is an asset that was explicitly added to an AssetBundle at build time. It can be
particularly useful to determine why an asset was included (and potentially more than once) in a
build.

The command takes the path of the database as argument. It also provides the following options:
* -i, --object-id \<id\>: the id of the object to analyze ('id' column in the database).
* -n, --object-name \<name\>: name of the objects to analyze (it can be useful to find the origin
  of duplicates as they will have different ids but the same name).
* -t, --object-type \<type\>: type of the objects to analyze, used to filter objects when using
  the -n option.
* -o, --output-file \<filename\>: name of the output file.
* -a, --find-all: this will force a search for all reference chains originating from the same root.
  object instead of stopping at the first one. It may take a lot more time. Note that
  either --object-id or --object-name must be provided.

Example: `UnityDataTool find-refs my_database.db -n "MyObjectName" -t "Texture2D" -o
references.txt`

**Refer to this [documentation](../ReferenceFinder/README.md#How-to-interpret-the-output-file) for
more information about the contents of the output file.**
