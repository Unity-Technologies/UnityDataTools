using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

public class ExtractedTypeTreeTests
{
    private string m_TestOutputFolder;
    private string m_DataFolder;
    private string m_SerializedFile;
    private string m_TypeTreeDataFile;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_folder_typetree");
        Directory.CreateDirectory(m_TestOutputFolder);

        m_DataFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "ExtractedTypeTree");
        m_SerializedFile = Path.Combine(m_DataFolder, "sfwithextractedtypetrees1");
        m_TypeTreeDataFile = Path.Combine(m_DataFolder, "sfwithextractedtypetrees1.typetreedata");
    }

    [SetUp]
    public void Setup()
    {
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

        foreach (var dir in new DirectoryInfo(m_TestOutputFolder).EnumerateDirectories())
        {
            dir.Delete(true);
        }
    }

    [Test]
    public async Task Analyze_WithTypeTreeData_DatabaseCorrect(
        [Values("-d", "--typetree-data")] string option)
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", m_DataFolder, option, m_TypeTreeDataFile }));

        using var db = SQLTestHelper.OpenDatabase(databasePath);

        var objectCount = SQLTestHelper.QueryInt(db, "SELECT COUNT(*) FROM objects");
        Assert.Greater(objectCount, 0, "Expected objects in database when TypeTree data file is provided");
    }

    [Test]
    public async Task Analyze_WithoutTypeTreeData_ReportsFailure()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        using var swOut = new StringWriter();
        using var swErr = new StringWriter();
        var currentOut = Console.Out;
        var currentErr = Console.Error;
        try
        {
            Console.SetOut(swOut);
            Console.SetError(swErr);

            await Program.Main(new string[] { "analyze", m_DataFolder });

            var output = swOut.ToString() + swErr.ToString();

            Assert.That(output, Does.Contain("Failed files: 1"),
                "Expected failure when analyzing without TypeTree data file");
        }
        finally
        {
            Console.SetOut(currentOut);
            Console.SetError(currentErr);
        }
    }

    [Test]
    public async Task Dump_WithTypeTreeData_Succeeds(
        [Values("-d", "--typetree-data")] string option)
    {
        Assert.AreEqual(0, await Program.Main(new string[] { "dump", m_SerializedFile, option, m_TypeTreeDataFile }));

        var outputFiles = Directory.GetFiles(m_TestOutputFolder, "*.txt");
        Assert.IsNotEmpty(outputFiles, "Expected dump output files when TypeTree data file is provided");
        foreach (var f in outputFiles)
        {
            var txt = File.ReadAllText(f);
            Assert.IsTrue(txt.Contains("m_GameObject (PPtr<GameObject>)"));
        }
    }

    [Test]
    public async Task Dump_WithoutTypeTreeData_Fails()
    {
        Assert.AreNotEqual(0, await Program.Main(new string[] { "dump", m_SerializedFile }));
    }

    [Test]
    public async Task TypeTreeData_FileNotFound_ReturnsError()
    {
        var result = await Program.Main(new string[] { "analyze", m_DataFolder, "--typetree-data", "nonexistent_file.bin" });
        Assert.AreNotEqual(0, result, "Expected non-zero return code when TypeTree data file does not exist");
    }
}
