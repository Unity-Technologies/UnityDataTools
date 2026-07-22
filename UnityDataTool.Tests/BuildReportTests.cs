using Microsoft.Data.Sqlite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using System.Collections.Generic;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

public class BuildReportTests
{
    private string m_TestOutputFolder;
    private string m_TestDataFolder;

    // Unity 6.6 reference reports, which include the new Summary fields and a ContentSummary object.
    private string m_ContentDirectoryReport;
    private string m_AssetBundleReport;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_folder");
        m_TestDataFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "BuildReports");
        var leadingEdge = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "LeadingEdgeBuilds");
        m_ContentDirectoryReport = Path.Combine(leadingEdge, "BuildReport-ContentDirectory", "f64157fb08bb9f645971d39c1203bd03.buildreport");
        m_AssetBundleReport = Path.Combine(leadingEdge, "BuildReport-AssetBundles", "LastBuild.buildreport");
        Directory.CreateDirectory(m_TestOutputFolder);
        Directory.SetCurrentDirectory(m_TestOutputFolder);
    }

    [TearDown]
    public void Teardown()
    {
        SqliteConnection.ClearAllPools();

        var testDir = new DirectoryInfo(m_TestOutputFolder);
        testDir.EnumerateFiles()
            .ToList().ForEach(f => f.Delete());
        testDir.EnumerateDirectories()
            .ToList().ForEach(d => d.Delete(true));
    }

    // Check the primary object/file tables and views which are populated by the general
    // object handling of the analyzer (e.g. nothing BuildReport specific)
    // This test is parameterized to run with and without "--skip-references"
    // in order to show that the core object tables are not impacted by whether
    // or not references are tracked.
    [Test]
    public async Task Analyze_BuildReport_ContainsExpected_ObjectInfo(
        [Values(false, true)] bool skipReferences)
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var args = new List<string> { "analyze", Path.Combine(m_TestDataFolder, "AssetBundle.buildreport") };
        if (skipReferences)
            args.Add("--skip-references");

        Assert.AreEqual(0, await Program.Main(args.ToArray()));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        // Sanity check the Unity objects found in this Build report file
        // Tip: The meaning of the hard coded type ids used in the queries can be found
        // at https://docs.unity3d.com/6000.3/Documentation/Manual/ClassIDReference.html

        // The BuildReport object is the most important.
        // PackedAssets objects are present for each output serialized file, .resS and .resource.
        const int packedAssetCount = 7;

        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM objects WHERE type = 1125", 1,
            "Unexpected number of BuildReport objects (type 1125)");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM objects WHERE type = 1126", packedAssetCount,
            "Unexpected number of PackedAssets objects");

        // This object is expected inside AssetBundle builds
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM objects WHERE type = 668709126", 1,
            "Unexpected number of BuiltAssetBundleInfoSet objects");

        // There can be other more obscure objects present, depending on the build,
        // e.g. PluginBuildInfo, AudioBuildInfo, VideoBuildInfo etc.
        var ttlObjCount = SQLTestHelper.QueryInt(db, "SELECT COUNT(*) FROM objects");
        Assert.That(ttlObjCount, Is.GreaterThanOrEqualTo(1+ packedAssetCount + 1),
            "Unexpected number of objects in BuildReport analysis");

        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM archives", 0,
            "Expected no AssetBundles found in reference folder");

        //
        // Tests using object_view which lets us refer to objects by type name
        //
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM object_view WHERE type = 'BuildReport'", 1,
            "Expected exactly one BuildReport in object_view");

        SQLTestHelper.AssertQueryString(db, "SELECT name FROM object_view WHERE type = 'BuildReport'", "Build AssetBundles",
            "Unexpected name");

        SQLTestHelper.AssertQueryString(db, "SELECT name FROM object_view WHERE type = 'BuildReport'", "Build AssetBundles",
            "Unexpected BuildReport name in object_view");

        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM object_view WHERE type = 'PackedAssets'", packedAssetCount,
            "Unexpected number of PackedAssets in object_view");

        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM object_view WHERE type = 'BuiltAssetBundleInfoSet'", 1,
            "Expected exactly one BuiltAssetBundleInfoSet in object_view");

        // Verify all rows have the same serialized_file
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(DISTINCT serialized_file) FROM object_view", 1,
            "All objects should be from the same serialized file");

        SQLTestHelper.AssertQueryString(db, "SELECT DISTINCT serialized_file FROM object_view", "AssetBundle.buildreport",
            "Unexpected serialized file name in object_view");

        // Verify the BuildReport object has expected properties
        var buildReportSize = SQLTestHelper.QueryInt(db, "SELECT size FROM object_view WHERE type = 'BuildReport'");
        Assert.That(buildReportSize, Is.GreaterThan(0), "BuildReport size should be greater than 0");

        //
        // Tests using view_breakdown_by_type which aggregates objects by type
        //

        // Verify counts match for specific types
        SQLTestHelper.AssertQueryInt(db, "SELECT count FROM view_breakdown_by_type WHERE type = 'BuildReport'", 1,
            "Expected 1 BuildReport in breakdown view");
        SQLTestHelper.AssertQueryInt(db, "SELECT count FROM view_breakdown_by_type WHERE type = 'PackedAssets'", packedAssetCount,
            "Expected 7 PackedAssets in breakdown view");

        var buildReportSize2 = SQLTestHelper.QueryInt(db, "SELECT byte_size FROM view_breakdown_by_type WHERE type = 'BuildReport'");
        Assert.AreEqual(buildReportSize, buildReportSize2, "Mismatch between object_view and breakdown_view for BuildReport size");

        // Verify pretty_size formatting exists
        var buildReportPrettySize = SQLTestHelper.QueryString(db, "SELECT pretty_size FROM view_breakdown_by_type WHERE type = 'BuildReport'");
        Assert.That(buildReportPrettySize, Does.Contain("KB").Or.Contain("B"), "BuildReport pretty_size should have size unit");

        // Verify total byte_size across all types
        var totalSize = SQLTestHelper.QueryInt(db, "SELECT SUM(byte_size) FROM view_breakdown_by_type");
        Assert.That(totalSize, Is.GreaterThan(buildReportSize),
            "Unexpected number of objects in BuildReport analysis");

        //
        // Tests using serialized_files table
        //
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM serialized_files", 1,
            "Expected exactly one serialized file");

        SQLTestHelper.AssertQueryString(db, "SELECT name FROM serialized_files WHERE id = 0", "AssetBundle.buildreport",
            "Unexpected serialized file name");

        // Verify archive column is empty/NULL for BuildReport files (they are not inside an archive)
        var archiveValue = SQLTestHelper.QueryString(db, "SELECT COALESCE(archive, '') FROM serialized_files WHERE id = 0");
        Assert.That(string.IsNullOrEmpty(archiveValue), "BuildReport serialized file should not have archive value");

        // Verify the serialized file name matches what we see in object_view
        var serializedFileName = SQLTestHelper.QueryString(db, "SELECT name FROM serialized_files WHERE id = 0");
        var objectViewFileName = SQLTestHelper.QueryString(db, "SELECT DISTINCT serialized_file FROM object_view");
        Assert.AreEqual(serializedFileName, objectViewFileName,
            "Serialized file name should match between serialized_files table and object_view");
    }

    // The BuildReport file has a simple structure with a single BuildReport object
    // and all other objects referenced from its Appendicies array.
    // This gives an opportunity for a detailed test that the "refs" table is properly populated.
    [Test]
    public async Task Analyze_BuildReport_ContainsExpectedReferences(
        [Values(false, true)] bool skipReferences)
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var args = new List<string> { "analyze", Path.Combine(m_TestDataFolder, "AssetBundle.buildreport") };
        if (skipReferences)
            args.Add("--skip-references");

        Assert.AreEqual(0, await Program.Main(args.ToArray()));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        if (skipReferences)
        {
            // When --skip-references is used, the refs table should be empty
            SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM refs", 0,
                "refs table should be empty when --skip-references is used");
            return;
        }

        var buildReportId = SQLTestHelper.QueryInt(db,
            "SELECT id FROM objects WHERE type = 1125");

        var totalObjectCount = SQLTestHelper.QueryInt(db, "SELECT COUNT(*) FROM objects");

        var expectedRefCount = totalObjectCount - 1;
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM refs", expectedRefCount,
            "BuildReport should reference all other objects");

        SQLTestHelper.AssertQueryInt(db, $"SELECT COUNT(*) FROM refs WHERE object = {buildReportId}", expectedRefCount,
            "All references should originate from BuildReport object");

        SQLTestHelper.AssertQueryInt(db, $"SELECT COUNT(*) FROM refs WHERE referenced_object = {buildReportId}", 0,
            "No object should reference the BuildReport object");

        var refsWithWrongPath = SQLTestHelper.QueryInt(db,
            "SELECT COUNT(*) FROM refs_view WHERE property_path NOT LIKE 'm_Appendices[%]'");
        Assert.AreEqual(0, refsWithWrongPath, "All property_path values should match pattern 'm_Appendices[N]'");

        SQLTestHelper.AssertQueryString(db, "SELECT DISTINCT property_type FROM refs_view", "Object",
            "All references should have property_type 'Object'");

        var objectsNotReferenced = SQLTestHelper.QueryInt(db,
            $@"SELECT COUNT(*) FROM objects
                WHERE id != {buildReportId}
                AND id NOT IN (SELECT referenced_object FROM refs)");
        Assert.AreEqual(0, objectsNotReferenced,
            "Every object except BuildReport should be referenced exactly once");

        var duplicateRefs = SQLTestHelper.QueryInt(db,
            "SELECT COUNT(*) FROM (SELECT referenced_object, COUNT(*) as cnt FROM refs GROUP BY referenced_object HAVING cnt > 1)");
        Assert.AreEqual(0, duplicateRefs,
            "No object should be referenced more than once");
    }

    [Test]
    public async Task Analyze_BuildReport_AssetBundle_ContainsBuildReportData()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var args = new List<string> { "analyze", Path.Combine(m_TestDataFolder, "AssetBundle.buildreport") };

        Assert.AreEqual(0, await Program.Main(args.ToArray()));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM build_reports", 1,
            "Expected exactly one row in build_reports table");
        SQLTestHelper.AssertQueryString(db, "SELECT platform_name FROM build_reports", "Win64",
            "Unexpected platform_name");
        SQLTestHelper.AssertQueryString(db, "SELECT build_type FROM build_reports", "AssetBundle",
            "Unexpected build_type");
        SQLTestHelper.AssertQueryInt(db, "SELECT subtarget FROM build_reports", 2,
            "Unexpected subtarget");
        SQLTestHelper.AssertQueryInt(db, "SELECT total_errors FROM build_reports", 0,
            "Unexpected total_errors");
        SQLTestHelper.AssertQueryInt(db, "SELECT total_warnings FROM build_reports", 0,
            "Unexpected total_warnings");
        SQLTestHelper.AssertQueryString(db, "SELECT build_result FROM build_reports", "Succeeded",
            "Unexpected build_result");

        var outputPath = SQLTestHelper.QueryString(db, "SELECT output_path FROM build_reports");
        Assert.That(outputPath, Does.Contain("AssetBundles"), "Output path should contain 'AssetBundles'");

        var totalSize = SQLTestHelper.QueryInt(db, "SELECT total_size FROM build_reports");
        Assert.That(totalSize, Is.GreaterThan(0), "total_size should be greater than 0");
    }

    [Test]
    public async Task Analyze_BuildReport_Player_ContainsBuildReportData()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var args = new List<string> { "analyze", Path.Combine(m_TestDataFolder, "Player.buildreport") };

        Assert.AreEqual(0, await Program.Main(args.ToArray()));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM build_reports", 1,
            "Expected exactly one row in build_reports table");
        SQLTestHelper.AssertQueryString(db, "SELECT build_type FROM build_reports", "Player",
            "Unexpected build_type");

        // These checks are based on knowledge what the specific values in this test build report
        SQLTestHelper.AssertQueryString(db, "SELECT build_guid FROM build_reports", "c743e3c6c0a541a69eae606c7991234e",
            "Unexpected build_guid");
        SQLTestHelper.AssertQueryInt(db, "SELECT subtarget FROM build_reports", 2,
            "Unexpected subtarget");
        SQLTestHelper.AssertQueryInt(db, "SELECT options FROM build_reports", 137,
            "Unexpected options");
        SQLTestHelper.AssertQueryString(db, "SELECT build_result FROM build_reports", "Succeeded",
            "Unexpected build_result");
        SQLTestHelper.AssertQueryString(db, "SELECT start_time FROM build_reports", "2025-12-29T13:03:00.5010432Z",
            "Unexpected start time");
        SQLTestHelper.AssertQueryString(db, "SELECT end_time FROM build_reports", "2025-12-29T13:03:06.3987171Z",
            "Unexpected end time");
        SQLTestHelper.AssertQueryInt(db, "SELECT total_time_seconds FROM build_reports", 6,
            "Unexpected total_time_seconds");

        var totalSize = SQLTestHelper.QueryInt(db, "SELECT total_size FROM build_reports");
        Assert.That(totalSize, Is.GreaterThan(0), "total_size should be greater than 0");

        var outputPath = SQLTestHelper.QueryString(db, "SELECT output_path FROM build_reports");
        Assert.That(outputPath, Does.Contain("TestProject.exe"), "Output path should contain 'TestProject.exe'");
    }

    [Test]
    public async Task Analyze_BuildReport_AssetBundle_ContainsPackedAssetsData()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var args = new List<string> { "analyze", Path.Combine(m_TestDataFolder, "AssetBundle.buildreport") };

        Assert.AreEqual(0, await Program.Main(args.ToArray()));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        // Verify the build_report_packed_assets table has the expected number of rows
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM build_report_packed_assets", 7,
            "Expected exactly 7 rows in build_report_packed_assets table");

        // Verify the specific PackedAssets object (corresponds to raw object ID -2699881322159949766 in the file)
        const string path = "CAB-6b49068aebcf9d3b05692c8efd933167";
        SQLTestHelper.AssertQueryInt(db, $"SELECT COUNT(*) FROM build_report_packed_assets WHERE path = '{path}'", 1,
            $"Expected exactly one PackedAssets with path = {path}");

        SQLTestHelper.AssertQueryInt(db, $"SELECT file_header_size FROM build_report_packed_assets WHERE path = '{path}'", 10720,
            "Unexpected file_header_size for PackedAssets");

        // Get the database ID for this PackedAssets
        var packedAssetId = SQLTestHelper.QueryInt(db, $"SELECT id FROM build_report_packed_assets WHERE path = '{path}'");

        // Verify there are 7 content rows for this PackedAssets
        SQLTestHelper.AssertQueryInt(db, $"SELECT COUNT(*) FROM build_report_packed_asset_info WHERE packed_assets_id = {packedAssetId}", 7,
            "Expected exactly 7 rows in build_report_packed_asset_info for this PackedAssets");

        // Verify the specific content row (data[3] from the dump)
        const long objectId = -1350043613627603771;
        var contentRow = SQLTestHelper.QueryInt(db,
            $@"SELECT COUNT(*) FROM build_report_packed_asset_contents_view
               WHERE packed_assets_id = {packedAssetId}
               AND object_id = {objectId}
               AND type = 'Texture2D'
               AND size = 204
               AND offset = 11840
               AND source_asset_guid = '8826f464101b93c4bb006e15a9aff317'
               AND build_time_asset_path = 'Assets/Sprites/Snow.jpg'");

        Assert.AreEqual(1, contentRow,
            "Expected exactly one packed_asset_contents row matching the specified criteria");

        // The type column shows the human-readable name (from TypeIdRegistry) even though the
        // build report records only the numeric class id and no build output was analyzed here.
        SQLTestHelper.AssertQueryString(db,
            $@"SELECT type FROM build_report_packed_asset_contents_view
               WHERE packed_assets_id = {packedAssetId}
               AND object_id = {objectId}",
            "Texture2D",
            "Expected type name 'Texture2D' in build_report_packed_asset_contents_view");

        // Verify the view works correctly for this content row
        SQLTestHelper.AssertQueryString(db,
            $@"SELECT source_asset_guid FROM build_report_packed_asset_contents_view 
               WHERE packed_assets_id = {packedAssetId} 
               AND object_id = {objectId}",
            "8826f464101b93c4bb006e15a9aff317",
            "Unexpected source_asset_guid in build_report_packed_asset_contents_view");

        SQLTestHelper.AssertQueryString(db,
            $@"SELECT build_time_asset_path FROM build_report_packed_asset_contents_view 
               WHERE packed_assets_id = {packedAssetId} 
               AND object_id = {objectId}",
            "Assets/Sprites/Snow.jpg",
            "Unexpected build_time_asset_path in build_report_packed_asset_contents_view");

        SQLTestHelper.AssertQueryString(db,
            $"SELECT path FROM build_report_packed_assets_view WHERE id = {packedAssetId}",
            "CAB-6b49068aebcf9d3b05692c8efd933167",
            "Unexpected path in build_report_packed_assets_view");
    }

    // The motivating case for issue #49: combine a scanned build-output directory with a build
    // report file that lives in a separate location, all in a single analyze invocation.
    [Test]
    public async Task Analyze_DirectoryPlusExternalFile_BothIncluded()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        // Simulate a build output directory containing one report, with a second report kept elsewhere.
        var buildOutputDir = Path.Combine(m_TestOutputFolder, "build_output");
        Directory.CreateDirectory(buildOutputDir);
        File.Copy(Path.Combine(m_TestDataFolder, "AssetBundle.buildreport"),
            Path.Combine(buildOutputDir, "AssetBundle.buildreport"));

        var args = new string[]
        {
            "analyze",
            buildOutputDir,
            Path.Combine(m_TestDataFolder, "Player.buildreport"),
        };

        Assert.AreEqual(0, await Program.Main(args));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM build_reports", 2,
            "Expected both the scanned directory's report and the external report");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM build_reports WHERE build_type = 'AssetBundle'", 1,
            "Expected the AssetBundle report from the scanned directory");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM build_reports WHERE build_type = 'Player'", 1,
            "Expected the Player report passed as an external file");
    }

    [Test]
    public async Task Analyze_BuildReports_BothReports_ContainsBuildReportFilesData()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        // Analyze multiple BuildReports into the same database by passing each file explicitly.
        var args = new List<string>
        {
            "analyze",
            Path.Combine(m_TestDataFolder, "AssetBundle.buildreport"),
            Path.Combine(m_TestDataFolder, "Player.buildreport"),
        };

        Assert.AreEqual(0, await Program.Main(args.ToArray()));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        // Verify we have 2 BuildReports
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM build_reports", 2,
            "Expected exactly 2 BuildReports");

        // Verify we have files from both BuildReports
        var totalFiles = SQLTestHelper.QueryInt(db, "SELECT COUNT(*) FROM build_report_files");
        Assert.That(totalFiles, Is.GreaterThan(0), "Expected at least some files in build_report_files");

        // Verify that an expected file from AssetBundle.buildreport is present
        var assetBundleFileCount = SQLTestHelper.QueryInt(db,
            @"SELECT COUNT(*) FROM build_report_files
              WHERE path = 'audio.bundle/CAB-76a378bdc9304bd3c3a82de8dd97981a.resource'");
        Assert.AreEqual(1, assetBundleFileCount,
            "Expected to find one file with 'CAB-76a378bdc9304bd3c3a82de8dd97981a.resource' in path from AssetBundle.buildreport");

        // Verify that an expected file from Player.buildreport is present
        var playerFileCount = SQLTestHelper.QueryInt(db,
            @"SELECT COUNT(*) FROM build_report_files
              WHERE path = 'TestProject_Data/sharedassets0.assets.resS'");
        Assert.AreEqual(1, playerFileCount,
            "Expected to find one file with 'sharedassets0.assets.resS' in path from Player.buildreport");

        // Verify that each BuildReport has its own set of files with the correct build_report_id
        var assetBundleReportId = SQLTestHelper.QueryInt(db,
            "SELECT id FROM build_reports WHERE build_type = 'AssetBundle'");
        var playerReportId = SQLTestHelper.QueryInt(db,
            "SELECT id FROM build_reports WHERE build_type = 'Player'");

        var assetBundleFileCountByReportId = SQLTestHelper.QueryInt(db,
            $"SELECT COUNT(*) FROM build_report_files WHERE build_report_id = {assetBundleReportId}");
        Assert.That(assetBundleFileCountByReportId, Is.GreaterThan(0),
            "Expected AssetBundle BuildReport to have files");

        var playerFileCountByReportId = SQLTestHelper.QueryInt(db,
            $"SELECT COUNT(*) FROM build_report_files WHERE build_report_id = {playerReportId}");
        Assert.That(playerFileCountByReportId, Is.GreaterThan(0),
            "Expected Player BuildReport to have files");

        // Verify the view includes serialized_file and can filter by it
        var playerFilesInView = SQLTestHelper.QueryInt(db,
            @"SELECT COUNT(*) FROM build_report_files_view 
              WHERE serialized_file = 'Player.buildreport'");
        Assert.That(playerFilesInView, Is.GreaterThan(0),
            "Expected to find files from Player.buildreport in the view using serialized_file");

        // Verify we can find the specific Player.buildreport file in the view
        var specificPlayerFile = SQLTestHelper.QueryInt(db,
            @"SELECT COUNT(*) FROM build_report_files_view 
              WHERE serialized_file = 'Player.buildreport'
              AND path = 'TestProject_Data/sharedassets0.assets.resS'");
        Assert.AreEqual(1, specificPlayerFile,
            "Expected to find exactly one row with path='TestProject_Data/sharedassets0.assets.resS' from Player.buildreport in view");

        // Verify the serialized_file column correctly identifies the source BuildReport
        var assetBundleSerializedFile = SQLTestHelper.QueryString(db,
            @"SELECT DISTINCT serialized_file FROM build_report_files_view 
              WHERE path = 'audio.bundle/CAB-76a378bdc9304bd3c3a82de8dd97981a.resource'");
        Assert.AreEqual("AssetBundle.buildreport", assetBundleSerializedFile,
            "Expected serialized_file to be 'AssetBundle.buildreport' for AssetBundle files");

        var playerSerializedFile = SQLTestHelper.QueryString(db,
            @"SELECT DISTINCT serialized_file FROM build_report_files_view 
              WHERE path = 'TestProject_Data/sharedassets0.assets.resS'");
        Assert.AreEqual("Player.buildreport", playerSerializedFile,
            "Expected serialized_file to be 'Player.buildreport' for Player files");

        // Verify build_report_archive_contents table has entries for AssetBundle build
        var archiveContentsCount = SQLTestHelper.QueryInt(db,
            $"SELECT COUNT(*) FROM build_report_archive_contents WHERE build_report_id = {assetBundleReportId}");
        Assert.That(archiveContentsCount, Is.GreaterThan(0),
            "Expected AssetBundle BuildReport to have archive contents mappings");

        // Verify specific archive content mapping exists
        var spritesArchiveContentCount = SQLTestHelper.QueryInt(db,
            $@"SELECT COUNT(*) FROM build_report_archive_contents 
               WHERE build_report_id = {assetBundleReportId}
               AND archive = 'sprites.bundle'
               AND archive_content = 'CAB-6b49068aebcf9d3b05692c8efd933167.resS'");
        Assert.AreEqual(1, spritesArchiveContentCount,
            "Expected to find mapping for sprites.bundle -> CAB-6b49068aebcf9d3b05692c8efd933167.resS");

        // Verify Player build has no archive contents (not an AssetBundle build)
        var playerArchiveContentsCount = SQLTestHelper.QueryInt(db,
            $"SELECT COUNT(*) FROM build_report_archive_contents WHERE build_report_id = {playerReportId}");
        Assert.AreEqual(0, playerArchiveContentsCount,
            "Expected Player BuildReport to have no archive contents mappings");

        // Verify build_report_packed_assets_view includes archive column
        var packedAssetsWithBundle = SQLTestHelper.QueryInt(db,
            @"SELECT COUNT(*) FROM build_report_packed_assets_view 
              WHERE archive IS NOT NULL");
        Assert.That(packedAssetsWithBundle, Is.GreaterThan(0),
            "Expected some PackedAssets to have archive name populated");

        // Verify specific PackedAsset has correct archive name
        var specificPackedAssetBundle = SQLTestHelper.QueryString(db,
            @"SELECT archive FROM build_report_packed_assets_view 
              WHERE path = 'CAB-6b49068aebcf9d3b05692c8efd933167'");
        Assert.AreEqual("sprites.bundle", specificPackedAssetBundle,
            "Expected PackedAsset CAB-6b49068aebcf9d3b05692c8efd933167 to have archive 'sprites.bundle'");

        // Verify PackedAssets from Player build have NULL archive
        var playerPackedAssetsWithNullBundle = SQLTestHelper.QueryInt(db,
            @"SELECT COUNT(*) FROM build_report_packed_assets_view 
              WHERE build_report_filename = 'Player.buildreport' AND archive IS NULL");
        Assert.That(playerPackedAssetsWithNullBundle, Is.GreaterThan(0),
            "Expected PackedAssets from Player.buildreport to have NULL archive");

        var playerPackedAssetsWithNonNullBundle = SQLTestHelper.QueryInt(db,
            @"SELECT COUNT(*) FROM build_report_packed_assets_view 
              WHERE build_report_filename = 'Player.buildreport' AND archive IS NOT NULL");
        Assert.AreEqual(0, playerPackedAssetsWithNonNullBundle,
            "Expected all PackedAssets from Player.buildreport have NULL archive");
    }

    // The Unity 6.6 Summary adds several fields (issue #107 Part 1). Verify they land in the new
    // build_reports columns for a ContentDirectory report, which populates all of them.
    [Test]
    public async Task Analyze_BuildReport_ContentDirectory_ContainsNewSummaryColumns()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        Assert.AreEqual(0, await Program.Main(new[] { "analyze", m_ContentDirectoryReport }));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        SQLTestHelper.AssertQueryString(db, "SELECT build_type FROM build_reports", "ContentDirectory",
            "Unexpected build_type");
        SQLTestHelper.AssertQueryString(db, "SELECT build_name FROM build_reports", "ContentDirectory",
            "Unexpected build_name");
        SQLTestHelper.AssertQueryInt(db, "SELECT build_content_options FROM build_reports", 32,
            "Unexpected build_content_options");
        // The session GUID matches the report's GUID-based filename.
        SQLTestHelper.AssertQueryString(db, "SELECT build_session_guid FROM build_reports",
            "f64157fb08bb9f645971d39c1203bd03", "Unexpected build_session_guid");
        SQLTestHelper.AssertQueryString(db, "SELECT build_manifest_hash FROM build_reports",
            "baff06b928d147276f2245dd3b19216a", "Unexpected build_manifest_hash");
        // No build profile was active: the path is present-but-empty, and the all-zero GUID -> NULL.
        SQLTestHelper.AssertQueryString(db, "SELECT build_profile_path FROM build_reports",
            "", "Expected empty build_profile_path");
        SQLTestHelper.AssertQueryString(db, "SELECT COALESCE(build_profile_guid, 'NULL') FROM build_reports",
            "NULL", "Expected NULL build_profile_guid");

        var dataPath = SQLTestHelper.QueryString(db, "SELECT data_path FROM build_reports");
        Assert.That(dataPath, Does.Contain("ContentDirectory"), "Unexpected data_path");
    }

    // An AssetBundle build has no build name; that field is present-but-empty in the report.
    [Test]
    public async Task Analyze_BuildReport_AssetBundle_HasEmptyBuildName()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        Assert.AreEqual(0, await Program.Main(new[] { "analyze", m_AssetBundleReport }));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        SQLTestHelper.AssertQueryString(db, "SELECT build_name FROM build_reports", "",
            "Expected empty build_name for AssetBundle build");
        SQLTestHelper.AssertQueryInt(db, "SELECT build_content_options FROM build_reports", 0,
            "Expected zero build_content_options for AssetBundle build");
    }

    // Older Unity reports lack all the new Summary fields; the columns must be NULL and analyze
    // must still succeed.
    [Test]
    public async Task Analyze_BuildReport_OldReport_NewSummaryColumnsAreNull()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        Assert.AreEqual(0, await Program.Main(new[] { "analyze", Path.Combine(m_TestDataFolder, "Player.buildreport") }));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        SQLTestHelper.AssertQueryInt(db,
            @"SELECT COUNT(*) FROM build_reports
              WHERE build_name IS NULL AND build_content_options IS NULL
              AND build_session_guid IS NULL AND build_manifest_hash IS NULL
              AND build_profile_path IS NULL AND build_profile_guid IS NULL AND data_path IS NULL",
            1, "Expected all new Summary columns to be NULL for an old report");
    }

    // issue #107 Part 2: a ContentSummary object (Unity 6.6+) populates the on-demand
    // build_report_content_* tables and views.
    [Test]
    public async Task Analyze_BuildReport_ContentDirectory_ContainsContentSummary()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        Assert.AreEqual(0, await Program.Main(new[] { "analyze", m_ContentDirectoryReport }));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM build_report_content_summary", 1,
            "Expected exactly one ContentSummary row");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM build_report_content_type_stats", 14,
            "Unexpected number of type stats rows");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM build_report_content_asset_stats", 15,
            "Unexpected number of asset stats rows");

        // Spot-check the cross-build stats.
        SQLTestHelper.AssertQueryInt(db, "SELECT serialized_file_size FROM build_report_content_summary", 158832,
            "Unexpected serialized_file_size");
        SQLTestHelper.AssertQueryInt(db, "SELECT object_count FROM build_report_content_summary", 28,
            "Unexpected object_count");

        // Spot-check a specific type stat (Cubemap, class id 89).
        SQLTestHelper.AssertQueryInt(db,
            "SELECT size FROM build_report_content_type_stats WHERE type = 89", 524532,
            "Unexpected size for type 89");
        SQLTestHelper.AssertQueryInt(db,
            "SELECT resource_count FROM build_report_content_type_stats WHERE type = 89", 1,
            "Unexpected resource_count for type 89");

        // The type-stats view resolves the class id to a name and the owning build report.
        SQLTestHelper.AssertQueryString(db,
            "SELECT type_name FROM build_report_content_type_stats_view WHERE type = 89", "Cubemap",
            "Expected type_name 'Cubemap' for type 89");

        var buildReportId = SQLTestHelper.QueryInt(db, "SELECT id FROM objects WHERE type = 1125");
        SQLTestHelper.AssertQueryInt(db,
            $"SELECT COUNT(*) FROM build_report_content_type_stats_view WHERE build_report_id != {buildReportId}", 0,
            "All type stats should resolve to the single BuildReport object");
        SQLTestHelper.AssertQueryInt(db,
            $"SELECT build_report_id FROM build_report_content_summary_view", buildReportId,
            "ContentSummary should resolve to the BuildReport object in the same file");

        // A specific source asset appears in the asset stats.
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM build_report_content_asset_stats WHERE source_asset_path = 'Assets/Audio/a.mp3'",
            1, "Expected the a.mp3 source asset in asset stats");
    }

    // The ContentSummary tables are created on demand, like PackedAssets: an older report without a
    // ContentSummary object must not create them.
    [Test]
    public async Task Analyze_BuildReport_OldReport_NoContentSummaryTables()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        Assert.AreEqual(0, await Program.Main(new[] { "analyze", Path.Combine(m_TestDataFolder, "Player.buildreport") }));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM sqlite_master WHERE name LIKE 'build_report_content%'", 0,
            "Expected no ContentSummary tables/views for a report without a ContentSummary object");
    }

    // When multiple 6.6 reports are analyzed together, each ContentSummary's rows must resolve back
    // to the BuildReport object in its own serialized file.
    [Test]
    public async Task Analyze_BuildReport_MultipleReports_ContentSummaryJoinsCorrectly()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        Assert.AreEqual(0, await Program.Main(new[] { "analyze", m_ContentDirectoryReport, m_AssetBundleReport }));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM build_report_content_summary", 2,
            "Expected one ContentSummary per report");

        // Each report's type stats resolve to a build report in the matching serialized file.
        SQLTestHelper.AssertQueryInt(db,
            @"SELECT COUNT(*) FROM build_report_content_type_stats_view
              WHERE build_report_filename = 'f64157fb08bb9f645971d39c1203bd03.buildreport'
              AND type_name = 'Cubemap'",
            1, "Expected the ContentDirectory report's Cubemap type stat");

        // Every type/summary row resolves to a non-NULL build report.
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM build_report_content_summary_view WHERE build_report_id IS NULL", 0,
            "Every ContentSummary should resolve to a BuildReport");
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM build_report_content_type_stats_view WHERE build_report_id IS NULL", 0,
            "Every type stat should resolve to a BuildReport");
    }
}
