using System;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityDataTools.TestCommon;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

public class UnityDataToolPlayerDataTests : PlayerDataTestFixture
{
    private string m_TestOutputFolder;

    public UnityDataToolPlayerDataTests(Context context) : base(context)
    {
    }

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_folder");
        Directory.CreateDirectory(m_TestOutputFolder);
        Directory.SetCurrentDirectory(m_TestOutputFolder);
    }

    [TearDown]
    public void Teardown()
    {
        SqliteConnection.ClearAllPools();

        foreach (var file in new DirectoryInfo(m_TestOutputFolder).EnumerateFiles())
        {
            file.Delete();
        }
    }

    [Test]
    public async Task Analyze_PlayerData_DatabaseCorrect()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);
        var analyzePath = Path.Combine(Context.UnityDataFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", analyzePath, "-p", "*." }));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        using var cmd = db.CreateCommand();

        cmd.CommandText =
            @"SELECT
                (SELECT COUNT(*) FROM asset_bundles),
                (SELECT COUNT(*) FROM assets),
                (SELECT COUNT(*) FROM objects),
                (SELECT COUNT(*) FROM refs),
                (SELECT COUNT(*) FROM serialized_files)";

        using var reader = cmd.ExecuteReader();

        reader.Read();

        Assert.AreEqual(0, reader.GetInt32(0));
        Assert.AreEqual(0, reader.GetInt32(1));
        Assert.Greater(reader.GetInt32(2), 0);
        Assert.Greater(reader.GetInt32(3), 0);
        Assert.AreEqual(1, reader.GetInt32(4));
    }

    [Test]
    public async Task DumpText_PlayerData_TextFileCreatedCorrectly()
    {
        var path = Path.Combine(Context.UnityDataFolder, "level0");
        var outputFile = Path.Combine(m_TestOutputFolder, "level0.txt");

        Assert.AreEqual(0, await Program.Main(new string[] { "dump", path }));
        Assert.IsTrue(File.Exists(outputFile));

        var content = File.ReadAllText(outputFile);
        var expected = File.ReadAllText(Path.Combine(Context.ExpectedDataFolder, "level0.txt"));

        // Normalize  line endings.
        content = Regex.Replace(content, @"\r\n|\n\r|\r", "\n");
        expected = Regex.Replace(expected, @"\r\n|\n\r|\r", "\n");

        Assert.AreEqual(expected, content);
    }
}
