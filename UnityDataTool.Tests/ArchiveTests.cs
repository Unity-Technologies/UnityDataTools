using System;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityDataTools.FileSystem;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

public class ArchiveTests
{
    private string m_TestOutputFolder;
    private string m_TestDataFolder;
    private string m_ArchivePath;

    // An LZ4 (chunk-based) archive in format version 9, where each block records its own offset
    // and the writer left padding between them.
    private string m_Version9ArchivePath;

    // A version 8 archive with several blocks, which has no stored offsets.
    private string m_LegacyMultiBlockArchivePath;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_folder");
        m_TestDataFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");
        m_ArchivePath = Path.Combine(m_TestDataFolder, "AssetBundles", "2023.1.0a16", "scenes");
        m_Version9ArchivePath = Path.Combine(m_TestDataFolder, "LeadingEdgeBuilds", "AssetBundlesLz4", "scenes");
        m_LegacyMultiBlockArchivePath = Path.Combine(m_TestDataFolder, "PlayerDataCompressed", "data.unity3d");
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
    public async Task ArchiveList_TextFormat()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "archive", "list", m_ArchivePath }));

            var actualOutput = sw.ToString().Replace("\r\n", "\n");

            var expectedOutput =
@"BuildPlayer-SampleScene.sharedAssets
  Data Offset: 0
  Size: 90732
  Flags: SerializedFile

BuildPlayer-SampleScene
  Data Offset: 90732
  Size: 153352
  Flags: SerializedFile

BuildPlayer-OtherScene.sharedAssets
  Data Offset: 244084
  Size: 136744
  Flags: SerializedFile

BuildPlayer-OtherScene
  Data Offset: 380828
  Size: 158340
  Flags: SerializedFile

";

            Assert.AreEqual(expectedOutput, actualOutput);
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task ArchiveList_JsonFormat()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "archive", "list", m_ArchivePath, "-f", "Json" }));

            var output = sw.ToString();
            var jsonArray = JsonDocument.Parse(output).RootElement;
            Assert.AreEqual(JsonValueKind.Array, jsonArray.ValueKind);
            Assert.AreEqual(4, jsonArray.GetArrayLength());

            foreach (var element in jsonArray.EnumerateArray())
            {
                Assert.IsTrue(element.TryGetProperty("path", out _));
                Assert.IsTrue(element.TryGetProperty("dataOffset", out _));
                Assert.IsTrue(element.TryGetProperty("size", out _));
                Assert.IsTrue(element.TryGetProperty("flags", out _));
                Assert.AreEqual("SerializedFile", element.GetProperty("flags").GetString());
            }

            Assert.AreEqual("BuildPlayer-SampleScene.sharedAssets", jsonArray[0].GetProperty("path").GetString());
            Assert.AreEqual(0, jsonArray[0].GetProperty("dataOffset").GetUInt64());
            Assert.AreEqual(90732, jsonArray[0].GetProperty("size").GetInt64());
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task ArchiveHeader_TextFormat()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "archive", "header", m_ArchivePath }));

            var output = sw.ToString();
            Assert.That(output, Does.Contain("UnityFS"));
            Assert.That(output, Does.Contain("2023.1.0a16"));
            Assert.That(output, Does.Contain("93,075"));
            Assert.That(output, Does.Contain("Lz4HC"));
            Assert.That(output, Does.Contain("BlocksAndDirectoryInfoCombined"));
            Assert.That(output, Does.Contain("BlockInfoNeedPaddingAtStart"));
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task ArchiveHeader_JsonFormat()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "archive", "header", m_ArchivePath, "-f", "Json" }));

            var output = sw.ToString();
            var json = JsonDocument.Parse(output).RootElement;
            Assert.AreEqual(JsonValueKind.Object, json.ValueKind);

            Assert.AreEqual("UnityFS", json.GetProperty("signature").GetString());
            Assert.AreEqual(8u, json.GetProperty("version").GetUInt32());
            Assert.AreEqual("2023.1.0a16", json.GetProperty("unityVersion").GetString());
            Assert.AreEqual(93075u, json.GetProperty("fileSize").GetUInt64());
            Assert.AreEqual(118u, json.GetProperty("compressedMetadataSize").GetUInt32());
            Assert.AreEqual(234u, json.GetProperty("uncompressedMetadataSize").GetUInt32());
            Assert.AreEqual("Lz4HC", json.GetProperty("metadataCompression").GetString());

            var flags = json.GetProperty("flags");
            Assert.AreEqual(JsonValueKind.Array, flags.ValueKind);
            Assert.AreEqual(2, flags.GetArrayLength());
            Assert.AreEqual("BlocksAndDirectoryInfoCombined", flags[0].GetString());
            Assert.AreEqual("BlockInfoNeedPaddingAtStart", flags[1].GetString());
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task ArchiveBlocks_TextFormat()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "archive", "blocks", m_ArchivePath }));

            var output = sw.ToString();
            Assert.That(output, Does.Contain("Blocks: 1"));
            Assert.That(output, Does.Contain("#0"));
            Assert.That(output, Does.Contain("FileOffset: 192"));
            Assert.That(output, Does.Contain("DataOffset: 0"));
            Assert.That(output, Does.Contain("Uncompressed: 539,168"));
            Assert.That(output, Does.Contain("Compressed: 92,883"));
            Assert.That(output, Does.Contain("Compression: Lzma"));
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task ArchiveBlocks_JsonFormat()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "archive", "blocks", m_ArchivePath, "-f", "Json" }));

            var output = sw.ToString();
            var json = JsonDocument.Parse(output).RootElement;
            Assert.AreEqual(JsonValueKind.Object, json.ValueKind);

            var blocks = json.GetProperty("blocks");
            Assert.AreEqual(JsonValueKind.Array, blocks.ValueKind);
            Assert.AreEqual(1, blocks.GetArrayLength());

            var block = blocks[0];
            Assert.AreEqual(0, block.GetProperty("index").GetInt32());
            Assert.AreEqual(192, block.GetProperty("fileOffset").GetInt64());
            Assert.AreEqual(0, block.GetProperty("dataOffset").GetInt64());
            Assert.AreEqual(539168u, block.GetProperty("uncompressedSize").GetUInt32());
            Assert.AreEqual(92883u, block.GetProperty("compressedSize").GetUInt32());
            Assert.AreEqual("Lzma", block.GetProperty("compression").GetString());
            Assert.AreEqual(true, block.GetProperty("isStreamed").GetBoolean());
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task ArchiveInfo_TextFormat()
    {
        var infoPath = Path.Combine(m_TestDataFolder, "PlayerDataCompressed", "data.unity3d");
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "archive", "info", infoPath }));

            var output = sw.ToString();
            Assert.That(output, Does.Contain("2021.3.20f1"));
            Assert.That(output, Does.Contain("459,654"));
            Assert.That(output, Does.Contain("459,382"));
            Assert.That(output, Does.Contain("963,117"));
            Assert.That(output, Does.Contain("2.10x"));
            Assert.That(output, Does.Contain("Lz4"));
            Assert.That(output, Does.Contain("Block Count"));
            Assert.That(output, Does.Contain("8"));
            Assert.That(output, Does.Contain("File Count"));
            Assert.That(output, Does.Contain("Serialized File Count"));
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task ArchiveInfo_JsonFormat()
    {
        var infoPath = Path.Combine(m_TestDataFolder, "PlayerDataCompressed", "data.unity3d");
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "archive", "info", infoPath, "-f", "Json" }));

            var output = sw.ToString();
            var json = JsonDocument.Parse(output).RootElement;
            Assert.AreEqual(JsonValueKind.Object, json.ValueKind);

            Assert.AreEqual("2021.3.20f1", json.GetProperty("unityVersion").GetString());
            Assert.AreEqual(459654u, json.GetProperty("fileSize").GetUInt64());
            Assert.AreEqual(459382, json.GetProperty("dataSize").GetInt64());
            Assert.AreEqual(963117, json.GetProperty("uncompressedDataSize").GetInt64());
            Assert.AreEqual(2.1, json.GetProperty("compressionRatio").GetDouble(), 0.01);
            Assert.AreEqual("Lz4", json.GetProperty("compression").GetString());
            Assert.AreEqual(8, json.GetProperty("blockCount").GetInt32());
            Assert.AreEqual(5, json.GetProperty("fileCount").GetInt32());
            Assert.AreEqual(5, json.GetProperty("serializedFileCount").GetInt32());
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task ArchiveInfo_ZstdCompression_JsonFormat()
    {
        var infoPath = Path.Combine(m_TestDataFolder, "contentdirectory-zstd", "content0.archive");
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "archive", "info", infoPath, "-f", "Json" }));

            var output = sw.ToString();
            var json = JsonDocument.Parse(output).RootElement;
            Assert.AreEqual(JsonValueKind.Object, json.ValueKind);

            Assert.AreEqual("5", json.GetProperty("compression").GetString(), "Unexpected compression value in contentdirectory-zstd/content0.archive");
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task ArchiveExtract_ZstdArchive_FileExtractedSuccessfully()
    {
        var zstdArchivePath = Path.Combine(m_TestDataFolder, "contentdirectory-zstd", "content0.archive");
        const string fileName = "d1f0968e6eb3997a264fda27f5eb02d3.cf"; // File known to be contained in the test archive

        Assert.AreEqual(0, await Program.Main(new string[] { "archive", "extract", zstdArchivePath, "--filter", fileName }));

        var extractedFile = new FileInfo(Path.Combine(m_TestOutputFolder, "archive", fileName));
        Assert.IsTrue(extractedFile.Exists, $"Expected file not found: {fileName}");
        Assert.AreEqual(2176, extractedFile.Length, "Unexpected size for file extracted from contentdirectory-zstd/content0.archive"); // Known size
    }

    [Test]
    public async Task ArchiveExtract_FilesExtractedSuccessfully()
    {
        Assert.AreEqual(0, await Program.Main(new string[] { "archive", "extract", m_ArchivePath }));

        string[] expectedFiles =
        {
            "BuildPlayer-SampleScene.sharedAssets",
            "BuildPlayer-SampleScene",
            "BuildPlayer-OtherScene.sharedAssets",
            "BuildPlayer-OtherScene",
        };

        foreach (var file in expectedFiles)
        {
            Assert.IsTrue(File.Exists(Path.Combine(m_TestOutputFolder, "archive", file)), $"Expected file not found: {file}");
        }

        // Verify extracted file size matches the size reported by the list command.
        var extractedFile = new FileInfo(Path.Combine(m_TestOutputFolder, "archive", "BuildPlayer-SampleScene.sharedAssets"));
        Assert.AreEqual(90732, extractedFile.Length);
    }

    [Test]
    public async Task ArchiveExtract_WithFilter_ExtractsOnlyMatchingFiles()
    {
        // "sampleSCENE" should match BuildPlayer-SampleScene.sharedAssets and BuildPlayer-SampleScene
        // (case-insensitive) but not the OtherScene files.
        Assert.AreEqual(0, await Program.Main(new string[] { "archive", "extract", m_ArchivePath, "--filter", "sampleSCENE" }));

        string[] expectedFiles =
        {
            "BuildPlayer-SampleScene.sharedAssets",
            "BuildPlayer-SampleScene",
        };

        string[] excludedFiles =
        {
            "BuildPlayer-OtherScene.sharedAssets",
            "BuildPlayer-OtherScene",
        };

        foreach (var file in expectedFiles)
        {
            Assert.IsTrue(File.Exists(Path.Combine(m_TestOutputFolder, "archive", file)), $"Expected file not found: {file}");
        }

        foreach (var file in excludedFiles)
        {
            Assert.IsFalse(File.Exists(Path.Combine(m_TestOutputFolder, "archive", file)), $"File should not have been extracted: {file}");
        }
    }

    [Test]
    public async Task ArchiveHeader_Version9_AllFlagsRecognized()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "archive", "header", m_Version9ArchivePath, "-f", "Json" }));

            var json = JsonDocument.Parse(sw.ToString()).RootElement;

            Assert.AreEqual(9u, json.GetProperty("version").GetUInt32());

            // Unrecognized bits are reported as raw hex, so this catches a flag we don't know about.
            var flags = json.GetProperty("flags").EnumerateArray().Select(f => f.GetString()).ToArray();
            Assert.That(flags, Has.None.StartsWith("0x"), $"Unrecognized archive flag bits: {string.Join(", ", flags)}");
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    // From version 9 each block records where its stored bytes start, so the reader must take the
    // position from the block list rather than accumulating the compressed sizes. This checks the
    // parsed offsets against the actual file: the blocks are in order, the gaps between them are
    // padding that belongs to no block (all zero bytes), and at least one block sits somewhere
    // accumulation would not have put it - which is what proves the stored offset is being used.
    [Test]
    public async Task ArchiveBlocks_Version9_OffsetsComeFromTheBlockList()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "archive", "blocks", m_Version9ArchivePath, "-f", "Json" }));

            var blocks = JsonDocument.Parse(sw.ToString()).RootElement.GetProperty("blocks").EnumerateArray().ToArray();
            Assert.Greater(blocks.Length, 1, "The test archive is expected to have several chunks.");

            var archiveBytes = File.ReadAllBytes(m_Version9ArchivePath);
            long totalPadding = 0;

            for (int i = 1; i < blocks.Length; i++)
            {
                var previousEnd = blocks[i - 1].GetProperty("fileOffset").GetInt64() +
                                  blocks[i - 1].GetProperty("compressedSize").GetInt64();
                var offset = blocks[i].GetProperty("fileOffset").GetInt64();

                Assert.GreaterOrEqual(offset, previousEnd, $"Block {i} overlaps the preceding block.");

                for (var p = previousEnd; p < offset; p++)
                    Assert.AreEqual(0, archiveBytes[p], $"Padding byte at offset {p} is not zero.");

                totalPadding += offset - previousEnd;
            }

            Assert.Greater(totalPadding, 0,
                "The archive's blocks are contiguous, so this test would pass even if the stored offsets were ignored. " +
                "The fixture needs to be an archive the writer left padding in.");
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    // Version 8 and earlier don't store block offsets; their blocks are contiguous and the parser
    // has to fall back to accumulating the compressed sizes.
    [Test]
    public async Task ArchiveBlocks_LegacyVersion8_BlocksAreContiguous()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "archive", "blocks", m_LegacyMultiBlockArchivePath, "-f", "Json" }));

            var blocks = JsonDocument.Parse(sw.ToString()).RootElement.GetProperty("blocks").EnumerateArray().ToArray();
            Assert.Greater(blocks.Length, 1, "The legacy test archive is expected to have several blocks.");

            for (int i = 1; i < blocks.Length; i++)
            {
                var previousEnd = blocks[i - 1].GetProperty("fileOffset").GetInt64() +
                                  blocks[i - 1].GetProperty("compressedSize").GetInt64();
                Assert.AreEqual(previousEnd, blocks[i].GetProperty("fileOffset").GetInt64(),
                    $"Block {i} of a version 8 archive should directly follow the preceding block.");
            }
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task ArchiveInfo_Version9_ReportsPaddingSize()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "archive", "info", m_Version9ArchivePath, "-f", "Json" }));

            var json = JsonDocument.Parse(sw.ToString()).RootElement;

            Assert.AreEqual("Lz4HC", json.GetProperty("compression").GetString());
            Assert.Greater(json.GetProperty("blockPaddingSize").GetInt64(), 0);
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    // The native library reads the block offsets independently of the C# parser, so successful
    // extraction of content spanning several non-contiguous blocks confirms the layout is understood.
    [Test]
    public async Task ArchiveExtract_Version9_FilesExtractedSuccessfully()
    {
        Assert.AreEqual(0, await Program.Main(new string[] { "archive", "extract", m_Version9ArchivePath }));

        // BuildPlayer-Scene1.sharedAssets spans the first eight blocks of the archive.
        var extractedFile = new FileInfo(Path.Combine(m_TestOutputFolder, "archive", "BuildPlayer-Scene1.sharedAssets"));
        Assert.IsTrue(extractedFile.Exists, "Expected file not found: BuildPlayer-Scene1.sharedAssets");
        Assert.AreEqual(761460, extractedFile.Length);
    }
}
