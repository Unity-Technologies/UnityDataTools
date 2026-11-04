using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityDataTools.Archive;
using UnityDataTools.BinaryFormat;
using UnityDataTools.FileSystem;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

public class ArchiveRepackTests
{
    private string m_TestOutputFolder;
    private string m_TestDataFolder;

    // A single LZMA block holding four SerializedFiles (format version 8).
    private string m_LzmaArchivePath;

    // An LZ4HC chunk-based archive in format version 9, with per-block offsets.
    private string m_Version9ArchivePath;

    // An LZ4 Player build archive with several blocks and no block offsets (format version 8).
    private string m_PlayerArchivePath;

    // A larger LZMA AssetBundle that contains a texture's .resS data.
    private string m_AssetBundlePath;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_folder_repack");
        m_TestDataFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");
        m_LzmaArchivePath = Path.Combine(m_TestDataFolder, "AssetBundles", "2023.1.0a16", "scenes");
        m_Version9ArchivePath = Path.Combine(m_TestDataFolder, "LeadingEdgeBuilds", "AssetBundlesLz4", "scenes");
        m_PlayerArchivePath = Path.Combine(m_TestDataFolder, "PlayerDataCompressed", "data.unity3d");
        m_AssetBundlePath = Path.Combine(m_TestDataFolder, "AssetBundles", "2023.1.0a16", "assetbundle");
        Directory.CreateDirectory(m_TestOutputFolder);
    }

    [TearDown]
    public void Teardown()
    {
        var testDir = new DirectoryInfo(m_TestOutputFolder);
        testDir.EnumerateFiles().ToList().ForEach(f => f.Delete());
        testDir.EnumerateDirectories().ToList().ForEach(d => d.Delete(true));
    }

    string OutputPath(string name) => Path.Combine(m_TestOutputFolder, name);

    static (ArchiveHeaderInfo header, ArchiveMetadata metadata) ReadArchive(string path)
    {
        Assert.IsTrue(ArchiveDetector.TryReadArchiveHeader(path, out var header, out var error), error);
        Assert.IsTrue(ArchiveDetector.TryReadArchiveMetadata(path, header, out var metadata, out error), error);
        return (header, metadata);
    }

    // Extracts both archives with the 'archive extract' command and compares the files, so the content
    // check does not depend on the verification inside repack.
    async Task AssertSameExtractedContent(string originalPath, string repackedPath)
    {
        var originalDir = OutputPath("extract-original");
        var repackedDir = OutputPath("extract-repacked");
        var (exitA, _) = await ConsoleTestHelper.RunCapturingStdOut("archive", "extract", originalPath, "-o", originalDir);
        var (exitB, _) = await ConsoleTestHelper.RunCapturingStdOut("archive", "extract", repackedPath, "-o", repackedDir);
        Assert.AreEqual(0, exitA);
        Assert.AreEqual(0, exitB);

        var originalFiles = Directory.GetFiles(originalDir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(originalDir, f)).OrderBy(f => f, StringComparer.Ordinal).ToArray();
        var repackedFiles = Directory.GetFiles(repackedDir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(repackedDir, f)).OrderBy(f => f, StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEqual(originalFiles, repackedFiles);
        Assert.Greater(originalFiles.Length, 0);

        foreach (var file in originalFiles)
        {
            var a = File.ReadAllBytes(Path.Combine(originalDir, file));
            var b = File.ReadAllBytes(Path.Combine(repackedDir, file));
            Assert.IsTrue(a.AsSpan().SequenceEqual(b), $"Content of {file} differs");
        }
    }

    static void AssertSameDirectory(ArchiveMetadata original, ArchiveMetadata repacked)
    {
        var a = original.DirectoryInfo.Nodes;
        var b = repacked.DirectoryInfo.Nodes;
        Assert.AreEqual(a.Length, b.Length);
        for (int i = 0; i < a.Length; i++)
        {
            Assert.AreEqual(a[i].Path, b[i].Path);
            Assert.AreEqual(a[i].Size, b[i].Size);
            Assert.AreEqual(a[i].Flags, b[i].Flags);
        }
    }

    [Test]
    public async Task Repack_LzmaArchive_DefaultsToUnityLz4HCLayout()
    {
        var output = OutputPath("scenes.lz4hc");
        var (exitCode, stdOut) = await ConsoleTestHelper.RunCapturingStdOut("archive", "repack", m_LzmaArchivePath, "-o", output);

        Assert.AreEqual(0, exitCode, stdOut);
        StringAssert.Contains("Verified: all 4 files match the original byte for byte.", stdOut);

        var (originalHeader, originalMetadata) = ReadArchive(m_LzmaArchivePath);
        var (header, metadata) = ReadArchive(output);

        Assert.AreEqual(originalHeader.Version, header.Version);
        Assert.AreEqual(originalHeader.UnityVersion, header.UnityVersion);
        Assert.AreEqual(new FileInfo(output).Length, (long)header.Size);
        Assert.AreEqual(originalHeader.ArchiveFlagBits, header.ArchiveFlagBits);
        AssertSameDirectory(originalMetadata, metadata);

        // Every block is a full 128 KB except the last, and they are LZ4HC compressed.
        var blocks = metadata.BlocksInfo.Blocks;
        Assert.Greater(blocks.Length, 1);
        for (int i = 0; i < blocks.Length - 1; i++)
            Assert.AreEqual(RepackOptions.DefaultBlockSize, blocks[i].UncompressedSize);
        Assert.IsTrue(blocks.All(b => b.CompressionType == 3 || b.CompressionType == 0));
        Assert.IsTrue(blocks.Any(b => b.CompressionType == 3));

        // The files are packed back to back, as in the original.
        for (int i = 0; i < metadata.DirectoryInfo.Nodes.Length; i++)
            Assert.AreEqual(originalMetadata.DirectoryInfo.Nodes[i].DataOffset, metadata.DirectoryInfo.Nodes[i].DataOffset);

        await AssertSameExtractedContent(m_LzmaArchivePath, output);
    }

    [Test]
    public async Task Repack_DumpOutputIsUnchanged()
    {
        var output = OutputPath("assetbundle.repacked");
        Assert.AreEqual(0, (await ConsoleTestHelper.RunCapturingStdOut(
            "archive", "repack", m_AssetBundlePath, "-o", output, "--block-size", "32768", "--split-at-file-boundaries")).exitCode);

        var (exitA, dumpA) = await ConsoleTestHelper.RunCapturingStdOut("dump", m_AssetBundlePath, "--stdout");
        var (exitB, dumpB) = await ConsoleTestHelper.RunCapturingStdOut("dump", output, "--stdout");
        Assert.AreEqual(0, exitA);
        Assert.AreEqual(0, exitB);
        Assert.Greater(dumpA.Length, 1000);
        Assert.AreEqual(dumpA, dumpB);
    }

    [Test]
    public async Task Repack_Version9Archive_KeepsBlockOffsets()
    {
        var output = OutputPath("v9.repacked");
        Assert.AreEqual(0, (await ConsoleTestHelper.RunCapturingStdOut(
            "archive", "repack", m_Version9ArchivePath, "-o", output, "--block-size", "16384")).exitCode);

        var (header, metadata) = ReadArchive(output);
        Assert.AreEqual(9u, header.Version);
        Assert.IsTrue(header.HasStorageBlockOffsets);

        var blocks = metadata.BlocksInfo.Blocks;
        Assert.IsTrue(blocks.All(b => b.UncompressedSize <= 16384));

        // Without alignment the blocks are stored back to back.
        for (int i = 1; i < blocks.Length; i++)
            Assert.AreEqual(blocks[i - 1].Offset + blocks[i - 1].CompressedSize, blocks[i].Offset);

        await AssertSameExtractedContent(m_Version9ArchivePath, output);
    }

    [Test]
    public async Task Repack_BlockAlignment_PadsBlockPositions()
    {
        var output = OutputPath("v9.aligned");
        Assert.AreEqual(0, (await ConsoleTestHelper.RunCapturingStdOut(
            "archive", "repack", m_Version9ArchivePath, "-o", output, "--block-size", "16384", "--block-alignment", "4096")).exitCode);

        var (header, metadata) = ReadArchive(output);
        var dataStart = ArchiveDetector.GetDataOffset(header);
        var blocks = metadata.BlocksInfo.Blocks;
        Assert.Greater(blocks.Length, 2);
        foreach (var block in blocks)
        {
            Assert.AreEqual(0, block.Offset % 4096, $"Block at {block.Offset} is not aligned");
            Assert.AreEqual(dataStart + block.Offset, block.FileOffset);
        }

        await AssertSameExtractedContent(m_Version9ArchivePath, output);
    }

    [Test]
    public async Task Repack_BlockAlignment_RejectsArchivesWithoutBlockOffsets()
    {
        var output = OutputPath("v8.aligned");
        var (exitCode, stdErr) = await ConsoleTestHelper.RunCapturingStdErr(
            "archive", "repack", m_LzmaArchivePath, "-o", output, "--block-alignment", "16");

        Assert.AreEqual(1, exitCode);
        StringAssert.Contains("version 9", stdErr);
        Assert.IsFalse(File.Exists(output));
    }

    [Test]
    public async Task Repack_SplitAtFileBoundaries_NoBlockSpansTwoFiles()
    {
        var spanned = OutputPath("spanned");
        var split = OutputPath("split");
        Assert.AreEqual(0, (await ConsoleTestHelper.RunCapturingStdOut("archive", "repack", m_LzmaArchivePath, "-o", spanned)).exitCode);
        Assert.AreEqual(0, (await ConsoleTestHelper.RunCapturingStdOut(
            "archive", "repack", m_LzmaArchivePath, "-o", split, "--split-at-file-boundaries")).exitCode);

        var (_, metadata) = ReadArchive(split);
        var nodes = metadata.DirectoryInfo.Nodes;
        foreach (var block in metadata.BlocksInfo.Blocks)
        {
            long start = block.DataOffset;
            long end = start + block.UncompressedSize;
            int owners = nodes.Count(n => (long)n.DataOffset < end && (long)(n.DataOffset + n.Size) > start);
            Assert.AreEqual(1, owners, $"Block at data offset {start} holds data from {owners} files");
        }

        // Each file can now be read on its own, which spanning blocks do not allow.
        Assert.AreEqual(1.0, ArchiveRepacker.ComputeStats(split).ReadAmplification, 1e-9);
        Assert.Greater(ArchiveRepacker.ComputeStats(spanned).ReadAmplification, 1.0);

        await AssertSameExtractedContent(m_LzmaArchivePath, split);
    }

    [Test]
    public async Task Repack_FileAlignment_AlignsFileOffsets()
    {
        var output = OutputPath("aligned");
        Assert.AreEqual(0, (await ConsoleTestHelper.RunCapturingStdOut(
            "archive", "repack", m_LzmaArchivePath, "-o", output, "--file-alignment", "4096")).exitCode);

        var (_, metadata) = ReadArchive(output);
        var nodes = metadata.DirectoryInfo.Nodes;
        foreach (var node in nodes)
            Assert.AreEqual(0ul, node.DataOffset % 4096, $"{node.Path} starts at {node.DataOffset}");

        // The padding is added only where needed and never overlaps the previous file.
        for (int i = 1; i < nodes.Length; i++)
        {
            Assert.GreaterOrEqual(nodes[i].DataOffset, nodes[i - 1].DataOffset + nodes[i - 1].Size);
            Assert.Less(nodes[i].DataOffset, nodes[i - 1].DataOffset + nodes[i - 1].Size + 4096);
        }

        await AssertSameExtractedContent(m_LzmaArchivePath, output);
    }

    [Test]
    public async Task Repack_MaxCompressedBlockSize_LimitsStoredBlockSize()
    {
        var output = OutputPath("capped");
        Assert.AreEqual(0, (await ConsoleTestHelper.RunCapturingStdOut(
            "archive", "repack", m_AssetBundlePath, "-o", output, "-c", "Lz4", "--block-size", "262144", "--max-compressed-block-size", "8192")).exitCode);

        var (_, metadata) = ReadArchive(output);
        var blocks = metadata.BlocksInfo.Blocks;
        Assert.IsTrue(blocks.All(b => b.CompressedSize <= 8192), "A block exceeds the compressed size limit");

        // Well-compressing data still gets blocks longer than the limit.
        Assert.IsTrue(blocks.Any(b => b.UncompressedSize > 8192));
        Assert.IsTrue(blocks.All(b => b.UncompressedSize <= 262144));

        await AssertSameExtractedContent(m_AssetBundlePath, output);
    }

    [Test]
    public async Task Repack_MinCompressionRatio_StoresPoorlyCompressingBlocksUncompressed()
    {
        var normal = OutputPath("normal");
        var strict = OutputPath("strict");
        Assert.AreEqual(0, (await ConsoleTestHelper.RunCapturingStdOut("archive", "repack", m_Version9ArchivePath, "-o", normal)).exitCode);
        Assert.AreEqual(0, (await ConsoleTestHelper.RunCapturingStdOut(
            "archive", "repack", m_Version9ArchivePath, "-o", strict, "--min-compression-ratio", "1.5")).exitCode);

        var normalBlocks = ReadArchive(normal).metadata.BlocksInfo.Blocks;
        var strictBlocks = ReadArchive(strict).metadata.BlocksInfo.Blocks;
        Assert.AreEqual(normalBlocks.Length, strictBlocks.Length);

        // Blocks that compress by less than 1.5x are stored as-is; the others are unchanged.
        bool anyFallback = false;
        for (int i = 0; i < normalBlocks.Length; i++)
        {
            var ratio = (double)normalBlocks[i].UncompressedSize / normalBlocks[i].CompressedSize;
            if (normalBlocks[i].CompressionType != 0 && ratio < 1.5)
            {
                Assert.AreEqual(0, strictBlocks[i].CompressionType);
                Assert.AreEqual(strictBlocks[i].UncompressedSize, strictBlocks[i].CompressedSize);
                anyFallback = true;
            }
            else
            {
                Assert.AreEqual(normalBlocks[i].CompressionType, strictBlocks[i].CompressionType);
            }
        }
        Assert.IsTrue(anyFallback, "The test data should contain a block that compresses poorly");

        await AssertSameExtractedContent(m_Version9ArchivePath, strict);
    }

    [Test]
    public async Task Repack_NoCompression_StoresDataAsIs()
    {
        var output = OutputPath("uncompressed");
        Assert.AreEqual(0, (await ConsoleTestHelper.RunCapturingStdOut("archive", "repack", m_PlayerArchivePath, "-o", output, "-c", "None")).exitCode);

        var (header, metadata) = ReadArchive(output);
        Assert.IsFalse(header.HasStorageBlockOffsets);
        Assert.IsTrue(metadata.BlocksInfo.Blocks.All(b => b.CompressionType == 0 && b.CompressedSize == b.UncompressedSize));

        var dataSize = metadata.BlocksInfo.Blocks.Sum(b => (long)b.UncompressedSize);
        var lastNode = metadata.DirectoryInfo.Nodes[^1];
        Assert.AreEqual((long)(lastNode.DataOffset + lastNode.Size), dataSize);

        await AssertSameExtractedContent(m_PlayerArchivePath, output);
    }

    [Test]
    public async Task Repack_JsonFormat_ReportsBothLayouts()
    {
        var output = OutputPath("json");
        var (exitCode, stdOut) = await ConsoleTestHelper.RunCapturingStdOut(
            "archive", "repack", m_LzmaArchivePath, "-o", output, "--split-at-file-boundaries", "-f", "Json");
        Assert.AreEqual(0, exitCode);

        var root = JsonDocument.Parse(stdOut).RootElement;
        Assert.AreEqual(4, root.GetProperty("fileCount").GetInt32());
        Assert.IsTrue(root.GetProperty("verified").GetBoolean());

        var original = root.GetProperty("original");
        var repacked = root.GetProperty("repacked");
        Assert.AreEqual("Lzma", original.GetProperty("compression").GetString());
        Assert.AreEqual(1, original.GetProperty("blockCount").GetInt32());
        Assert.AreEqual("Lz4HC", repacked.GetProperty("compression").GetString());
        Assert.AreEqual(new FileInfo(output).Length, repacked.GetProperty("fileSize").GetInt64());
        Assert.AreEqual(1.0, repacked.GetProperty("readAmplification").GetDouble(), 1e-9);
        Assert.AreEqual(4.0, original.GetProperty("readAmplification").GetDouble(), 1e-9);
    }

    [Test]
    public async Task Repack_SkipVerify_IsReported()
    {
        var output = OutputPath("unverified");
        var (exitCode, stdOut) = await ConsoleTestHelper.RunCapturingStdOut("archive", "repack", m_LzmaArchivePath, "-o", output, "--skip-verify");
        Assert.AreEqual(0, exitCode);
        StringAssert.Contains("Not verified", stdOut);
    }

    [Test]
    public async Task Repack_OutputSameAsInput_Fails()
    {
        var copy = OutputPath("copy");
        File.Copy(m_LzmaArchivePath, copy);
        var before = File.ReadAllBytes(copy);

        var (exitCode, stdErr) = await ConsoleTestHelper.RunCapturingStdErr("archive", "repack", copy, "-o", copy);
        Assert.AreEqual(1, exitCode);
        StringAssert.Contains("different from the input", stdErr);
        Assert.IsTrue(before.AsSpan().SequenceEqual(File.ReadAllBytes(copy)));
    }

    [TestCase("--block-size", "100", "Block size")]
    [TestCase("--block-size", "134217728", "Block size")]
    [TestCase("--file-alignment", "3", "File alignment")]
    [TestCase("--block-alignment", "0", "Block alignment")]
    [TestCase("--min-compression-ratio", "0.5", "compression ratio")]
    [TestCase("--max-compressed-block-size", "10", "Max compressed block size")]
    public async Task Repack_InvalidOptions_Fail(string option, string value, string expectedMessage)
    {
        var output = OutputPath("invalid");
        var (exitCode, stdErr) = await ConsoleTestHelper.RunCapturingStdErr("archive", "repack", m_LzmaArchivePath, "-o", output, option, value);
        Assert.AreEqual(1, exitCode);
        StringAssert.Contains(expectedMessage, stdErr);
        Assert.IsFalse(File.Exists(output));
    }

    [Test]
    public async Task Repack_MaxCompressedBlockSizeWithoutCompression_Fails()
    {
        var (exitCode, stdErr) = await ConsoleTestHelper.RunCapturingStdErr(
            "archive", "repack", m_LzmaArchivePath, "-o", OutputPath("x"), "-c", "None", "--max-compressed-block-size", "4096");
        Assert.AreEqual(1, exitCode);
        StringAssert.Contains("no effect without compression", stdErr);
    }

    [Test]
    public async Task Repack_NotAnArchive_Fails()
    {
        var output = OutputPath("text");
        var (exitCode, stdErr) = await ConsoleTestHelper.RunCapturingStdErr(
            "archive", "repack", Path.Combine(m_TestDataFolder, "TextFile.txt"), "-o", output);
        Assert.AreEqual(1, exitCode);
        StringAssert.Contains("not a Unity Archive", stdErr);
        Assert.IsFalse(File.Exists(output));
    }

    [Test]
    public async Task Repack_WebBundle_Fails()
    {
        var (exitCode, stdErr) = await ConsoleTestHelper.RunCapturingStdErr(
            "archive", "repack", Path.Combine(m_TestDataFolder, "WebBundles", "HelloWorld.data"), "-o", OutputPath("web"));
        Assert.AreEqual(1, exitCode);
        StringAssert.Contains("only supported for Unity Archive files", stdErr);
    }

    [Test]
    public async Task VerifySameContent_DetectsChangedContent()
    {
        // An uncompressed repack lets the test change one byte of file content directly on disk.
        var output = OutputPath("tampered");
        Assert.AreEqual(0, (await ConsoleTestHelper.RunCapturingStdOut("archive", "repack", m_LzmaArchivePath, "-o", output, "-c", "None")).exitCode);

        var (header, metadata) = ReadArchive(output);
        var bytes = File.ReadAllBytes(output);
        var position = ArchiveDetector.GetDataOffset(header) + (long)metadata.DirectoryInfo.Nodes[1].DataOffset + 100;
        bytes[position] ^= 0xFF;
        File.WriteAllBytes(output, bytes);

        UnityFileSystem.Init();
        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => ArchiveRepacker.VerifySameContent(m_LzmaArchivePath, output));
            StringAssert.Contains(metadata.DirectoryInfo.Nodes[1].Path, ex.Message);
        }
        finally
        {
            UnityFileSystem.Cleanup();
        }
    }
}
