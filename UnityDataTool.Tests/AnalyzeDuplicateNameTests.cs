using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

// Tests the duplicate-name handling (issue #51): analyze supports only a single build, so a second
// SerializedFile or archive with a name that was already processed is skipped with a clear
// single-line message instead of a raw "UNIQUE constraint failed" SQLite error. Covers the three
// scenarios from the issue: loose files, archives with the same name, and differently-named
// archives (hashed bundle names) that share the same inner SerializedFile.
//
// Also covers the opposite case (issue #149): bundles sharing a file name in different sub-folders
// of one scanned directory are distinct archives, not duplicates.
public class AnalyzeDuplicateNameTests
{
    private string m_TestOutputFolder;
    private string m_AssetBundlesFolder;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "duplicate_name_test_folder");
        m_AssetBundlesFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "AssetBundles");
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

    // Runs analyze and returns its exit code plus whatever it wrote to stderr (where the
    // duplicate messages are printed).
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

    // Case 2 / Case 3: two build folders each contain an archive named "assetbundle" (and "scenes").
    // The second archive of each name is rejected before it is recorded (its contents are never
    // processed), so exactly one row per name survives and no UNIQUE constraint error is shown.
    [Test]
    public async Task Analyze_ArchivesWithSameName_SkippedWithClearMessage()
    {
        var build1 = Path.Combine(m_AssetBundlesFolder, "2019.4.0f1");
        var build2 = Path.Combine(m_AssetBundlesFolder, "2020.3.0f1");
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stderr) = await RunAnalyze(build1, build2, "-o", databasePath);

        Assert.AreEqual(0, exitCode, "analyze should continue and exit 0 after skipping duplicates");
        StringAssert.Contains("Duplicate archive name 'assetbundle'", stderr);
        StringAssert.DoesNotContain("UNIQUE constraint", stderr);

        using var db = SQLTestHelper.OpenDatabase(databasePath);
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM archives WHERE name = 'assetbundle'",
            1, "only one archive named 'assetbundle' should be recorded");
    }

    // Case 1: two loose SerializedFiles with the same name in different folders. The duplicate is
    // rejected before its transaction is opened, so only the first copy is recorded.
    [Test]
    public async Task Analyze_LooseFilesWithSameName_SkippedWithClearMessage()
    {
        var source = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "PlayerWithTypeTrees", "level0");
        var build1 = Path.Combine(m_TestOutputFolder, "build1");
        var build2 = Path.Combine(m_TestOutputFolder, "build2");
        Directory.CreateDirectory(build1);
        Directory.CreateDirectory(build2);
        File.Copy(source, Path.Combine(build1, "level0"));
        File.Copy(source, Path.Combine(build2, "level0"));
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stderr) = await RunAnalyze(m_TestOutputFolder, "-o", databasePath);

        Assert.AreEqual(0, exitCode, "analyze should continue and exit 0 after skipping duplicates");
        StringAssert.Contains("Duplicate SerializedFile name 'level0'", stderr);
        StringAssert.DoesNotContain("UNIQUE constraint", stderr);

        using var db = SQLTestHelper.OpenDatabase(databasePath);
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM serialized_files WHERE name = 'level0'",
            1, "only one SerializedFile named 'level0' should be recorded");
    }

    // AssetBundle variant shape: archives that differ only in their extension and share the same
    // inner SerializedFile. The second is skipped with the variant-specific message, printed once
    // for the archive rather than once per inner file plus a generic failure line.
    [Test]
    public async Task Analyze_AssetBundleVariants_SkippedWithVariantMessage()
    {
        var source = Path.Combine(m_AssetBundlesFolder, "2019.4.0f1", "assetbundle");
        var variantHd = Path.Combine(m_TestOutputFolder, "ui.hd");
        var variantSd = Path.Combine(m_TestOutputFolder, "ui.sd");
        File.Copy(source, variantHd);
        File.Copy(source, variantSd);
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stderr) = await RunAnalyze(variantHd, variantSd, "-o", databasePath);

        Assert.AreEqual(0, exitCode, "analyze should continue and exit 0 after skipping the variant");
        StringAssert.Contains("Skipping ui.sd: AssetBundle variant of 'ui.hd'", stderr);
        StringAssert.DoesNotContain("Duplicate SerializedFile name", stderr);
        StringAssert.DoesNotContain("Failed to process", stderr);

        using var db = SQLTestHelper.OpenDatabase(databasePath);
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM archives WHERE name IN ('ui.hd', 'ui.sd')",
            2, "both variant archives should be recorded");
    }

    // Hashed-name shape: the same archive under two different file names (as with hashed bundle
    // names). The archive names differ, so both are recorded, but they share the same inner
    // SerializedFile ("CAB-<hash>"), which is rejected the second time.
    [Test]
    public async Task Analyze_DifferentArchiveNamesSharingSerializedFile_SkippedWithClearMessage()
    {
        var source = Path.Combine(m_AssetBundlesFolder, "2019.4.0f1", "assetbundle");
        var bundleA = Path.Combine(m_TestOutputFolder, "bundleA");
        var bundleB = Path.Combine(m_TestOutputFolder, "bundleB");
        File.Copy(source, bundleA);
        File.Copy(source, bundleB);
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stderr) = await RunAnalyze(bundleA, bundleB, "-o", databasePath);

        Assert.AreEqual(0, exitCode, "analyze should continue and exit 0 after skipping duplicates");
        StringAssert.Contains("Duplicate SerializedFile name", stderr);
        StringAssert.DoesNotContain("UNIQUE constraint", stderr);

        using var db = SQLTestHelper.OpenDatabase(databasePath);
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM archives WHERE name IN ('bundleA', 'bundleB')",
            2, "both differently-named archives should be recorded");
        // The shared inner SerializedFile is analyzed once; count only files that actually have
        // objects, since references also create name-only stub rows for un-analyzed files.
        SQLTestHelper.AssertQueryInt(db,
            @"SELECT COUNT(*) FROM serialized_files
              WHERE name LIKE 'CAB-%' AND id IN (SELECT serialized_file FROM objects)",
            1, "the shared inner SerializedFile should be analyzed only once");
    }

    // Issue #149: an AssetBundle name can be a path, so a build commonly contains several bundles
    // with the same file name in different folders. They are recorded under their path relative to
    // the scanned directory and are not treated as duplicates.
    [Test]
    public async Task Analyze_SameFileNameInDifferentFolders_NamedByRelativePath()
    {
        var sourceFolder = Path.Combine(m_AssetBundlesFolder, "2019.4.0f1");
        var folderA = Path.Combine(m_TestOutputFolder, "dlc", "weapons");
        var folderB = Path.Combine(m_TestOutputFolder, "dlc", "armor");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);
        File.Copy(Path.Combine(sourceFolder, "assetbundle"), Path.Combine(folderA, "main"));
        File.Copy(Path.Combine(sourceFolder, "scenes"), Path.Combine(folderB, "main"));
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stderr) = await RunAnalyze(m_TestOutputFolder, "-o", databasePath);

        Assert.AreEqual(0, exitCode);
        StringAssert.DoesNotContain("Duplicate archive name", stderr);
        StringAssert.DoesNotContain("UNIQUE constraint", stderr);

        using var db = SQLTestHelper.OpenDatabase(databasePath);
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM archives WHERE name IN ('dlc/weapons/main', 'dlc/armor/main')",
            2, "both bundles should be recorded under their relative path");
        Assert.Greater(SQLTestHelper.QueryInt(db,
            "SELECT COUNT(*) FROM object_view WHERE archive = 'dlc/weapons/main'"), 0);
        Assert.Greater(SQLTestHelper.QueryInt(db,
            "SELECT COUNT(*) FROM object_view WHERE archive = 'dlc/armor/main'"), 0);
    }

    // A file named directly on the command line has no scanned directory to be relative to, so it
    // keeps its bare file name.
    [Test]
    public async Task Analyze_FileNamedDirectly_KeepsBareFileName()
    {
        var source = Path.Combine(m_AssetBundlesFolder, "2019.4.0f1", "assetbundle");
        var nested = Path.Combine(m_TestOutputFolder, "dlc", "weapons");
        Directory.CreateDirectory(nested);
        var bundle = Path.Combine(nested, "main");
        File.Copy(source, bundle);
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, _) = await RunAnalyze(bundle, "-o", databasePath);

        Assert.AreEqual(0, exitCode);

        using var db = SQLTestHelper.OpenDatabase(databasePath);
        SQLTestHelper.AssertQueryString(db, "SELECT name FROM archives", "main",
            "a directly-named archive keeps its bare file name");
    }

    // The variant check compares the extensions of two archive names, which are now paths. A dot
    // in a folder name is not an extension, so two bundles under "v1.2" and "v1.3" must be
    // reported as a plain duplicate rather than as variants of each other.
    [Test]
    public async Task Analyze_DottedFolderNames_NotReportedAsVariants()
    {
        var source = Path.Combine(m_AssetBundlesFolder, "2019.4.0f1", "assetbundle");
        foreach (var folder in new[] { "v1.2", "v1.3" })
        {
            Directory.CreateDirectory(Path.Combine(m_TestOutputFolder, folder));
            File.Copy(source, Path.Combine(m_TestOutputFolder, folder, "main"));
        }
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        var (exitCode, stderr) = await RunAnalyze(m_TestOutputFolder, "-o", databasePath);

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains("Duplicate SerializedFile name", stderr);
        StringAssert.DoesNotContain("AssetBundle variant", stderr);

        using var db = SQLTestHelper.OpenDatabase(databasePath);
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM archives WHERE name IN ('v1.2/main', 'v1.3/main')",
            2, "both archives should be recorded under their dotted-folder relative path");
    }
}
