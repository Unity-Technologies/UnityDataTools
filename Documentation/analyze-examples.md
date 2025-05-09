# Example usage of Analyze

This topic gives some examples of using the SQLite output of the UnityDataTools Analyze command.

The command line arguments to invoke Analyze are documented [here](../UnityDataTool/README.md#analyzeanalyse).

The definition of the views, and some internal details about how Analyze is implemented, can be found [here](../Analyzer/README.md).

## Running Queries from the Command line

You can find data in the SQLite database by running SQL queries.

Graphical tools such as "DB Browser" offer a way to run these queries directly from the UI based on whatever database you have open.

However often it is useful to run queries from the command line, and to incorporate queries into your scripts (bash, PowerShell, etc).  Some of the example on this page show the command line syntax for running simple queries.

These examples assume you have `sqlite3` available in the path for your command prompt or terminal. On Windows that means that a directory containing `sqlite3.exe` is included in your PATH environmental variable.

On Windows, sqlite3.exe is available as part of the "SQLite command line tools", published from [www.sqlite.org](www.sqlite.org).

Note: Many of the examples in this topic assume that your database file is named `Analysis.db` in your current working directory.  

Disclaimer: The command line examples and scripts will need to be modified for your specific needs, and may need to be rewritten for your platform and preferred command line environment.  It is a best practice to only run commands that you fully understand from a command prompt.

## Example: Object count

Starting things simple: running the following command on a command prompt will invoke a query will print the total number of objects in the build.

```
sqlite3 Analysis.db "SELECT COUNT(*) FROM objects;"
```

## Example: Shader information

shader_view has a lot of useful information about shaders.  For example to see the list of keywords for a particular shader, try the following command.  This should work with both Powershell and Bash:
```
sqlite3 Analysis.db ".mode column" "SELECT keywords FROM shader_view WHERE name = 'Sprites/Default';"
```

Example output:

```
keywords
-------------------
PIXELSNAP_ON,
INSTANCING_ON,
ETC1_EXTERNAL_ALPHA
```

Another example query is the top 5 shaders by size.

```
sqlite3 Analysis.db ".mode column" "SELECT name, pretty_size, serialized_file FROM shader_view ORDER BY size DESC LIMIT 5;"
```

Example output (based on a build of the [megacity-metro](https://github.com/Unity-Technologies/megacity-metro) project):
```
name                                        pretty_size  serialized_file
------------------------------------------  -----------  --------------------------------
TextMeshPro/Mobile/Distance Field           191.5 KB     resources.assets
Hidden/Universal Render Pipeline/UberPost   144.3 KB     globalgamemanagers.assets
Shader Graphs/CustomLightingBuildingsB_LOD  139.6 KB     1b2fdfe013c58ffd57d7663eb8db3e60
Universal Render Pipeline/Lit               115.5 KB     1b2fdfe013c58ffd57d7663eb8db3e60
Shader Graphs/CustomLightingBuildingsB      113.4 KB     1b2fdfe013c58ffd57d7663eb8db3e60
```


## Example: Using AI tools to help write queries

This is not a tutorial on using AI tools.  However one useful tip:

Many AI tools let you provide context by uploading a file or copying text.  They are helpful for crafting SQL statements and creating scripts.  However by default they probably do not know what to expect inside a UnityDataTools SQLite database.

To provide this information you could run this command that dumps the current schema into a text file.

```
sqlite3 Analysis.db ".schema" > schema_dump.sql.txt
```

Then provide that file as context, prior to asking it to write queries based on the available tables, views and columns.  For example: *Help me write a command line calling sqlite3 for Analysis.db that will print the top 5 shaders by the size column.  It will print the name, pretty_size and serialized_file.*

## Example: Finding AssetBundles containing a certain object type

If you want to find out which AssetBundles in a build contain a certain object type you can try a query like this:

```
sqlite3 Analysis.db "SELECT DISTINCT asset_bundle FROM object_view WHERE type = 'MonoBehaviour';"
```

The above query takes advantage of the object_view which pulls together the data from multiple tables.  The following query does exactly the same thing, but uses the underlying tables directly: 

```
sqlite3 Analysis.db "SELECT DISTINCT ab.name AS asset_bundle FROM objects o INNER JOIN types t ON o.type = t.id INNER JOIN serialized_files sf ON o.serialized_file = sf.id LEFT JOIN asset_bundles ab ON sf.asset_bundle = ab.id WHERE t.name = 'MonoBehaviour';"
```

Note: Both MonoBehaviours and ScriptableObjects have the same serialized type "MonoBehaviour".


## Example: Finding instances of a scripting class

The previous example shows how to find all MonoBehaviours and ScriptableObjects.  But you may want to filter this based on the actual scripting class.  This is a bit more involved than the previous examples, so lets first breakdown the approach.

The serialized data for scripting class does not directly sort the class name, instead it stores a reference to a MonoScript.  The MonoScript in turn records the assembly, namespace and classname.

This is an example MonoScript from a `UnityDataTool dump` of a Serialized File:

```
ID: -5763254701832525334 (ClassID: 115) MonoScript
  m_Name (string) ReferencedUnityObjects
  m_ExecutionOrder (int) 0
  m_PropertiesHash (Hash128)
  ...
  m_ClassName (string) ReferencedUnityObjects
  m_Namespace (string) Unity.Scenes
  m_AssemblyName (string) Unity.Scenes
```

Currently UnityDataTool does implement custom handling for MonoScript objects, so we only have the m_Name field, which matches the m_ClassName field._ However so long as the class name is unique in your project this can be used to match against.

For example to list all distinct class names in the build you can run this query

```
SELECT DISTINCT name FROM object_view WHERE type = 'MonoScript';
```

The actual scripting objects of that type may be spread all through your AssetBundles (or Player build).  To find them we need to make use of the `refs` table, which records the references from each object to other objects.  If we find each MonoBehaviour object that references the MonoScript with the desired class name then we have found all instances of that class.

For example, to search for all instances of the class ReferencedUnityObjects we could run this query:

```
SELECT mb.asset_bundle, mb.serialized_file, mb.name, mb.object_id
FROM object_view mb
INNER JOIN refs r ON mb.id = r.object
INNER JOIN objects ms ON r.referenced_object = ms.id
WHERE mb.type = 'MonoBehaviour' 
  AND r.property_type = 'MonoScript'
  AND ms.name = 'ReferencedUnityObjects';
```

## Example: Quick summary for individual AssetBundles

Often Analyze is used for an entire build output, so that you can view information about the build output as a whole.
However it can also be used in a more light weight fashion for quickly printing information about a specific AssetBundle.
Typically the time to run analyze on a single file should be very fast, so it can be acceptable to use a temporary database and
erase it immediately after that.

This is an example if you want to look at an AssetBundle called sprites.bundle in the current working directory.

```
UnityDataTool analyze . -o sprites.bundle.db -p sprites.bundle
sqlite3 .\sprites.bundle.db ".mode column" "SELECT object_id, type, name, pretty_size, crc32 from object_view"
```

After running this sprites.bundle.db is not needed anymore, and could be erased (e.g. using a platform/shell appropriate command like "del" or "rm").

The following is an example PowerShell script that generalizes the idea:

```
# PowerShell Script to run UnityDataTool on a single AssetBundle (or single SerializedFile) and print out a summary of the objects.

param(
    [Parameter(Mandatory=$true)]
    [string]$FileName
)

if (-not (Test-Path -Path $FileName)) {
    Write-Error "File '$FileName' does not exist."
    exit 1
}

#Query to run on the temporary analyze database
$select_statement = "SELECT object_id, type, name, pretty_size, crc32 from object_view"

# Separate the directory and file name
$FileDir = Split-Path -Path $FileName -Parent
$FileLeaf = Split-Path -Path $FileName -Leaf

# If no directory is detected (relative file name), use the current working directory
if (-not $FileDir) {
    $FileDir = "."
}

# Retrieve the system's temp folder and create the temporary database file name
$tempFolder = $env:TEMP
$dbName = Join-Path -Path $tempFolder -ChildPath ("$FileLeaf.db")

try {
    # Run UnityDataTool
    UnityDataTool analyze $FileDir -o $dbName -p $FileLeaf

    # Query the database using sqlite3
    sqlite3 $dbName ".mode column" $select_statement

    # Delete the temporary database file
    Remove-Item $dbName -Force
} catch {
    Write-Error "An error occurred: $_"
}
```

Here is an example output from the script above (where the script has been saved as `objectlist.ps1` somewhere in the Path):

```
>objectlist.ps1 .\AssetBundles\sprites.bundle

...

object_id             type         name            pretty_size  crc32
--------------------  -----------  --------------  -----------  ----------
-3600607445234681765  Texture2D    red             148.5 KB     3115177070
-2408881041259534328  Sprite       Snow            460.0 B      2324949527
-1350043613627603771  Texture2D    Snow            512.2 KB     3894005184
1                     AssetBundle  sprites.bundle  332.0 B      2353941696
3866367853307903194   Sprite       red             460.0 B      1811343945
```

## Example: Matching content back to the source asset

UnityDataTool works on the output of a Unity build, which, by its very nature, only contains the crucial data needed to efficiently load built content in the Player.  So it does not include any information about the assets and scenes in the project that was used to create that build.  However you may want to match content back to the original source asset or scene.  For example if the size of an AssetBundle has unexpectedly changed between builds then you may want to track down which source assets could be responsible for that change.  Or you may want to confirm that some particular image has been included in the build.

In many cases the source asset can be inferred based on your specific knowledge of your project, and how the build was configured.  For example the level files in a Player build match the Scenes in the Build Profile Scene list.  And the content of AssetBundles is driven from the assignment of specific assets to those AssetBundles (or Addressable groups).

Also, in many cases the name of objects matches the file name of the asset.  For example the Texture2D "red" object probably comes from a file named red.png somewhere in the project.  

Similarly, it may be possible to find an object based on a distinctive property values, such as a string or hash, by doing text-based searches in the output from the `dump` command.

For more precise information about how Source Assets contribute to the build result it may be better to consult files that are produced during the build process, instead of UnityDataTools.

Examples of alternative sources of build information:

* The [BuildReport](https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport.html) has detailed source information in the PackedAssets section.  The [BuildReportInspector](https://github.com/Unity-Technologies/BuildReportInspector) is a useful way to view data from the BuildReport.
* The Editor log reports a lot of information during a build. 
* Regular AssetBundle builds create [.manifest files](https://docs.unity3d.com/Manual/assetbundles-file-format.html), which contain information about the source assets and types.
* Addressable builds do not produce BuildReport files, nor .manifest files. But there is similar reporting, for example the [Build Layout Report](https://docs.unity3d.com/Packages/com.unity.addressables@2.4/manual/BuildLayoutReport.html).

