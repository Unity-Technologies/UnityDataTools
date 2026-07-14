using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using UnityDataTools.FileSystem;
using UnityDataTools.TestCommon;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

public class UnityDataToolAssetBundleTests : AssetBundleTestFixture
{
    private string m_TestOutputFolder;

    public UnityDataToolAssetBundleTests(Context context) : base(context)
    {
    }

    protected override void OnLoadExpectedData(Context context)
    {
        // Uncomment to regenerate expected data.
        //ExpectedDataGenerator.Generate(context);
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

        var testDir = new DirectoryInfo(m_TestOutputFolder);
        testDir.EnumerateFiles()
            .ToList().ForEach(f => f.Delete());
        testDir.EnumerateDirectories()
            .ToList().ForEach(d => d.Delete(true));
    }

    [Test]
    public async Task InvalidFile(
        [Values(
            new string[] {"archive", "extract"},
            new string[] {"archive", "list"},
            new string[] {"dump"}
        )] string[] args)
    {
        var path = Path.Combine(Context.TestDataFolder, "invalidfile");
        var command = args.Append(path);
        Assert.AreNotEqual(0, await Program.Main(command.ToArray()));
    }

    [Test]
    public async Task ArchiveExtract_AssetBundle_FilesExtractedSuccessfully(
        [Values("", "-o archive", "--output-path archive")] string options)
    {
        var path = Path.Combine(Context.UnityDataFolder, "assetbundle");

        Assert.AreEqual(0, await Program.Main(new string[] { "archive", "extract", path }.Concat(options.Split(" ", StringSplitOptions.RemoveEmptyEntries)).ToArray()));
        Assert.IsTrue(File.Exists(Path.Combine(m_TestOutputFolder, "archive", "CAB-5d40f7cad7c871cf2ad2af19ac542994")));
        Assert.IsTrue(File.Exists(Path.Combine(m_TestOutputFolder, "archive", "CAB-5d40f7cad7c871cf2ad2af19ac542994.resS")));
        Assert.IsTrue(File.Exists(Path.Combine(m_TestOutputFolder, "archive", "CAB-5d40f7cad7c871cf2ad2af19ac542994.resource")));
    }

    [Test]
    public async Task ArchiveList_AssetBundle_ListFilesCorrectly()
    {
        var path = Path.Combine(Context.UnityDataFolder, "assetbundle");
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "archive", "list", path }));

            var lines = sw.ToString().Split(sw.NewLine);

            // Each entry: path, offset, size, flags, blank = 5 lines
            Assert.AreEqual("CAB-5d40f7cad7c871cf2ad2af19ac542994", lines[0]);
            Assert.AreEqual($"  Data Offset: {Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994-DataOffset")}", lines[1]);
            Assert.AreEqual($"  Size: {Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994-Size")}", lines[2]);
            Assert.AreEqual($"  Flags: {(ArchiveNodeFlags)(long)Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994-Flags")}", lines[3]);

            Assert.AreEqual("CAB-5d40f7cad7c871cf2ad2af19ac542994.resS", lines[5]);
            Assert.AreEqual($"  Data Offset: {Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994.resS-DataOffset")}", lines[6]);
            Assert.AreEqual($"  Size: {Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994.resS-Size")}", lines[7]);
            Assert.AreEqual($"  Flags: {(ArchiveNodeFlags)(long)Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994.resS-Flags")}", lines[8]);

            Assert.AreEqual("CAB-5d40f7cad7c871cf2ad2af19ac542994.resource", lines[10]);
            Assert.AreEqual($"  Data Offset: {Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994.resource-DataOffset")}", lines[11]);
            Assert.AreEqual($"  Size: {Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994.resource-Size")}", lines[12]);
            Assert.AreEqual($"  Flags: {(ArchiveNodeFlags)(long)Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994.resource-Flags")}", lines[13]);

        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task DumpText_DefaultArgs_TextFileCreatedCorrectly(
        [Values("", "-f text", "--output-format text")] string options)
    {
        var path = Path.Combine(Context.UnityDataFolder, "assetbundle");
        var outputFile = Path.Combine(m_TestOutputFolder, "CAB-5d40f7cad7c871cf2ad2af19ac542994.txt");

        Assert.AreEqual(0, await Program.Main(new string[] { "dump", path }.Concat(options.Split(" ", StringSplitOptions.RemoveEmptyEntries)).ToArray()));
        Assert.IsTrue(File.Exists(outputFile));

        var content = File.ReadAllText(outputFile);
        var expected = File.ReadAllText(Path.Combine(Context.ExpectedDataFolder, "dump", "CAB-5d40f7cad7c871cf2ad2af19ac542994.txt"));

        // Normalize  line endings.
        content = Regex.Replace(content, @"\r\n|\n\r|\r", "\n");
        expected = Regex.Replace(expected, @"\r\n|\n\r|\r", "\n");

        Assert.AreEqual(expected, content);
    }

    [Test]
    public async Task DumpText_SkipLargeArrays_TextFileCreatedCorrectly(
        [Values("-s", "--skip-large-arrays")] string options)
    {
        var path = Path.Combine(Context.UnityDataFolder, "assetbundle");
        var outputFile = Path.Combine(m_TestOutputFolder, "CAB-5d40f7cad7c871cf2ad2af19ac542994.txt");

        Assert.AreEqual(0, await Program.Main(new string[] { "dump", path }.Concat(options.Split(" ", StringSplitOptions.RemoveEmptyEntries)).ToArray()));
        Assert.IsTrue(File.Exists(outputFile));

        var content = File.ReadAllText(outputFile);
        var expected = File.ReadAllText(Path.Combine(Context.ExpectedDataFolder, "dump-s", "CAB-5d40f7cad7c871cf2ad2af19ac542994.txt"));

        // Normalize  line endings.
        content = Regex.Replace(content, @"\r\n|\n\r|\r", "\n");
        expected = Regex.Replace(expected, @"\r\n|\n\r|\r", "\n");

        Assert.AreEqual(expected, content);
    }

    [Test]
    public async Task DumpText_Stdout_WritesDumpToStdout()
    {
        var path = Path.Combine(Context.UnityDataFolder, "assetbundle");
        var unwantedOutputFile = Path.Combine(m_TestOutputFolder, "CAB-5d40f7cad7c871cf2ad2af19ac542994.txt");

        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            Assert.AreEqual(0, await Program.Main(new string[] { "dump", path, "--stdout" }));
        }
        finally
        {
            Console.SetOut(currentOut);
        }

        Assert.IsFalse(File.Exists(unwantedOutputFile), "--stdout should not also write a .txt file");

        var content = sw.ToString();
        var expected = File.ReadAllText(Path.Combine(Context.ExpectedDataFolder, "dump", "CAB-5d40f7cad7c871cf2ad2af19ac542994.txt"));

        // Normalize line endings.
        content = Regex.Replace(content, @"\r\n|\n\r|\r", "\n");
        expected = Regex.Replace(expected, @"\r\n|\n\r|\r", "\n");

        Assert.AreEqual(expected, content);
    }

    [Test]
    public async Task DumpText_TypeFilterByName_OnlyMatchingObjectsDumped()
    {
        var path = Path.Combine(Context.UnityDataFolder, "assetbundle");
        var outputFile = Path.Combine(m_TestOutputFolder, "CAB-5d40f7cad7c871cf2ad2af19ac542994.txt");

        Assert.AreEqual(0, await Program.Main(new string[] { "dump", path, "-t", "MonoBehaviour" }));
        Assert.IsTrue(File.Exists(outputFile));

        var content = File.ReadAllText(outputFile);
        Assert.That(content, Does.Contain("(ClassID: 114)"));
        Assert.That(content, Does.Not.Contain("(ClassID: 1)"));
    }

    [Test]
    public async Task DumpText_TypeFilterByClassID_OnlyMatchingObjectsDumped()
    {
        var path = Path.Combine(Context.UnityDataFolder, "assetbundle");
        var outputFile = Path.Combine(m_TestOutputFolder, "CAB-5d40f7cad7c871cf2ad2af19ac542994.txt");

        Assert.AreEqual(0, await Program.Main(new string[] { "dump", path, "-t", "114" }));
        Assert.IsTrue(File.Exists(outputFile));

        var content = File.ReadAllText(outputFile);
        Assert.That(content, Does.Contain("(ClassID: 114)"));
        Assert.That(content, Does.Not.Contain("(ClassID: 1)"));
    }

    [Test]
    public async Task DumpText_TypeFilterNoMatch_ShowsNotFoundMessage()
    {
        var path = Path.Combine(Context.UnityDataFolder, "assetbundle");
        var outputFile = Path.Combine(m_TestOutputFolder, "CAB-5d40f7cad7c871cf2ad2af19ac542994.txt");

        Assert.AreEqual(0, await Program.Main(new string[] { "dump", path, "-t", "NonExistentType" }));
        Assert.IsTrue(File.Exists(outputFile));

        var content = File.ReadAllText(outputFile);
        Assert.That(content, Does.Contain("No objects found matching type"));
    }

    [Test]
    public async Task Analyze_DefaultArgs_DatabaseCorrect()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);
        var analyzePath = Path.Combine(Context.UnityDataFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", analyzePath }));

        ValidateDatabase(databasePath, true);
    }

    [Test]
    public async Task Analyze_WithoutRefs_DatabaseCorrect(
        [Values("-s", "--skip-references")] string options)
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);
        var analyzePath = Path.Combine(Context.UnityDataFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", analyzePath }.Concat(options.Split(" ")).ToArray()));

        ValidateDatabase(databasePath, false);
    }

    [Test]
    public async Task Analyze_WithPattern_DatabaseCorrect(
        [Values("-p *.", "--search-pattern *.")] string options)
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);
        var analyzePath = Path.Combine(Context.UnityDataFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", analyzePath }.Concat(options.Split(" ")).ToArray()));

        ValidateDatabase(databasePath, true);
    }

    [Test]
    public async Task Analyze_WithPatternNoMatch_DatabaseEmpty(
        [Values("-p *.x", "--search-pattern *.x")] string options)
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);
        var analyzePath = Path.Combine(Context.UnityDataFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", analyzePath }.Concat(options.Split(" ")).ToArray()));

        using var db = SQLTestHelper.OpenDatabase(databasePath);

        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM objects";

            Assert.AreEqual(0, cmd.ExecuteScalar());
        }
    }

    [Test]
    public async Task Analyze_WithOutputFile_DatabaseCorrect(
        [Values("-o my_database", "--output-file my_database")] string options)
    {
        var databasePath = Path.Combine(m_TestOutputFolder, "my_database");
        var analyzePath = Path.Combine(Context.UnityDataFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", analyzePath }.Concat(options.Split(" ")).ToArray()));

        ValidateDatabase(databasePath, true);
    }

    [Test]
    public async Task Analyze_MonoScripts_DatabaseContainsExpectedContent()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);
        var analyzePath = Path.Combine(Context.UnityDataFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", analyzePath }));

        using var db = SQLTestHelper.OpenDatabase(databasePath);

        // Verify MonoScript table and views exist
        SQLTestHelper.AssertTableExists(db, "monoscripts");
        SQLTestHelper.AssertViewExists(db, "monoscript_view");
        SQLTestHelper.AssertViewExists(db, "script_object_view");

        // Verify MonoScript table contains data
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM monoscripts",
            1,
            "Unexpected number of MonoScripts");

        // Verify the specific MonoScript from the example
        // Note: Assembly name format changed in Unity 2023.1 from 'Assembly-CSharp.dll' to 'Assembly-CSharp'
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM monoscript_view WHERE class_name = 'SerializeReferencePolymorphismExample' AND assembly_name LIKE 'Assembly-CSharp%'",
            1,
            "Expected to find SerializeReferencePolymorphismExample MonoScript");

        // Verify script_object_view finds the SerializeReferencePolymorphismExample MonoBehaviour
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM script_object_view WHERE class_name = 'SerializeReferencePolymorphismExample'",
            1,
            "Expected to find exactly one MonoBehaviour instance of SerializeReferencePolymorphismExample");
    }

    private void ValidateDatabase(string databasePath, bool withRefs)
    {
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText =
                @"SELECT
                    (SELECT COUNT(*) FROM animation_clips),
                    (SELECT COUNT(*) FROM archives),
                    (SELECT COUNT(*) FROM assetbundle_assets),
                    (SELECT COUNT(*) FROM audio_clips),
                    (SELECT COUNT(*) FROM meshes),
                    (SELECT COUNT(*) FROM objects),
                    (SELECT COUNT(*) FROM refs),
                    -- Count only analyzed files; serialized_files now also holds referenced-but-not-analyzed
                    -- files that are dangling-ref targets (issue #85), which are not part of this count.
                    (SELECT COUNT(*) FROM serialized_files WHERE id IN (SELECT serialized_file FROM objects)),
                    (SELECT COUNT(*) FROM shader_subprograms),
                    (SELECT COUNT(*) FROM shaders),
                    (SELECT COUNT(*) FROM shader_keywords),
                    (SELECT COUNT(*) FROM shader_subprogram_keywords),
                    (SELECT COUNT(*) FROM textures),
                    (SELECT COUNT(*) FROM types)";

            using var reader = cmd.ExecuteReader();

            reader.Read();

            Assert.AreEqual(Context.ExpectedData.Get("animation_clips_count"), reader.GetInt32(0));
            Assert.AreEqual(Context.ExpectedData.Get("archives_count"), reader.GetInt32(1));
            Assert.AreEqual(Context.ExpectedData.Get("assets_count"), reader.GetInt32(2));
            Assert.AreEqual(Context.ExpectedData.Get("audio_clips_count"), reader.GetInt32(3));
            Assert.AreEqual(Context.ExpectedData.Get("meshes_count"), reader.GetInt32(4));
            Assert.AreEqual(Context.ExpectedData.Get("objects_count"), reader.GetInt32(5));
            Assert.AreEqual(withRefs ? Context.ExpectedData.Get("refs_count") : 0, reader.GetInt32(6));
            Assert.AreEqual(Context.ExpectedData.Get("serialized_files_count"), reader.GetInt32(7));
            Assert.AreEqual(Context.ExpectedData.Get("shader_subprograms_count"), reader.GetInt32(8));
            Assert.AreEqual(Context.ExpectedData.Get("shaders_count"), reader.GetInt32(9));
            Assert.AreEqual(Context.ExpectedData.Get("shader_keywords_count"), reader.GetInt32(10));
            Assert.AreEqual(Context.ExpectedData.Get("shader_subprogram_keywords_count"), reader.GetInt32(11));
            Assert.AreEqual(Context.ExpectedData.Get("textures_count"), reader.GetInt32(12));
            Assert.AreEqual(Context.ExpectedData.Get("types_count"), reader.GetInt32(13));
        }
    }
}
