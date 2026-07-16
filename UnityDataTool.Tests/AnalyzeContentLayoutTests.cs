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

        // The core-table link is only populated when the build content is analyzed too.
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM content_layout_serialized_files WHERE serialized_file IS NOT NULL", 0,
            "a layout-only analyze cannot link to analyzed serialized files");

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
        SQLTestHelper.AssertViewExists(db, "content_layout_data_files_view");

        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_data_files_view", 4,
            "the reference build has 2 .resS and 2 .resource data files");
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
    public async Task Analyze_MultipleContentLayouts_ImportsOnlyTheFirst()
    {
        // Two directories each containing a (identical) ContentLayout.json. Only a single layout
        // per database is supported; the second one is reported as failed.
        var folderA = Path.Combine(m_TestOutputFolder, "layout_a");
        var folderB = Path.Combine(m_TestOutputFolder, "layout_b");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);
        File.Copy(m_ContentLayoutPath, Path.Combine(folderA, "ContentLayout.json"));
        File.Copy(m_ContentLayoutPath, Path.Combine(folderB, "ContentLayout.json"));
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", folderA, folderB, "-o", databasePath }));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout", 1,
            "only a single layout should be imported");
        Assert.IsTrue(SQLTestHelper.QueryString(db, "SELECT name FROM content_layout").Contains("layout_a"),
            "the first layout on the input should be the imported one");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_serialized_files", 14,
            "the imported layout content should be intact");
    }
}
