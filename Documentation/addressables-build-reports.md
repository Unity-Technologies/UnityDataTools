# Addressables Build Report Analysis

## Overview

Unity Data Tools provides the ability to analyse Unity Addressables build reports. The tool extracts detailed information about bundles, assets, dependencies, file sizes, and build performance metrics. This information can complement extracting information from the Asset Bundles directly.

When you run the analyzer on a directory containing Addressables build reports, the tool will parse them and add the data to the sqlite database.

Each build report is generated a unique id that is used as the id in addressables_builds and build_id field in subsequent tables. This allows you to compare builds against each other.

## Concepts

**Builds**
: a build corresponds to a content build. This can be part of a player build, or standalone through the Addressables groups window

**Bundles**
: asset bundles that are output by the build. 

**Groups**
: groups in the Addressable Groups window whose settings generate one or more Asset Bundles

**Schemas**
: settings for groups that determine how bundles are generated

**Files**
: the file in the asset bundle that contains serialized files

**SubFiles**
: files that are bundled in the asset bundle, but not stored with the rest of the serialized files (resS, scene sharedAssets)

**Explicit Assets**
: these are assets that have had the Addressable checkbox checked in the Editor

**Other Assets**
: these are assets that are included because an explicit asset depends upon them

## Database Schema

The Addressables build data is stored across multiple related tables in the SQLite database prefixed with addressables_.


### Core Tables

#### `addressables_builds`
Main build information table
  * id maps to build_id in other tables

#### `addressables_build_groups`
Contains groups used in the build and whether they're pack separate or together.
  * guid maps to group_guid in other tables

#### `addressables_build_group_schemas`
Map groups to their schemas
  * schema_rid maps to addressables_group_schemas.id
  * group_id maps to addressables_build_groups.id

#### `addressables_build_schemas`
Contain schema names.
  * id maps to addressables_group_schemas.id

#### `add_build_schema_data_pairs`
Contains key value pairs of schema settings at time of build.
  * schema_id maps to addressables_build_schemas.id

#### `addressables_build_bundles`
Bundle-level information including asset counts and file sizes.

#### `addressables_build_bundle_dependent_bundles`
Maps bundles to the bundles they depend upon (dependent bundles will be loaded as long as the bundle in question is loaded).
  * bundle_id maps to addressables_build_bundles.id
  * dependent_bundle_rid maps to addressables_build_bundles.id

#### `addressables_build_bundle_files`
List files in bundles. These are the serialized files and external files.
  * bundle_id maps to addressables_build_bundles.id
  * file_rid maps to addressables_build_files.id

#### `addressables_build_explicit_assets`
Explicit assets (marked as Addressable). Has Addressable name and asset information including paths. 
  * bundle maps to addressables_build_bundles.id
  * group_guid maps to addressables_build_groups.guid
  * file maps to addressables_build_files.id

#### `addressables_build_explicit_asset_internal_referenced_other_assets`
Map explicit assets to other assets they refer to. For instance a prefab to its underlying FBX
  * referencing_asset_rid maps to addressables_build_explicit_assets.id
  * data_from_other_asset_Id maps to addressables_build_data_from_other_assets.id

#### `addressables_build_data_from_other_assets`
Assets added into the build implicitly by explictly defined assets.
  * file maps to addressables_build_files.id

#### `addressables_build_cached_bundles`
A view that contains the filename of the built bundle and the name it is stored in the Unity runtime cache.

### Basic Analysis

To analyze Addressables build reports in your project:

```bash
# Analyze all files in a directory (automatically detects Addressables JSON files)
UnityDataTools.exe "Library\\com.unity.addressables\\BuildReports\\" -o "addressables_analysis.db"

# Include verbose output to see processing details
UnityDataTools.exe "Library\\com.unity.addressables\\BuildReports\\" -o "addressables_analysis.db" --verbose

# Analyze only JSON files specifically
UnityDataTools.exe "C:\\Temp\\MyExtractedFiles" -o "addressables_analysis.db" -p "*.json"
```

You can analyze a directory with both asset bundles (*.bundle) and json files (*.json) at the same time.

### Sample Queries

Once the data is in the database, you can run queries to analyze your Addressables build:

#### Find the cache name for an addressables bundle
Addressables renames bundles to make it possible to do content updates. Internally bundles are still named by their internal hash and are cached based upon this name. If you want to lookup how a remote bundle will be cached in Unity's [cache](https://docs.unity3d.com/ScriptReference/Caching.html) you can use the addressables_build_cached_bundles view.
```sql
-- Find cache name for an addressables bundle
SELECT cached_name
  FROM addressables_build_cached_bundles
 WHERE name_in_catalog = 'sharedenvironment_assets_all_5935f9c20c9b10664721f1591e3d2036.bundle'
   AND build_id = 1;
```

#### Bundle Size Analysis
This query works for groups that are packed together. It assumes group guids stay the same between builds and compares the bundle file size for a group between builds.
```sql
-- Compare bundle size changes between builds
SELECT second_build.name as bundle_name, second_build.file_size as second_build_file_size, second_build.asset_count as second_build_asset_count, 
first_build.file_size as first_build_File_size, first_build.asset_count as first_build_asset_count
FROM addressables_build_bundles first_build,
     addressables_build_bundles second_build,
     addressables_build_groups g1,
     addressables_build_groups g2
 WHERE second_build.file_size > first_build.file_size
   AND first_build.group_rid = g1.id
   AND second_build.group_rid = g2.id
   AND g1.guid = g2.guid
   AND first_build.build_id = 1
   AND second_build.build_id = 2
   AND g1.build_id = first_build.build_id
   AND g2.build_id = second_build.build_id
   AND second_build.file_size != first_build.file_size
 ORDER BY second_build.file_size DESC;
```

#### Build Performance Analysis
```sql
-- Analyze build duration and asset counts for successful builds
SELECT 
    a.id,
    a.name,
    a.duration,
    COUNT(b.id) as bundle_count,
    SUM(b.asset_count) as total_assets,
    SUM(b.file_size) as total_size
FROM addressables_builds a
LEFT JOIN addressables_build_bundles b ON a.id = b.build_id
WHERE coalesce(a.error, '') = ''
group by a.id;

-- Find builds with errors
SELECT name, start_time, duration, error 
FROM addressables_builds 
WHERE coalesce(error, '') != '';
```