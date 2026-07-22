using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

// Tests the --build-history option of analyze (issue #99): the folder of the analyzed build is
// located inside a build history (Library/BuildHistory) by matching the BuildManifestHash of its
// ContentLayout.json, and that folder's layout and build report join the analysis. Build history
// folders are assembled per test from the BuildReport-ContentDirectory fixture of the LeadingEdge
// reference build.
public class AnalyzeBuildHistoryTests
{
    private const string BuildHash = "baff06b928d147276f2245dd3b19216a";

    private string m_TestOutputFolder;
    private string m_ContentDirectory;
    private string m_FixtureReportFolder;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "build_history_test_folder");
        m_ContentDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "LeadingEdgeBuilds", "ContentDirectory");
        m_FixtureReportFolder = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "LeadingEdgeBuilds", "BuildReport-ContentDirectory");
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

    // Creates a build folder holding the reference build's ContentLayout.json, .buildreport and
    // BuildReportSummary.json.
    private string CreateMatchingBuildFolder(string historyRoot, string name)
    {
        var folder = Path.Combine(historyRoot, name);
        Directory.CreateDirectory(folder);
        File.Copy(Path.Combine(m_FixtureReportFolder, "ContentLayout.json"),
            Path.Combine(folder, "ContentLayout.json"));
        File.Copy(Path.Combine(m_FixtureReportFolder, "BuildReportSummary.json"),
            Path.Combine(folder, "BuildReportSummary.json"));
        foreach (var report in Directory.EnumerateFiles(m_FixtureReportFolder, "*.buildreport"))
            File.Copy(report, Path.Combine(folder, Path.GetFileName(report)));
        return folder;
    }

    // Creates a build folder whose layout belongs to some other build.
    private string CreateStaleBuildFolder(string historyRoot, string name)
    {
        var folder = Path.Combine(historyRoot, name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "ContentLayout.json"),
            "{\"Version\":2,\"BuildManifestHash\":\"deadbeefdeadbeefdeadbeefdeadbeef\"}");
        return folder;
    }

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

    private static async Task<(int exitCode, string stdOut)> RunCapturingStdOut(params string[] args)
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        int exitCode;
        try
        {
            Console.SetOut(sw);
            exitCode = await Program.Main(args);
        }
        finally
        {
            Console.SetOut(currentOut);
        }

        return (exitCode, sw.ToString());
    }

    [Test]
    public async Task Analyze_WithBuildHistory_ImportsLayoutAndBuildReport()
    {
        var history = Path.Combine(m_TestOutputFolder, "history");
        CreateStaleBuildFolder(history, "Build-Stale");
        CreateMatchingBuildFolder(history, "Build-Match");
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        Assert.AreEqual(0, await Program.Main(new string[]
            { "analyze", m_ContentDirectory, "--build-history", history, "-o", databasePath }));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        SQLTestHelper.AssertQueryString(db, "SELECT build_manifest_hash FROM content_layout",
            BuildHash, "the matching layout should be imported");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM build_reports", 1,
            "the build report of the matched folder should be imported");
        SQLTestHelper.AssertQueryInt(db,
            @"SELECT COUNT(*) FROM dangling_refs d
              INNER JOIN serialized_files sf ON sf.id = d.serialized_file
              WHERE sf.name LIKE '%.cfid'", 0,
            "the layout should resolve the content file references");
    }

    [Test]
    public async Task Analyze_BuildHistoryPointedAtBuildFolder_Works()
    {
        // Pointing --build-history directly at the per-build folder (instead of its parent) is
        // accepted: the folder itself is checked as well as its children.
        var history = Path.Combine(m_TestOutputFolder, "history");
        var buildFolder = CreateMatchingBuildFolder(history, "Build-Match");
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        Assert.AreEqual(0, await Program.Main(new string[]
            { "analyze", m_ContentDirectory, "--build-history", buildFolder, "-o", databasePath }));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        SQLTestHelper.AssertQueryString(db, "SELECT build_manifest_hash FROM content_layout",
            BuildHash, "the layout of the folder itself should be found");
    }

    [Test]
    public async Task Analyze_BuildHistoryWithoutMatch_Fails()
    {
        var history = Path.Combine(m_TestOutputFolder, "history");
        CreateStaleBuildFolder(history, "Build-Stale");
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stdErr) = await RunCapturingStdErr(
            "analyze", m_ContentDirectory, "--build-history", history, "-o", databasePath);

        Assert.AreEqual(1, exitCode, "a build history without the analyzed build must fail");
        Assert.That(stdErr, Does.Contain("matches the analyzed build"));
        Assert.IsFalse(File.Exists(databasePath), "no partial database should be left behind");
    }

    [Test]
    public async Task Analyze_BuildHistoryWithoutContentDirectoryInput_Fails()
    {
        // Only ContentDirectory builds can be matched against a build history (via their
        // BuildManifestHash.txt); for other content the option is an error.
        var history = Path.Combine(m_TestOutputFolder, "history");
        CreateMatchingBuildFolder(history, "Build-Match");
        var bundlePath = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "LeadingEdgeBuilds", "AssetBundles", "assetbundleroot");
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stdErr) = await RunCapturingStdErr(
            "analyze", bundlePath, "--build-history", history, "-o", databasePath);

        Assert.AreEqual(1, exitCode);
        Assert.That(stdErr, Does.Contain("requires a ContentDirectory build"));
        Assert.IsFalse(File.Exists(databasePath), "no partial database should be left behind");
    }

    [Test]
    public async Task Analyze_NonexistentBuildHistoryPath_Fails()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var exitCode = await Program.Main(new string[]
        {
            "analyze", m_ContentDirectory,
            "--build-history", Path.Combine(m_TestOutputFolder, "does_not_exist"),
            "-o", databasePath
        });

        Assert.AreNotEqual(0, exitCode, "a nonexistent build history path must fail");
        Assert.IsFalse(File.Exists(databasePath), "no partial database should be left behind");
    }

    // Overwrites the BuildReportSummary.json of a build folder with the given build start time.
    private static void SetBuildStartTime(string buildFolder, string buildStartedAt)
    {
        File.WriteAllText(Path.Combine(buildFolder, "BuildReportSummary.json"),
            $"{{\"Version\":2,\"BuildStartedAt\":\"{buildStartedAt}\"}}");
    }

    [Test]
    public async Task Analyze_MultipleMatchingBuilds_UsesMostRecent()
    {
        // Rebuilding identical content produces several history folders with the same
        // BuildManifestHash; the one with the latest BuildStartedAt (from
        // BuildReportSummary.json) wins. The older build sorts first alphabetically, so a
        // "first found wins" regression would pick it instead.
        // A folder without a BuildReportSummary.json gets a zero timestamp and loses.
        var history = Path.Combine(m_TestOutputFolder, "history");
        var older = CreateMatchingBuildFolder(history, "Build-A-Older");
        var newer = CreateMatchingBuildFolder(history, "Build-B-Newer");
        var noSummary = CreateMatchingBuildFolder(history, "Build-C-NoSummary");
        SetBuildStartTime(older, "2026-07-14T18:37:14.2095142Z");
        SetBuildStartTime(newer, "2026-07-22T09:00:00.0000000Z");
        File.Delete(Path.Combine(noSummary, "BuildReportSummary.json"));
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stdOut) = await RunCapturingStdOut(
            "analyze", m_ContentDirectory, "--build-history", history, "-o", databasePath);

        Assert.AreEqual(0, exitCode);
        Assert.That(stdOut, Does.Contain("Build-B-Newer"), "the selected folder should be reported");

        using var db = SQLTestHelper.OpenDatabase(databasePath);
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout", 1,
            "a single layout should be imported");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM build_reports", 1,
            "only the selected folder's build report should be imported");
    }

    [Test]
    public async Task Analyze_BuildHistoryAndSameLayoutPassedPositionally_UsesOneLayout()
    {
        var history = Path.Combine(m_TestOutputFolder, "history");
        CreateMatchingBuildFolder(history, "Build-Match");
        var positionalLayout = Path.Combine(m_FixtureReportFolder, "ContentLayout.json");
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stdErr) = await RunCapturingStdErr(
            "analyze", m_ContentDirectory, positionalLayout, "--build-history", history, "-o", databasePath);

        Assert.AreEqual(0, exitCode);
        Assert.That(stdErr, Does.Contain("duplicates the selected layout"),
            "the redundant copy should be reported as a duplicate, not as a mismatch");

        using var db = SQLTestHelper.OpenDatabase(databasePath);
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM content_layout", 1,
            "the layout should be imported once");
    }

    [Test]
    public async Task Analyze_ArchiveBuildWithBuildHistory_MatchesThroughArchiveHashFile()
    {
        // The compressed reference build is a different build, so the LeadingEdge history cannot
        // match — proving the hash discovery next to .archive files feeds the history lookup.
        var history = Path.Combine(m_TestOutputFolder, "history");
        CreateMatchingBuildFolder(history, "Build-Match");
        var archiveBuild = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "contentdirectory-zstd");
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stdErr) = await RunCapturingStdErr(
            "analyze", archiveBuild, "--build-history", history, "-o", databasePath);

        Assert.AreEqual(1, exitCode);
        Assert.That(stdErr, Does.Contain("matches the analyzed build"));
    }
}
