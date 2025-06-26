# PowerShell Script to compare the objects inside two versions of an AssetBundle
# It requires that sqlite3 is installed, in a location that is available in the PATH environmental variable.

# For the sake of brevity, the query does not list objects when they are the same in both AssetBundles.

# DISCLAIMER:
# This script is provided "as-is," without any warranty of any kind, express or implied.
# By using this script, you agree that you understand its purpose and that you use it entirely at your own risk.
# The author assumes no liability for any damages resulting from its use, misuse, or inability to use.
#
# Always review and test this script in a safe environment before applying it to a production system.


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
