using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

// Analyze replaces an existing output database instead of merging into it. That is the intended
// behaviour, but it must be announced so the database of a previous build is not destroyed
// unnoticed (issue #117).
public class AnalyzeOverwriteTests
{
    private const string OverwriteMessage = "Overwriting existing database";

    private string m_TestOutputFolder;
    private string m_AssetBundlesFolder;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "overwrite_test_folder");
        m_AssetBundlesFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "AssetBundles", "2019.4.0f1");
        Directory.CreateDirectory(m_TestOutputFolder);
        Directory.SetCurrentDirectory(m_TestOutputFolder);
    }

    [TearDown]
    public void Teardown()
    {
        SqliteConnection.ClearAllPools();
        var testDir = new DirectoryInfo(m_TestOutputFolder);
        testDir.EnumerateFiles().ToList().ForEach(f => f.Delete());
        testDir.EnumerateDirectories().ToList().ForEach(d => d.Delete(true));
    }

    private static async Task<(int exitCode, string stderr)> RunAnalyze(params string[] args)
    {
        var originalError = System.Console.Error;
        using var sw = new StringWriter();
        try
        {
            System.Console.SetError(sw);
            var exitCode = await Program.Main(new[] { "analyze" }.Concat(args).ToArray());
            return (exitCode, sw.ToString());
        }
        finally
        {
            System.Console.SetError(originalError);
        }
    }

    [Test]
    public async Task Analyze_NewDatabase_NoOverwriteMessage()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stderr) = await RunAnalyze(m_AssetBundlesFolder, "-o", databasePath);

        Assert.AreEqual(0, exitCode);
        StringAssert.DoesNotContain(OverwriteMessage, stderr);
    }

    [Test]
    public async Task Analyze_ExistingDatabase_WarnsAboutOverwrite()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (firstExitCode, _) = await RunAnalyze(m_AssetBundlesFolder, "-o", databasePath);
        Assert.AreEqual(0, firstExitCode);
        SqliteConnection.ClearAllPools();

        var (exitCode, stderr) = await RunAnalyze(m_AssetBundlesFolder, "-o", databasePath);

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(OverwriteMessage, stderr);
        StringAssert.Contains(databasePath, stderr);
    }
}
