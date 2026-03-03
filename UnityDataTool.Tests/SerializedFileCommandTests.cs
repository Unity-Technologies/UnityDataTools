using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using UnityDataTools.FileSystem;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

/// <summary>
/// Tests for the serialized-file command using PlayerWithTypeTrees test data.
/// This data contains Player build output with TypeTrees enabled.
/// </summary>
public class SerializedFileCommandTests
{
    private string m_TestOutputFolder;
    private string m_TestDataFolder;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        UnityFileSystem.Init();
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_folder");
        m_TestDataFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "PlayerWithTypeTrees");
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

    [OneTimeTearDown]
    public void OneTimeTeardown()
    {
        UnityFileSystem.Cleanup();
    }

    #region ExternalRefs Tests

    [Test]
    public async Task ExternalRefs_TextFormat_OutputsCorrectly()
    {
        var path = Path.Combine(m_TestDataFolder, "sharedassets0.assets");
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "serialized-file", "externalrefs", path }));

            var output = sw.ToString();
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // sharedassets0.assets should have external references
            Assert.Greater(lines.Length, 0, "Expected at least one external reference");

            // Check format: "Index: N, Path: <path>"
            foreach (var line in lines)
            {
                StringAssert.Contains("Index:", line);
                StringAssert.Contains("Path:", line);
            }
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task ExternalRefs_JsonFormat_OutputsValidJson()
    {
        var path = Path.Combine(m_TestDataFolder, "sharedassets0.assets");
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "serialized-file", "externalrefs", path, "-f", "json" }));

            var output = sw.ToString();

            // Parse JSON to verify it's valid
            var jsonArray = JsonDocument.Parse(output).RootElement;
            Assert.IsTrue(jsonArray.ValueKind == JsonValueKind.Array);

            // Verify structure of each element
            foreach (var element in jsonArray.EnumerateArray())
            {
                Assert.IsTrue(element.TryGetProperty("index", out _));
                Assert.IsTrue(element.TryGetProperty("path", out _));
                Assert.IsTrue(element.TryGetProperty("guid", out _));
                Assert.IsTrue(element.TryGetProperty("type", out _));
            }
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task ExternalRefs_Level0_HasExpectedReferences()
    {
        var path = Path.Combine(m_TestDataFolder, "level0");
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "serialized-file", "externalrefs", path, "-f", "json" }));

            var output = sw.ToString();
            var jsonArray = JsonDocument.Parse(output).RootElement;

            // level0 should reference sharedassets0.assets
            bool foundSharedAssets = false;
            foreach (var element in jsonArray.EnumerateArray())
            {
                var pathValue = element.GetProperty("path").GetString();
                if (pathValue != null && pathValue.Contains("sharedassets0"))
                {
                    foundSharedAssets = true;
                    break;
                }
            }

            Assert.IsTrue(foundSharedAssets, "Expected level0 to reference sharedassets0.assets");
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    #endregion

    #region ObjectList Tests

    [Test]
    public async Task ObjectList_TextFormat_OutputsTable()
    {
        var path = Path.Combine(m_TestDataFolder, "level0");
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "serialized-file", "objectlist", path }));

            var output = sw.ToString();
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Should have header line
            Assert.Greater(lines.Length, 2, "Expected header and at least one data row");
            StringAssert.Contains("Id", lines[0]);
            StringAssert.Contains("Type", lines[0]);
            StringAssert.Contains("Offset", lines[0]);
            StringAssert.Contains("Size", lines[0]);

            // Second line should be separator
            StringAssert.Contains("---", lines[1]);

            // Should have data rows with numeric values
            Assert.Greater(lines.Length, 2);
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task ObjectList_JsonFormat_OutputsValidJson()
    {
        var path = Path.Combine(m_TestDataFolder, "level0");
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "serialized-file", "objectlist", path, "--format", "json" }));

            var output = sw.ToString();

            // Parse JSON to verify it's valid
            var jsonArray = JsonDocument.Parse(output).RootElement;
            Assert.IsTrue(jsonArray.ValueKind == JsonValueKind.Array);
            Assert.Greater(jsonArray.GetArrayLength(), 0);

            // Verify structure of each element
            foreach (var element in jsonArray.EnumerateArray())
            {
                Assert.IsTrue(element.TryGetProperty("id", out _));
                Assert.IsTrue(element.TryGetProperty("typeId", out _));
                Assert.IsTrue(element.TryGetProperty("typeName", out _));
                Assert.IsTrue(element.TryGetProperty("offset", out _));
                Assert.IsTrue(element.TryGetProperty("size", out _));
            }
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task ObjectList_ShowsTypeNames_NotJustNumbers()
    {
        var path = Path.Combine(m_TestDataFolder, "level0");
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "sf", "objectlist", path, "-f", "json" }));

            var output = sw.ToString();
            var jsonArray = JsonDocument.Parse(output).RootElement;

            // Look for common Unity types by name (not just numeric TypeIds)
            bool foundGameObject = false;
            bool foundTransform = false;

            foreach (var element in jsonArray.EnumerateArray())
            {
                var typeName = element.GetProperty("typeName").GetString();
                if (typeName == "GameObject") foundGameObject = true;
                if (typeName == "Transform") foundTransform = true;
            }

            Assert.IsTrue(foundGameObject, "Expected to find GameObject type");
            Assert.IsTrue(foundTransform, "Expected to find Transform type");
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task ObjectList_SharedAssets_ContainsExpectedTypes()
    {
        var path = Path.Combine(m_TestDataFolder, "sharedassets0.assets");
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "serialized-file", "objectlist", path, "-f", "json" }));

            var output = sw.ToString();
            var jsonArray = JsonDocument.Parse(output).RootElement;

            // SharedAssets should contain MonoBehaviour (114) or MonoScript (115)
            bool foundScriptType = false;

            foreach (var element in jsonArray.EnumerateArray())
            {
                var typeName = element.GetProperty("typeName").GetString();
                if (typeName == "MonoBehaviour" || typeName == "MonoScript")
                {
                    foundScriptType = true;
                    break;
                }
            }

            Assert.IsTrue(foundScriptType, "Expected to find MonoBehaviour or MonoScript in sharedassets");
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    #endregion

    #region Header Tests

    [Test]
    public async Task Header_TextFormat_OutputsCorrectly()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "PlayerNoTypeTree", "sharedassets0.assets");
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "serialized-file", "header", path }));

            var output = sw.ToString();
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Should have header information lines
            Assert.Greater(lines.Length, 0, "Expected header information");

            // Check for expected fields
            StringAssert.Contains("Version", output);
            StringAssert.Contains("Format", output);
            StringAssert.Contains("File Size", output);
            StringAssert.Contains("Metadata Size", output);
            StringAssert.Contains("Data Offset", output);
            StringAssert.Contains("Endianness", output);
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task Header_JsonFormat_OutputsValidJson()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "BuildReports", "Player.buildreport");
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "serialized-file", "header", path, "-f", "json" }));

            var output = sw.ToString();

            // Parse JSON to verify it's valid
            var jsonDoc = JsonDocument.Parse(output);
            var root = jsonDoc.RootElement;

            // Verify all expected properties are present
            Assert.IsTrue(root.TryGetProperty("version", out _));
            Assert.IsTrue(root.TryGetProperty("format", out _));
            Assert.IsTrue(root.TryGetProperty("fileSize", out _));
            Assert.IsTrue(root.TryGetProperty("metadataSize", out _));
            Assert.IsTrue(root.TryGetProperty("dataOffset", out _));
            Assert.IsTrue(root.TryGetProperty("endianness", out _));

            // Verify version is a number
            var version = root.GetProperty("version").GetUInt32();
            Assert.Greater(version, 0u, "Version should be greater than 0");

            // Verify format is a valid string
            var format = root.GetProperty("format").GetString();
            Assert.IsTrue(format == "Legacy (32-bit)" || format == "Modern (64-bit)",
                $"Format should be either Legacy or Modern, got: {format}");
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task Header_InvalidFile_ReturnsError()
    {
        var path = Path.Combine(m_TestDataFolder, "README.md");

        var result = await Program.Main(new string[] { "serialized-file", "header", path });
        Assert.AreNotEqual(0, result, "Should return error code for invalid file");
    }

    [Test]
    public async Task Header_ArchiveFile_ReturnsError()
    {
        var legacyDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "LegacyFormats");
        var archivePath = Path.Combine(legacyDir, "alienprefab");

        if (!File.Exists(archivePath))
        {
            Assert.Ignore("alienprefab test file not found");
            return;
        }

        using var sw = new StringWriter();
        var currentErr = Console.Error;
        try
        {
            Console.SetError(sw);

            var result = await Program.Main(new string[] { "serialized-file", "header", archivePath });

            Assert.AreNotEqual(0, result, "Should return error code for archive file");

            var errorOutput = sw.ToString();
            StringAssert.Contains("Unity Archive", errorOutput, "Error message should mention Unity Archive");
        }
        finally
        {
            Console.SetError(currentErr);
        }
    }

    #endregion

    #region Metadata Tests

    [Test]
    public async Task Metadata_LegacyVersion_ReturnsError()
    {
        // CAB-c5053efeda8860d7e7b7ce4b4c66705b is a version 17 SerializedFile.
        // Version 17 is below the minimum supported version for metadata parsing (19),
        // so the command should fail with an appropriate error message.
        var cabPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "LegacyFormats",
            "CAB-c5053efeda8860d7e7b7ce4b4c66705b");

        if (!File.Exists(cabPath))
        {
            Assert.Ignore("CAB test file not found");
            return;
        }

        using var sw = new StringWriter();
        var currentErr = Console.Error;
        try
        {
            Console.SetError(sw);

            var result = await Program.Main(new string[] { "serialized-file", "metadata", cabPath, "-f", "json" });

            Assert.AreNotEqual(0, result, "Should return error code for version 17 file (too old for metadata parsing)");

            var errorOutput = sw.ToString();
            StringAssert.Contains("not supported", errorOutput, "Error should mention that the version is not supported");
            StringAssert.Contains("17", errorOutput, "Error should mention the file's version number");
        }
        finally
        {
            Console.SetError(currentErr);
        }
    }

    #endregion

    #region Cross-Validation with Analyze Command

    [Test]
    public async Task ObjectList_CrossValidate_MatchesAnalyzeCommand()
    {
        // First, run analyze command to create database
        var databasePath = Path.Combine(m_TestOutputFolder, "test_analyze.db");
        var analyzePath = m_TestDataFolder;
        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", analyzePath, "-o", databasePath, "-p", "level0" }));

        // Now run serialized-file objectlist
        var path = Path.Combine(m_TestDataFolder, "level0");
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "serialized-file", "objectlist", path, "-f", "json" }));

            var output = sw.ToString();
            var jsonArray = JsonDocument.Parse(output).RootElement;
            var sfObjectCount = jsonArray.GetArrayLength();

            // Query database for the same file
            using var db = new SqliteConnection($"Data Source={databasePath}");
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) 
                FROM objects o 
                INNER JOIN serialized_files sf ON o.serialized_file = sf.id 
                WHERE sf.name = 'level0'";

            var dbObjectCount = Convert.ToInt32(cmd.ExecuteScalar());

            // Object counts should match
            Assert.AreEqual(dbObjectCount, sfObjectCount, "Object count from serialized-file command should match analyze database");

            // Verify a few specific objects match by type and size
            cmd.CommandText = @"
                SELECT o.object_id, t.name, o.size 
                FROM objects o 
                INNER JOIN types t ON o.type = t.id
                INNER JOIN serialized_files sf ON o.serialized_file = sf.id 
                WHERE sf.name = 'level0'
                LIMIT 5";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var dbObjectId = reader.GetInt64(0);
                var dbTypeName = reader.GetString(1);
                var dbSize = reader.GetInt64(2);

                // Find matching object in serialized-file output
                bool found = false;
                foreach (var element in jsonArray.EnumerateArray())
                {
                    var sfObjectId = element.GetProperty("id").GetInt64();
                    if (sfObjectId == dbObjectId)
                    {
                        var sfTypeName = element.GetProperty("typeName").GetString();
                        var sfSize = element.GetProperty("size").GetInt64();

                        Assert.AreEqual(dbTypeName, sfTypeName, $"Type name mismatch for object {dbObjectId}");
                        Assert.AreEqual(dbSize, sfSize, $"Size mismatch for object {dbObjectId}");
                        found = true;
                        break;
                    }
                }

                Assert.IsTrue(found, $"Object {dbObjectId} found in database but not in serialized-file output");
            }
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    #endregion

    #region Format Option Tests

    [Test]
    public async Task FormatOption_DefaultIsText()
    {
        var path = Path.Combine(m_TestDataFolder, "level0");
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "serialized-file", "objectlist", path }));

            var output = sw.ToString();

            // Text format should have header line with "Id", "Type", etc.
            StringAssert.Contains("Id", output);
            StringAssert.Contains("Type", output);
            StringAssert.Contains("Offset", output);
            StringAssert.Contains("Size", output);

            // Should not start with '[' or '{' (not JSON)
            Assert.IsFalse(output.TrimStart().StartsWith("["));
            Assert.IsFalse(output.TrimStart().StartsWith("{"));
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task FormatOption_ShortAndLongForms_Work()
    {
        var path = Path.Combine(m_TestDataFolder, "level0");

        // Test short form -f
        using (var sw = new StringWriter())
        {
            var currentOut = Console.Out;
            try
            {
                Console.SetOut(sw);
                Assert.AreEqual(0, await Program.Main(new string[] { "serialized-file", "objectlist", path, "-f", "json" }));
                var output = sw.ToString();
                Assert.DoesNotThrow(() => JsonDocument.Parse(output));
            }
            finally
            {
                Console.SetOut(currentOut);
            }
        }

        // Test long form --format
        using (var sw = new StringWriter())
        {
            var currentOut = Console.Out;
            try
            {
                Console.SetOut(sw);
                Assert.AreEqual(0, await Program.Main(new string[] { "serialized-file", "objectlist", path, "--format", "json" }));
                var output = sw.ToString();
                Assert.DoesNotThrow(() => JsonDocument.Parse(output));
            }
            finally
            {
                Console.SetOut(currentOut);
            }
        }
    }

    [Test]
    public async Task Alias_SF_Works()
    {
        var path = Path.Combine(m_TestDataFolder, "level0");
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            // Use 'sf' alias instead of 'serialized-file'
            Assert.AreEqual(0, await Program.Main(new string[] { "sf", "objectlist", path }));

            var output = sw.ToString();
            Assert.IsNotEmpty(output);
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public async Task ErrorHandling_InvalidFile_ReturnsError()
    {
        var path = Path.Combine(m_TestDataFolder, "README.md"); // Text file, not a SerializedFile

        var result = await Program.Main(new string[] { "serialized-file", "objectlist", path });
        Assert.AreNotEqual(0, result, "Should return error code for invalid file");
    }

    [Test]
    public async Task ErrorHandling_NonExistentFile_ReturnsError()
    {
        var path = Path.Combine(m_TestDataFolder, "nonexistent.file");

        // System.CommandLine should catch this and return error
        var result = await Program.Main(new string[] { "serialized-file", "objectlist", path });
        Assert.AreNotEqual(0, result, "Should return error code for non-existent file");
    }

    [Test]
    public async Task ErrorHandling_ArchiveFile_ReturnsHelpfulError()
    {
        // Use an AssetBundle from test data
        var assetBundlesDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "AssetBundles", "2022.1.20f1");

        // Skip if the test data doesn't exist (CI environments might not have all test data)
        if (!Directory.Exists(assetBundlesDir))
        {
            Assert.Ignore("AssetBundle test data not found");
            return;
        }

        var archiveFiles = Directory.GetFiles(assetBundlesDir, "*", SearchOption.TopDirectoryOnly);
        if (archiveFiles.Length == 0)
        {
            Assert.Ignore("No AssetBundle test files found");
            return;
        }

        var archivePath = archiveFiles[0]; // Use first archive file found

        using var sw = new StringWriter();
        var currentErr = Console.Error;
        try
        {
            Console.SetError(sw);

            var result = await Program.Main(new string[] { "serialized-file", "objectlist", archivePath });

            Assert.AreNotEqual(0, result, "Should return error code for archive file");

            var errorOutput = sw.ToString();
            StringAssert.Contains("Unity Archive", errorOutput, "Error message should mention Unity Archive");
            StringAssert.Contains("archive extract", errorOutput, "Error message should suggest using archive extract command");
        }
        finally
        {
            Console.SetError(currentErr);
        }
    }

    [Test]
    public async Task ErrorHandling_InvalidFile_ShowsHelpfulMessage()
    {
        var path = Path.Combine(m_TestDataFolder, "README.md");

        using var sw = new StringWriter();
        var currentErr = Console.Error;
        try
        {
            Console.SetError(sw);

            var result = await Program.Main(new string[] { "serialized-file", "objectlist", path });

            Assert.AreNotEqual(0, result, "Should return error code for invalid file");

            var errorOutput = sw.ToString();
            StringAssert.Contains("not appear to be a valid Unity SerializedFile", errorOutput,
                "Error message should explain that the file is not a valid SerializedFile");
        }
        finally
        {
            Console.SetError(currentErr);
        }
    }

    [Test]
    public async Task ErrorHandling_YamlFile_ReturnsHelpfulError()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "YamlFormat.asset");

        using var sw = new StringWriter();
        var currentErr = Console.Error;
        try
        {
            Console.SetError(sw);

            var result = await Program.Main(new string[] { "serialized-file", "header", path });

            Assert.AreNotEqual(0, result, "Should return error code for YAML file");

            var errorOutput = sw.ToString();
            StringAssert.Contains("YAML-format SerializedFile", errorOutput, "Error message should mention YAML format");
            StringAssert.Contains("not supported", errorOutput, "Error message should explain YAML is not supported");
            StringAssert.Contains("binary-format", errorOutput, "Error message should mention binary format is supported");
        }
        finally
        {
            Console.SetError(currentErr);
        }
    }

    [Test]
    public async Task ErrorHandling_YamlFile_ExternalRefs_ReturnsHelpfulError()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "YamlFormat.asset");

        using var sw = new StringWriter();
        var currentErr = Console.Error;
        try
        {
            Console.SetError(sw);

            var result = await Program.Main(new string[] { "serialized-file", "externalrefs", path });

            Assert.AreNotEqual(0, result, "Should return error code for YAML file");

            var errorOutput = sw.ToString();
            StringAssert.Contains("YAML-format SerializedFile", errorOutput, "Error message should mention YAML format");
        }
        finally
        {
            Console.SetError(currentErr);
        }
    }

    [Test]
    public async Task ErrorHandling_YamlFile_ObjectList_ReturnsHelpfulError()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", "YamlFormat.asset");

        using var sw = new StringWriter();
        var currentErr = Console.Error;
        try
        {
            Console.SetError(sw);

            var result = await Program.Main(new string[] { "sf", "objectlist", path });

            Assert.AreNotEqual(0, result, "Should return error code for YAML file");

            var errorOutput = sw.ToString();
            StringAssert.Contains("YAML-format SerializedFile", errorOutput, "Error message should mention YAML format");
        }
        finally
        {
            Console.SetError(currentErr);
        }
    }

    #endregion
}

