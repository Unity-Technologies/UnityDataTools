using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

// Tests the import of ContentLayout.json into the content_layout* tables (issue #99). Runs
// against the version 3 ContentLayout.json of the LeadingEdge ContentDirectory reference build,
// whose content is well-known (see UnityProjects/LeadingEdge/Assets/Editor/BuildContentDirectory.cs):
// 14 serialized files (1 built-in), 3 loadable objects with ContentDirectoryRoot as the single
// root asset, 2 loadable scenes, and 18 binary artifacts. The version 2 import is covered by
// AnalyzeContentLayoutV2Tests.
public class AnalyzeContentLayoutTests
{
    private string m_TestOutputFolder;
    private string m_ContentLayoutPath;
    private string m_BuildReportPath;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "content_layout_test_folder");
        m_ContentLayoutPath = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "LeadingEdgeBuilds", "BuildReport-ContentDirectory", "ContentLayout.json");
        m_BuildReportPath = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "LeadingEdgeBuilds", "BuildReport-ContentDirectory", "f096bba0b9ce4c44194edd738d64b9df.buildreport");
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

        // Header row identifying the imported layout.
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout", 1,
            "a single layout should be imported");
        SQLTestHelper.AssertQueryInt(db, "SELECT version FROM content_layout", 3,
            "the layout schema version should be recorded");
        SQLTestHelper.AssertQueryString(db, "SELECT build_manifest_hash FROM content_layout",
            "e320fc78984f8430afa90a591fd02004", "the BuildManifestHash should be recorded");

        // Row counts of each table, matching the json content.
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_serialized_files", 14,
            "one row per SerializedFiles entry");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_source_assets", 16,
            "one row per source asset per file");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_serialized_file_dependencies", 16,
            "one row per file dependency");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_loadable_dependencies", 3,
            "one row per loadable dependency");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_loadable_scene_dependencies", 2,
            "one row per loadable scene dependency");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_loadable_objects", 3,
            "one row per LoadableObjectIds entry");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_loadable_scenes", 2,
            "one row per LoadableSceneIds entry");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_binary_artifacts", 18,
            "one row per BinaryArtifacts entry");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_artifact_references", 17,
            "one row per artifact reference");

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

        // The ContentDirectoryRoot file and its dependencies, preserving the json array order
        // (json SerializedFileDependencies for index 10: [4, 5, 2, 9, 3]).
        SQLTestHelper.AssertQueryString(db,
            "SELECT stable_id FROM content_layout_serialized_files WHERE file_index = 10",
            "52b43dad178849b42ac753005736e7bb", "stable id of the ContentDirectoryRoot file");
        SQLTestHelper.AssertQueryString(db,
            @"SELECT ba.content_hash FROM content_layout_serialized_files f
              INNER JOIN content_layout_binary_artifacts ba ON ba.artifact_index = f.artifact_index
              WHERE f.file_index = 10",
            "eb3abd5ab5b0d790980fe9e9df872484", "artifact link of the ContentDirectoryRoot file");
        SQLTestHelper.AssertQueryString(db,
            @"SELECT GROUP_CONCAT(dependency_index) FROM (
                SELECT dependency_index FROM content_layout_serialized_file_dependencies
                WHERE serialized_file_index = 10 ORDER BY position)",
            "4,5,2,9,3", "dependency order must match the json array order");
        SQLTestHelper.AssertQueryString(db,
            "SELECT asset_path FROM content_layout_source_assets WHERE serialized_file_index = 10",
            "Assets/ScriptableObjects/ContentDirectoryRoot.asset", "source asset of the root file");

        // RootAssets is folded into is_root_asset, which holds the 1-based root position
        // (json RootAssets: [2]).
        SQLTestHelper.AssertQueryInt(db,
            "SELECT loadable_index FROM content_layout_loadable_objects WHERE is_root_asset = 1", 2,
            "ContentDirectoryRoot is the only root asset");
        SQLTestHelper.AssertQueryString(db,
            "SELECT guid FROM content_layout_loadable_objects WHERE is_root_asset = 1",
            "52b43dad178849b42ac753005736e7bb", "the root loadable records its source asset guid");

        // The loadable dependencies are indices into the loadables (json file 5: [0, 1]).
        SQLTestHelper.AssertQueryString(db,
            @"SELECT GROUP_CONCAT(loadable_index) FROM (
                SELECT loadable_index FROM content_layout_loadable_dependencies
                WHERE serialized_file_index = 5 ORDER BY loadable_index)",
            "0,1", "loadable dependencies reference loadables by index");

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
            13, "one contentfile artifact per non-built-in serialized file");
        SQLTestHelper.AssertQueryString(db,
            "SELECT filename FROM content_layout_binary_artifacts_view WHERE category = 'manifest'",
            "e320fc78984f8430afa90a591fd02004.json", "the artifact filename is derived from the category");

        // Views are created with the tables and their joins produce the expected rows.
        SQLTestHelper.AssertViewExists(db, "content_layout_serialized_files_view");
        SQLTestHelper.AssertViewExists(db, "content_layout_source_assets_view");
        SQLTestHelper.AssertViewExists(db, "content_layout_serialized_file_dependencies_view");
        SQLTestHelper.AssertViewExists(db, "content_layout_loadable_objects_view");
        SQLTestHelper.AssertViewExists(db, "content_layout_binary_artifacts_view");
        SQLTestHelper.AssertViewExists(db, "content_layout_resource_files_view");

        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_resource_files_view", 4,
            "the reference build has 2 .resS and 2 .resource data files");
        SQLTestHelper.AssertQueryString(db,
            "SELECT filename FROM content_layout_serialized_files_view WHERE is_builtin = 1",
            "Library/unity default resources", "built-in entries show their path as the filename");
        SQLTestHelper.AssertQueryString(db,
            @"SELECT dependency_filename FROM content_layout_serialized_file_dependencies_view
              WHERE serialized_file_index = 6 AND position = 1",
            "Library/unity default resources", "dependencies on built-in entries resolve to their path");
        SQLTestHelper.AssertQueryString(db,
            @"SELECT filename FROM content_layout_loadable_objects_view WHERE loadable_index = 2",
            "eb3abd5ab5b0d790980fe9e9df872484.cf", "the loadable view resolves the containing file");
        SQLTestHelper.AssertQueryString(db,
            @"SELECT dependency_filename FROM content_layout_serialized_file_dependencies_view
              WHERE serialized_file_index = 10 AND position = 1",
            "79bdcb3e9bc659d3519594280240cd1b.cf", "the dependencies view resolves target filenames");
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
            "e320fc78984f8430afa90a591fd02004", "the matching layout should be the imported one");
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
        const string rootFile = "eb3abd5ab5b0d790980fe9e9df872484.cf";
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
        const string rootFile = "eb3abd5ab5b0d790980fe9e9df872484.cf";
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
