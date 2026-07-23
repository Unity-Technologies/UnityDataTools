# BuildReport Support

Unity generates a [BuildReport](https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport.html) file for its common build types:

* Player builds
* AssetBundle builds via [BuildPipeline.BuildAssetBundles](https://docs.unity3d.com/ScriptReference/BuildPipeline.BuildAssetBundles.html)
* [Content directory](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/BuildPipeline.BuildContentDirectory.html) builds (Unity 6.6+), including content directories built through the Addressables package

Build reports are **not** generated for AssetBundle builds done with the [Addressables](addressables-build-reports.md) package or the Scriptable Build Pipeline. Those produce a `buildlayout.json` instead — see [Addressables Build Reports](addressables-build-reports.md).

A build report is a Unity SerializedFile (the same binary format used for build output), so UnityDataTool reads it with the same mechanisms as any other Unity object. This page focuses on **what UnityDataTool extracts** and on the **differences between Unity versions**, which the versioned [Unity Manual](https://docs.unity3d.com/6000.6/Documentation/Manual/build-history-file-reference.html#buildreport-file) intentionally does not cover.

## What a build report contains

The information in a build report depends on the build type and the Unity version. UnityDataTool imports whatever data is present, so the same commands work across all of them; the tables that get populated differ.

| Data | Player | AssetBundle | Content Directory | UnityDataTool tables |
|------|:------:|:-----------:|:-----------------:|----------------------|
| Build summary | ✓ | ✓ | ✓ | `build_reports` |
| File list | ✓ | ✓ | ✓ | `build_report_files`, `build_report_archive_contents` |
| PackedAssets (per-object breakdown) | ✓ | ✓ | ✗ | `build_report_packed_*` |
| ContentSummary (aggregate statistics) | ✓ | ✓ | ✓ | `build_report_content_*` |

All build types always include the **build summary** and **file list**, so `build_reports` and `build_report_files` are populated for every report.

### Unity version differences

**ContentSummary** is new in Unity 6.6. It provides aggregate statistics (total sizes, counts, and per-type and per-source-asset breakdowns) that are compact compared to the per-object PackedAssets data. Reports from earlier Unity versions contain no ContentSummary object, and the `build_report_content_*` tables are not created for them.

* It is **not** written for Player builds that reuse content from a previous build (for example [scripts-only builds](https://docs.unity3d.com/6000.6/Documentation/Manual/build-scripts-only.html)).
* For incremental AssetBundle builds it reflects only the AssetBundles that were rebuilt, not those reused from a previous build.

> **Tip:** For pre-6.6 reports, the [BuildReportInspector](https://github.com/Unity-Technologies/BuildReportInspector) package has a C# helper that derives ContentSummary-like statistics from PackedAssets.

**PackedAssets** behavior changed at Unity 6.6:

* Before 6.6, PackedAssets data was not written for scene files.
* From 6.6, PackedAssets covers scene content in Player and AssetBundle builds.
* Content directory builds do **not** include PackedAssets. The ContentSummary provides the aggregate view, and [`ContentLayout.json`](contentlayout-database.md) provides the source-asset-to-file mapping, so the larger per-object data was omitted to keep content directory reports small.

**Build report location** also changed at 6.6 — see [Analyzing multiple build reports](#analyzing-multiple-build-reports).

The Unity Manual's [Build report file reference](https://docs.unity3d.com/6000.6/Documentation/Manual/build-history-file-reference.html#buildreport-file) documents the full set of build report members and their availability for the current Unity version.

## What UnityDataTool extracts

The [`analyze`](command-analyze.md) command extracts build report data into dedicated database tables using custom handlers for these objects:

* **BuildReport** — the primary object, containing the build inputs and results (`build_reports`, `build_report_files`, `build_report_archive_contents`).
* **PackedAssets** — the contents of each SerializedFile, `.resS`, or `.resource` file: the type, size, and source asset for each object or resource blob, enabling object-level analysis (`build_report_packed_*`).
* **ContentSummary** — aggregate content statistics with per-type and per-source-asset breakdowns (`build_report_content_*`, Unity 6.6+).

These tables are created on demand, so a database analyzed without a build report does not contain them, and a report missing an object (for example a content directory report has no PackedAssets) does not create that object's tables. See the [Database Schema](#database-schema) for the full list and the exact creation rules.

Because a build report is a SerializedFile, you can also use [`dump`](command-dump.md) to convert its full contents to text — useful for inspecting data that `analyze` does not import (see [Information not exported](#information-not-exported)).

## Analyzing build reports

### A single build report

Pass the build report file (or a directory containing it) to `analyze`:

```bash
UnityDataTool analyze /path/to/Library/LastBuild.buildreport
```

### Cross-referencing with build output

For the most complete analysis, run `analyze` on the build output **and** the matching build report together. `analyze` accepts multiple path arguments, each a file or a directory:

```bash
UnityDataTool analyze /path/to/build/output /path/to/Library/LastBuild.buildreport
```

PackedAssets adds the source asset for each object, which is not available from the build output alone. Objects are listed in the same order they appear in the output SerializedFile, `.resS`, or `.resource` file. For Player and AssetBundle builds, use a clean build so the PackedAssets data is fully populated.

For **content directory** builds it is more important to pair the build output with its [`ContentLayout.json`](contentlayout-database.md) than with the build report, because the layout carries the source-asset mapping. Adding the build report still contributes useful summary data, but is more optional. The `--build-history` option locates and adds both files automatically:

```bash
UnityDataTool analyze /path/to/ContentDirectory --build-history /path/to/Library/BuildHistory
```

See the [`analyze` command reference](command-analyze.md) for details.

#### Database relationships

- Match `build_report_packed_assets` rows to analyzed SerializedFiles using `object_view.serialized_file` and `build_report_packed_assets.path`.
- Match `build_report_packed_asset_info` entries to objects in the build output using `object_id` (local file ID).

> **Note:** `build_report_packed_assets` also records `.resS` and `.resource` files. Those rows do not match the `serialized_files` table.

> **Note:** The source object's local file ID is not recorded in PackedAssetInfo. You can identify the source asset (for example, which Prefab), but not the specific object within that asset. When needed, objects can often be distinguished by name or other properties.

### Analyzing multiple build reports

Multiple build reports can be imported into the same database as long as their filenames differ (UnityDataTool keys SerializedFiles by filename). Pass each report, and any build output directories, as separate path arguments to a single `analyze` command. This enables build history tracking, cross-build comparisons, and finding data duplicated between Player and AssetBundle builds.

**Prior to Unity 6.6**, each build overwrites `Library/LastBuild.buildreport`. To compare builds, collect the report after each build and rename the copies so their filenames are unique, then pass them together:

```bash
UnityDataTool analyze build1.buildreport build2.buildreport
```

**From Unity 6.6**, Player and content directory builds record a structured [build history](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Build.BuildHistory.html) (default location `Library/BuildHistory`). Unity gives each build its own directory and a unique GUID-based build report filename, so no copying or renaming is needed. Run `analyze` on the whole history folder or on specific build directories:

```bash
# Analyze every build in the history
UnityDataTool analyze Library/BuildHistory

# Analyze two specific builds
UnityDataTool analyze Library/BuildHistory/20260504-153912Z-2dd7642e Library/BuildHistory/20260504-153855Z-7aff42f4
```

AssetBundle builds are not tracked in the build history; they still write only to `Library/LastBuild.buildreport`.

For more detail on the build history layout, see the Unity Manual's [Build history file reference](https://docs.unity3d.com/6000.6/Documentation/Manual/build-history-file-reference.html).

## Example queries

Run these after analyzing a build report file.

Show all successful builds recorded in the database:

```sql
SELECT * FROM build_reports WHERE build_result = 'Succeeded'
```

Show data in the build that originates from a specific source asset:

```sql
SELECT *
FROM build_report_packed_asset_contents_view
WHERE build_time_asset_path LIKE 'Assets/Sprites/Snow.jpg'
```

Show the AssetBundles that contain content from a specific source asset:

```sql
SELECT DISTINCT archive
FROM build_report_packed_asset_contents_view
WHERE build_time_asset_path LIKE 'Assets/Sprites/Snow.jpg'
```

Show all source assets included in the build, excluding C# scripts (MonoScript objects):

```sql
SELECT build_time_asset_path FROM build_report_source_assets WHERE build_time_asset_path NOT LIKE '%.cs'
```

Show the content size breakdown by type (Unity 6.6+, requires a ContentSummary):

```sql
SELECT type_name, size, object_count FROM build_report_content_type_stats_view ORDER BY size DESC
```

## Database Schema

Build report data is stored in the following tables and views:

| Name | Type | Description |
|------|------|-------------|
| `build_reports` | table | Build summary (type, result, platform, duration, etc.) |
| `build_report_files` | table | Files included in the build (path, role, size). See [BuildReport.GetFiles](https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport.GetFiles.html) |
| `build_report_archive_contents` | table | Files inside each Unity Archive (AssetBundle or ContentArchive) |
| `build_report_packed_assets` | table | SerializedFile, .resS, or .resource file info. See [PackedAssets](https://docs.unity3d.com/ScriptReference/Build.Reporting.PackedAssets.html) |
| `build_report_packed_asset_info` | table | Each object inside a SerializedFile (or data in .resS/.resource files). See [PackedAssetInfo](https://docs.unity3d.com/ScriptReference/Build.Reporting.PackedAssetInfo.html) |
| `build_report_source_assets` | table | Source asset GUID and path for each PackedAssetInfo reference |
| `build_report_content_summary` | table | Cross-build content totals (sizes and counts). See [ContentSummary](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Build.Reporting.ContentSummary.html) |
| `build_report_content_type_stats` | table | Per Unity object type: size, object count, resource count |
| `build_report_content_asset_stats` | table | Per source asset: GUID, path, size, object count, resource count |
| `build_report_files_view` | view | All files from all build reports |
| `build_report_packed_assets_view` | view | All PackedAssets with their BuildReport, Archive, and SerializedFile |
| `build_report_packed_asset_contents_view` | view | All objects and resources tracked in build reports |
| `build_report_content_summary_view` | view | Cross-build content totals with their BuildReport |
| `build_report_content_type_stats_view` | view | Per-type stats with the type name and their BuildReport |

The `build_reports` table holds the primary build information; the other tables hold detailed content data. Views simplify queries by joining tables automatically, which matters most when working with multiple build reports.

These tables and views are created on demand, so a database analyzed without any build report does not contain them:

* The `build_reports`, `build_report_files`, and `build_report_archive_contents` group (and `build_report_files_view`) is created with the first **BuildReport** object.
* The `build_report_packed_*` tables and views are created with the first **PackedAssets** object (a report with none, such as a content directory build, will not have them).
* The `build_report_content_*` tables and views are created with the first **ContentSummary** object (Unity 6.6+ only).

The new-in-6.6 `build_reports` columns (`build_name`, `build_content_options`, `build_session_guid`, `build_manifest_hash`, `build_profile_path`, `build_profile_guid`, `data_path`) are left NULL when analyzing reports from older Unity versions.

### Schema overview

Views automatically identify which build report each row belongs to, simplifying multi-report queries. To write custom queries, it helps to understand the relationships:

**Primary relationships**

- `build_reports`: One row per analyzed BuildReport file, corresponding to the BuildReport object in the `objects` table via the `id` column.
- `build_report_packed_assets`: Records the `id` of each PackedAssets object. Find the associated BuildReport via the shared `objects.serialized_file` value (PackedAssets are processed independently of BuildReport objects).
- `build_report_content_summary`: Records the `id` of the ContentSummary object. Like PackedAssets, a ContentSummary does not record which build it belongs to, so `build_report_content_summary_view` and `build_report_content_type_stats_view` resolve `build_report_id` via the BuildReport object (type 1125) in the same serialized file.

**Auxiliary tables**

- `build_report_files` and `build_report_archive_contents`: store the BuildReport object `id` for each row (as `build_report_id`).
- `build_report_packed_asset_info`: stores the PackedAssets object `id` for each row (as `packed_assets_id`).
- `build_report_source_assets`: normalized table of distinct source asset GUIDs and paths, linked via `build_report_packed_asset_info.source_asset_id`.
- `build_report_content_type_stats` and `build_report_content_asset_stats`: store the ContentSummary object `id` for each row (as `content_summary_id`).

> **Note:** BuildReport and PackedAssets objects are also linked in the `refs` table (BuildReport references PackedAssets in its appendices array), but this relationship is not used in the built-in views because `refs` population is optional.

**Example: `build_report_packed_assets_view`**

This view demonstrates the key relationships:
- Finds the BuildReport object (`br_obj`) by type (1125) and shared `serialized_file` with the PackedAssets (`pa`).
- Retrieves the serialized file name from the `serialized_files` table (`sf.name`).
- For archive-based builds (AssetBundle, ContentDirectory), retrieves the archive name from `build_report_archive_contents` by matching BuildReport ID and PackedAssets path (`archive` is NULL for Player builds).

```sql
CREATE VIEW build_report_packed_assets_view AS
SELECT
    pa.id,
    o.object_id,
    brac.archive,
    pa.path,
    pa.file_header_size,
    br_obj.id as build_report_id,
    sf.name as build_report_filename
FROM build_report_packed_assets pa
INNER JOIN objects o ON pa.id = o.id
INNER JOIN serialized_files sf ON o.serialized_file = sf.id
LEFT JOIN objects br_obj ON o.serialized_file = br_obj.serialized_file AND br_obj.type = 1125
LEFT JOIN build_report_archive_contents brac ON br_obj.id = brac.build_report_id AND pa.path = brac.archive_content;
```

### Column naming

For consistency and clarity, some database columns use different names than the BuildReport API:

| Database Column | BuildReport API | Notes |
|-----------------|-----------------|-------|
| `build_report_packed_assets.path` | `PackedAssets.ShortPath` | Filename of the SerializedFile, .resS, or .resource file ("short" was redundant since only one path is recorded) |
| `build_report_packed_assets.file_header_size` | `PackedAssets.Overhead` | Size of the file header (zero for .resS and .resource files) |
| `build_report_packed_asset_info.object_id` | `PackedAssetInfo.fileID` | Local file ID of the object (renamed for consistency with `objects.object_id`) |
| `build_report_packed_asset_info.type` | `PackedAssetInfo.classID` | Unity object type as a numeric [Class ID](https://docs.unity3d.com/Manual/ClassIDReference.html) (renamed for consistency with `objects.type`). `build_report_packed_asset_contents_view` exposes this as the type name. |

## Alternatives

UnityDataTool provides low-level access to build reports. Consider these alternatives for easier or more convenient workflows.

### BuildReportInspector package

View build reports in the Unity Editor using the [BuildReportInspector](https://github.com/Unity-Technologies/BuildReportInspector) package.

### BuildReport API

Access build report data programmatically within Unity using the BuildReport API.

**Most recent build:** use [BuildPipeline.GetLatestReport()](https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport.GetLatestReport.html).

**Build report in the Assets folder:** load via the AssetDatabase API:

```csharp
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class BuildReportInProjectUtility
{
    static public BuildReport LoadBuildReport(string buildReportPath)
    {
        var report = AssetDatabase.LoadAssetAtPath<BuildReport>("Assets/MyBuildReport.buildReport");

        if (report == null)
            Debug.LogWarning($"Failed to load build report from {buildReportPath}");

        return report;
    }
}
```

**Build report outside the Assets folder:** for files in Library or elsewhere, use `InternalEditorUtility`:

```csharp
using System;
using System.IO;
using UnityEditor.Build.Reporting;
using UnityEditorInternal;
using UnityEngine;

public class BuildReportUtility
{
    static public BuildReport LoadBuildReport(string buildReportPath)
    {
        if (!File.Exists(buildReportPath))
            return null;

        try
        {
            var objs = InternalEditorUtility.LoadSerializedFileAndForget(buildReportPath);
            foreach (UnityEngine.Object obj in objs)
            {
                if (obj is BuildReport)
                    return obj as BuildReport;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to load build report from {buildReportPath}: {ex.Message}");
        }
        return null;
    }
}
```

### Text format access

Build reports can be output in Unity's pseudo-YAML format using a diagnostic flag:

![](Diagnostics-TextBasedBuildReport.png)

Text files are significantly larger than binary. You can also convert to text by moving the binary file into your Unity project (assets default to text format). UnityDataTool's `dump` command produces a non-YAML text representation of build report contents.

* **Use text formats** for quick extraction of specific information via text-processing tools (regex, YAML parsers, etc.).
* **Use structured access** (`analyze`, or Unity's BuildReport API) for working with the full structured data.

### Addressables build reports

The Addressables package generates `buildlayout.json` files instead of BuildReport files. The format and schema differ but the information is similar. See [Addressables Build Reports](addressables-build-reports.md).

## Limitations

**Duplicate filenames:** Multiple build reports cannot be imported into the same database if they share the same filename. This is a general UnityDataTool limitation that assumes unique SerializedFile names. Build history reports (Unity 6.6+) have unique filenames (they incorporate the build session GUID), so analyzing multiple entries from a `BuildHistory` folder does not hit this issue. When analyzing multiple AssetBundle build reports, or reports from older Unity versions, assign a unique filename to each. See also issue #36.

### Information not exported

Currently only the most useful BuildReport data is extracted to SQL. Additional data may be added as needed:

* [Code stripping](https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport-strippingInfo.html) appendix (IL2CPP Player builds only)
* [ScenesUsingAssets](https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport-scenesUsingAssets.html) (detailed Player build reports only)
* `BuildReport.m_BuildSteps` array. Low priority — SQL is not an ideal representation for this hierarchical data. From Unity 6.6 the build steps are also reported in `BuildLog.jsonl` and Trace Event Profile (TEP) files, which are easy to parse.
* `BuildAssetBundleInfoSet` appendix (undocumented object listing files in each AssetBundle; `build_report_archive_contents` derives this from the file list without relying on that data)
* Analytics-only appendices (unlikely to be valuable for analysis)

> **Tip:** All of this additional BuildReport data can be viewed with the `dump` command.
