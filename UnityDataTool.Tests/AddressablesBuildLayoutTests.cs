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

        using var cmd = db.CreateCommand();

        // Sanity check some expected content in the output SQLite database
        cmd.CommandText =
            @"SELECT
                (SELECT COUNT(*) FROM addressables_builds),
                (SELECT COUNT(*) FROM addressables_builds WHERE name = ""buildlayout_2025.01.28.16.35.01.json""),
                (SELECT unity_version FROM addressables_builds WHERE id = 1),
                (SELECT package_version FROM addressables_builds WHERE id = 1),
                (SELECT COUNT(*) FROM addressables_build_bundles WHERE build_id = 1 and name = ""samplepack1_assets_0.bundle""),
                (SELECT file_size FROM addressables_build_bundles WHERE build_id = 2 and name = ""samplepack1_assets_0.bundle""),
                (SELECT packing_mode FROM addressables_build_groups WHERE build_id = 1 and name = ""SamplePack1""),
                (SELECT COUNT(*) FROM asset_bundles)";

        using var reader = cmd.ExecuteReader();
        reader.Read();

        Assert.AreEqual(2, reader.GetInt32(0), "Unexpected number of builds");
        Assert.AreEqual(1, reader.GetInt32(1), "Failed to find build matching reference filename");
        Assert.AreEqual("6000.1.0b2", reader.GetString(2), "Unexpected Unity Version");
        Assert.AreEqual("com.unity.addressables: 2.2.2", reader.GetString(3), "Unexpected Addressables version");
        Assert.AreEqual(1, reader.GetInt32(4), "Expected to find specific AssetBundle by name");
        Assert.AreEqual(33824, reader.GetInt32(5), "Unexpected size for specific AssetBundle in build 2");
        Assert.AreEqual("PackSeparately", reader.GetString(6), "Unexpected packing_mode for group");
        Assert.AreEqual(0, reader.GetInt32(7), "Expected no AssetBundles found in reference folder");
    }
}
