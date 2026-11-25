using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

// Locates the LeadingEdge reference builds (TestCommon/Data/LeadingEdgeBuilds) and the values that
// change whenever they are rebuilt: the build report name, the build manifest hash and the
// content-hash names of the Content Files. Tests read these from the data rather than hard-coding
// them, so a routine rebuild with a newer Unity does not require test edits.
//
// ContentLayout.json is read with JsonDocument rather than the UnityDataModels types, so expected
// values come from the raw file instead of the code under test.
public static class LeadingEdgeTestData
{
    public static string Folder =>
        Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "LeadingEdgeBuilds");

    public static string ContentDirectory => Path.Combine(Folder, "ContentDirectory");

    public static string ContentDirectoryBuildHistory => Path.Combine(Folder, "BuildReport-ContentDirectory");

    public static string ContentLayoutPath => Path.Combine(ContentDirectoryBuildHistory, "ContentLayout.json");

    // The build history holds a single build report, named after the build session GUID.
    public static string ContentDirectoryBuildReportPath
    {
        get
        {
            var reports = Directory.GetFiles(ContentDirectoryBuildHistory, "*.buildreport");
            Assert.AreEqual(1, reports.Length, $"Expected a single build report in {ContentDirectoryBuildHistory}");
            return reports[0];
        }
    }

    public static string BuildSessionGuid => Path.GetFileNameWithoutExtension(ContentDirectoryBuildReportPath);

    public static string BuildManifestHash =>
        File.ReadAllText(Path.Combine(ContentDirectory, "BuildManifestHash.txt")).Trim();

    public static JsonElement ReadContentLayout() =>
        JsonDocument.Parse(File.ReadAllText(ContentLayoutPath)).RootElement;

    // The SerializedFiles entry whose source assets include the given asset path.
    public static JsonElement SerializedFileForAsset(JsonElement layout, string assetPath) =>
        layout.GetProperty("SerializedFiles").EnumerateArray()
            .Single(f => f.GetProperty("SourceAssets").EnumerateArray().Any(a => a.GetString() == assetPath));

    public static JsonElement SerializedFileAt(JsonElement layout, int fileIndex) =>
        layout.GetProperty("SerializedFiles").EnumerateArray()
            .Single(f => f.GetProperty("Index").GetInt32() == fileIndex);

    // The content hash of the artifact holding a SerializedFiles entry.
    public static string ContentHashOf(JsonElement layout, JsonElement serializedFile)
    {
        var artifactIndex = serializedFile.GetProperty("ArtifactIndex").GetInt32();
        return layout.GetProperty("BinaryArtifacts").EnumerateArray()
            .Single(a => a.GetProperty("Index").GetInt32() == artifactIndex)
            .GetProperty("ContentHash").GetString();
    }

    // The Content File name (content hash + ".cf") that holds the given source asset.
    public static string ContentFileForAsset(string assetPath)
    {
        var layout = ReadContentLayout();
        return ContentHashOf(layout, SerializedFileForAsset(layout, assetPath)) + ".cf";
    }

    public const string RootAssetPath = "Assets/ScriptableObjects/ContentDirectoryRoot.asset";
}
