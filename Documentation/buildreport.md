# BuildReport support

The [BuildReport](https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport.html) file is created by Player builds.  It is also created if you build AssetBundles with [BuildPipeline.BuildAssetBundle](https://docs.unity3d.com/ScriptReference/BuildPipeline.BuildAssetBundles.html).  However it is not created for Addressables or builds using the Scriptable Build Pipeline.

This file is currently written to `Library/LastBuild.buildReport`.  By default this is a Unity binary serialized file, the same format used for build output.  Because UnityDataTool supports reading this format it can read this file and extract information about the build results, using the same mechanisms used for other Unity object types.

## UnityDataTool support

Because it is a SerializedFile you can ["dump"](command-dump.md) it into text format.

The [`analyze`](command-analyze.md) command now has custom support for BuildReport files.  When you analyze a BuildReport file it will extract information into dedicated tables in the output database.

Specifically there are now custom handlers for the following types:

* **BuildReport** - The primary object that reports the inputs and results for a Unity build.
* **PackedAssets** - An object that describes the contents of a specific Serialized file, or resource file.  It records information such as the type, size and source asset for each object or resource blob.  This makes it possible to do analysis of the content down to the object level.

These are represented in the following tables and views:

| Name in Database                        | Type    | Description/Notes |
|-----------------------------------------|---------|------------------|
| build_reports                           | table   | BuildReport summary (build type, result, platform, duration etc) |
| build_report_packed_assets              | table   | Info about a SerializedFile, .resS or .resource file in the build output |
| build_report_packed_asset_info          | table   | Info about each object inside a Serialized file (or data owned by an object inside a .resS or .resource file |
| build_report_source_assets              | table   | AssetDatabase GUID and path for each source asset referenced from a PackedAssetInfo.  This table normalizes the repeated paths and GUIDs, which makes the SQL representation much more compact than the BuildReport format for some large build results. |
| build_report_packed_assets_view         | view    | Shows the name of the BuildReport file along with each build_report_packed_assets.  This view is useful if the database contains the analysis of more than one build report file. |
| build_report_packed_asset_contents_view | view    | View of all the PackedAssetInfo entries, including the BuildReport file and source asset path and GUID |


## Notes on column naming

For consistency and clarify, the SQL representation uses slightly different names than the BuildReport API for some fields.

| Name in Database                      | Name in BuildReport API   | Notes |
|---------------------------------------|--------------------------|-------|
| build_report_packed_assets.path       | PackedAssets.ShortPath | Filename of the Serialized File, .resS or .resource file.  This is the only path that is recorded, so "short" was redundant |
| build_report_packed_assets.file_header_size | PackedAssets.Overhead | The "overhead" is the size of the file header (zero for .resS and .resource files) |
| build_report_packed_asset_info.object_id | PackedAssetsInfo.fileID | Local file ID of the object in the build output.  Named object_id for consistency with objects.object_id |
| build_report_packed_asset_info.type | PackedAssetsInfo.classID | Type of the Unity Object.  Named type for consistency with objects.type |

## Cross referencing with the build result

A suggested usage is run analyze on a full build as well as the matching BuildReport.  For best results this should be a clean build so that the PackedAsset information is fully populated.  You may need to temporarily copy the BuildReport file into the build output location so that it is found by your call to analyze.

The PackedAsset information adds the extra information about the source asset of each object that is missing when only analyzing the build output.  The PackedAsset will list objects in the same order as they are found in the output Serialized file, resS or resource file.  For objects in Serialized file it records the `object_id`, e.g. local file id.  This would match an entry in the `objects` object table if you have also analyzed the built file.

Note: currently the source LFID is not recorded in the PackedAssetInfo entry.  So, while you can find the source asset (e.g. which prefab), its not possible to directly pinpoint the precise object within that asset.  When necessary it should be possible to determine the precise object in a more ad hoc way, based on its name or other distinguishing values.

## Limitations

Currently you cannot analyze multiple build reports in a single database if they have the same name. This is a general limitation of UnityDataTool where it assumes all SerializedFiles have unique filenames.

The `build_report_packed_asset_info.type` will record a valid [Class ID](https://docs.unity3d.com/Manual/ClassIDReference.html) for the type of each object.  However there may be no string version of this type in the `types` table.  That is because the types table is only populated when processing instances of those object types (e.g. part of the TypeTree analysis).  So if you analyze both the build output and the build report together the types should be fully populated, otherwise only the numeric value is available.

### Information that is not exported

Only a subset of information is currently extracted from the BuildReport.  Initially the focus is on the most useful information, not attempting to fully export the entire BuildReport into SQL.  More support can be added as needed.  Possible data to add would be:

* The BuildReport.m_Files array
* the BuildReport.m_BuildSteps array
* BuildAssetBundleInfoSet appendix (reporting which files belong inside each AssetBundle)
* [code stripping](https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport-strippingInfo.html) appendix (available for IL2CPP player builds)
* [ScenesUsingAssets](https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport-scenesUsingAssets.html) available in some detailed build reports.

## Alternatives

Using UnityDataTool to look at the BuildReport is rather low-level, so it is a good idea to consider other options that may be easier.

### Inspecting within Unity

You can view BuildReports using the [BuildReportInspector](https://github.com/Unity-Technologies/BuildReportInspector) package.

### API access within Unity

From within the Unity Editor you can retrive information from a BuildReport using the BuildReport API.  

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

The text formats can potentially be useful for quick extraction of very specific information using text processing tools (YAML, regular expression text searching etc).

### Addressables Build Layout Support

When using the Addressables package, the equivalent to a BuildReport is the `buildlayout.json` file.  This is an entirely different file format and schema, but it records much of the same information as the BuildReport.  The UnityDataTool analyze command supports importing these files, see [Addressables Build Reports](addressables-build-reports.md) for details.
