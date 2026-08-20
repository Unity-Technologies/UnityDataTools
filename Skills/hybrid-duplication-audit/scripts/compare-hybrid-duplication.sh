#!/usr/bin/env bash
#
# Finds source assets duplicated across the AssetBundle / ContentDirectory boundary of a
# hybrid Addressables 4.x build (some groups build to .bundle files, others build through
# BuildPipeline.BuildContentDirectory / ContentDirectoryGroupSchema).
#
# Linux/macOS counterpart of Compare-HybridDuplication.ps1 -- same behavior, same queries.
# Requires UnityDataTool to be built (see ../../../README.md, "How to Build") and sqlite3 on
# PATH, exactly like the PowerShell version.
#
# See ../SKILL.md for the concept, the query this script runs, and how to read the output.
#
# DISCLAIMER:
# This script is provided "as-is," without any warranty of any kind, express or implied.
# By using this script, you agree that you understand its purpose and that you use it entirely
# at your own risk. Always review and test this script in a safe environment before applying it
# to a production system.

set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

project_root="."
tool_path=""
build_layout=""
content_directory=""
build_history=""
database=""
keep_database=0
max_rows=50

usage() {
    cat <<'EOF'
Usage: compare-hybrid-duplication.sh [options]

  --project-root <path>        Root of the Unity project to audit (default: .)
  --tool-path <path>           Path to the UnityDataTool executable
  --build-layout <path>        Addressables build layout report to read
                                (default: Library/com.unity.addressables/buildlayout.json)
  --content-directory <path>   Content directory build output folder
                                (default: auto-detected under Library/com.unity.addressables/aa)
  --build-history <path>       Project's build history folder
                                (default: Library/BuildHistory)
  --database <path>            Output database path (default: a temp file, deleted afterwards)
  --keep-database              Keep the generated database instead of deleting it
  --max-rows <n>                Maximum number of duplicate rows to print (default: 50)
  -h, --help                   Show this help
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --project-root) project_root="$2"; shift 2 ;;
        --tool-path) tool_path="$2"; shift 2 ;;
        --build-layout) build_layout="$2"; shift 2 ;;
        --content-directory) content_directory="$2"; shift 2 ;;
        --build-history) build_history="$2"; shift 2 ;;
        --database) database="$2"; shift 2 ;;
        --keep-database) keep_database=1; shift ;;
        --max-rows) max_rows="$2"; shift 2 ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown option: $1" >&2; usage >&2; exit 1 ;;
    esac
done

add_commas() {
    printf "%s" "$1" | sed -E ':a;s/(^[-]?[0-9]+)([0-9]{3})/\1,\2/;ta'
}

# Resolve the UnityDataTool executable: explicit flag -> UNITYDATATOOL_PATH -> PATH -> this
# checkout's own build output (only valid while the skill is still inside a UnityDataTools
# clone; once copied into another project's .claude/skills, pass --tool-path or set
# UNITYDATATOOL_PATH instead).
if [[ -z "$tool_path" ]]; then
    if [[ -n "${UNITYDATATOOL_PATH:-}" ]]; then
        tool_path="$UNITYDATATOOL_PATH"
    elif command -v UnityDataTool >/dev/null 2>&1; then
        tool_path="$(command -v UnityDataTool)"
    elif [[ -x "$script_dir/../../../UnityDataTool/bin/Release/net9.0/UnityDataTool" ]]; then
        tool_path="$script_dir/../../../UnityDataTool/bin/Release/net9.0/UnityDataTool"
    fi
fi

if [[ -z "$tool_path" || ! -x "$tool_path" ]]; then
    echo "Error: UnityDataTool executable not found. Tried: --tool-path, UNITYDATATOOL_PATH, PATH, and this checkout's own build output. Build it with 'dotnet build -c Release' or pass --tool-path explicitly." >&2
    exit 1
fi

if [[ ! -d "$project_root" ]]; then
    echo "Error: Project root '$project_root' not found." >&2
    exit 1
fi
project_root="$(cd "$project_root" && pwd)"

if [[ -z "$build_layout" ]]; then
    build_layout="$project_root/Library/com.unity.addressables/buildlayout.json"
fi
if [[ ! -f "$build_layout" ]]; then
    echo "Error: Addressables build layout report not found at '$build_layout'. Build the project with Addressables first, or pass --build-layout explicitly." >&2
    exit 1
fi

if [[ -z "$content_directory" ]]; then
    candidates=()
    if [[ -d "$project_root/Library/com.unity.addressables/aa" ]]; then
        while IFS= read -r -d '' manifest_hash_file; do
            candidates+=("$(dirname "$manifest_hash_file")")
        done < <(find "$project_root/Library/com.unity.addressables/aa" -name "BuildManifestHash.txt" -print0 2>/dev/null)
    fi

    if [[ ${#candidates[@]} -eq 0 ]]; then
        echo "Error: No content directory build output found under Library/com.unity.addressables/aa. Pass --content-directory explicitly, or this project may not have a ContentDirectoryGroupSchema group." >&2
        exit 1
    fi
    if [[ ${#candidates[@]} -gt 1 ]]; then
        echo "Error: More than one content directory build output found (multiple platforms built?): ${candidates[*]}. Pass --content-directory to pick one." >&2
        exit 1
    fi
    content_directory="${candidates[0]}"
fi
if [[ ! -d "$content_directory" ]]; then
    echo "Error: Content directory build output '$content_directory' not found." >&2
    exit 1
fi

if [[ -z "$build_history" ]]; then
    build_history="$project_root/Library/BuildHistory"
fi
if [[ ! -d "$build_history" ]]; then
    echo "Error: Build history folder '$build_history' not found. It is required to pair the content directory output with its ContentLayout.json." >&2
    exit 1
fi

delete_database_after=0
if [[ -z "$database" ]]; then
    database="$(mktemp -u "${TMPDIR:-/tmp}/hybrid-duplication-XXXXXX.db")"
    if [[ "$keep_database" -eq 0 ]]; then
        delete_database_after=1
    fi
fi
cleanup() {
    if [[ "$delete_database_after" -eq 1 ]]; then
        rm -f "$database"
    fi
}
trap cleanup EXIT

echo "Analyzing:"
echo "  Build layout:      $build_layout"
echo "  Content directory: $content_directory"
echo "  Build history:     $build_history"
echo ""

"$tool_path" analyze "$build_layout" "$content_directory" --build-history "$build_history" -o "$database"

# Hybrid check: both a bundle-producing group and a content-directory build must be present,
# otherwise the intersection below is meaningless.
bundle_count="$(sqlite3 "$database" "SELECT COUNT(*) FROM addressables_build_bundles;")"
has_layout_table="$(sqlite3 "$database" "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='content_layout_source_assets';")"
source_asset_count=0
if [[ "$has_layout_table" -gt 0 ]]; then
    source_asset_count="$(sqlite3 "$database" "SELECT COUNT(*) FROM content_layout_source_assets;")"
fi

if [[ "$bundle_count" -eq 0 || "$source_asset_count" -eq 0 ]]; then
    echo "This is not a hybrid build:"
    echo "  AssetBundle groups in the build layout: $bundle_count"
    echo "  Source assets in the content directory: $source_asset_count"
    echo ""
    echo "Both must be non-zero to audit cross-boundary duplication."
    exit 2
fi

# The one query that answers "which source assets were built into both forms": bundle-side
# asset paths (explicit + implicit) intersected with the content directory's source assets.
# Matching by asset_path, not by object CRC, is what makes this catch cases a plain CRC diff
# (e.g. view_potential_duplicates, see Documentation/analyzer.md) would miss -- the same
# source asset can be built with a different variant set on each side, so the two copies do
# not have the same CRC despite being duplicates of the same asset.
duplicates_query="
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
LIMIT $max_rows;
"

totals_query="
WITH bundle_assets AS (
    SELECT serialized_size + streamed_size AS bytes FROM addressables_build_explicit_assets
    UNION ALL
    SELECT serialized_size + streamed_size AS bytes FROM addressables_build_data_from_other_assets
)
SELECT SUM(bytes) FROM bundle_assets;
"

content_dir_total_query="SELECT SUM(size) FROM content_layout_binary_artifacts WHERE category != 'manifest';"

duplicate_bytes_query="
WITH bundle_assets AS (
    SELECT asset_path, serialized_size + streamed_size AS bytes FROM addressables_build_explicit_assets
    UNION ALL
    SELECT asset_path, serialized_size + streamed_size AS bytes FROM addressables_build_data_from_other_assets
)
SELECT COALESCE(SUM(bytes), 0)
FROM bundle_assets
WHERE asset_path IN (SELECT DISTINCT asset_path FROM content_layout_source_assets);
"

bundle_total_bytes="$(sqlite3 "$database" "$totals_query")"
content_dir_total_bytes="$(sqlite3 "$database" "$content_dir_total_query")"
duplicate_bytes="$(sqlite3 "$database" "$duplicate_bytes_query")"

declared_duplicate_count="$(grep -o '"DuplicatedAssetCount"[[:space:]]*:[[:space:]]*-\{0,1\}[0-9]*' "$build_layout" | grep -o -- '-\{0,1\}[0-9]*$' || true)"

echo "=== Cross-boundary duplicates (built into both a bundle and the content directory) ==="
sqlite3 "$database" ".mode column" ".headers on" "$duplicates_query"
echo ""

echo "=== Summary ==="
printf "Bundle-side asset payload:      %15s bytes\n" "$(add_commas "$bundle_total_bytes")"
printf "Content directory payload:      %15s bytes\n" "$(add_commas "$content_dir_total_bytes")"
printf "Cross-boundary duplicated:      %15s bytes\n" "$(add_commas "$duplicate_bytes")"
if [[ "$bundle_total_bytes" -gt 0 ]]; then
    pct="$(awk -v d="$duplicate_bytes" -v t="$bundle_total_bytes" 'BEGIN { printf "%.1f", (100.0 * d / t) }')"
    echo "  = ${pct}% of the bundle-side payload"
fi
if [[ -n "$declared_duplicate_count" ]]; then
    echo ""
    echo "Addressables' own DuplicatedAssetCount for this build: $declared_duplicate_count"
    echo "(That count only scans AssetBundle-to-AssetBundle duplication -- it cannot see a copy that lives in the content directory, so it will read low or zero even when the total above is not.)"
fi

if [[ "$delete_database_after" -eq 0 ]]; then
    echo ""
    echo "Database kept at: $database"
fi
