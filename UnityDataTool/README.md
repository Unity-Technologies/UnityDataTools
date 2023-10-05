# UnityDataTool

The UnityDataTool is a command line tool providing a set of commands related to Unity data files. It
is the main project of this repository and produces an executable file that can be found in
UnityDataTool/bin/[target specific folders] after a build.

> Note that on Mac, you need to publish the UnityDataTool project to get an executable file. You
can do it from your IDE or execute this command in the UnityDataTool folder (not from the root
folder):
>
> `dotnet publish -c Release -r osx-x64 -p:PublishSingleFile=true -p:UseAppHost=true`
>
> Also on Mac, if you get a warning because "UnityFileSystemApi.dylib" cannot be opened because the
developer cannot be verified, click "Cancel" and then open the System Preferences -> Security &
Privacy window. You should be able to allow the file from there.

The tool is invoked from the command line like this:

`UnityDataTool [command] [command options]`

# Commands

## analyze/analyse

This command extracts information from AssetBundles and SerializedFiles and dumps the results
into a SQLite database. The files must include a TypeTree, otherwise it will fail. It is an
improved version of the [AssetBundle Analyzer](https://github.com/faelenor/asset-bundle-analyzer).

The command takes the path of the folder containing the files to analyze as argument. It also
provides the following options:
* -o, --output-file \<database-filename\>: filename of the database that will be created, the
  default is database.db.
* -s, --skip-references: skip CRC and reference (PPtrs) extraction. Faster processing and smaller
  database, but inaccurate duplicate asset detection and no references table.
* -p, --search-pattern \<pattern\>: search pattern used to determine which files are asset bundles,
  the default is \*.

Example: `UnityDataTool analyze /path/to/asset/bundles -o my_database.db -p *.ab`

**Refer to this [documentation](../Analyzer/README.md#How-to-use-the-database) for more information
about the output database structure.**

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

## dump

This command dumps the contents of a SerializedFile into a file of the selected format. It currently
only supports the 'text' format, which is similar to the binary2text output format.

The command takes the path of the file to dump as argument. It also provides the following options:
* -f, --output-format \<format\>: output format, default is 'text'.
* -s, --skip-large-arrays: the contents of basic data type arrays with a large number of elements
  won't be dumped.

Example: `UnityDataTool dump /path/to/file`

**Refer to this [documentation](../TextDumper/README.md#How-to-interpret-the-output-files) for more
information about the contents of the output file.**

## archive

The archive command offers a set of sub-commands related to Unity archives (AssetBundles and web platform .data files).

**extract** This sub-command extracts the contents of an archive. It takes the archive path as
argument and also provides the following option:
* -o, --output-path \<path\>: Output directory of the extracted archive (default: archive)

**list** This sub-command lists the contents of an archive. It takes the archive path as argument.
