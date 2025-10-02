# Addressables Build Report Analysis

## Overview

Unity Data Tools provides the ability to analyse Unity Addressables build reports. The tool extracts detailed information about bundles, assets, dependencies, file sizes, and build performance metrics. This information can complement extracting information from the Asset Bundles directly.

When you run the analyzer on a directory containing Addressables build reports, the tool will parse them and add the data to the sqlite database.

## Database Schema

The Addressables build data is stored across multiple related tables in the SQLite database:

## Concepts

**Builds** - a build corresponds to a content build. This can be part of a player build, or standalone through the Addressables groups window
**Bundles** - asset bundles that are output by the build. 
**Groups** - groups in the Addressable Groups window whose settings generate one or more Asset Bundles
**Schemas** - settings for groups that determine how bundles are generated
**Files** - the file in the asset bundle that contains serialized files
**SubFiles** - files that are bundled in the asset bundle, but not stored with the rest of the serialized files (resS, scene sharedAssets)
**Explicit Assets** - these are assets that have had the Addressable checkbox checked in the Editor
**Other Assets** - these are assets that are included because an explcit asset depends upon them

### Core Tables

#### `addr_builds`
Main build information table
  * id maps to build_id in other tables

#### `addr_build_groups`
Contains groups used in the build and whether they're pack separate or together.
  * guid maps to group_guid in other tables

#### `addr_build_group_schemas`
Map groups to their schemas
  * schema_rid maps to addr_group_schemas.id
  * group_id maps to addr_build_groups.id

#### `addr_build_schemas`
Contain schema names.
  * id maps to addr_group_schemas.id

#### `add_build_schema_data_pairs`
Contains key value pairs of schema settings at time of build.
  * schema_id maps to addr_build_schemas.id

#### `addr_build_bundles`
Bundle-level information including asset counts and file sizes.

#### `addr_build_bundle_dependent_bundles
Maps bundles to the bundles they depend upon (dependent bundles will be loaded as long as the bundle in question is loaded).
  * bundle_id maps to addr_build_bundles.id
  * dependent_bundle_rid maps ot addr_build_bundles.id

#### `addr_build_bundle_files`
List files in bundles. These are the serialized files and external files.
  * bundle_id maps to addr_build_bundles.id
  * file_rid maps to addr_build_files.id

### `addr_build_explicit_assets`
Explicit assets (marked as Addressable). Has Addressable name and asset information including paths. 
  * bundle maps to addr_build_bundles.id
  * group_guid maps to addr_build_groups.guid
  * file maps to addr_build_files.id

### `addr_build_explicit_asset_internal_referenced_other_assets`
Map explicit assets to other assets they refer to. For instance a prefab to its underlying FBX
  * referencing_asset_rid maps to addr_build_explicit_assets.id
  * data_from_other_asset_Id maps to addr_build_data_from_other_assets.id

### `addr_build_data_from_other_assets`
Assets added into the build implicitly by explictly defined assets.
  * file maps to addr_build_files.id

#### `addr_build_cached_bundles`
A view that contains the filename of the built bundle and the name it is stored in the Unity runtime cache.

### Basic Analysis

To analyze Addressables build reports in your project:

```bash
# Analyze all files in a directory (automatically detects Addressables JSON files)
UnityDataTools.exe "Library\com.unity.addressables\BuildReports\" -o "addressables_analysis.db"

# Include verbose output to see processing details
UnityDataTools.exe "Library\com.unity.addressables\BuildReports\" -o "addressables_analysis.db" --verbose

# Analyze only JSON files specifically
UnityDataTools.exe "C:\Temp\MyExtractedFiles" -o "addressables_analysis.db" -p "*.json"
```

You can analyze a directory with both asset bundles (*.bundle) and json files (*.json) at the same time.

### Sample Queries

Once the data is in the database, you can run queries to analyze your Addressables build:

#### Find the cache name for an addressables bundle
```sql
-- Find cache name for an addressables bundle
SELECT cached_name
FROM addr_build_cached_bundles
WHERE catalog_name = 'packedassets7_assets_all_61d3358060e969d3aad2d9c5c3a7d69b.bundle';

#### Bundle Size Analysis
```sql
-- Find largest bundles by file size
SELECT name, file_size, asset_count, compression 
FROM addr_build_bundles 
ORDER BY file_size DESC 
LIMIT 10;
```

#### Build Performance Analysis
```sql
-- Analyze build duration and asset counts
SELECT 
    a.name,
    a.duration,
    COUNT(b.id) as bundle_count,
    SUM(b.asset_count) as total_assets,
    SUM(b.file_size) as total_size
FROM addr_builds a
LEFT JOIN addr_build_bundles b ON a.rowid = b.build_id
GROUP BY a.rowid;

-- Find builds with errors
SELECT name, start_time, duration, error 
FROM addr_builds 
WHERE error IS NOT NULL AND error != '';
```

#### MonoScript Analysis
```sql
-- Analyze MonoScript distribution across files
SELECT 
    f.name,
    f.mono_script_count,
    f.mono_script_size,
    b.name as bundle_name
FROM addr_build_files f
JOIN addr_build_bundles b ON f.bundle = b.id AND f.build_id = b.build_id
WHERE f.mono_script_count > 0
ORDER BY f.mono_script_size DESC;
```
