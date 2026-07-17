using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

// Tests the import of ContentLayout.json into the content_layout* tables (issue #99). Runs
// against the ContentLayout.json of the LeadingEdge ContentDirectory reference build, whose
// content is well-known (see UnityProjects/LeadingEdge/Assets/Editor/BuildContentDirectory.cs):
// 14 serialized files (1 built-in), 3 loadable objects with ContentDirectoryRoot as the single
// root asset, 2 loadable scenes, and 18 binary artifacts.
public class AnalyzeContentLayoutTests
{
    private string m_TestOutputFolder;
    private string m_ContentLayoutPath;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "content_layout_test_folder");
        m_ContentLayoutPath = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "LeadingEdgeBuilds", "BuildReport-ContentDirectory", "ContentLayout.json");
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
        SQLTestHelper.AssertQueryInt(db, "SELECT version FROM content_layout", 2,
            "the layout schema version should be recorded");
        SQLTestHelper.AssertQueryString(db, "SELECT build_manifest_hash FROM content_layout",
            "baff06b928d147276f2245dd3b19216a", "the BuildManifestHash should be recorded");

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

        // The built-in entry keeps its human-readable ID and has no content hash.
        SQLTestHelper.AssertQueryString(db,
            "SELECT cfid FROM content_layout_serialized_files WHERE file_index = 0",
            "Library/unity default resources", "the built-in entry should be at index 0");
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM content_layout_serialized_files WHERE is_builtin = 1", 1,
            "the reference build has a single built-in entry");
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM content_layout_serialized_files WHERE is_builtin = 1 AND content_hash IS NOT NULL", 0,
            "built-in entries have no content hash");

        // The ContentDirectoryRoot file and its dependencies, preserving the json array order
        // (json SerializedFileDependencies for index 5: [8, 4, 11, 13, 7]).
        SQLTestHelper.AssertQueryString(db,
            "SELECT cfid FROM content_layout_serialized_files WHERE file_index = 5",
            "52b43dad178849b42ac753005736e7bb.cfid", "cfid of the ContentDirectoryRoot file");
        SQLTestHelper.AssertQueryString(db,
            "SELECT content_hash FROM content_layout_serialized_files WHERE file_index = 5",
            "c0152db4dd710be51b2decb997325f34", "content hash of the ContentDirectoryRoot file");
        SQLTestHelper.AssertQueryString(db,
            @"SELECT GROUP_CONCAT(dependency_index) FROM (
                SELECT dependency_index FROM content_layout_serialized_file_dependencies
                WHERE serialized_file_index = 5 ORDER BY position)",
            "8,4,11,13,7", "dependency order must match the json array order");
        SQLTestHelper.AssertQueryString(db,
            "SELECT asset_path FROM content_layout_source_assets WHERE serialized_file_index = 5",
            "Assets/ScriptableObjects/ContentDirectoryRoot.asset", "source asset of the root file");

        // RootAssets is folded into the is_root_asset flag.
        SQLTestHelper.AssertQueryString(db,
            "SELECT asset_path FROM content_layout_loadable_objects WHERE is_root_asset = 1",
            "Assets/ScriptableObjects/ContentDirectoryRoot.asset",
            "ContentDirectoryRoot is the only root asset");

        // Even without the build content, every non-built-in entry links to a serialized_files
        // row: a placeholder holding just the filename (archive NULL, no objects).
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM content_layout_serialized_files WHERE is_builtin = 0 AND serialized_file IS NULL", 0,
            "a layout-only analyze links every entry to a placeholder row");
        SQLTestHelper.AssertQueryInt(db,
            @"SELECT COUNT(*) FROM content_layout_serialized_files f
              INNER JOIN serialized_files sf ON sf.id = f.serialized_file
              WHERE sf.name != f.content_hash || '.cf' OR sf.archive IS NOT NULL", 0,
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
            "baff06b928d147276f2245dd3b19216a.json", "the artifact filename is derived from the category");

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
              WHERE serialized_file_index = 2 AND position = 1",
            "Library/unity default resources", "dependencies on built-in entries resolve to their path");
        SQLTestHelper.AssertQueryString(db,
            @"SELECT filename FROM content_layout_loadable_objects_view
              WHERE asset_path = 'Assets/ScriptableObjects/ContentDirectoryRoot.asset'",
            "c0152db4dd710be51b2decb997325f34.cf", "the loadable view resolves the containing file");
        SQLTestHelper.AssertQueryString(db,
            @"SELECT dependency_filename FROM content_layout_serialized_file_dependencies_view
              WHERE serialized_file_index = 5 AND position = 1",
            "86d71ff2bb38e064697257d35d6421b8.cf", "the dependencies view resolves target filenames");
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
            @"SELECT name FROM content_layout_loadable_objects_view
              WHERE asset_path = 'Assets/ScriptableObjects/ContentDirectoryRoot.asset'",
            "ContentDirectoryRoot", "the root loadable resolves to the root ScriptableObject");
        SQLTestHelper.AssertQueryString(db,
            @"SELECT type FROM content_layout_loadable_objects_view
              WHERE asset_path = 'Assets/ScriptableObjects/ContentDirectoryRoot.asset'",
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

    // Runs a command capturing stderr, where analyze prints its warnings.
    private static async Task<(int exitCode, string stdErr)> RunCapturingStdErr(params string[] args)
    {
        using var sw = new StringWriter();
        var currentError = Console.Error;
        int exitCode;
        try
        {
            Console.SetError(sw);
            exitCode = await Program.Main(args);
        }
        finally
        {
            Console.SetError(currentError);
        }

        return (exitCode, sw.ToString());
    }

    [Test]
    public async Task Analyze_ContentDirectoryWithoutLayout_WarnsAndRecordsDanglingRefs()
    {
        var contentDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "LeadingEdgeBuilds", "ContentDirectory");
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stdErr) = await RunCapturingStdErr("analyze", contentDirectory, "-o", databasePath);

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

        var (exitCode, stdErr) = await RunCapturingStdErr(
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

        var (exitCode, stdErr) = await RunCapturingStdErr(
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
            "{\"Version\":2,\"BuildManifestHash\":\"deadbeefdeadbeefdeadbeefdeadbeef\"}");
        var contentDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "LeadingEdgeBuilds", "ContentDirectory");
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stdErr) = await RunCapturingStdErr(
            "analyze", staleFolder, m_ContentLayoutPath, contentDirectory, "-o", databasePath);

        Assert.AreEqual(0, exitCode);
        Assert.That(stdErr, Does.Contain("Ignoring"), "the stale layout should be reported as ignored");

        using var db = SQLTestHelper.OpenDatabase(databasePath);
        SQLTestHelper.AssertQueryString(db, "SELECT build_manifest_hash FROM content_layout",
            "baff06b928d147276f2245dd3b19216a", "the matching layout should be the imported one");
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
        const string rootFile = "c0152db4dd710be51b2decb997325f34.cf";
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
        const string rootFile = "c0152db4dd710be51b2decb997325f34.cf";
        File.Copy(Path.Combine(contentDirectory, rootFile), Path.Combine(subsetFolder, rootFile));
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stdErr) = await RunCapturingStdErr(
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

        using var sw = new StringWriter();
        var currentOut = Console.Out;
        int exitCode;
        try
        {
            Console.SetOut(sw);
            exitCode = await Program.Main(new string[]
                { "find-refs", databasePath, "-n", "SerializationDemo", "-t", "MonoBehaviour", "--stdout" });
        }
        finally
        {
            Console.SetOut(currentOut);
        }

        Assert.AreEqual(0, exitCode);
        Assert.That(sw.ToString(), Does.Contain("ContentDirectoryRoot"),
            "the chain from the root asset should be found across content files");
    }

    [Test]
    public async Task Analyze_WithoutContentLayout_DoesNotCreateLayoutTables()
    {
        var bundlePath = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "LeadingEdgeBuilds", "AssetBundles", "assetbundleroot");
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", bundlePath, "-o", databasePath }));
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

        // Analyze reports the file as failed but the run itself still completes.
        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", layoutFolder, "-o", databasePath }));
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

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", layoutFolder, "-o", databasePath }));
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

        var (exitCode, stdErr) = await RunCapturingStdErr(
            "analyze", folderA, folderB, "-o", databasePath);

        Assert.AreEqual(1, exitCode, "multiple layouts without build content cannot be disambiguated");
        Assert.That(stdErr, Does.Contain("multiple ContentLayout.json"));
        Assert.IsFalse(File.Exists(databasePath), "no partial database should be left behind");
    }
}
