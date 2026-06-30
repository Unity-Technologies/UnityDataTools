using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Builds AssetBundles directly into the checked-in reference data under TestCommon. Every asset gets its own bundle
// (named after its filename): the AssetBundleRoot, both DirectAudioClipReference assets, and each mp3 in
// Assets/Audio. This highly granular layout ensures no asset is duplicated across bundles - shared assets
// (e.g. a.mp3) live in a single bundle that the others depend on. The build report is copied alongside the output.
public static class BuildAssetBundles
{
    const string AudioFolder = "Assets/Audio";

    // Relative to the project root, which is the working directory when the build runs.
    const string TestDataFolder = "../../TestCommon/Data/LeadingEdgeBuilds";
    const string OutputFolder = TestDataFolder + "/AssetBundles";
    const string BuildReportFolder = TestDataFolder + "/BuildReport-AssetBundles";

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
        Directory.CreateDirectory(OutputFolder);

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

        var parameters = new BuildAssetBundlesParameters
        {
            outputPath = OutputFolder,
            bundleDefinitions = bundles.ToArray(),
            options = BuildAssetBundleOptions.None,
            targetPlatform = EditorUserBuildSettings.activeBuildTarget
        };

        var manifest = BuildPipeline.BuildAssetBundles(parameters);
        if (manifest == null)
        {
            Debug.LogError("BuildAssetBundles: build failed.");
            return;
        }

        Directory.CreateDirectory(BuildReportFolder);
        File.Copy("Library/LastBuild.buildreport", $"{BuildReportFolder}/LastBuild.buildreport", true);

        Debug.Log($"BuildAssetBundles: built {manifest.GetAllAssetBundles().Length} bundles into {OutputFolder}.");
    }
}
