using NUnit.Framework;
using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnityDataTools.UnityDataTool.Tests
{
    public class UnityDataToolTests
    {
        string m_TestDirectory = null;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            m_TestDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_folder");
            Directory.CreateDirectory(m_TestDirectory);
            Directory.SetCurrentDirectory(m_TestDirectory);
        }

        [TearDown]
        public void Teardown()
        {
            foreach (var file in new DirectoryInfo(m_TestDirectory).EnumerateFiles())
            {
                file.Delete();
            }
        }

        [Test]
        public void ArchiveExtract_FilesExtractedSuccessfully()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "AssetBundles", "assetbundle.ab");

            Assert.AreEqual(0, Program.Main(new string[] { "archive", "extract", path }));
            Assert.IsTrue(File.Exists(Path.Combine(m_TestDirectory, "CAB-5d40f7cad7c871cf2ad2af19ac542994")));
            Assert.IsTrue(File.Exists(Path.Combine(m_TestDirectory, "CAB-5d40f7cad7c871cf2ad2af19ac542994.resS")));
            Assert.IsTrue(File.Exists(Path.Combine(m_TestDirectory, "CAB-5d40f7cad7c871cf2ad2af19ac542994.resource")));
        }

        [Test]
        public void ArchiveList_ListFilesCorrectly()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "AssetBundles", "assetbundle.ab");

            using var sw = new StringWriter();

            var currentOut = Console.Out;
            Console.SetOut(sw);

            Assert.AreEqual(0, Program.Main(new string[] { "archive", "list", path }));

            var lines = sw.ToString().Split(sw.NewLine);

            Assert.AreEqual(lines[0], "CAB-5d40f7cad7c871cf2ad2af19ac542994");
            Assert.AreEqual(lines[1], "  Size: 199368");
            Assert.AreEqual(lines[2], "  Flags: SerializedFile");

            Assert.AreEqual(lines[4], "CAB-5d40f7cad7c871cf2ad2af19ac542994.resS");
            Assert.AreEqual(lines[5], "  Size: 2833848");
            Assert.AreEqual(lines[6], "  Flags: None");

            Assert.AreEqual(lines[8], "CAB-5d40f7cad7c871cf2ad2af19ac542994.resource");
            Assert.AreEqual(lines[9], "  Size: 5248");
            Assert.AreEqual(lines[10], "  Flags: None");

            Console.SetOut(currentOut);
        }

        [TestCase(arg:new string[] { })]
        [TestCase(arg:new string[] { "-f", "text" })]
        [TestCase(arg:new string[] { "--output-format", "text" })]
        public void DumpText_DefaultArgs_TextFileCreatedCorrectly(string[] options)
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "AssetBundles", "assetbundle.ab");
            var outputFile = Path.Combine(m_TestDirectory, "CAB-5d40f7cad7c871cf2ad2af19ac542994.txt");

            Assert.AreEqual(0, Program.Main(new string[] { "dump", path }.Concat(options).ToArray()));
            Assert.IsTrue(File.Exists(outputFile));

            var content = File.ReadAllText(outputFile);
            var expected = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "expected.txt"));

            // Normalize  line endings.
            content = Regex.Replace(content, @"\r\n|\n\r|\r", "\n");
            expected = Regex.Replace(expected, @"\r\n|\n\r|\r", "\n");

            Assert.AreEqual(expected, content);
        }

        [TestCase(arg: new string[] { "--skip-large-arrays" })]
        [TestCase(arg: new string[] { "-s" })]
        public void DumpText_SkipLargeArrays_TextFileCreatedCorrectly(string[] options)
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "AssetBundles", "assetbundle.ab");
            var outputFile = Path.Combine(m_TestDirectory, "CAB-5d40f7cad7c871cf2ad2af19ac542994.txt");

            Assert.AreEqual(0, Program.Main(new string[] { "dump", path }.Concat(options).ToArray()));
            Assert.IsTrue(File.Exists(outputFile));

            var content = File.ReadAllText(outputFile);
            var expected = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "expected_s.txt"));

            // Normalize  line endings.
            content = Regex.Replace(content, @"\r\n|\n\r|\r", "\n");
            expected = Regex.Replace(expected, @"\r\n|\n\r|\r", "\n");

            Assert.AreEqual(expected, content);
        }

        [Test]
        public void Analyze_DefaultArgs_DatabaseCorrect()
        {
            var databasePath = Path.Combine(m_TestDirectory, "database.db");
            var analyzePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "AssetBundles");

            Assert.AreEqual(0, Program.Main(new string[] { "analyze", analyzePath }));

            ValidateDatabase(databasePath, false);
        }

        [TestCase(arg: new string[] { "--extract-references" })]
        [TestCase(arg: new string[] { "-r" })]
        public void Analyze_WithRefs_DatabaseCorrect(string[] options)
        {
            var databasePath = Path.Combine(m_TestDirectory, "database.db");
            var analyzePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "AssetBundles");

            Assert.AreEqual(0, Program.Main(new string[] { "analyze", analyzePath }.Concat(options).ToArray()));

            ValidateDatabase(databasePath, true);
        }

        [TestCase(arg: new string[] { "--search-pattern", "*.ab" })]
        [TestCase(arg: new string[] { "-p", "*.ab" })]
        public void Analyze_WithPattern_DatabaseCorrect(string[] options)
        {
            var databasePath = Path.Combine(m_TestDirectory, "database.db");
            var analyzePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "AssetBundles");

            Assert.AreEqual(0, Program.Main(new string[] { "analyze", analyzePath }.Concat(options).ToArray()));

            ValidateDatabase(databasePath, false);
        }

        [TestCase(arg: new string[] { "--search-pattern", "*.x" })]
        [TestCase(arg: new string[] { "-p", "*.x" })]
        public void Analyze_WithPatternNoMatch_DatabaseEmpty(string[] options)
        {
            var databasePath = Path.Combine(m_TestDirectory, "database.db");
            var analyzePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "AssetBundles");

            Assert.AreEqual(0, Program.Main(new string[] { "analyze", analyzePath }.Concat(options).ToArray()));

            using var db = new SQLiteConnection($"Data Source={databasePath};Version=3;New=True;Foreign Keys=False;");
            db.Open();

            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM objects";

                Assert.AreEqual(0, cmd.ExecuteScalar());
            }
        }

        [TestCase(arg: new string[] { "--output-file", "my_database" })]
        [TestCase(arg: new string[] { "-o", "my_database" })]
        public void Analyze_WithOutputFile_DatabaseCorrect(string[] options)
        {
            var databasePath = Path.Combine(m_TestDirectory, "my_database");
            var analyzePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "AssetBundles");

            Assert.AreEqual(0, Program.Main(new string[] { "analyze", analyzePath }.Concat(options).ToArray()));

            ValidateDatabase(databasePath, false);
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
                    (SELECT COUNT(*) FROM textures),
                    (SELECT COUNT(*) FROM types)";

                using var reader = cmd.ExecuteReader();

                reader.Read();

                Assert.AreEqual(1, reader.GetInt32(0));
                Assert.AreEqual(1, reader.GetInt32(1));
                Assert.AreEqual(12, reader.GetInt32(2));
                Assert.AreEqual(1, reader.GetInt32(3));
                Assert.AreEqual(4, reader.GetInt32(4));
                Assert.AreEqual(41, reader.GetInt32(5));
                Assert.AreEqual(withRefs ? 125 : 0, reader.GetInt32(6));
                Assert.AreEqual(1, reader.GetInt32(7));
                Assert.AreEqual(86, reader.GetInt32(8));
                Assert.AreEqual(1, reader.GetInt32(9));
                Assert.AreEqual(1, reader.GetInt32(10));
                Assert.AreEqual(13, reader.GetInt32(11));
            }
        }
    }
}
