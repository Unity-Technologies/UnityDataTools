using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

// Tests the import of a version 2 ContentLayout.json (Unity 6.6) into the content_layout* tables
// (issue #131). v2 files are upgraded to the current model on import: hash-based references become
// indices and the stable ids lose their ".cfid" extension, so both versions produce the same
// schema. The v2-only source data (asset path, source lfid) lands in extra columns that only exist
// in a database imported from a v2 file. Runs against the archived v2 layout of the LeadingEdge
// reference build in TestCommon/Data/ContentLayoutVersions/v2 (its build content is not archived,
// so this is a layout-only analyze).
public class AnalyzeContentLayoutV2Tests
{
    private string m_TestOutputFolder;
    private string m_ContentLayoutPath;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "content_layout_v2_test_folder");
        m_ContentLayoutPath = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "ContentLayoutVersions", "v2", "ContentLayout.json");
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
    public async Task Analyze_V2ContentLayout_ImportsUpgradedLayoutTables()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", m_ContentLayoutPath, "-o", databasePath }));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        // The original file version is recorded, not the version it was upgraded to.
        SQLTestHelper.AssertQueryInt(db, "SELECT version FROM content_layout", 2,
            "the original schema version should be recorded");
        SQLTestHelper.AssertQueryString(db, "SELECT build_manifest_hash FROM content_layout",
            "baff06b928d147276f2245dd3b19216a", "the BuildManifestHash should be recorded");

        // Row counts of each table, matching the json content.
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_serialized_files", 14,
            "one row per SerializedFiles entry");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_source_assets", 16,
            "one row per source asset per file");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_loadable_objects", 3,
            "one row per LoadableObjectIds entry");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout_binary_artifacts", 18,
            "one row per BinaryArtifacts entry");

        // The v2 ".cfid" extension is stripped so stable_id is uniform across layout versions.
        // The built-in entry keeps its human-readable path.
        SQLTestHelper.AssertQueryString(db,
            "SELECT stable_id FROM content_layout_serialized_files WHERE file_index = 5",
            "52b43dad178849b42ac753005736e7bb", "stable_id should not carry the .cfid extension");
        SQLTestHelper.AssertQueryString(db,
            "SELECT stable_id FROM content_layout_serialized_files WHERE file_index = 0",
            "Library/unity default resources", "the built-in entry should be at index 0");

        // The v2 ContentHash is resolved to the index of the matching contentfile artifact.
        SQLTestHelper.AssertQueryString(db,
            @"SELECT ba.content_hash FROM content_layout_serialized_files f
              INNER JOIN content_layout_binary_artifacts ba ON ba.artifact_index = f.artifact_index
              WHERE f.file_index = 5",
            "c0152db4dd710be51b2decb997325f34", "the ContentHash becomes a reference to its artifact");
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM content_layout_serialized_files WHERE is_builtin = 1 AND artifact_index IS NOT NULL", 0,
            "built-in entries have no artifact");

        // The v2 ObjectIdHash references are resolved to loadable indices during import
        // (json LoadableDependencies for file 4 reference the loadables at indices 0 and 2).
        SQLTestHelper.AssertQueryString(db,
            @"SELECT GROUP_CONCAT(loadable_index) FROM (
                SELECT loadable_index FROM content_layout_loadable_dependencies
                WHERE serialized_file_index = 4 ORDER BY loadable_index)",
            "0,2", "hash-based loadable dependencies resolve to loadable indices");

        // The loadables keep their json array position as the key. lfid is the id of the object
        // in its output file (the v2 OutputLFID); the v2 source lfid is preserved separately.
        SQLTestHelper.AssertQueryString(db,
            "SELECT asset_path FROM content_layout_loadable_objects WHERE loadable_index = 1",
            "Assets/ScriptableObjects/ContentDirectoryRoot.asset", "v2 layouts record the loadable's asset path");
        SQLTestHelper.AssertQueryString(db,
            "SELECT lfid FROM content_layout_loadable_objects WHERE loadable_index = 1",
            "-775554941117088049", "lfid holds the output-file lfid");
        SQLTestHelper.AssertQueryString(db,
            "SELECT source_lfid FROM content_layout_loadable_objects WHERE loadable_index = 1",
            "11400000", "v2 layouts record the source lfid");

        // The single RootAssets entry names the ContentDirectoryRoot loadable; its 1-based
        // position lands in is_root_asset.
        SQLTestHelper.AssertQueryString(db,
            "SELECT asset_path FROM content_layout_loadable_objects WHERE is_root_asset = 1",
            "Assets/ScriptableObjects/ContentDirectoryRoot.asset",
            "ContentDirectoryRoot is the only root asset");
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM content_layout_loadable_objects WHERE is_root_asset = 0", 2,
            "the other loadables are not roots");

        // The v2-only columns appear in the loadables view as well.
        SQLTestHelper.AssertQueryString(db,
            @"SELECT filename FROM content_layout_loadable_objects_view
              WHERE asset_path = 'Assets/ScriptableObjects/ContentDirectoryRoot.asset'",
            "c0152db4dd710be51b2decb997325f34.cf", "the loadable view resolves the containing file");

        // Layout-only analyze still links every non-built-in entry to a placeholder row.
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM content_layout_serialized_files WHERE is_builtin = 0 AND serialized_file IS NULL", 0,
            "a layout-only analyze links every entry to a placeholder row");

        // The views derive filenames through the artifact link.
        SQLTestHelper.AssertQueryString(db,
            "SELECT filename FROM content_layout_serialized_files_view WHERE file_index = 5",
            "c0152db4dd710be51b2decb997325f34.cf", "filenames are derived from the linked artifact");
        SQLTestHelper.AssertQueryString(db,
            "SELECT filename FROM content_layout_serialized_files_view WHERE is_builtin = 1",
            "Library/unity default resources", "built-in entries show their path as the filename");
    }

    [Test]
    public async Task Analyze_V1ContentLayout_ImportsNothing()
    {
        var layoutFolder = Path.Combine(m_TestOutputFolder, "v1_layout");
        Directory.CreateDirectory(layoutFolder);
        File.WriteAllText(Path.Combine(layoutFolder, "ContentLayout.json"),
            "{\"Version\":1,\"BuildManifestHash\":\"deadbeefdeadbeefdeadbeefdeadbeef\"}");
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stdErr) = await ConsoleTestHelper.RunCapturingStdErr("analyze", layoutFolder, "-o", databasePath);

        Assert.AreEqual(1, exitCode, "a layout-only analyze of an unsupported version has nothing to import");
        Assert.That(stdErr, Does.Contain("Unsupported ContentLayout.json version 1"));
        Assert.That(stdErr, Does.Contain("versions 2 and 3"), "the supported versions should be named");
    }
}
