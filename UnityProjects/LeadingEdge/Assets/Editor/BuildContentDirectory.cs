using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds a Content Directory directly into the checked-in reference data under TestCommon, using
// ContentDirectoryRoot.asset as the root. ContentDirectoryRoot directly references the two
// LoadableAudioClipReference assets, so those are loaded automatically when the content directory is registered.
// The AudioClips themselves are referenced through Loadable<AudioClip>, so they are included in the build but only
// loaded on demand, not at registration. The build report folder is copied alongside the output.
public static class BuildContentDirectory
{
    const string RootAsset = "Assets/ScriptableObjects/ContentDirectoryRoot.asset";

    // Relative to the project root, which is the working directory when the build runs.
    const string TestDataFolder = "../../TestCommon/Data/LeadingEdgeBuilds";
    const string OutputFolder = TestDataFolder + "/ContentDirectory";
    const string BuildReportFolder = TestDataFolder + "/BuildReport-ContentDirectory";

    [MenuItem("ContentDirectory/Build Content Directory")]
    public static void Build()
    {
        var parameters = new BuildContentDirectoryParameters
        {
            outputPath = OutputFolder,
            rootAssetPaths = new[] { RootAsset },
            options = BuildContentOptions.CleanBuildCache,
            compression = BuildCompression.Uncompressed
        };

        var report = BuildPipeline.BuildContentDirectory(parameters);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"BuildContentDirectory: failed with result {report.summary.result}.");
            return;
        }

        CopyLatestBuildReport();

        Debug.Log($"BuildContentDirectory: succeeded, output in {OutputFolder}.");
    }

    // The content directory build writes its report (including ContentLayout.json) to a timestamped folder under
    // Library/BuildHistory. Mirror the most recent one into the checked-in reference data.
    static void CopyLatestBuildReport()
    {
        var latest = new DirectoryInfo("Library/BuildHistory")
            .GetDirectories()
            .OrderByDescending(d => d.CreationTimeUtc)
            .First();

        if (Directory.Exists(BuildReportFolder))
            Directory.Delete(BuildReportFolder, true);

        CopyDirectory(latest.FullName, BuildReportFolder);
    }

    static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }
}
