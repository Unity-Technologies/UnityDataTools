using System;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

public class AddressablesBuildLayoutTests
{
    private string m_TestOutputFolder;
    private string m_TestDataFolder;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_folder");
        m_TestDataFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");
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

    [Test]
    public async Task Analyze_BuildLayout_ContainsExpectedSQLContent()
    {
        // This folder contains reference files from two builds of the "AudioExample"
        // Addressables test project.
        // The test confirms some expected content in the database
        var path = Path.Combine(m_TestDataFolder, "AddressableBuildLayouts");

        var databasePath = Path.Combine(m_TestOutputFolder, "database.db");

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", path, "-p", "*.json" }));
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            ForeignKeys = false,
        }.ConnectionString);
        db.Open();

        // Sanity check some expected content in the output SQLite database
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM addressables_builds", 2,
            "Unexpected number of builds");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM addressables_builds WHERE name = \"buildlayout_2025.01.28.16.35.01.json\"", 1,
            "Failed to find build matching reference filename");
        SQLTestHelper.AssertQueryString(db, "SELECT unity_version FROM addressables_builds WHERE id = 1", "6000.1.0b2",
            "Unexpected Unity Version");
        SQLTestHelper.AssertQueryString(db, "SELECT package_version FROM addressables_builds WHERE id = 1", "com.unity.addressables: 2.2.2",
            "Unexpected Addressables version");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM addressables_build_bundles WHERE build_id = 1 and name = \"samplepack1_assets_0.bundle\"", 1,
            "Expected to find specific AssetBundle by name");
        SQLTestHelper.AssertQueryInt(db, "SELECT file_size FROM addressables_build_bundles WHERE build_id = 2 and name = \"samplepack1_assets_0.bundle\"", 33824,
            "Unexpected size for specific AssetBundle in build 2");
        SQLTestHelper.AssertQueryString(db, "SELECT packing_mode FROM addressables_build_groups WHERE build_id = 1 and name = \"SamplePack1\"", "PackSeparately",
            "Unexpected packing_mode for group");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM asset_bundles", 0,
            "Expected no AssetBundles found in reference folder");
    }
}
