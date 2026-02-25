using System;
using System.IO;
using NUnit.Framework;
using UnityDataTools.Analyzer.Util;
using UnityDataTools.FileSystem;

namespace UnityDataTools.Analyzer.Tests;

/// <summary>
/// Tests for file format detection utilities (ArchiveDetector and SerializedFileDetector).
/// </summary>
public class FileDetectionTests
{
    private string m_TestDataPath;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        m_TestDataPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");
        UnityFileSystem.Init();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        UnityFileSystem.Cleanup();
    }

    #region SerializedFile Detection Tests

    [Test]
    public void TryDetectSerializedFile_ValidPlayerDataFile_ReturnsTrue()
    {
        var testFile = Path.Combine(m_TestDataPath, "PlayerData", "2022.1.20f1", "level0");

        bool result = SerializedFileDetector.TryDetectSerializedFile(testFile, out var info);

        Assert.IsTrue(result, "level0 should be detected as a valid SerializedFile");
        Assert.IsNotNull(info);

        // Verify exact values from the level0 file header
        Assert.That(info.Version, Is.EqualTo(22u), "Version should be 22");
        Assert.That(info.FileSize, Is.EqualTo(31988UL), "FileSize should be 31988");
        Assert.That(info.MetadataSize, Is.EqualTo(24580UL), "MetadataSize should be 24580");
        Assert.That(info.DataOffset, Is.EqualTo(24640UL), "DataOffset should be 24640");
        Assert.That(info.Endianness, Is.EqualTo((byte)0), "Endianness should be 0 (LittleEndian)");
        Assert.IsFalse(info.IsLegacyFormat, "Version 22 uses modern format (64-bit header)");
    }

    [Test]
    public void TryDetectSerializedFile_SerializedFileInsideArchive_ReturnsTrue()
    {
        // This tests a serialized file extracted from the alienprefab archive
        // The file was originally at CAB-c5053efeda8860d7e7b7ce4b4c66705b inside the archive
        var testFile = Path.Combine(m_TestDataPath, "LegacyFormats", "CAB-c5053efeda8860d7e7b7ce4b4c66705b");

        bool result = SerializedFileDetector.TryDetectSerializedFile(testFile, out var info);

        Assert.IsTrue(result, "CAB-c5053efeda8860d7e7b7ce4b4c66705b should be detected as a valid SerializedFile");
        Assert.IsNotNull(info);

        // Verify exact values from the CAB file header
        Assert.That(info.Version, Is.EqualTo(17u), "Version should be 17");
        Assert.That(info.FileSize, Is.EqualTo(595380UL), "FileSize should be 595380");
        Assert.That(info.MetadataSize, Is.EqualTo(61328UL), "MetadataSize should be 61328");
        Assert.That(info.DataOffset, Is.EqualTo(61360UL), "DataOffset should be 61360");
        Assert.That(info.Endianness, Is.EqualTo((byte)0), "Endianness should be 0 (LittleEndian)");
        Assert.IsTrue(info.IsLegacyFormat, "Version 17 uses legacy format (32-bit header)");
    }

    [Test]
    public void TryDetectSerializedFile_JsonFile_ReturnsFalse()
    {
        var testFiles = Directory.GetFiles(Path.Combine(m_TestDataPath, "AddressableBuildLayouts"), "*.json");
        Assert.Greater(testFiles.Length, 0, "Should have at least one JSON test file");

        foreach (var testFile in testFiles)
        {
            bool result = SerializedFileDetector.TryDetectSerializedFile(testFile, out var info);

            Assert.IsFalse(result, $"{Path.GetFileName(testFile)} should not be detected as a SerializedFile");
            Assert.IsNull(info, "Info should be null for non-SerializedFile");
        }
    }

    [Test]
    public void TryDetectSerializedFile_TextFile_ReturnsFalse()
    {
        var testFile = Path.Combine(m_TestDataPath, "PlayerNoTypeTree", "README.md");

        bool result = SerializedFileDetector.TryDetectSerializedFile(testFile, out var info);

        Assert.IsFalse(result, "README.md should not be detected as a SerializedFile");
        Assert.IsNull(info);
    }

    [Test]
    public void TryDetectSerializedFile_EmptyFile_ReturnsFalse()
    {
        // Create a temporary empty file
        var tempFile = Path.GetTempFileName();
        try
        {
            bool result = SerializedFileDetector.TryDetectSerializedFile(tempFile, out var info);

            Assert.IsFalse(result, "Empty file should not be detected as a SerializedFile");
            Assert.IsNull(info);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void TryDetectSerializedFile_TruncatedHeader_ReturnsFalse()
    {
        // Create a temporary file with only partial header (10 bytes)
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, new byte[10]); // Less than minimum header size (20 bytes)

            bool result = SerializedFileDetector.TryDetectSerializedFile(tempFile, out var info);

            Assert.IsFalse(result, "Truncated file should not be detected as a SerializedFile");
            Assert.IsNull(info);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void TryDetectSerializedFile_RandomBytes_ReturnsFalse()
    {
        // Create a temporary file with random bytes
        var tempFile = Path.GetTempFileName();
        try
        {
            var random = new Random(12345); // Fixed seed for reproducibility
            byte[] randomData = new byte[100];
            random.NextBytes(randomData);
            File.WriteAllBytes(tempFile, randomData);

            bool result = SerializedFileDetector.TryDetectSerializedFile(tempFile, out var info);

            Assert.IsFalse(result, "Random bytes should not be detected as a SerializedFile");
            Assert.IsNull(info);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void TryDetectSerializedFile_NonExistentFile_ReturnsFalse()
    {
        var nonExistentFile = Path.Combine(m_TestDataPath, "ThisFileDoesNotExist.xyz");

        bool result = SerializedFileDetector.TryDetectSerializedFile(nonExistentFile, out var info);

        Assert.IsFalse(result, "Non-existent file should not be detected as a SerializedFile");
        Assert.IsNull(info);
    }

    #endregion

    #region YAML SerializedFile Detection Tests

    [Test]
    public void IsYamlSerializedFile_ValidYamlAsset_ReturnsTrue()
    {
        var testFile = Path.Combine(m_TestDataPath, "YamlFormat.asset");

        bool result = YamlSerializedFileDetector.IsYamlSerializedFile(testFile);

        Assert.IsTrue(result, "YamlFormat.asset should be detected as a YAML SerializedFile");
    }

    [Test]
    public void IsYamlSerializedFile_BinarySerializedFile_ReturnsFalse()
    {
        var testFile = Path.Combine(m_TestDataPath, "PlayerData", "2022.1.20f1", "level0");

        bool result = YamlSerializedFileDetector.IsYamlSerializedFile(testFile);

        Assert.IsFalse(result, "Binary SerializedFile should not be detected as YAML");
    }

    [Test]
    public void IsYamlSerializedFile_Archive_ReturnsFalse()
    {
        var testFile = Path.Combine(m_TestDataPath, "AssetBundles", "2022.1.20f1", "assetbundle");

        bool result = YamlSerializedFileDetector.IsYamlSerializedFile(testFile);

        Assert.IsFalse(result, "AssetBundle should not be detected as YAML");
    }

    [Test]
    public void IsYamlSerializedFile_JsonFile_ReturnsFalse()
    {
        var testFiles = Directory.GetFiles(Path.Combine(m_TestDataPath, "AddressableBuildLayouts"), "*.json");
        Assert.Greater(testFiles.Length, 0, "Should have at least one JSON test file");

        foreach (var testFile in testFiles)
        {
            bool result = YamlSerializedFileDetector.IsYamlSerializedFile(testFile);

            Assert.IsFalse(result, $"JSON file should not be detected as YAML SerializedFile: {Path.GetFileName(testFile)}");
        }
    }

    [Test]
    public void IsYamlSerializedFile_NonExistentFile_ReturnsFalse()
    {
        var nonExistentFile = Path.Combine(m_TestDataPath, "ThisFileDoesNotExist.asset");

        bool result = YamlSerializedFileDetector.IsYamlSerializedFile(nonExistentFile);

        Assert.IsFalse(result, "Non-existent file should not be detected as YAML");
    }

    #endregion

    #region Archive Detection Tests

    [Test]
    public void IsUnityArchive_ValidAssetBundle_ReturnsTrue()
    {
        var testFile = Path.Combine(m_TestDataPath, "AssetBundles", "2022.1.20f1", "assetbundle");

        bool result = ArchiveDetector.IsUnityArchive(testFile);

        Assert.IsTrue(result, "assetbundle should be detected as a Unity Archive");
    }

    [Test]
    public void IsUnityArchive_OldFormatArchive_ReturnsTrue()
    {
        var testFile = Path.Combine(m_TestDataPath, "LegacyFormats", "alienprefab");

        bool result = ArchiveDetector.IsUnityArchive(testFile);

        Assert.IsTrue(result, "alienprefab should be detected as a Unity Archive");
    }

    [Test]
    public void IsUnityArchive_SerializedFile_ReturnsFalse()
    {
        var testFile = Path.Combine(m_TestDataPath, "PlayerData", "2022.1.20f1", "level0");

        bool result = ArchiveDetector.IsUnityArchive(testFile);

        Assert.IsFalse(result, "level0 (SerializedFile) should not be detected as an archive");
    }

    [Test]
    public void IsUnityArchive_JsonFile_ReturnsFalse()
    {
        var testFiles = Directory.GetFiles(Path.Combine(m_TestDataPath, "AddressableBuildLayouts"), "*.json");
        Assert.Greater(testFiles.Length, 0, "Should have at least one JSON test file");

        foreach (var testFile in testFiles)
        {
            bool result = ArchiveDetector.IsUnityArchive(testFile);

            Assert.IsFalse(result, $"{Path.GetFileName(testFile)} should not be detected as an archive");
        }
    }

    [Test]
    public void IsUnityArchive_EmptyFile_ReturnsFalse()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            bool result = ArchiveDetector.IsUnityArchive(tempFile);

            Assert.IsFalse(result, "Empty file should not be detected as an archive");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void IsUnityArchive_NonExistentFile_ReturnsFalse()
    {
        var nonExistentFile = Path.Combine(m_TestDataPath, "ThisFileDoesNotExist.xyz");

        bool result = ArchiveDetector.IsUnityArchive(nonExistentFile);

        Assert.IsFalse(result, "Non-existent file should not be detected as an archive");
    }

    #endregion
}
