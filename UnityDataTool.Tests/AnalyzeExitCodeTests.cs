using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

// A run that analyzed nothing must fail through both signals a caller can look at: a non-zero exit
// code, and the absence of the output database (issue #115). A run that analyzed something still
// succeeds, even when other files in the same input failed.
public class AnalyzeExitCodeTests
{
    private const string NothingAnalyzedMessage = "no files were successfully analyzed";

    private string m_TestOutputFolder;
    private string m_AssetBundlePath;
    private string m_NoTypeTreeBundle;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "exitcode_test_folder");
        m_AssetBundlePath = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "AssetBundles", "2019.4.0f1", "scenes");
        m_NoTypeTreeBundle = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "AssetBundleTypeTreeVariations", "AssetBundle-NoTypeTree", "small.bundle");
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

    private static async Task<(int exitCode, string output)> RunAnalyze(params string[] args)
    {
        var originalOut = System.Console.Out;
        var originalError = System.Console.Error;
        using var swOut = new StringWriter();
        using var swErr = new StringWriter();
        try
        {
            System.Console.SetOut(swOut);
            System.Console.SetError(swErr);
            var exitCode = await Program.Main(new[] { "analyze" }.Concat(args).ToArray());
            return (exitCode, swOut.ToString() + swErr.ToString());
        }
        finally
        {
            System.Console.SetOut(originalOut);
            System.Console.SetError(originalError);
        }
    }

    [Test]
    public async Task Analyze_NothingAnalyzed_RemovesDatabaseOfPreviousRun()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (firstExitCode, _) = await RunAnalyze(m_AssetBundlePath, "-o", databasePath);
        Assert.AreEqual(0, firstExitCode);
        Assert.That(File.Exists(databasePath), Is.True);

        var (exitCode, output) = await RunAnalyze(m_NoTypeTreeBundle, "-o", databasePath);

        Assert.AreEqual(1, exitCode);
        StringAssert.Contains(NothingAnalyzedMessage, output);
        Assert.That(File.Exists(databasePath), Is.False, "Expected the overwritten database to be removed, not left empty");
    }

    [Test]
    public async Task Analyze_SomeFilesFailed_SucceedsAndKeepsDatabase()
    {
        var inputFolder = Path.Combine(m_TestOutputFolder, "mixed_input");
        Directory.CreateDirectory(inputFolder);
        File.Copy(m_AssetBundlePath, Path.Combine(inputFolder, Path.GetFileName(m_AssetBundlePath)));
        File.Copy(m_NoTypeTreeBundle, Path.Combine(inputFolder, Path.GetFileName(m_NoTypeTreeBundle)));

        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, output) = await RunAnalyze(inputFolder, "-o", databasePath);

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains("Successfully processed files: 1", output);
        StringAssert.Contains("Files without TypeTrees: 1", output);
        StringAssert.DoesNotContain(NothingAnalyzedMessage, output);
        Assert.That(File.Exists(databasePath), Is.True);
    }
}
