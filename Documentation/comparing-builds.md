# Comparing Builds

Each time a build is performed the output can change, based on changes in the Unity project.

A common question is "what changed", especially if more files changed that were expected.  This question arises most frequently in the area of AssetBundles, rather than the player build, so the examples here focus on the AssetBundle case.  But the same principals can apply to Player builds.

For the purpose of these examples, suppose that two builds of the same project are located side-by-side in two directories, /build1 and /build2.

## File-level comparison

A quick way to compare two builds is to do a file-level comparison, e.g. using a diff tool to compare build1 and build2.

This will quickly narrow down which AssetBundle files have changed.  But AssetBundles files are binary archive files, so this won't show what changed inside the files.

## UnityDataTool object comparison

UnityDataTools does not natively support comparing two builds, but it can be done by analyzing each build individually into separate SQLite databases, then running queries to compare the contents of the two databases.

For example suppose two database are generated as follows:


```
UnityDataTool.exe analyze -o build1.db .\Build1\
UnityDataTool.exe analyze -o build2.db .\Build2\
```

Running this PowerShell script would print all the objects, with info about whether they match between the two builds.  Objects are matched primarily based on the AssetBundle and local file ID (object_id) matching.  If there is a change in content then the CRC should change, and the size is also shown.


```
param (
    [Parameter(Mandatory=$true, HelpMessage="Path to the first UnityDataTool database")]
    [string]$db1,

    [Parameter(Mandatory=$true, HelpMessage="Path to the second UnityDataTool database")]
    [string]$db2
)

# Check if the database file exists
if (-not (Test-Path $db1)) {
    Write-Error "Database file '$db1' not found."
    exit 1
}

if (-not (Test-Path $db2)) {
    Write-Error "Database file '$db2' not found."
    exit 1
}

# SQL query to compare the content of two builds.
# Note: when the ID of an object changes then it will not be matched as the same.
$query = @"
ATTACH DATABASE '$db2' AS db2;

SELECT
    COALESCE(o1.asset_bundle, o2.asset_bundle) AS asset_bundle,
    COALESCE(o1.object_id, o2.object_id) AS object_id,
    COALESCE(o1.type, o2.type) AS type,
    COALESCE(o1.name, o2.name) AS name,
    CASE
        WHEN o1.asset_bundle IS NULL THEN 'Only in Build 1'
        WHEN o2.asset_bundle IS NULL THEN 'Only in Build 2'
        WHEN o1.crc32 != o2.crc32 OR o1.size != o2.size THEN 'Different'
        ELSE 'Same'
    END AS status,
    o1.size AS size_build1,
    o2.size AS size_build2,
    o1.crc32 AS crc32_build1,
    o2.crc32 AS crc32_build2
FROM (
    SELECT
        ab.name AS asset_bundle,
        o.object_id,
        t.name AS type,
        o.name,
        o.size,
        o.crc32,
        sf.name AS serialized_file
    FROM
        objects o
    INNER JOIN
        types t ON o.type = t.id
    INNER JOIN
        serialized_files sf ON o.serialized_file = sf.id
    LEFT JOIN
        asset_bundles ab ON sf.asset_bundle = ab.id
) AS o1
FULL OUTER JOIN (
    SELECT
        ab.name AS asset_bundle,
        o.object_id,
        t.name AS type,
        o.name,
        o.size,
        o.crc32,
        sf.name AS serialized_file
    FROM
        db2.objects o
    INNER JOIN
        db2.types t ON o.type = t.id
    INNER JOIN
        db2.serialized_files sf ON o.serialized_file = sf.id
    LEFT JOIN
        db2.asset_bundles ab ON sf.asset_bundle = ab.id
) AS o2 ON o1.asset_bundle = o2.asset_bundle
    AND o1.object_id = o2.object_id
    AND o1.type = o2.type
    AND o1.name = o2.name
    AND o1.serialized_file = o2.serialized_file;

DETACH DATABASE db2;
"@

# Execute the query
Write-Host "Objects with differences, only in one DB, or the same:"
$results = sqlite3 $db1 ".mode column" $query
$results | ForEach-Object { Write-Output $_ }
```

This is a partial example output, where the "red.png" file has been edited between builds:


```
asset_bundle    object_id             type                 name                 status     size_build1  size_build2  crc32_build1  crc32_build2
--------------  --------------------  -------------------  -------------------  ---------  -----------  -----------  ------------  ------------
AssetBundles    1                     AssetBundle                               Same       104          104          241569179     241569179
AssetBundles    2                     AssetBundleManifest  AssetBundleManifest  Different  184          184          4124235088    3102991602
audio.bundle    -1630896013228033972  AudioClip            audio                Same       18656        18656        883020518     883020518
audio.bundle    1                     AssetBundle          audio.bundle         Same       144          144          2644028121    2644028121
sprites.bundle  -4266742476527514910  Sprite               Snow 1               Same       464          464          2360191667    2360191667
sprites.bundle  -39415655269619539    Texture2D            Snow 1               Same       524496       524496       3893000759    3893000759
sprites.bundle  -3600607445234681765  Texture2D            red                  Different  152079       152079       3533099562    3115177070
sprites.bundle  -1350043613627603771  Texture2D            Snow                 Same       524492       524492       3894005184    3894005184
sprites.bundle  1                     AssetBundle          sprites.bundle       Same       460          460          245831303     245831303
```

The output pinpoints that "red" has changed. The AssetBundleManifest object also changes, because it lists AssetBundle hashes.

This script is intentionally verbose for clarity.  For very large builds you probably would want to filter down to only show the objects that have changed, and perhaps only compare individual AssetBundles rather than the whole build.


### Comparing Individual AssetBundles

A variation of the PowerShell script above could be used to more directly compare AssetBundles.  This uses temporary sqlite databases so that it becomes a one step process, for example it could be invoked like this:

```
comparebundles.ps1 .\Build1\sprites.bundle .\Build2\sprites.bundle
```

The script is as follows:

```
# PowerShell Script to compare the objects inside two versions of an AssetBundle
param(
    [Parameter(Mandatory=$true, HelpMessage="Path to the first AssetBundle")]
    [string]$FileName1,

    [Parameter(Mandatory=$true, HelpMessage="Path to the second AssetBundle")]
    [string]$FileName2
)

if (-not (Test-Path -Path $FileName1)) {
    Write-Error "File '$FileName1' does not exist."
    exit 1
}

if (-not (Test-Path -Path $FileName2)) {
    Write-Error "File '$FileName2' does not exist."
    exit 1
}

# Separate the directory and file name
$FileDir1 = Split-Path -Path $FileName1 -Parent
$FileLeaf1 = Split-Path -Path $FileName1 -Leaf

# If no directory is detected (relative file name), use the current working directory
if (-not $FileDir1) {
    $FileDir1 = "."
}

$FileDir2 = Split-Path -Path $FileName2 -Parent
$FileLeaf2 = Split-Path -Path $FileName2 -Leaf
if (-not $FileDir2) {
    $FileDir2 = "."
}

# Retrieve the system's temp folder and create the temporary database file names
$tempFolder = $env:TEMP
$dbName1 = Join-Path -Path $tempFolder -ChildPath ("1_$FileLeaf1.db")
$dbName2 = Join-Path -Path $tempFolder -ChildPath ("2_$FileLeaf2.db")

#Analyze each AssetBundle into temporary databases
UnityDataTool analyze $FileDir1 -o $dbName1 -p $FileLeaf1
UnityDataTool analyze $FileDir2 -o $dbName2 -p $FileLeaf2

$query = @"
ATTACH DATABASE '$dbName2' AS db2;

SELECT
    COALESCE(o1.serialized_file, o2.serialized_file) AS serialized_file,
    COALESCE(o1.object_id, o2.object_id) AS object_id,
    COALESCE(o1.type, o2.type) AS type,
    COALESCE(o1.name, o2.name) AS name,
    CASE
        WHEN o1.asset_bundle IS NULL THEN 'Only in Build 1'
        WHEN o2.asset_bundle IS NULL THEN 'Only in Build 2'
        ELSE 'Different'
    END AS status,
    o1.size AS size_build1,
    o2.size AS size_build2,
    o1.crc32 AS crc32_build1,
    o2.crc32 AS crc32_build2
FROM (
    SELECT
        ab.name AS asset_bundle,
        o.object_id,
        t.name AS type,
        o.name,
        o.size,
        o.crc32,
        sf.name AS serialized_file
    FROM
        objects o
    INNER JOIN
        types t ON o.type = t.id
    INNER JOIN
        serialized_files sf ON o.serialized_file = sf.id
    LEFT JOIN
        asset_bundles ab ON sf.asset_bundle = ab.id
) AS o1
FULL OUTER JOIN (
    SELECT
        ab.name AS asset_bundle,
        o.object_id,
        t.name AS type,
        o.name,
        o.size,
        o.crc32,
        sf.name AS serialized_file
    FROM
        db2.objects o
    INNER JOIN
        db2.types t ON o.type = t.id
    INNER JOIN
        db2.serialized_files sf ON o.serialized_file = sf.id
    LEFT JOIN
        db2.asset_bundles ab ON sf.asset_bundle = ab.id
) AS o2 ON o1.asset_bundle = o2.asset_bundle
    AND o1.object_id = o2.object_id
    AND o1.serialized_file = o2.serialized_file
WHERE NOT (o1.asset_bundle IS NOT NULL AND o2.asset_bundle IS NOT NULL AND o1.crc32 = o2.crc32 AND o1.size = o2.size);

DETACH DATABASE db2;
"@

# Query the database using sqlite3
sqlite3 $dbName1 ".mode column" $query

# Delete the temporary database files
Remove-Item $dbName1 -Force
Remove-Item $dbName2 -Force
```

The query above does not show objects that are the same in both AssetBundles, so that only the differences are emphasized.  And it shows the SerializedFile name instead of the AssetBundle name.  For example:


```
serialized_file                       object_id             type       name  status     size_build1  size_build2  crc32_build1  crc32_build2
------------------------------------  --------------------  ---------  ----  ---------  -----------  -----------  ------------  ------------
CAB-6b49068aebcf9d3b05692c8efd933167  -3600607445234681765  Texture2D  red   Different  152079       152079       3533099562    3115177070
```

### In depth AssetBundle comparison

The tips show above help pinpoint individual AssetBundles and objects, but do not determine "what" has changed at the object level.  To dig deeper it can be necessary to look directly into the content of the AssetBundles and the Serialized values for Unity Objects.

Although this level of comparison is quite low level, it does often give insight into exactly why an object is different, and hence why an AssetBundle has new content.  This technique is also useful in pinpointing "non-determinism" bugs, e.g. where building a project more than once produces different results, even when nothing has changed at all.

For this the **WebExtract** tool that is shipped with Unity is useful because it lets you compare the entire content of an AssetBundle.

Use the **WebExtract** tool to expand the content of the AssetBundle from one build into a directory.  Repeat the operation on the equivalent AssetBundle from the second build.  Then compare the content of those output directories used a Diff tool.

Some of the files in the output are Serialized Files, e.g. names like "CAB-6b49068aebcf9d3b05692c8efd933167" or "BuildPlayer-SampleScene.sharedAssets".  These are files containing Unity object serialized in binary format.  If they show up as different between the builds then you should convert them to text so you can compare the serialized forms of the objects.

For that you can use **binary2text** or **UnityDataTools dump**.  For example the output could be a file like "CAB-6b49068aebcf9d3b05692c8efd933167.txt".  Different versions of these files can be compared using a diff tool. 

Warning: for Shaders the text dump output is quite huge and difficult to understand.  And some objects may contain large arrays, where the change may just be some bytes changing mysteriously inside a very large list of numbers.

If the difference in an AssetBundle turns out to be a change inside a ".resS" or ".resource" file then the change has happened in the raw data for a Mesh, Texture, Audio Clip or Video Clip.  You can cross reference back to Serialized file with the same name (in text format) to find which objects own the data in those resource files.

