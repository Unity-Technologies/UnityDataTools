# Example Power Shell script that compare two builds (at the object level).
# It requires that you first run "UnityDataTool analyze" on each build, then pass the resulting databases to this script.
# It requires that sqlite3 is installed, in a location that is available in the PATH environmental variable.

# Note: This script is intentionally verbose for the sake of demonstration.  For very large builds you probably
# would want to hide unchanged objects, which can be achieved with a small change in the embedded SQL statement (see comparebundles.ps1).

# DISCLAIMER:
# This script is provided "as-is," without any warranty of any kind, express or implied.
# By using this script, you agree that you understand its purpose and that you use it entirely at your own risk.
# The author assumes no liability for any damages resulting from its use, misuse, or inability to use.
#
# Always review and test this script in a safe environment before applying it to a production system.

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
# Note: matching is done based on the SerializedFile, but not the AssetBundle name. In this way AssetBundles that include
# the content hash in the name can still be compared.
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
) AS o2 ON o1.object_id = o2.object_id
    AND o1.type = o2.type
    AND o1.name = o2.name
    AND o1.serialized_file = o2.serialized_file;

DETACH DATABASE db2;
"@

# Execute the query
$results = sqlite3 $db1 ".mode column" $query
$results | ForEach-Object { Write-Output $_ }
