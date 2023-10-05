using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityDataTools.FileSystem;
using UnityDataTools.TestCommon;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

public class UnityDataToolTests : AssetBundleTestFixture
{
    private string m_TestOutputFolder;

    public UnityDataToolTests(Context context) : base(context)
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

    public void IsWebBundle_True()
    {

        var webBundlePath = Path.Combine(Context.TestDataFolder, "WebBundles", "HelloWorld.data");
        Assert.IsTrue(Archive.IsWebBundle(new FileInfo(webBundlePath)));
    }

    [Test]
    public void IsWebBundle_False()
    {
        var nonWebBundlePath = Path.Combine(Context.TestDataFolder, "WebBundles", "NotAWebBundle.txt");
        Assert.IsFalse(Archive.IsWebBundle(new FileInfo(nonWebBundlePath)));
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
    public async Task ArchiveExtract_WebBundle_FileExtractedSuccessfully(
        [Values("", "-o archive", "--output-path archive")] string options,
        [Values("HelloWorld.data", "HelloWorld.data.gz", "HelloWorld.data.br")] string bundlePath)
    {
        var path = Path.Combine(Context.TestDataFolder, "WebBundles", bundlePath);
        string[] expectedFiles = {
            "boot.config",
            "data.unity3d",
            "RuntimeInitializeOnLoads.json",
            "ScriptingAssemblies.json",
            Path.Combine("Il2CppData", "Metadata", "global-metadata.dat"),
            Path.Combine("Resources", "unity_default_resources"),
        };
        Assert.AreEqual(0, await Program.Main(new string[] { "archive", "extract", path }.Concat(options.Split(" ", StringSplitOptions.RemoveEmptyEntries)).ToArray()));
        foreach (var file in expectedFiles)
        {
            Assert.IsTrue(File.Exists(Path.Combine(m_TestOutputFolder, "archive", file)));
        }
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

            Assert.AreEqual("CAB-5d40f7cad7c871cf2ad2af19ac542994", lines[0]);
            Assert.AreEqual($"  Size: {Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994-Size")}", lines[1]);
            Assert.AreEqual($"  Flags: {(ArchiveNodeFlags)(long)Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994-Flags")}", lines[2]);

            Assert.AreEqual("CAB-5d40f7cad7c871cf2ad2af19ac542994.resS", lines[4]);
            Assert.AreEqual($"  Size: {Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994.resS-Size")}", lines[5]);
            Assert.AreEqual($"  Flags: {(ArchiveNodeFlags)(long)Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994.resS-Flags")}", lines[6]);

            Assert.AreEqual("CAB-5d40f7cad7c871cf2ad2af19ac542994.resource", lines[8]);
            Assert.AreEqual($"  Size: {Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994.resource-Size")}", lines[9]);
            Assert.AreEqual($"  Flags: {(ArchiveNodeFlags)(long)Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994.resource-Flags")}", lines[10]);

        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task ArchiveList_WebBundle_ListFilesCorrectly(
         [Values(
            "HelloWorld.data",
            "HelloWorld.data.gz",
            "HelloWorld.data.br"
        )] string bundlePath)
    {
        var path = Path.Combine(Context.TestDataFolder, "WebBundles", bundlePath);
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "archive", "list", path }));

            var actualOutput = sw.ToString();
            var expectedOutput = (
@"data.unity3d
  Size: 253044

RuntimeInitializeOnLoads.json
  Size: 700

ScriptingAssemblies.json
  Size: 3060

boot.config
  Size: 93

Il2CppData/Metadata/global-metadata.dat
  Size: 1641180

Resources/unity_default_resources
  Size: 607376

"
            );

            Assert.AreEqual(expectedOutput, actualOutput);
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
    public async Task Analyze_DefaultArgs_DatabaseCorrect()
    {
        var databasePath = Path.Combine(m_TestOutputFolder, "database.db");
        var analyzePath = Path.Combine(Context.UnityDataFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", analyzePath }));

        ValidateDatabase(databasePath, true);
    }

    [Test]
    public async Task Analyze_WithoutRefs_DatabaseCorrect(
        [Values("-s", "--skip-references")] string options)
    {
        var databasePath = Path.Combine(m_TestOutputFolder, "database.db");
        var analyzePath = Path.Combine(Context.UnityDataFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", analyzePath }.Concat(options.Split(" ")).ToArray()));

        ValidateDatabase(databasePath, false);
    }

    [Test]
    public async Task Analyze_WithPattern_DatabaseCorrect(
        [Values("-p *.", "--search-pattern *.")] string options)
    {
        var databasePath = Path.Combine(m_TestOutputFolder, "database.db");
        var analyzePath = Path.Combine(Context.UnityDataFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", analyzePath }.Concat(options.Split(" ")).ToArray()));

        ValidateDatabase(databasePath, true);
    }

    [Test]
    public async Task Analyze_WithPatternNoMatch_DatabaseEmpty(
        [Values("-p *.x", "--search-pattern *.x")] string options)
    {
        var databasePath = Path.Combine(m_TestOutputFolder, "database.db");
        var analyzePath = Path.Combine(Context.UnityDataFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", analyzePath }.Concat(options.Split(" ")).ToArray()));

        using var db = new SQLiteConnection($"Data Source={databasePath};Version=3;New=True;Foreign Keys=False;");
        db.Open();

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

    private void ValidateDatabase(string databasePath, bool withRefs)
    {
        using var db = new SQLiteConnection($"Data Source={databasePath};Version=3;New=True;Foreign Keys=False;");
        db.Open();

        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText =
                @"SELECT
                    (SELECT COUNT(*) FROM animation_clips),
                    (SELECT COUNT(*) FROM asset_bundles),
                    (SELECT COUNT(*) FROM assets),
                    (SELECT COUNT(*) FROM audio_clips),
                    (SELECT COUNT(*) FROM meshes),
                    (SELECT COUNT(*) FROM objects),
                    (SELECT COUNT(*) FROM refs),
                    (SELECT COUNT(*) FROM serialized_files),
                    (SELECT COUNT(*) FROM shader_subprograms),
                    (SELECT COUNT(*) FROM shaders),
                    (SELECT COUNT(*) FROM shader_keywords),
                    (SELECT COUNT(*) FROM shader_subprogram_keywords),
                    (SELECT COUNT(*) FROM textures),
                    (SELECT COUNT(*) FROM types)";

            using var reader = cmd.ExecuteReader();

            reader.Read();

            Assert.AreEqual(Context.ExpectedData.Get("animation_clips_count"), reader.GetInt32(0));
            Assert.AreEqual(Context.ExpectedData.Get("asset_bundles_count"), reader.GetInt32(1));
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
        foreach (var file in new DirectoryInfo(m_TestOutputFolder).EnumerateFiles())
        {
            file.Delete();
        }
    }

    [Test]
    public async Task Analyze_PlayerData_DatabaseCorrect()
    {
        var databasePath = Path.Combine(m_TestOutputFolder, "database.db");
        var analyzePath = Path.Combine(Context.UnityDataFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", analyzePath, "-p", "*." }));

        using var db = new SQLiteConnection($"Data Source={databasePath};Version=3;New=True;Foreign Keys=False;");
        db.Open();
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
