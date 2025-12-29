# BuildReport support

The [BuildReport](https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport.html) file is written by Player builds.  It is also written when AssetBundles are built using [BuildPipeline.BuildAssetBundle](https://docs.unity3d.com/ScriptReference/BuildPipeline.BuildAssetBundles.html).  However it is not written by the [Addressables](addressables-build-reports.md) package, nor builds make with the Scriptable Build Pipeline package.

This file is currently written to `Library/LastBuild.buildReport`.  By default this is a Unity binary serialized file, the same format that Unity uses for build output.  Because UnityDataTool supports reading this format it can read this file and extract information about the build results, using the same mechanisms used for other Unity object types.

## UnityDataTool support

Because it is a SerializedFile you can [`dump`](command-dump.md) it into text format.

The [`analyze`](command-analyze.md) command now has custom support for BuildReport files.  When you analyze a BuildReport file it will extract information into dedicated tables in the output database.

Specifically there are now custom handlers for the following types:

* **BuildReport** - The primary object that reports the inputs and results for a Unity build.
* **PackedAssets** - An object that describes the contents of a specific Serialized file, or resource file.  It records information such as the type, size and source asset for each object or resource blob.  This makes it possible to do analysis of the content down to the object level.  Note: the PackedAsset information is currently not written for Scenes in the build.

## Cross referencing with the build result

A suggested usage is to run analyze on both the build results and the matching BuildReport file.  For best results this should be a clean build, so that the PackedAsset information is fully populated in the BuildReport.  You may need to temporarily copy the BuildReport file into the build output location, so that it is found by your call to analyze.

The PackedAsset information adds the extra information about the source asset of each object that is missing when only analyzing the build output.  The PackedAsset will list objects in the same order as they are found in the output Serialized file, resS or resource file.

In the database this means that a row in the `build_report_packed_assets` table can matched with the associated analyzed Serialized file through the `object_view_.serialized_file` and `build_report_packed_assets.path` values.  Similarly the `build_report_packed_asset_info` entries can be matched to the objects in the build output based on the `object_id` (local file id).

Note: currently the source local file id is not recorded in the PackedAssetInfo entry.  So, while you can find the source asset (e.g. which prefab), it is not possible to directly pinpoint the precise object within that asset.  When necessary it is often possible to determine the precise object in a more ad hoc way, based on its name or other distinguishing values.

## Working with Multiple Build Reports

So long as the file names of each build report is different, `analyze` can import multiple files into the same database.  That can be useful for seeing a comprehensive history of builds, for comparison or looking for duplicated data between Player and AssetBundle builds.

The following sections that detail the schema can be useful for writing queries that work correctly with multiple build reports in the same database.

## Alternatives

Using UnityDataTool to look at the BuildReport is rather low-level, so it is a good idea to consider other options to find the easiest and most convenient approach.

### Inspecting within Unity

You can view BuildReports using the [BuildReportInspector](https://github.com/Unity-Technologies/BuildReportInspector) package.

### API access within Unity

From within the Unity Editor you can retrieve information from a BuildReport using the BuildReport API.  

There are three ways to load a BuildReport

1. Results of the most recently completed build can be accessed using [BuildPipeline.GetLastBuildReport()](https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport.GetLatestReport.html).

2. If the build report is saved inside your Assets folder you can load it using the AssetDatabase API. For example:

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

3. If the file is outside your assets folder (for example inside the Library folder) you can load it using code like this:


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

### Text parsing

The BuildReport can be output in Unity's pseudo-YAML format instead of binary, using a diagnostic flag.

![](Diagnostics-TextBasedBuildReport.png)

The resulting file will tend to be a lot larger.  You can also get a text version by copying or moving the binary file into your Unity project (because, by default, assets are stored in Unity's text format instead of binary).  

The "dump" command of UnityDataTool can be used to get a non-YAML text representation of the BuildReport contents.

The text formats can potentially be useful for quick extraction of very specific information using text processing tools (YAML, regular expression text searching etc).  But when working with the full structured data it is better to use the UnityDataTool analyze support, or the Unity BuildReport API.

### Addressables Build Layout Support

When using the Addressables package, the equivalent to a BuildReport is the `buildlayout.json` file.  This is an entirely different file format and schema, but it records much of the same information as the BuildReport.  The UnityDataTool analyze command supports importing these files, see [Addressables Build Reports](addressables-build-reports.md) for details.

## BuildReport Schema

The BuildReport is represented in the following tables and views:

| Name in Database                              | Type    | Description/Notes |
|-----------------------------------------------|---------|------------------|
| build_reports                                 | table   | BuildReport summary (build type, result, platform, duration etc) |
| build_report_files                            | table   | Files included in the build (path, role, size).  Corresponds to [BuildReport.GetFiles](https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport.GetFiles.html) |
| build_report_archive_contents                 | table   | Tracks the files inside each AssetBundle |
| build_report_packed_assets                    | table   | Info about a SerializedFile, .resS or .resource file in the build output.  See [PackedAssets](https://docs.unity3d.com/ScriptReference/Build.Reporting.PackedAssets.html) |
| build_report_packed_asset_info                | table   | Info about each object inside a Serialized file (or data owned by an object inside a .resS or .resource file). See [PackedAssetInfo](https://docs.unity3d.com/ScriptReference/Build.Reporting.PackedAssetInfo.html) |
| build_report_source_assets                    | table   | AssetDatabase GUID and path for each source asset referenced from a PackedAssetInfo |
| build_report_files_view                       | view    | List all files from each build report |
| build_report_packed_assets_view               | view    | List of all PackedAssets in the database, along with which BuildReport, AssetBundle and Serialized File they correspond to |
| build_report_packed_asset_contents_view       | view    | List of all objects and object resources tracked in the build reports |

The build_reports table has the primary information for looking at the inputs and results of a build. For analysis of the contents of the build the raw data is recorded in the additional tables.  The views are provided for convenience, making it easier to work with the file and PackedAsset information without needing to join multiple tables, especially in the case where multiple build reports are being analyzed in the same database.

### Overview of the Schema

Several views are provided that automatically show which build report the data comes from, which aids in working with multiple reports in the same file.  However to create your own queries that work correctly it is useful to understand the relationships between the tables.  This section gives a high level overview of the schema, for details refer to the actual table and view definitions.

The build_reports table will have one entry per BuildReport file analyzed. More precisely each row corresponds (through the `id` column) to the BuildReport object tracked inside the `objects` table.  This object is where the main information about the build is stored, and there is always only one BuildReport object per BuildReport file.  The `id` column makes possible to JOIN from the build_reports table to the objects table to find the object.  And from that its possible to find the file containing the BuildReport (objects.serialized_file) and a lot of other information.

Similarly build_report_packed_assets records the `id` of the PackedAssets object, which can be used to JOIN into the `objects` table.  build_report_packed_assets doesn't directly record the `id` of the BuildReport object (because PackedAsset objects are processed individually and independently of the BuildReport object).  However it is possible to find the associated BuildReport object based on the shared `objects.serialized_file` value.

Note: There is also a relationship between the BuildReport and PackedAsset objects recorded in the `refs` table. That exists because the BuildReport has references to each PackedAsset object in its appendices array.  However the population of the `refs` table is optional so this is not used for the definition of the built in views.

The auxiliary tables such as build_report_files and build_report_archive_contents record the `id` of the BuildReport object that each row belongs to.  Similarly the build_report_packed_asset_info records the `id` of the PackedAssets object that each row belongs to.

The build_report_source_assets table records the distinct source info (GUID and path) globally for all build_report_packed_asset_info entries.  This is linked through the build_report_packed_asset_info.source_asset_id and build_report_source_assets.id columns.

Example 1:

The `build_report_packed_assets_view` demonstrates some of these relationships.  Specifically it finds the BuildReport object (br_obj) based on its Unity object type (1125) and the fact it is in the same serialized_file as the PackedAsset (pa).  It retrieves the name of the serialized file from the serialized_files table (sf.name).  For AssetBundle builds the PackedAsset path is the name of a file inside a Unity archive, so the view also retrieves the AssetBundle name from the build_report_archive_contents table (brac.assetbundle) by matching the BuildReport id and the PackedAsset path.  The AssetBundle name will be null for Player builds.

```
CREATE VIEW build_report_packed_assets_view AS
SELECT
    pa.id,
    o.object_id,
    brac.assetbundle,
    sf.name as build_report,
    pa.path,
    pa.file_header_size
FROM build_report_packed_assets pa
INNER JOIN objects o ON pa.id = o.id
INNER JOIN serialized_files sf ON o.serialized_file = sf.id
LEFT JOIN objects br_obj ON o.serialized_file = br_obj.serialized_file AND br_obj.type = 1125
LEFT JOIN build_report_archive_contents brac ON br_obj.id = brac.build_report_id AND pa.path = brac.assetbundle_content;
```

Example 2:

The `build_report_packed_asset_contents_view` shows all the entries in the build_report_packed_asset_info table and uses 

The `build_report_packed_asset_contents_view` makes use of the `object_view` rather than raw access to `objects`. That makes it a little easier to retrieve the filename of the BuildReport (`o.serialized_file`).

```
CREATE VIEW build_report_packed_asset_contents_view AS
SELECT
    o.serialized_file,
    pa.path,
    pac.packed_assets_id,
    pac.object_id,
    pac.type,
    pac.size,
    pac.offset,
    sa.source_asset_guid,
    sa.build_time_asset_path
FROM build_report_packed_asset_info pac
LEFT JOIN build_report_packed_assets pa ON pac.packed_assets_id = pa.id
LEFT JOIN object_view o ON o.id = pa.id
LEFT JOIN build_report_source_assets sa ON pac.source_asset_id = sa.id;
```

### Notes on column naming

For consistency and clarify, the SQL representation uses slightly different names than the BuildReport API for some fields.

| Name in Database                      | Name in BuildReport API   | Notes |
|---------------------------------------|--------------------------|-------|
| build_report_packed_assets.path       | PackedAssets.ShortPath | Filename of the Serialized File, .resS or .resource file.  This is the only path that is recorded, so "short" was redundant |
| build_report_packed_assets.file_header_size | PackedAssets.Overhead | The "overhead" is the size of the file header (zero for .resS and .resource files) |
| build_report_packed_asset_info.object_id | PackedAssetsInfo.fileID | Local file ID of the object in the build output.  Named object_id for consistency with objects.object_id |
| build_report_packed_asset_info.type | PackedAssetsInfo.classID | Type of the Unity Object.  Named type for consistency with objects.type |

## Limitations

Currently you cannot analyze multiple build reports in a single database if they have the same name. This is a general limitation of UnityDataTool where it assumes all SerializedFiles have unique filenames.

The `build_report_packed_asset_info.type` will record a valid [Class ID](https://docs.unity3d.com/Manual/ClassIDReference.html) for the type of each object.  However there may be no string version of this type in the `types` table.  That is because the types table is only populated when processing instances of those object types (e.g. part of the TypeTree analysis).  So if you analyze both the build output and the build report together the types should be fully populated, otherwise only the numeric value is available.

### Information that is not exported

Only a subset of information is currently extracted from the BuildReport.  Initially the focus is on the most useful information, not attempting to fully export the entire BuildReport into SQL.  More support can be added as needed.  Possible data to add would be:

* [code stripping](https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport-strippingInfo.html) appendix (available for IL2CPP player builds)
* [ScenesUsingAssets](https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport-scenesUsingAssets.html) available in some detailed build reports.
* the BuildReport.m_BuildSteps array.  SQL is not necessarily the ideal format for working with this hierarchical data.
* BuildAssetBundleInfoSet appendix (an undocumented object that reports which files belong inside each AssetBundle).  Currently the build_report_archive_contents is populated based on analysis of the BuildReport object's File list.

There are also some appendices used for purely analytics purposes, it is unlikely these would be valuable to analyze.

