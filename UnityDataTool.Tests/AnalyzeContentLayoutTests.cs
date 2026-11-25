using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

// Tests the import of ContentLayout.json into the content_layout* tables (issue #99). Runs
// against the version 3 ContentLayout.json of the LeadingEdge ContentDirectory reference build
// (see UnityProjects/LeadingEdge/Assets/Editor/BuildContentDirectory.cs), with ContentDirectoryRoot
// as the single root asset. That build is regenerated with each new Unity version, so the expected
// indices, hashes and row counts are read from the json (see LeadingEdgeTestData) rather than
// hard-coded. The version 2 import is covered by AnalyzeContentLayoutV2Tests.
public class AnalyzeContentLayoutTests
{
    private string m_TestOutputFolder;
    private string m_ContentLayoutPath;
    private string m_BuildReportPath;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "content_layout_test_folder");
        m_ContentLayoutPath = LeadingEdgeTestData.ContentLayoutPath;
        m_BuildReportPath = LeadingEdgeTestData.ContentDirectoryBuildReportPath;
        Directory.CreateDirectory(m_TestOutputFolder);
        Directory.SetCurrentDirectory(m_TestOutputFolder);
    }

    [TearDown]
    public void Teardown()
    {
        SqliteConnection.ClearAllPools();
        var outputFolder = new DirectoryInfo(m_TestOutputFolder);
        outputFolder.EnumerateFiles().ToList().ForEach(f => f.Delete());
        outputFolder.EnumerateDirectories().ToList().ForEach(d => d.Delete(true));
    }

    [Test]
    public async Task Analyze_ContentLayoutOnly_ImportsLayoutTables()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", m_ContentLayoutPath, "-o", databasePath }));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        var layout = LeadingEdgeTestData.ReadContentLayout();
        var serializedFiles = layout.GetProperty("SerializedFiles").EnumerateArray().ToArray();
        var artifacts = layout.GetProperty("BinaryArtifacts").EnumerateArray().ToArray();
        int ArrayLength(JsonElement e, string property) =>
            e.TryGetProperty(property, out var array) ? array.GetArrayLength() : 0;
        int FileIndexOf(string assetPath) =>
            LeadingEdgeTestData.SerializedFileForAsset(layout, assetPath).GetProperty("Index").GetInt32();

        // Header row identifying the imported layout.
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout", 1,
            "a single layout should be imported");
        SQLTestHelper.AssertQueryInt(db, "SELECT version FROM content_layout", 3,
            "the layout schema version should be recorded");
        SQLTestHelper.AssertQueryString(db, "SELECT build_manifest_hash FROM content_layout",
            LeadingEdgeTestData.BuildManifestHash, "the BuildManifestHash should match BuildManifestHash.txt");

        // Row counts of each table, matching the json content.
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_serialized_files", serializedFiles.Length,
            "one row per SerializedFiles entry");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_source_assets",
            serializedFiles.Sum(f => ArrayLength(f, "SourceAssets")), "one row per source asset per file");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_serialized_file_dependencies",
            serializedFiles.Sum(f => ArrayLength(f, "SerializedFileDependencies")), "one row per file dependency");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_loadable_dependencies",
            serializedFiles.Sum(f => ArrayLength(f, "LoadableDependencies")), "one row per loadable dependency");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_loadable_scene_dependencies",
            serializedFiles.Sum(f => ArrayLength(f, "LoadableSceneDependencies")), "one row per loadable scene dependency");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_loadable_objects",
            ArrayLength(layout, "LoadableObjectIds"), "one row per LoadableObjectIds entry");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_loadable_scenes",
            ArrayLength(layout, "LoadableSceneIds"), "one row per LoadableSceneIds entry");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_binary_artifacts", artifacts.Length,
            "one row per BinaryArtifacts entry");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_artifact_references",
            artifacts.Sum(a => ArrayLength(a, "ArtifactReferences")), "one row per artifact reference");

        // The built-in entry keeps its human-readable path as the stable id and has no artifact.
        SQLTestHelper.AssertQueryString(db,
            "SELECT stable_id FROM content_layout_serialized_files WHERE file_index = 0",
            "Library/unity default resources", "the built-in entry should be at index 0");
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM content_layout_serialized_files WHERE is_builtin = 1", 1,
            "the reference build has a single built-in entry");
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM content_layout_serialized_files WHERE is_builtin = 1 AND artifact_index IS NOT NULL", 0,
            "built-in entries have no artifact");

        // The ContentDirectoryRoot file and its dependencies, preserving the json array order. The
        // stable id is the asset's GUID (from its .meta file), so it survives a rebuild.
        var root = LeadingEdgeTestData.SerializedFileForAsset(layout, LeadingEdgeTestData.RootAssetPath);
        var rootIndex = root.GetProperty("Index").GetInt32();
        var rootDependencies = root.GetProperty("SerializedFileDependencies").EnumerateArray().Select(d => d.GetInt32()).ToArray();
        Assert.Greater(rootDependencies.Length, 1, "the root needs several dependencies for the order check to mean anything");
        SQLTestHelper.AssertQueryString(db,
            $"SELECT stable_id FROM content_layout_serialized_files WHERE file_index = {rootIndex}",
            "52b43dad178849b42ac753005736e7bb", "stable id of the ContentDirectoryRoot file");
        SQLTestHelper.AssertQueryString(db,
            $@"SELECT ba.content_hash FROM content_layout_serialized_files f
              INNER JOIN content_layout_binary_artifacts ba ON ba.artifact_index = f.artifact_index
              WHERE f.file_index = {rootIndex}",
            LeadingEdgeTestData.ContentHashOf(layout, root), "artifact link of the ContentDirectoryRoot file");
        SQLTestHelper.AssertQueryString(db,
            $@"SELECT GROUP_CONCAT(dependency_index) FROM (
                SELECT dependency_index FROM content_layout_serialized_file_dependencies
                WHERE serialized_file_index = {rootIndex} ORDER BY position)",
            string.Join(",", rootDependencies), "dependency order must match the json array order");
        SQLTestHelper.AssertQueryString(db,
            $"SELECT asset_path FROM content_layout_source_assets WHERE serialized_file_index = {rootIndex}",
            LeadingEdgeTestData.RootAssetPath, "source asset of the root file");

        // RootAssets is folded into is_root_asset, which holds the 1-based root position.
        var rootAssets = layout.GetProperty("RootAssets").EnumerateArray().Select(r => r.GetInt32()).ToArray();
        Assert.AreEqual(1, rootAssets.Length, "ContentDirectoryRoot is the only root asset");
        SQLTestHelper.AssertQueryInt(db,
            "SELECT loadable_index FROM content_layout_loadable_objects WHERE is_root_asset = 1", rootAssets[0],
            "is_root_asset marks the RootAssets entry");
        SQLTestHelper.AssertQueryString(db,
            "SELECT guid FROM content_layout_loadable_objects WHERE is_root_asset = 1",
            "52b43dad178849b42ac753005736e7bb", "the root loadable records its source asset guid");

        // The loadable dependencies are indices into the loadables: LoadableAudioClipReference holds
        // Loadable<AudioClip> references to both mp3s.
        var loadableReference = LeadingEdgeTestData.SerializedFileForAsset(layout,
            "Assets/ScriptableObjects/LoadableAudioClipReference.asset");
        var loadableDependencies = loadableReference.GetProperty("LoadableDependencies").EnumerateArray()
            .Select(d => d.GetInt32()).OrderBy(d => d).ToArray();
        Assert.AreEqual(2, loadableDependencies.Length, "LoadableAudioClipReference references both AudioClips");
        SQLTestHelper.AssertQueryString(db,
            $@"SELECT GROUP_CONCAT(loadable_index) FROM (
                SELECT loadable_index FROM content_layout_loadable_dependencies
                WHERE serialized_file_index = {loadableReference.GetProperty("Index").GetInt32()} ORDER BY loadable_index)",
            string.Join(",", loadableDependencies), "loadable dependencies reference loadables by index");

        // v3 layouts do not record the source asset path or source lfid of a loadable, so the
        // v2-only columns must not exist in this database.
        SQLTestHelper.AssertQueryInt(db,
            @"SELECT COUNT(*) FROM pragma_table_info('content_layout_loadable_objects')
              WHERE name IN ('asset_path', 'source_lfid')", 0,
            "the v2-only columns exist only in databases imported from a v2 layout");

        // Even without the build content, every non-built-in entry links to a serialized_files
        // row: a placeholder holding just the filename (archive NULL, no objects).
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM content_layout_serialized_files WHERE is_builtin = 0 AND serialized_file IS NULL", 0,
            "a layout-only analyze links every entry to a placeholder row");
        SQLTestHelper.AssertQueryInt(db,
            @"SELECT COUNT(*) FROM content_layout_serialized_files_view v
              INNER JOIN serialized_files sf ON sf.id = v.serialized_file
              WHERE sf.name != v.filename OR sf.archive IS NOT NULL", 0,
            "placeholder rows hold the filename and no archive");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM objects", 0,
            "a layout-only analyze produces no objects");
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM content_layout_loadable_objects_view WHERE object IS NOT NULL", 0,
            "loadables cannot resolve to objects in a layout-only analyze");

        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_binary_artifacts WHERE category = 'contentfile'",
            serializedFiles.Count(f => !(f.TryGetProperty("IsBuiltIn", out var builtIn) && builtIn.GetBoolean())),
            "one contentfile artifact per non-built-in serialized file");
        SQLTestHelper.AssertQueryString(db,
            "SELECT filename FROM content_layout_binary_artifacts_view WHERE category = 'manifest'",
            LeadingEdgeTestData.BuildManifestHash + ".json", "the artifact filename is derived from the category");

        // Views are created with the tables and their joins produce the expected rows.
        SQLTestHelper.AssertViewExists(db, "content_layout_serialized_files_view");
        SQLTestHelper.AssertViewExists(db, "content_layout_source_assets_view");
        SQLTestHelper.AssertViewExists(db, "content_layout_serialized_file_dependencies_view");
        SQLTestHelper.AssertViewExists(db, "content_layout_loadable_objects_view");
        SQLTestHelper.AssertViewExists(db, "content_layout_binary_artifacts_view");
        SQLTestHelper.AssertViewExists(db, "content_layout_resource_files_view");

        // Content Files reference their .resS/.resource data files: the audio, video and texture
        // assets of the reference build.
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_resource_files_view",
            artifacts.Where(a => a.GetProperty("Category").GetString() == "contentfile")
                .Sum(a => ArrayLength(a, "ArtifactReferences")),
            "one row per data file referenced by a Content File");
        SQLTestHelper.AssertQueryString(db,
            @"SELECT GROUP_CONCAT(DISTINCT category) FROM (
                SELECT category FROM content_layout_resource_files_view ORDER BY category)",
            "audio,texture,video", "the reference build has audio, texture and video data files");
        SQLTestHelper.AssertQueryString(db,
            "SELECT filename FROM content_layout_serialized_files_view WHERE is_builtin = 1",
            "Library/unity default resources", "built-in entries show their path as the filename");

        // Scene1's first dependency is the built-in entry (index 0).
        var scene1Index = FileIndexOf("Assets/Scenes/Scene1.unity");
        Assert.AreEqual(0, LeadingEdgeTestData.SerializedFileAt(layout, scene1Index)
            .GetProperty("SerializedFileDependencies")[0].GetInt32());
        SQLTestHelper.AssertQueryString(db,
            $@"SELECT dependency_filename FROM content_layout_serialized_file_dependencies_view
              WHERE serialized_file_index = {scene1Index} AND position = 1",
            "Library/unity default resources", "dependencies on built-in entries resolve to their path");

        SQLTestHelper.AssertQueryString(db,
            "SELECT filename FROM content_layout_loadable_objects_view WHERE is_root_asset = 1",
            LeadingEdgeTestData.ContentFileForAsset(LeadingEdgeTestData.RootAssetPath),
            "the loadable view resolves the containing file");
        SQLTestHelper.AssertQueryString(db,
            $@"SELECT dependency_filename FROM content_layout_serialized_file_dependencies_view
              WHERE serialized_file_index = {rootIndex} AND position = 1",
            LeadingEdgeTestData.ContentHashOf(layout, LeadingEdgeTestData.SerializedFileAt(layout, rootDependencies[0])) + ".cf",
            "the dependencies view resolves target filenames");
    }

    [Test]
    public async Task Analyze_ContentDirectoryWithLayout_LinksLayoutToAnalyzedContent()
    {
        var contentDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "LeadingEdgeBuilds", "ContentDirectory");
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", m_ContentLayoutPath, contentDirectory, "-o", databasePath }));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        // Every non-built-in layout entry links to the serialized_files row of its analyzed file.
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM content_layout_serialized_files WHERE is_builtin = 0 AND serialized_file IS NULL", 0,
            "all analyzed files should be linked");
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM content_layout_serialized_files WHERE is_builtin = 1 AND serialized_file IS NOT NULL", 0,
            "built-in entries have no file to link to");
        SQLTestHelper.AssertQueryInt(db,
            @"SELECT COUNT(*) FROM content_layout_serialized_files f
              WHERE f.serialized_file IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM objects o WHERE o.serialized_file = f.serialized_file)", 0,
            "every linked file should have analyzed objects");

        // The loadables resolve to their analyzed objects through the link.
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM content_layout_loadable_objects_view WHERE object IS NULL", 0,
            "every loadable should resolve to an analyzed object");
        SQLTestHelper.AssertQueryString(db,
            "SELECT name FROM content_layout_loadable_objects_view WHERE is_root_asset = 1",
            "ContentDirectoryRoot", "the root loadable resolves to the root ScriptableObject");
        SQLTestHelper.AssertQueryString(db,
            "SELECT type FROM content_layout_loadable_objects_view WHERE is_root_asset = 1",
            "MonoBehaviour", "ScriptableObjects are serialized as MonoBehaviour");

        // The layout resolves the .cfid placeholder references, so the only dangling targets
        // left are Unity's built-in resources (shipped without TypeTrees, never analyzed).
        SQLTestHelper.AssertQueryString(db,
            @"SELECT GROUP_CONCAT(DISTINCT sf.name) FROM dangling_refs d
              INNER JOIN serialized_files sf ON sf.id = d.serialized_file",
            "unity default resources", "only built-in references should dangle");
        Assert.Greater(SQLTestHelper.QueryInt(db,
            @"SELECT COUNT(*) FROM refs r
              INNER JOIN objects a ON a.id = r.object
              INNER JOIN objects b ON b.id = r.referenced_object
              WHERE a.serialized_file != b.serialized_file"), 0,
            "references between content files should resolve to analyzed objects");

        // The known chain of the reference build: the ContentDirectoryRoot ScriptableObject
        // directly references the SerializationDemo ScriptableObject in another content file.
        SQLTestHelper.AssertQueryInt(db,
            @"SELECT COUNT(*) FROM refs r
              INNER JOIN objects src ON src.id = r.object
              INNER JOIN objects tgt ON tgt.id = r.referenced_object
              WHERE src.name = 'ContentDirectoryRoot' AND tgt.name = 'SerializationDemo'
                AND src.serialized_file != tgt.serialized_file", 1,
            "ContentDirectoryRoot should reference SerializationDemo across files");

        // The scenes land as analyzed files, linked from the layout (issue #97 follow-up).
        SQLTestHelper.AssertQueryInt(db,
            @"SELECT COUNT(*) FROM content_layout_loadable_scenes s
              INNER JOIN content_layout_serialized_files f ON f.file_index = s.serialized_file_index
              WHERE EXISTS (SELECT 1 FROM objects o WHERE o.serialized_file = f.serialized_file)", 2,
            "both loadable scenes should link to analyzed content files");

        // The VideoClip is a separate content file from the ScriptableObject that references it.
        SQLTestHelper.AssertQueryInt(db,
            @"SELECT COUNT(*) FROM refs r
              INNER JOIN objects src ON src.id = r.object
              INNER JOIN object_view tgt ON tgt.id = r.referenced_object
              WHERE src.name = 'VideoClipReference' AND tgt.type = 'VideoClip'
                AND tgt.name = 'basic_clip_no_audio_160_100_1_1_50_100_H264'
                AND src.serialized_file != tgt.serialized_file", 1,
            "VideoClipReference should reference the VideoClip across files");
    }

    [Test]
    public async Task Analyze_ContentDirectoryWithoutLayout_WarnsAndRecordsDanglingRefs()
    {
        var contentDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "LeadingEdgeBuilds", "ContentDirectory");
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stdErr) = await ConsoleTestHelper.RunCapturingStdErr("analyze", contentDirectory, "-o", databasePath);

        Assert.AreEqual(0, exitCode, "a ContentDirectory without its layout is still analyzable");
        Assert.That(stdErr, Does.Contain("without its ContentLayout.json"),
            "the incomplete analysis should be called out");

        using var db = SQLTestHelper.OpenDatabase(databasePath);
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM sqlite_master WHERE name LIKE 'content_layout%'", 0,
            "no layout tables without a layout");
        Assert.Greater(SQLTestHelper.QueryInt(db,
            @"SELECT COUNT(*) FROM dangling_refs d
              INNER JOIN serialized_files sf ON sf.id = d.serialized_file
              WHERE sf.name LIKE '%.cfid'"), 0,
            "without the layout, cross-file references dangle on their .cfid placeholders");
    }

    [Test]
    public async Task Analyze_MultipleContentDirectories_Fails()
    {
        var contentDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "LeadingEdgeBuilds", "ContentDirectory");
        var otherBuild = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "contentdirectory-zstd");
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stdErr) = await ConsoleTestHelper.RunCapturingStdErr(
            "analyze", contentDirectory, otherBuild, "-o", databasePath);

        Assert.AreEqual(1, exitCode, "analyzing two different ContentDirectory builds must fail");
        Assert.That(stdErr, Does.Contain("more than one ContentDirectory build"));
        Assert.IsFalse(File.Exists(databasePath), "no partial database should be left behind");
    }

    [Test]
    public async Task Analyze_ContentDirectoryWithWrongLayout_Fails()
    {
        // The zstd reference build is a different build, so the LeadingEdge layout cannot match.
        var otherBuild = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "contentdirectory-zstd");
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stdErr) = await ConsoleTestHelper.RunCapturingStdErr(
            "analyze", otherBuild, m_ContentLayoutPath, "-o", databasePath);

        Assert.AreEqual(1, exitCode, "a layout that does not match the build must not be used");
        Assert.That(stdErr, Does.Contain("matches the analyzed build"));
    }

    [Test]
    public async Task Analyze_MultipleLayoutCandidates_SelectsTheMatchingHash()
    {
        // A stale layout (wrong hash) ahead of the real one on the input: the matching one is
        // selected and the stale one ignored. This is the Library/BuildHistory convenience case.
        var staleFolder = Path.Combine(m_TestOutputFolder, "stale_layout");
        Directory.CreateDirectory(staleFolder);
        File.WriteAllText(Path.Combine(staleFolder, "ContentLayout.json"),
            "{\"Version\":3,\"BuildManifestHash\":\"deadbeefdeadbeefdeadbeefdeadbeef\"}");
        var contentDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "LeadingEdgeBuilds", "ContentDirectory");
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stdErr) = await ConsoleTestHelper.RunCapturingStdErr(
            "analyze", staleFolder, m_ContentLayoutPath, contentDirectory, "-o", databasePath);

        Assert.AreEqual(0, exitCode);
        Assert.That(stdErr, Does.Contain("Ignoring"), "the stale layout should be reported as ignored");

        using var db = SQLTestHelper.OpenDatabase(databasePath);
        SQLTestHelper.AssertQueryString(db, "SELECT build_manifest_hash FROM content_layout",
            LeadingEdgeTestData.BuildManifestHash, "the matching layout should be the imported one");
    }

    [Test]
    public async Task Analyze_SubsetOfContentDirectoryWithLayout_ResolvesReferences()
    {
        // A single .cf file plus the BuildManifestHash.txt identifying its build: the layout is
        // validated through the hash file and the file's references resolve to the actual .cf
        // filenames of its (un-analyzed) dependencies instead of dangling on .cfid placeholders.
        var contentDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "LeadingEdgeBuilds", "ContentDirectory");
        var subsetFolder = Path.Combine(m_TestOutputFolder, "subset");
        Directory.CreateDirectory(subsetFolder);
        var rootFile = LeadingEdgeTestData.ContentFileForAsset(LeadingEdgeTestData.RootAssetPath);
        File.Copy(Path.Combine(contentDirectory, rootFile), Path.Combine(subsetFolder, rootFile));
        File.Copy(Path.Combine(contentDirectory, "BuildManifestHash.txt"),
            Path.Combine(subsetFolder, "BuildManifestHash.txt"));
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", subsetFolder, m_ContentLayoutPath, "-o", databasePath }));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout", 1,
            "the layout should be imported");
        SQLTestHelper.AssertQueryInt(db,
            @"SELECT COUNT(*) FROM dangling_refs d
              INNER JOIN serialized_files sf ON sf.id = d.serialized_file
              WHERE sf.name LIKE '%.cfid'", 0,
            "no reference should dangle on a .cfid placeholder");
        Assert.Greater(SQLTestHelper.QueryInt(db,
            @"SELECT COUNT(*) FROM dangling_refs d
              INNER JOIN serialized_files sf ON sf.id = d.serialized_file
              WHERE sf.name LIKE '%.cf'"), 0,
            "references into the un-analyzed dependencies resolve to their actual filenames");
    }

    [Test]
    public async Task Analyze_SubsetWithLayoutButNoHashFile_Fails()
    {
        // Without a BuildManifestHash.txt the layout cannot be validated against the content,
        // so the analyze fails rather than producing potentially misleading results.
        var contentDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "LeadingEdgeBuilds", "ContentDirectory");
        var subsetFolder = Path.Combine(m_TestOutputFolder, "subset_no_hash");
        Directory.CreateDirectory(subsetFolder);
        var rootFile = LeadingEdgeTestData.ContentFileForAsset(LeadingEdgeTestData.RootAssetPath);
        File.Copy(Path.Combine(contentDirectory, rootFile), Path.Combine(subsetFolder, rootFile));
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stdErr) = await ConsoleTestHelper.RunCapturingStdErr(
            "analyze", subsetFolder, m_ContentLayoutPath, "-o", databasePath);

        Assert.AreEqual(1, exitCode);
        Assert.That(stdErr, Does.Contain("cannot be validated"));
    }

    [Test]
    public async Task FindRefs_ContentDirectoryWithLayout_WalksCrossFileChain()
    {
        var contentDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "LeadingEdgeBuilds", "ContentDirectory");
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);
        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", m_ContentLayoutPath, contentDirectory, "-o", databasePath }));

        var (exitCode, stdOut) = await ConsoleTestHelper.RunCapturingStdOut(
            "find-refs", databasePath, "-n", "SerializationDemo", "-t", "MonoBehaviour", "--stdout");

        Assert.AreEqual(0, exitCode);
        Assert.That(stdOut, Does.Contain("ContentDirectoryRoot"),
            "the chain from the root asset should be found across content files");
    }

    [Test]
    public async Task Analyze_WithoutContentLayout_DoesNotCreateLayoutTables()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", m_BuildReportPath, "-o", databasePath }));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM sqlite_master WHERE name LIKE 'content_layout%'", 0,
            "the content_layout tables are only created when a ContentLayout.json is imported");
    }

    [Test]
    public async Task Analyze_UnsupportedLayoutVersion_ImportsNothing()
    {
        var layoutFolder = Path.Combine(m_TestOutputFolder, "future_version");
        Directory.CreateDirectory(layoutFolder);
        File.WriteAllText(Path.Combine(layoutFolder, "ContentLayout.json"), "{\"Version\": 99}");
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        // Analyze reports the layout as failed but the run itself still completes. The build report
        // gives it something to analyze, so the database is kept and can be inspected (issue #115).
        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", layoutFolder, m_BuildReportPath, "-o", databasePath }));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM sqlite_master WHERE name LIKE 'content_layout%'", 0,
            "an unsupported layout version must not be imported");
    }

    [Test]
    public async Task Analyze_ContentLayoutWithoutContent_ImportsNothing()
    {
        var layoutFolder = Path.Combine(m_TestOutputFolder, "null_layout");
        Directory.CreateDirectory(layoutFolder);
        File.WriteAllText(Path.Combine(layoutFolder, "ContentLayout.json"), "null");
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", layoutFolder, m_BuildReportPath, "-o", databasePath }));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM sqlite_master WHERE name LIKE 'content_layout%'", 0,
            "a json file without a ContentLayout must not be imported");
    }

    [Test]
    public async Task Analyze_MultipleContentLayoutsWithoutBuild_Fails()
    {
        // Two ContentLayout.json files but no build content to match them against: there is no
        // way to choose, and only a single layout per database is supported.
        var folderA = Path.Combine(m_TestOutputFolder, "layout_a");
        var folderB = Path.Combine(m_TestOutputFolder, "layout_b");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);
        File.Copy(m_ContentLayoutPath, Path.Combine(folderA, "ContentLayout.json"));
        File.Copy(m_ContentLayoutPath, Path.Combine(folderB, "ContentLayout.json"));
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stdErr) = await ConsoleTestHelper.RunCapturingStdErr(
            "analyze", folderA, folderB, "-o", databasePath);

        Assert.AreEqual(1, exitCode, "multiple layouts without build content cannot be disambiguated");
        Assert.That(stdErr, Does.Contain("multiple ContentLayout.json"));
        Assert.IsFalse(File.Exists(databasePath), "no partial database should be left behind");
    }
}
