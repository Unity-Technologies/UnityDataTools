using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Builds AssetBundles directly into the checked-in reference data under TestCommon. Every asset gets its own bundle
// (named after its filename): the AssetBundleRoot, both DirectAudioClipReference assets, and each mp3 in
// Assets/Audio. This highly granular layout ensures no asset is duplicated across bundles - shared assets
// (e.g. a.mp3) live in a single bundle that the others depend on. A scene bundle holds the two scenes (AssetBundles
// require scenes and assets in separate bundles). The build report is copied alongside the output.
//
// Two variants are produced into separate folders: the default LZMA build (a single streamed data block) and an
// LZ4 chunk-based build (many small blocks), which is the layout that exercises the alignment padding between
// chunks added in archive format version 9.
public static class BuildAssetBundles
{
    const string AudioFolder = "Assets/Audio";

    // Relative to the project root, which is the working directory when the build runs.
    const string TestDataFolder = "../../TestCommon/Data/LeadingEdgeBuilds";
    const string OutputFolder = TestDataFolder + "/AssetBundles";
    const string BuildReportFolder = TestDataFolder + "/BuildReport-AssetBundles";
    const string Lz4OutputFolder = TestDataFolder + "/AssetBundlesLz4";

    static readonly string[] DirectAssets =
    {
        "Assets/ScriptableObjects/AssetBundleRoot.asset",
        "Assets/ScriptableObjects/DirectAudioClipReference.asset",
        "Assets/ScriptableObjects/SingleAudioClipDirectReference.asset",
        "Assets/ScriptableObjects/SerializationDemo.asset"
    };

    [MenuItem("ContentDirectory/Build AssetBundles")]
    public static void Build()
    {
        Build(BuildAssetBundleOptions.None, OutputFolder, BuildReportFolder);
    }

    [MenuItem("ContentDirectory/Build AssetBundles (LZ4)")]
    public static void BuildLz4()
    {
        Build(BuildAssetBundleOptions.ChunkBasedCompression, Lz4OutputFolder, null);
    }

    // buildReportFolder may be null to skip copying the report.
    static void Build(BuildAssetBundleOptions options, string outputFolder, string buildReportFolder)
    {
        Directory.CreateDirectory(outputFolder);

        var bundles = new List<AssetBundleBuild>();

        foreach (var assetPath in DirectAssets)
        {
            bundles.Add(new AssetBundleBuild
            {
                assetBundleName = Path.GetFileNameWithoutExtension(assetPath),
                assetNames = new[] { assetPath }
            });
        }

        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            bundles.Add(new AssetBundleBuild
            {
                assetBundleName = Path.GetFileNameWithoutExtension(path),
                assetNames = new[] { path }
            });
        }

        bundles.Add(new AssetBundleBuild
        {
            assetBundleName = "scenes",
            assetNames = GenerateAssets.ScenePaths
        });

        var parameters = new BuildAssetBundlesParameters
        {
            outputPath = outputFolder,
            bundleDefinitions = bundles.ToArray(),
            options = options,
            targetPlatform = EditorUserBuildSettings.activeBuildTarget
        };

        var manifest = BuildPipeline.BuildAssetBundles(parameters);
        if (manifest == null)
        {
            Debug.LogError("BuildAssetBundles: build failed.");
            return;
        }

        if (buildReportFolder != null)
        {
            Directory.CreateDirectory(buildReportFolder);
            File.Copy("Library/LastBuild.buildreport", $"{buildReportFolder}/LastBuild.buildreport", true);
        }

        Debug.Log($"BuildAssetBundles: built {manifest.GetAllAssetBundles().Length} bundles into {outputFolder}.");
    }
}
