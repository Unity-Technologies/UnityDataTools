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

    [Test]
    public async Task Analyze_PlayerDataNoTypeTree_ReportsFailureCorrectly()
    {
        // Test for issue #48: files that cannot be processed must not be counted as successes.
        // Files without TypeTrees are detected up front and reported in their own summary category
        // (rather than the generic failure count) so they can be told apart in a large run.
        var testDataFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "PlayerNoTypeTree");

        using var swOut = new StringWriter();
        using var swErr = new StringWriter();
        var currentOut = Console.Out;
        var currentErr = Console.Error;
        try
        {
            Console.SetOut(swOut);
            Console.SetError(swErr);

            // Analyze should return 0 even if files fail (non-zero would be a critical error)
            Assert.AreEqual(0, await Program.Main(new string[] { "analyze", Path.Combine(testDataFolder, "level0") }));

            var output = swOut.ToString() + swErr.ToString();

            // Check that the filename appears in the error output
            Assert.That(output, Does.Contain("level0"), "Expected 'level0' to appear in error output");

            // Check that the summary line categorizes the file as missing TypeTrees, not a success.
            Assert.That(output, Does.Contain("Files without TypeTrees: 1"), "Expected 'Files without TypeTrees: 1' in summary");
            Assert.That(output, Does.Contain("Successfully processed files: 0"), "Expected 'Successfully processed files: 0' in summary");
        }
        finally
        {
            Console.SetOut(currentOut);
            Console.SetError(currentErr);
        }
    }

    [Test]
    public async Task Analyze_AssetBundleNoTypeTree_ReportsMissingTypeTreesWithoutCrashing()
    {
        // A no-TypeTree SerializedFile inside an archive must be detected up front and skipped
        // cleanly. Handing it to the native loader otherwise emits misleading version mismatch
        // errors and can crash the process with an access violation.
        var bundlePath = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "AssetBundleTypeTreeVariations", "AssetBundle-NoTypeTree", "small.bundle");

        using var swOut = new StringWriter();
        using var swErr = new StringWriter();
        var currentOut = Console.Out;
        var currentErr = Console.Error;
        try
        {
            Console.SetOut(swOut);
            Console.SetError(swErr);

            // Analyze should return 0 even when a bundle has no TypeTrees (no crash, no critical error).
            Assert.AreEqual(0, await Program.Main(new string[] { "analyze", bundlePath }));

            var output = swOut.ToString() + swErr.ToString();

            Assert.That(output, Does.Contain("Skipped (no TypeTrees)"), "Expected the file to be reported as skipped");
            Assert.That(output, Does.Contain("Files without TypeTrees: 1"), "Expected 'Files without TypeTrees: 1' in summary");
            Assert.That(output, Does.Contain("Successfully processed files: 0"), "Expected 'Successfully processed files: 0' in summary");
        }
        finally
        {
            Console.SetOut(currentOut);
            Console.SetError(currentErr);
        }
    }
}
