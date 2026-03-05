using System;
using System.IO;
using System.Linq;
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

    #region SerializedFile Metadata Parsing Tests

    [Test]
    public void TryParseMetadata_VersionTooOld_ReturnsFalseWithMessage()
    {
        var headerInfo = new SerializedFileInfo { Version = 18 };

        bool result = SerializedFileDetector.TryParseMetadata("irrelevant", headerInfo, out var metadata, out var errorMessage);

        Assert.IsFalse(result);
        Assert.IsNull(metadata);
        Assert.IsNotNull(errorMessage);
        Assert.That(errorMessage, Does.Contain("18"), "Error should mention the actual version");
        Assert.That(errorMessage, Does.Contain("19"), "Error should mention the minimum supported version");
    }

    [Test]
    public void TryParseMetadata_VersionTooNew_ReturnsFalseWithMessage()
    {
        var headerInfo = new SerializedFileInfo { Version = 24 };

        bool result = SerializedFileDetector.TryParseMetadata("irrelevant", headerInfo, out var metadata, out var errorMessage);

        Assert.IsFalse(result);
        Assert.IsNull(metadata);
        Assert.IsNotNull(errorMessage);
        Assert.That(errorMessage, Does.Contain("24"), "Error should mention the actual version");
        Assert.That(errorMessage, Does.Contain("23"), "Error should mention the maximum supported version");
        Assert.That(errorMessage, Does.Contain("UnityDataTool"), "Error should mention UnityDataTool");
    }

    [Test]
    public void TryParseMetadata_PlayerDataLevel0_ReturnsExpectedValues()
    {
        var testFile = Path.Combine(m_TestDataPath, "PlayerData", "2022.1.20f1", "level0");

        bool headerResult = SerializedFileDetector.TryDetectSerializedFile(testFile, out var headerInfo);
        Assert.IsTrue(headerResult, "level0 should be detected as a valid SerializedFile");

        bool result = SerializedFileDetector.TryParseMetadata(testFile, headerInfo, out var metadata, out var errorMessage);

        Assert.IsTrue(result, $"Metadata parsing should succeed. Error: {errorMessage}");
        Assert.IsNotNull(metadata);

        // Verify exact values from the level0 metadata section.
        // This file was built with Unity 2022.1.20f1 for StandaloneOSX (platform 2),
        // with TypeTrees enabled.
        Assert.That(metadata.UnityVersion, Is.EqualTo("2022.1.20f1"), "Unity version should be 2022.1.20f1");
        Assert.That(metadata.TargetPlatform, Is.EqualTo(2u), "Target platform should be 2 (StandaloneOSX)");
        Assert.IsTrue(metadata.EnableTypeTree, "EnableTypeTree should be true");

        // --- TypeTree counts ---
        Assert.That(metadata.TypeTreeCount, Is.EqualTo(10), "Should have 10 regular type entries");
        Assert.That(metadata.SerializedReferenceTypeTreeCount, Is.EqualTo(0), "Should have 0 SerializeReference type entries");
        Assert.IsNotNull(metadata.TypeTrees, "TypeTrees should be populated");
        Assert.That(metadata.TypeTrees.Length, Is.EqualTo(10));

        // --- Per-entry invariants for a player scene with no MonoBehaviours ---
        // All types are native Unity types: inline TypeTrees, no script IDs, no ref-type fields.
        foreach (var entry in metadata.TypeTrees)
        {
            Assert.IsTrue(entry.InlineTypeTree,
                $"InlineTypeTree should be true (persistentTypeID={entry.PersistentTypeID})");
            Assert.IsFalse(entry.TypeTreeStructureHash.IsZero,
                $"TypeTreeStructureHash should not be zero (persistentTypeID={entry.PersistentTypeID})");
            Assert.IsTrue(entry.TypeTreeContentHash.IsZero,
                $"TypeTreeContentHash should be zero for version < 23 (persistentTypeID={entry.PersistentTypeID})");
            Assert.Greater(entry.TypeTreeSerializedSize, 0u,
                $"TypeTreeSerializedSize should be non-zero (persistentTypeID={entry.PersistentTypeID})");
            Assert.Greater(entry.PersistentTypeID, 0,
                $"PersistentTypeID should be positive for native types (got {entry.PersistentTypeID})");
            Assert.That(entry.PersistentTypeID, Is.Not.EqualTo(114),
                "No MonoBehaviour types expected in this scene");
            Assert.That(entry.ScriptTypeIndex, Is.EqualTo((short)-1),
                $"ScriptTypeIndex should be -1 for native types (persistentTypeID={entry.PersistentTypeID})");
            Assert.IsTrue(entry.ScriptID.IsZero,
                $"ScriptID should be zero for native types (persistentTypeID={entry.PersistentTypeID})");
            Assert.That(entry.ClassName, Is.EqualTo(string.Empty),
                $"ClassName should be empty for non-ref types (persistentTypeID={entry.PersistentTypeID})");
            Assert.That(entry.Namespace, Is.EqualTo(string.Empty),
                $"Namespace should be empty for non-ref types (persistentTypeID={entry.PersistentTypeID})");
            Assert.That(entry.AssemblyName, Is.EqualTo(string.Empty),
                $"AssemblyName should be empty for non-ref types (persistentTypeID={entry.PersistentTypeID})");
            Assert.That(entry.TypeDependencies.Length, Is.EqualTo(0),
                $"TypeDependencies should be empty (persistentTypeID={entry.PersistentTypeID})");
        }
    }

    [Test]
    public void TryParseMetadata_PlayerNoTypeTreeLevel1_ReturnsExpectedValues()
    {
        var testFile = Path.Combine(m_TestDataPath, "PlayerNoTypeTree", "level1");

        bool headerResult = SerializedFileDetector.TryDetectSerializedFile(testFile, out var headerInfo);
        Assert.IsTrue(headerResult, "level1 should be detected as a valid SerializedFile");

        bool result = SerializedFileDetector.TryParseMetadata(testFile, headerInfo, out var metadata, out var errorMessage);

        Assert.IsTrue(result, $"Metadata parsing should succeed. Error: {errorMessage}");
        Assert.IsNotNull(metadata);

        // Verify exact values from the level1 metadata section.
        // This file was built with Unity 6000.0.65f1 for Windows Standalone (platform 19),
        // with TypeTrees disabled (PlayerNoTypeTree build).
        Assert.That(metadata.UnityVersion, Is.EqualTo("6000.0.65f1"), "Unity version should be 6000.0.65f1");
        Assert.That(metadata.TargetPlatform, Is.EqualTo(19u), "Target platform should be 19 (Windows Standalone x64)");
        Assert.IsFalse(metadata.EnableTypeTree, "EnableTypeTree should be false for a no-type-tree build");

        // Even when TypeTrees are not stored inline, the metadata still records the full list of
        // types used in the file along with their oldTypeHash values. The hashes allow the runtime
        // to verify type compatibility against its built-in type definitions at load time.
        Assert.That(metadata.TypeTreeCount, Is.EqualTo(6), "Should have 6 type entries");
        Assert.IsNotNull(metadata.TypeTrees, "TypeTrees should be populated");
        Assert.That(metadata.TypeTrees.Length, Is.EqualTo(6));

        foreach (var entry in metadata.TypeTrees)
        {
            Assert.Greater(entry.PersistentTypeID, 0,
                $"PersistentTypeID should be positive (got {entry.PersistentTypeID})");
            Assert.IsFalse(entry.TypeTreeStructureHash.IsZero,
                $"TypeTreeStructureHash should not be zero (persistentTypeID={entry.PersistentTypeID})");
            Assert.IsFalse(entry.InlineTypeTree,
                $"InlineTypeTree should be false when EnableTypeTree=false (persistentTypeID={entry.PersistentTypeID})");
            Assert.IsTrue(entry.TypeTreeContentHash.IsZero,
                $"TypeTreeContentHash should be zero for this version < 23 file (persistentTypeID={entry.PersistentTypeID})");
        }
    }

    [Test]
    public void TryParseMetadata_V22PrefabWithSerializedReference_ReturnsExpectedTypeTreeData()
    {
        var testFile = Path.Combine(m_TestDataPath, "AssetBundleTypeTreeVariations", "v22",
            "prefab_with_serializedreference.serializedfile");

        bool headerResult = SerializedFileDetector.TryDetectSerializedFile(testFile, out var headerInfo);
        Assert.IsTrue(headerResult, "File should be detected as a valid SerializedFile");

        bool result = SerializedFileDetector.TryParseMetadata(testFile, headerInfo, out var metadata, out var errorMessage);
        Assert.IsTrue(result, $"Metadata parsing should succeed. Error: {errorMessage}");
        Assert.IsNotNull(metadata);

        // --- Initial metadata fields ---
        Assert.IsTrue(metadata.EnableTypeTree, "EnableTypeTree should be true");

        // --- Type counts ---
        Assert.That(metadata.TypeTreeCount, Is.EqualTo(4), "Should have 4 regular type entries");
        Assert.That(metadata.SerializedReferenceTypeTreeCount, Is.EqualTo(1), "Should have 1 SerializeReference type entry");
        Assert.IsNotNull(metadata.TypeTrees, "TypeTrees array should be populated");
        Assert.IsNotNull(metadata.SerializedReferenceTypeTrees, "SerializedReferenceTypeTrees array should be populated");

        // --- Regular type entries: persistentTypeIDs in order ---
        int[] expectedTypeIDs = { 142, 4, 1, 114 };
        Assert.That(metadata.TypeTrees.Length, Is.EqualTo(expectedTypeIDs.Length));
        for (int i = 0; i < expectedTypeIDs.Length; i++)
            Assert.That(metadata.TypeTrees[i].PersistentTypeID, Is.EqualTo(expectedTypeIDs[i]),
                $"TypeTrees[{i}].PersistentTypeID");

        // --- v22 files do not store TypeTreeContentHash (it is all-zeros) ---
        foreach (var entry in metadata.TypeTrees)
            Assert.IsTrue(entry.TypeTreeContentHash.IsZero,
                $"TypeTreeContentHash should be zero for v22 (persistentTypeID={entry.PersistentTypeID})");
        foreach (var entry in metadata.SerializedReferenceTypeTrees)
            Assert.IsTrue(entry.TypeTreeContentHash.IsZero,
                "SerializedReferenceTypeTrees TypeTreeContentHash should be zero for v22");

        // --- All type trees are inline (non-zero size, InlineTypeTree=true) ---
        foreach (var entry in metadata.TypeTrees)
        {
            Assert.IsTrue(entry.InlineTypeTree,
                $"InlineTypeTree should be true (persistentTypeID={entry.PersistentTypeID})");
            Assert.Greater(entry.TypeTreeSerializedSize, 0u,
                $"TypeTreeSerializedSize should be non-zero (persistentTypeID={entry.PersistentTypeID})");
        }
        foreach (var entry in metadata.SerializedReferenceTypeTrees)
        {
            Assert.IsTrue(entry.InlineTypeTree, "SerializedReferenceTypeTrees[0].InlineTypeTree should be true");
            Assert.Greater(entry.TypeTreeSerializedSize, 0u,
                "SerializedReferenceTypeTrees[0].TypeTreeSerializedSize should be non-zero");
        }

        // --- MonoBehaviour (114) has special entries because it refers to a specific C# class ---
        // Note: if multiple C# MonoBehaviour-derived types were used in this serialized files then we would have multiple entries.
        var monoBehaviour = metadata.TypeTrees.First(t => t.PersistentTypeID == 114);
        Assert.IsFalse(monoBehaviour.ScriptID.IsZero,
            "MonoBehaviour type entry should carry a non-zero scriptID");

        Assert.That(monoBehaviour.ScriptTypeIndex, Is.EqualTo(0),
            "MonoBehaviour type entry should have a valid ScriptTypeIndex"); // -1 is used for non-script types, so 0 is the first valid index

        Assert.That(monoBehaviour.TypeDependencies.Length, Is.EqualTo(1),
            "MonoBehaviour should have TypeDependencies array because to record SerializedReference dependencies");

        Assert.That(monoBehaviour.TypeDependencies[0], Is.EqualTo(0),
            "MonoBehaviour should record dependency on SerializedReference");

        // --- Script types ---
        // ScriptTypeIndex=0 on the MonoBehaviour entry above means it is backed by ScriptTypes[0].
        // The MonoScript lives in external file #1 (the companion monoscriptbundle).
        Assert.IsNotNull(metadata.ScriptTypes, "ScriptTypes should be populated");
        Assert.That(metadata.ScriptTypes.Length, Is.EqualTo(1), "Should have 1 script type entry");
        Assert.That(metadata.ScriptTypes[0].FileID, Is.EqualTo(1),
            "ScriptTypes[0].FileID should be 1 (first external reference)");
        Assert.That(metadata.ScriptTypes[0].PathID, Is.EqualTo(3225487681952536265L),
            "ScriptTypes[0].PathID should match the MonoScript object ID in monoscriptbundle");

        // --- SerializedReference type entry ---
        Assert.That(metadata.SerializedReferenceTypeTrees.Length, Is.EqualTo(1));
        var refType = metadata.SerializedReferenceTypeTrees[0];
        Assert.That(refType.PersistentTypeID, Is.EqualTo(-1));
        Assert.That(refType.ClassName, Is.EqualTo("Data"));
        Assert.That(refType.Namespace, Is.EqualTo("MyScripts"));
        Assert.That(refType.AssemblyName, Is.EqualTo("Assembly-CSharp"));
    }

    [Test]
    public void TryParseMetadata_V23ExtractedMonoscriptBundle_ReturnsExpectedTypeTreeData()
    {
        // This is a v23 (kExtractedTypeTreeSupport) file where the TypeTree blobs have been
        // extracted to a shared external store. The metadata records a non-zero TypeTreeContentHash
        // as a cache key, but typeTreeSerializedSize == 0 and InlineTypeTree == false for every entry.
        var testFile = Path.Combine(m_TestDataPath, "AssetBundleTypeTreeVariations", "v23_extracted",
            "monoscriptbundle.serializedfile");

        bool headerResult = SerializedFileDetector.TryDetectSerializedFile(testFile, out var headerInfo);
        Assert.IsTrue(headerResult, "File should be detected as a valid SerializedFile");

        bool result = SerializedFileDetector.TryParseMetadata(testFile, headerInfo, out var metadata, out var errorMessage);
        Assert.IsTrue(result, $"Metadata parsing should succeed. Error: {errorMessage}");
        Assert.IsNotNull(metadata);

        // --- Initial metadata fields ---
        Assert.IsTrue(metadata.EnableTypeTree, "EnableTypeTree should be true");

        // --- Type counts ---
        Assert.That(metadata.TypeTreeCount, Is.EqualTo(2), "Should have 2 regular type entries");
        Assert.That(metadata.SerializedReferenceTypeTreeCount, Is.EqualTo(0), "Should have 0 SerializeReference type entries");
        Assert.IsNotNull(metadata.TypeTrees, "TypeTrees array should be populated");
        Assert.That(metadata.TypeTrees.Length, Is.EqualTo(2));

        // --- All TypeTree blobs are extracted: non-zero content hash, zero size, not inline ---
        foreach (var entry in metadata.TypeTrees)
        {
            Assert.IsFalse(entry.TypeTreeContentHash.IsZero,
                $"TypeTreeContentHash should be non-zero for extracted v23 entry (persistentTypeID={entry.PersistentTypeID})");
            Assert.That(entry.TypeTreeSerializedSize, Is.EqualTo(0u),
                $"TypeTreeSerializedSize should be 0 for extracted entry (persistentTypeID={entry.PersistentTypeID})");
            Assert.IsFalse(entry.InlineTypeTree,
                $"InlineTypeTree should be false for extracted entry (persistentTypeID={entry.PersistentTypeID})");
        }
    }

    [Test]
    public void TryParseMetadata_V22PrefabWithSerializedReference_ReturnsExpectedObjectList()
    {
        var testFile = Path.Combine(m_TestDataPath, "AssetBundleTypeTreeVariations", "v22",
            "prefab_with_serializedreference.serializedfile");

        bool headerResult = SerializedFileDetector.TryDetectSerializedFile(testFile, out var headerInfo);
        Assert.IsTrue(headerResult, "File should be detected as a valid SerializedFile");

        bool result = SerializedFileDetector.TryParseMetadata(testFile, headerInfo, out var metadata, out var errorMessage);
        Assert.IsTrue(result, $"Metadata parsing should succeed. Error: {errorMessage}");
        Assert.IsNotNull(metadata);

        Assert.IsNotNull(metadata.ObjectList, "ObjectList should be populated");
        Assert.That(metadata.ObjectList.Length, Is.EqualTo(6), "Should have 6 objects");

        // Verify exact values for each object entry.
        // Expected data from the file's object table (fileID, typeID, offset, size):
        var expected = new (long Id, int TypeId, long Offset, long Size)[]
        {
            (                   1L, 142,  5552L, 300L),  // AssetBundle
            ( 674343093664966924L,   4,  5856L,  68L),  // Transform
            (4902368549205534988L,   4,  5936L,  80L),  // Transform
            (5206304541755795724L,   1,  6016L,  51L),  // GameObject
            (6854740422901983500L,   1,  6080L,  35L),  // GameObject
            (8430482813342345484L, 114,  6128L, 104L),  // MonoBehaviour
        };

        for (int i = 0; i < expected.Length; i++)
        {
            var obj = metadata.ObjectList[i];
            Assert.That(obj.Id,     Is.EqualTo(expected[i].Id),     $"ObjectList[{i}].Id");
            Assert.That(obj.TypeId, Is.EqualTo(expected[i].TypeId), $"ObjectList[{i}].TypeId");
            Assert.That(obj.Offset, Is.EqualTo(expected[i].Offset), $"ObjectList[{i}].Offset");
            Assert.That(obj.Size,   Is.EqualTo(expected[i].Size),   $"ObjectList[{i}].Size");
        }
    }

    [Test]
    public void TryParseMetadata_V22PrefabWithSerializedReference_ReturnsExpectedExternalReferences()
    {
        var testFile = Path.Combine(m_TestDataPath, "AssetBundleTypeTreeVariations", "v22",
            "prefab_with_serializedreference.serializedfile");

        bool headerResult = SerializedFileDetector.TryDetectSerializedFile(testFile, out var headerInfo);
        Assert.IsTrue(headerResult, "File should be detected as a valid SerializedFile");

        bool result = SerializedFileDetector.TryParseMetadata(testFile, headerInfo, out var metadata, out var errorMessage);
        Assert.IsTrue(result, $"Metadata parsing should succeed. Error: {errorMessage}");
        Assert.IsNotNull(metadata);

        Assert.IsNotNull(metadata.ExternalReferences, "ExternalReferences should be populated");
        Assert.That(metadata.ExternalReferences.Length, Is.EqualTo(1), "Should have 1 external reference");

        var extRef = metadata.ExternalReferences[0];
        Assert.That(extRef.Path, Is.EqualTo("archive:/CAB-d57a1d89ac0708bf030936c59479c685/CAB-d57a1d89ac0708bf030936c59479c685"));
        Assert.That(extRef.Guid, Is.EqualTo("00000000000000000000000000000000"));
        Assert.That(extRef.Type, Is.EqualTo(ExternalReferenceType.NonAssetType));
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
        var testFile = Path.Combine(m_TestDataPath, "LegacyFormats", "AssetBundles", "alienprefab");

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
