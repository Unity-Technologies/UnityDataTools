# Finds source assets duplicated across the AssetBundle / ContentDirectory boundary of a
# hybrid Addressables 4.x build (some groups build to .bundle files, others build through
# BuildPipeline.BuildContentDirectory / ContentDirectoryGroupSchema).
#
# It requires that UnityDataTool has been built (see ../../../README.md, "How to Build") and
# that sqlite3 is installed and available on PATH, exactly like Scripts/comparebuilds.ps1 and
# Scripts/comparebundles.ps1 in this repo.
#
# See ../SKILL.md for the concept, the query this script runs, and how to read the output.
#
# DISCLAIMER:
# This script is provided "as-is," without any warranty of any kind, express or implied.
# By using this script, you agree that you understand its purpose and that you use it entirely
# at your own risk. Always review and test this script in a safe environment before applying it
# to a production system.

param (
    [Parameter(HelpMessage = "Root of the Unity project to audit")]
    [string]$ProjectRoot = ".",

    [Parameter(HelpMessage = "Path to UnityDataTool.exe. Defaults to UNITYDATATOOL_PATH, then PATH, then the build output of this checkout")]
    [string]$ToolPath,

    [Parameter(HelpMessage = "Addressables build layout report to read. Defaults to the mirrored Library/com.unity.addressables/buildlayout.json, which Addressables keeps in sync with its latest build")]
    [string]$BuildLayout,

    [Parameter(HelpMessage = "Content directory build output folder (the one containing BuildManifestHash.txt). Auto-detected under Library/com.unity.addressables/aa when there is exactly one")]
    [string]$ContentDirectory,

    [Parameter(HelpMessage = "Project's build history folder")]
    [string]$BuildHistory,

    [Parameter(HelpMessage = "Output database path. Defaults to a temp file, deleted afterwards unless -KeepDatabase is set")]
    [string]$Database,

    [Parameter(HelpMessage = "Keep the generated database instead of deleting it, for follow-up queries")]
    [switch]$KeepDatabase,

    [Parameter(HelpMessage = "Maximum number of duplicate rows to print")]
    [int]$MaxRows = 50
)

function Resolve-ToolPath {
    param([string]$Explicit)

    if ($Explicit) { return $Explicit }
    if ($env:UNITYDATATOOL_PATH) { return $env:UNITYDATATOOL_PATH }

    $onPath = Get-Command "UnityDataTool" -ErrorAction SilentlyContinue
    if (-not $onPath) { $onPath = Get-Command "UnityDataTool.exe" -ErrorAction SilentlyContinue }
    if ($onPath) { return $onPath.Source }

    # Fall back to this checkout's own build output (only valid while the skill is still
    # inside a UnityDataTools clone; once copied into another project's .claude/skills, pass
    # -ToolPath or set UNITYDATATOOL_PATH instead).
    $repoRelative = Join-Path $PSScriptRoot "..\..\..\UnityDataTool\bin\Release\net9.0\UnityDataTool.exe"
    if (Test-Path $repoRelative) { return (Resolve-Path $repoRelative).Path }

    return $null
}

$resolvedToolPath = Resolve-ToolPath -Explicit $ToolPath
if (-not $resolvedToolPath -or -not (Test-Path $resolvedToolPath)) {
    Write-Error "UnityDataTool executable not found. Tried: -ToolPath, UNITYDATATOOL_PATH, PATH, and this checkout's own build output. Build it with 'dotnet build -c Release' or pass -ToolPath explicitly."
    exit 1
}

if (-not (Test-Path $ProjectRoot)) {
    Write-Error "Project root '$ProjectRoot' not found."
    exit 1
}
$ProjectRoot = (Resolve-Path $ProjectRoot).Path

if (-not $BuildLayout) {
    $BuildLayout = Join-Path $ProjectRoot "Library\com.unity.addressables\buildlayout.json"
}
if (-not (Test-Path $BuildLayout)) {
    Write-Error "Addressables build layout report not found at '$BuildLayout'. Build the project with Addressables first, or pass -BuildLayout explicitly."
    exit 1
}

if (-not $ContentDirectory) {
    $candidates = @(Get-ChildItem -Path (Join-Path $ProjectRoot "Library\com.unity.addressables\aa") `
        -Recurse -Filter "BuildManifestHash.txt" -ErrorAction SilentlyContinue |
        ForEach-Object { $_.Directory.FullName })

    if ($candidates.Count -eq 0) {
        Write-Error "No content directory build output found under Library/com.unity.addressables/aa. Pass -ContentDirectory explicitly, or this project may not have a ContentDirectoryGroupSchema group."
        exit 1
    }
    if ($candidates.Count -gt 1) {
        Write-Error "More than one content directory build output found (multiple platforms built?): $($candidates -join ', '). Pass -ContentDirectory to pick one."
        exit 1
    }
    $ContentDirectory = $candidates[0]
}
if (-not (Test-Path $ContentDirectory)) {
    Write-Error "Content directory build output '$ContentDirectory' not found."
    exit 1
}

if (-not $BuildHistory) {
    $BuildHistory = Join-Path $ProjectRoot "Library\BuildHistory"
}
if (-not (Test-Path $BuildHistory)) {
    Write-Error "Build history folder '$BuildHistory' not found. It is required to pair the content directory output with its ContentLayout.json."
    exit 1
}

$deleteDatabaseAfter = $false
if (-not $Database) {
    $Database = Join-Path ([System.IO.Path]::GetTempPath()) "hybrid-duplication-$([guid]::NewGuid()).db"
    $deleteDatabaseAfter = -not $KeepDatabase
}

Write-Output "Analyzing:"
Write-Output "  Build layout:      $BuildLayout"
Write-Output "  Content directory: $ContentDirectory"
Write-Output "  Build history:     $BuildHistory"
Write-Output ""

& $resolvedToolPath analyze $BuildLayout $ContentDirectory --build-history $BuildHistory -o $Database
if ($LASTEXITCODE -ne 0) {
    Write-Error "UnityDataTool analyze failed (exit code $LASTEXITCODE)."
    exit $LASTEXITCODE
}

# Hybrid check: both a bundle-producing group and a content-directory build must be present,
# otherwise the intersection below is meaningless.
$bundleCount = [int](sqlite3 $Database "SELECT COUNT(*) FROM addressables_build_bundles;")
$hasLayoutTable = [int](sqlite3 $Database "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='content_layout_source_assets';")
$sourceAssetCount = 0
if ($hasLayoutTable -gt 0) {
    $sourceAssetCount = [int](sqlite3 $Database "SELECT COUNT(*) FROM content_layout_source_assets;")
}

if ($bundleCount -eq 0 -or $sourceAssetCount -eq 0) {
    Write-Output "This is not a hybrid build:"
    Write-Output "  AssetBundle groups in the build layout: $bundleCount"
    Write-Output "  Source assets in the content directory: $sourceAssetCount"
    Write-Output ""
    Write-Output "Both must be non-zero to audit cross-boundary duplication."
    if ($deleteDatabaseAfter) { Remove-Item $Database -ErrorAction SilentlyContinue }
    exit 2
}

# The one query that answers "which source assets were built into both forms": bundle-side
# asset paths (explicit + implicit) intersected with the content directory's source assets.
# Matching by asset_path, not by object CRC, is what makes this catch cases a plain CRC diff
# (e.g. view_potential_duplicates, see Documentation/analyzer.md) would miss -- the same
# source asset can be built with a different variant set on each side, so the two copies do
# not have the same CRC despite being duplicates of the same asset.
$duplicatesQuery = @"
WITH bundle_assets AS (
    SELECT asset_path, serialized_size + streamed_size AS bytes
    FROM addressables_build_explicit_assets
    UNION ALL
    SELECT asset_path, serialized_size + streamed_size AS bytes
    FROM addressables_build_data_from_other_assets
),
content_dir_assets AS (
    SELECT DISTINCT asset_path FROM content_layout_source_assets
)
SELECT b.asset_path, SUM(b.bytes) AS bundle_bytes, COUNT(*) AS instances
FROM bundle_assets b
WHERE b.asset_path IN (SELECT asset_path FROM content_dir_assets)
GROUP BY b.asset_path
ORDER BY bundle_bytes DESC
LIMIT $MaxRows;
"@

$totalsQuery = @"
WITH bundle_assets AS (
    SELECT serialized_size + streamed_size AS bytes FROM addressables_build_explicit_assets
    UNION ALL
    SELECT serialized_size + streamed_size AS bytes FROM addressables_build_data_from_other_assets
)
SELECT SUM(bytes) FROM bundle_assets;
"@

$contentDirTotalQuery = "SELECT SUM(size) FROM content_layout_binary_artifacts WHERE category != 'manifest';"

$duplicateBytesQuery = @"
WITH bundle_assets AS (
    SELECT asset_path, serialized_size + streamed_size AS bytes FROM addressables_build_explicit_assets
    UNION ALL
    SELECT asset_path, serialized_size + streamed_size AS bytes FROM addressables_build_data_from_other_assets
)
SELECT COALESCE(SUM(bytes), 0)
FROM bundle_assets
WHERE asset_path IN (SELECT DISTINCT asset_path FROM content_layout_source_assets);
"@

$bundleTotalBytes = [int64](sqlite3 $Database $totalsQuery)
$contentDirTotalBytes = [int64](sqlite3 $Database $contentDirTotalQuery)
$duplicateBytes = [int64](sqlite3 $Database $duplicateBytesQuery)

$declaredDuplicateCount = $null
try {
    $layoutJson = Get-Content $BuildLayout -Raw | ConvertFrom-Json
    $declaredDuplicateCount = $layoutJson.DuplicatedAssetCount
} catch {
    Write-Output "(could not read DuplicatedAssetCount from '$BuildLayout': $_)"
}

Write-Output "=== Cross-boundary duplicates (built into both a bundle and the content directory) ==="
sqlite3 $Database ".mode column" ".headers on" $duplicatesQuery
Write-Output ""

Write-Output "=== Summary ==="
Write-Output ("Bundle-side asset payload:      {0,15:N0} bytes" -f $bundleTotalBytes)
Write-Output ("Content directory payload:      {0,15:N0} bytes" -f $contentDirTotalBytes)
Write-Output ("Cross-boundary duplicated:      {0,15:N0} bytes" -f $duplicateBytes)
if ($bundleTotalBytes -gt 0) {
    $pct = [math]::Round(100.0 * $duplicateBytes / $bundleTotalBytes, 1)
    Write-Output "  = $pct% of the bundle-side payload"
}
if ($null -ne $declaredDuplicateCount) {
    Write-Output ""
    Write-Output "Addressables' own DuplicatedAssetCount for this build: $declaredDuplicateCount"
    Write-Output "(That count only scans AssetBundle-to-AssetBundle duplication -- it cannot see a copy that lives in the content directory, so it will read low or zero even when the total above is not.)"
}

if ($deleteDatabaseAfter) {
    Remove-Item $Database -ErrorAction SilentlyContinue
} else {
    Write-Output ""
    Write-Output "Database kept at: $Database"
}
