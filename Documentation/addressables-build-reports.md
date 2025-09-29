# Addressables Build Report Analysis

## Overview

Unity Data Tools provides the ability to analyse Unity Addressables build reports. The tool extracts detailed information about bundles, assets, dependencies, file sizes, and build performance metrics. This information can complement extracting information from the Asset Bundles directly.

When you run the analyzer on a directory containing Addressables build reports, the tool will parse them and add the data to the sqlite database.

## Database Schema

The Addressables build data is stored across multiple related tables in the SQLite database:

## Concepts

**Builds** - a build is 
**Explicit Assets** - these are assets that have had the Addressable checkbox checked in the Editor



### Core Tables

#### `addr_builds`
Main build information table containing:
- Build metadata (target, start time, duration, errors)
- Unity and Addressables version information
- Build script and result hash
- Build type (new build vs. update)

#### `addr_build_bundles`
Bundle-level information including:
- Asset counts and file sizes
- Compression settings and CRC values
- Load paths and provider information
- Dependency file sizes (compressed and expanded)
- Internal name (filename used in Unity cache)
- Build status and result types

#### `addr_build_cached_bundles`
A view that contains the filename of the built bundle and the name it is stored in the Unity runtime cache.`

#### `addr_build_groups`
Addressables group configuration:
- Group names and GUIDs
- Packing mode settings
- Associated bundles and schemas

#### `addr_build_files`
File-level details within bundles:
- MonoScript counts and sizes
- Bundle object information
- Preload information sizes
- File names and result filenames

### Asset and Reference Tables

#### `addr_build_explicit_assets`
Explicit Addressable assets. These are assets that have had the Addressables checkbox checked. With:
- Asset paths and addresses
- GUID and internal ID information
- Size information (serialized and streamed)
- Asset type and labeling information

#### `addr_build_explicit_asset_labels`
Labels that have been assigned to explict addresable asses.

#### `addr_build_schemas`
Group schema configurations:
- Schema types and GUIDs
- Configuration data pairs

#### `addr_build_sub_files`
Sub-file information:
- File sizes and serialization status
- Relationship to parent files

### Relationship Tables

The system includes several junction tables to maintain relationships:
- `addr_build_bundle_dependencies`: Bundle-to-bundle dependencies
- `addr_build_bundle_files`: Bundle-to-file relationships
- `addr_build_group_bundles`: Group-to-bundle associations
- `addr_build_group_schemas`: Group-to-schema relationships
- `addr_build_explicit_asset_labels`: Asset labeling information
- `addr_build_file_assets`: File-to-asset associations

## Usage

### Basic Analysis

To analyze Addressables build reports in your project:

```bash
# Analyze all files in a directory (automatically detects Addressables JSON files)
UnityDataTools.exe "C:\MyProject\ServerData" -o "addressables_analysis.db"

# Include verbose output to see processing details
UnityDataTools.exe "C:\MyProject\ServerData" -o "addressables_analysis.db" --verbose

# Analyze only JSON files specifically
UnityDataTools.exe "C:\MyProject\ServerData" -o "addressables_analysis.db" -p "*.json"
```

You can analyze a directory with both asset bundles (*.bundle) and json files (*.json) at the same time.

### Sample Queries

Once the data is in the database, you can run queries to analyze your Addressables build:

#### Bundle Size Analysis
```sql
-- Find largest bundles by file size
SELECT name, file_size, asset_count, compression 
FROM addr_build_bundles 
ORDER BY file_size DESC 
LIMIT 10;

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

#### Asset and Dependency Analysis
```sql
-- Find assets with the most references
SELECT 
    asset_path,
    addressable_name,
    serialized_size,
    streamed_size
FROM addr_build_explicit_assets
ORDER BY (serialized_size + streamed_size) DESC
LIMIT 20;

-- Analyze bundle dependencies
SELECT 
    b1.name as bundle,
    b2.name as depends_on,
    b2.file_size as dependency_size
FROM addr_build_bundle_dependencies bd
JOIN addr_build_bundles b1 ON bd.bundle_id = b1.id AND bd.build_id = b1.build_id
JOIN addr_build_bundles b2 ON bd.dependency_rid = b2.id AND bd.build_id = b2.build_id
ORDER BY b1.name, b2.file_size DESC;
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