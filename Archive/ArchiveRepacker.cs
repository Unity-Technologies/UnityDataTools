using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using K4os.Compression.LZ4;
using UnityDataTools.BinaryFormat;
using UnityDataTools.FileSystem;

namespace UnityDataTools.Archive;

public enum RepackCompression
{
    None,
    Lz4,
    Lz4HC,
}

/// <summary>
/// Settings for how the repacked archive splits its content into blocks.
/// </summary>
public class RepackOptions
{
    public const int DefaultBlockSize = 128 * 1024;
    public const int MinBlockSize = 1024;
    public const int MaxBlockSize = 64 * 1024 * 1024;

    public RepackCompression Compression { get; set; } = RepackCompression.Lz4HC;

    /// <summary>Largest amount of uncompressed data in one block. Unity always uses 128 KB.</summary>
    public int BlockSize { get; set; } = DefaultBlockSize;

    /// <summary>
    /// Largest size a block may take in the file, after compression. When a full block would compress
    /// to more than this, the block is made shorter. Null means no limit.
    /// </summary>
    public int? MaxCompressedBlockSize { get; set; }

    /// <summary>
    /// Start a new block at the start of every file, so that no block holds data from two files.
    /// Unity lets blocks span file boundaries.
    /// </summary>
    public bool SplitAtFileBoundaries { get; set; }

    /// <summary>
    /// Alignment of the start of each file within the uncompressed data. The gap is filled with zeros.
    /// 1 means files are packed back to back.
    /// </summary>
    public int FileAlignment { get; set; } = 1;

    /// <summary>
    /// Alignment of each block's position within the data section, as the Unity 6.7 writer does with
    /// 16 bytes. Only possible from archive format version 9, where each block records its own offset.
    /// 1 means blocks are stored back to back.
    /// </summary>
    public int BlockAlignment { get; set; } = 1;

    /// <summary>
    /// A block is only stored compressed if uncompressed size / compressed size reaches this ratio;
    /// otherwise it is stored uncompressed, which saves the decompression work for data that is
    /// already compressed (for example textures in a compressed GPU format, or audio).
    /// </summary>
    public double MinCompressionRatio { get; set; } = 1.0;

    /// <summary>
    /// Returns an error message for the first invalid setting, or null when all of them are valid.
    /// </summary>
    public string Validate()
    {
        if (BlockSize < MinBlockSize || BlockSize > MaxBlockSize)
            return $"Block size must be between {MinBlockSize:N0} and {MaxBlockSize:N0} bytes.";
        if (MaxCompressedBlockSize.HasValue && (MaxCompressedBlockSize.Value < MinBlockSize || MaxCompressedBlockSize.Value > MaxBlockSize))
            return $"Max compressed block size must be between {MinBlockSize:N0} and {MaxBlockSize:N0} bytes.";
        if (FileAlignment < 1 || FileAlignment > MaxBlockSize || (FileAlignment & (FileAlignment - 1)) != 0)
            return "File alignment must be a power of two (1 for no alignment).";
        if (BlockAlignment < 1 || BlockAlignment > MaxBlockSize || (BlockAlignment & (BlockAlignment - 1)) != 0)
            return "Block alignment must be a power of two (1 for no alignment).";
        if (double.IsNaN(MinCompressionRatio) || MinCompressionRatio < 1.0)
            return "Minimum compression ratio must be 1.0 or more.";
        return null;
    }
}

/// <summary>
/// Layout statistics of an archive, computed from its header and metadata. They describe the
/// trade-off a block layout makes between file size and the cost of reading the content.
/// </summary>
public class ArchiveLayoutStats
{
    public long FileSize { get; set; }
    public string Compression { get; set; }
    public int BlockCount { get; set; }

    /// <summary>Blocks stored without compression (in an archive that otherwise uses compression).</summary>
    public int UncompressedBlockCount { get; set; }
    public long LargestBlockSize { get; set; }
    public long DataSize { get; set; }
    public long UncompressedDataSize { get; set; }

    /// <summary>
    /// Uncompressed size of the blocks that must be read (and decompressed) to load every file once,
    /// on its own, divided by the total size of the files. 1.0 means no block is shared between files;
    /// a single LZMA block holding several files is far above 1.
    /// </summary>
    public double ReadAmplification { get; set; }
}

public class RepackResult
{
    public ArchiveLayoutStats Original { get; set; }
    public ArchiveLayoutStats Repacked { get; set; }
    public int FileCount { get; set; }
    public bool Verified { get; set; }
}

/// <summary>
/// Rewrites an existing Unity Archive (for example an AssetBundle) into a new archive with the same
/// files, compressed with a configurable LZ4/LZ4HC block layout.
///
/// Unity writes chunk-based archives with fixed 128 KB blocks that span file boundaries. The format
/// itself records the size and compression of every block, so other layouts load as well; this lets
/// a team measure what a different layout does to download size, patch size and random-access cost
/// on their target platform. The file content is read through UnityFileSystemApi, so any archive it
/// can mount (including LZMA ones) can be repacked.
/// </summary>
public static class ArchiveRepacker
{
    const string SourceMountPoint = "repack:/";

    public static RepackResult Repack(string inputPath, string outputPath, RepackOptions options, bool verify = true)
    {
        var error = options.Validate();
        if (error != null)
            throw new ArgumentException(error);

        if (string.Equals(Path.GetFullPath(inputPath), Path.GetFullPath(outputPath), StringComparison.Ordinal))
            throw new ArgumentException("The output path must be different from the input archive.");

        var header = ReadHeader(inputPath);
        var metadata = ReadMetadata(inputPath, header);

        const uint knownFlags = ArchiveFlags.BlocksAndDirectoryInfoCombined | ArchiveFlags.BlocksInfoAtTheEnd
            | ArchiveFlags.OldWebPluginCompatibility | ArchiveFlags.BlockInfoNeedPaddingAtStart;
        if ((header.ArchiveFlagBits & ~knownFlags) != 0)
            throw new NotSupportedException($"The archive uses unknown header flags (0x{header.ArchiveFlagBits & ~knownFlags:X}), so it cannot be repacked safely.");

        var sourceNodes = metadata.DirectoryInfo.Nodes;
        var outputNodes = LayOutNodes(sourceNodes, options.FileAlignment);

        var writeBlockOffsets = header.HasStorageBlockOffsets;
        if (options.BlockAlignment > 1 && !writeBlockOffsets)
            throw new NotSupportedException($"Block alignment needs archive format version 9 or later (Unity 6.7), where blocks record their own offsets. This archive is version {header.Version}.");
        var tempDataPath = outputPath + ".data.tmp";
        List<ArchiveStorageBlock> blocks;

        try
        {
            using (var dataStream = new FileStream(tempDataPath, FileMode.Create, FileAccess.Write))
            {
                blocks = new List<ArchiveStorageBlock>();
                if (sourceNodes.Length > 0)
                {
                    using var archive = UnityFileSystem.MountArchive(Path.GetFullPath(inputPath), SourceMountPoint);
                    var mountedNodes = archive.Nodes;
                    if (mountedNodes.Count != sourceNodes.Length)
                        throw new InvalidDataException("The archive directory and the mounted archive disagree on the number of files.");

                    using var source = new ArchiveContentReader(mountedNodes, outputNodes);
                    blocks = WriteBlocks(source, outputNodes, options, dataStream);
                }
            }

            WriteArchive(outputPath, header, blocks, outputNodes, writeBlockOffsets, tempDataPath);
        }
        finally
        {
            if (File.Exists(tempDataPath))
                File.Delete(tempDataPath);
        }

        var result = new RepackResult
        {
            Original = ComputeStats(inputPath),
            Repacked = ComputeStats(outputPath),
            FileCount = sourceNodes.Length,
        };

        if (verify)
        {
            try
            {
                VerifySameContent(inputPath, outputPath);
            }
            catch
            {
                // Do not leave an archive behind that does not match its source.
                File.Delete(outputPath);
                throw;
            }
            result.Verified = true;
        }

        return result;
    }

    /// <summary>
    /// Reads both archives through UnityFileSystemApi and checks that they hold the same files, with
    /// the same flags and byte-for-byte identical content. Throws InvalidDataException on a mismatch.
    /// </summary>
    public static void VerifySameContent(string originalPath, string repackedPath)
    {
        // The archives are mounted one after the other rather than side by side. With both mounted
        // (under different mount points, holding files with the same names), reading the repacked
        // archive's files returned the original's content, so a tampered archive passed the check.
        var original = HashArchiveContent(originalPath);
        var repacked = HashArchiveContent(repackedPath);

        if (original.Count != repacked.Count)
            throw new InvalidDataException($"The repacked archive has {repacked.Count} files, the original has {original.Count}.");

        for (int i = 0; i < original.Count; i++)
        {
            var a = original[i];
            var b = repacked[i];
            if (a.Node.Path != b.Node.Path || a.Node.Size != b.Node.Size || a.Node.Flags != b.Node.Flags)
                throw new InvalidDataException($"File {i} differs: \"{a.Node.Path}\" ({a.Node.Size} bytes, {a.Node.Flags}) vs \"{b.Node.Path}\" ({b.Node.Size} bytes, {b.Node.Flags}).");
            if (!a.Hash.AsSpan().SequenceEqual(b.Hash))
                throw new InvalidDataException($"The content of \"{a.Node.Path}\" differs from the original.");
        }
    }

    static List<(ArchiveNode Node, byte[] Hash)> HashArchiveContent(string path)
    {
        var result = new List<(ArchiveNode, byte[])>();

        // UnityArchive.Nodes cannot represent an archive without files.
        if (ReadMetadata(path, ReadHeader(path)).DirectoryInfo.Nodes.Length == 0)
            return result;

        using var archive = UnityFileSystem.MountArchive(Path.GetFullPath(path), SourceMountPoint);
        const int chunkSize = 256 * 1024;
        var buffer = new byte[chunkSize];

        foreach (var node in archive.Nodes)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using var file = UnityFileSystem.OpenFile(SourceMountPoint + node.Path);
            long remaining = node.Size;
            while (remaining > 0)
            {
                long size = Math.Min(chunkSize, remaining);
                long read = file.Read(size, buffer);
                if (read != size)
                    throw new InvalidDataException($"Could not read \"{node.Path}\" from {path}.");
                hash.AppendData(buffer, 0, (int)size);
                remaining -= size;
            }
            result.Add((node, hash.GetHashAndReset()));
        }

        return result;
    }

    public static ArchiveLayoutStats ComputeStats(string path)
    {
        var header = ReadHeader(path);
        var metadata = ReadMetadata(path, header);
        var blocks = metadata.BlocksInfo.Blocks;
        var nodes = metadata.DirectoryInfo.Nodes;

        var stats = new ArchiveLayoutStats
        {
            FileSize = (long)header.Size,
            BlockCount = blocks.Length,
            Compression = "None",
        };

        bool anyCompressed = false;
        foreach (var block in blocks)
        {
            stats.DataSize += block.CompressedSize;
            stats.UncompressedDataSize += block.UncompressedSize;
            stats.LargestBlockSize = Math.Max(stats.LargestBlockSize, block.UncompressedSize);
            if (block.CompressionType != 0 && !anyCompressed)
            {
                anyCompressed = true;
                stats.Compression = FormatCompression(block.CompressionType);
            }
        }

        if (anyCompressed)
        {
            foreach (var block in blocks)
            {
                if (block.CompressionType == 0)
                    stats.UncompressedBlockCount++;
            }
        }

        // For every file, add up the blocks that overlap it. Blocks are sorted by DataOffset, so a
        // binary search finds the first one.
        long fileBytes = 0;
        long decompressedBytes = 0;
        foreach (var node in nodes)
        {
            if (node.Size == 0)
                continue;
            long start = (long)node.DataOffset;
            long end = start + (long)node.Size;
            fileBytes += (long)node.Size;

            int i = FindBlock(blocks, start);
            for (; i < blocks.Length && blocks[i].DataOffset < end; i++)
                decompressedBytes += blocks[i].UncompressedSize;
        }
        stats.ReadAmplification = fileBytes > 0 ? (double)decompressedBytes / fileBytes : 0;

        return stats;
    }

    static int FindBlock(ArchiveStorageBlock[] blocks, long dataOffset)
    {
        int lo = 0, hi = blocks.Length - 1, found = 0;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (blocks[mid].DataOffset <= dataOffset)
            {
                found = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return found;
    }

    static ArchiveHeaderInfo ReadHeader(string path)
    {
        if (!ArchiveDetector.TryReadArchiveHeader(path, out var header, out var error))
            throw new NotSupportedException(error);
        return header;
    }

    static ArchiveMetadata ReadMetadata(string path, ArchiveHeaderInfo header)
    {
        if (!ArchiveDetector.TryReadArchiveMetadata(path, header, out var metadata, out var error))
            throw new NotSupportedException(error);
        return metadata;
    }

    static ArchiveDirectoryNode[] LayOutNodes(ArchiveDirectoryNode[] sourceNodes, int alignment)
    {
        var nodes = new ArchiveDirectoryNode[sourceNodes.Length];
        ulong offset = 0;
        for (int i = 0; i < sourceNodes.Length; i++)
        {
            offset = (offset + (ulong)alignment - 1) & ~((ulong)alignment - 1);
            nodes[i] = new ArchiveDirectoryNode
            {
                DataOffset = offset,
                Size = sourceNodes[i].Size,
                Flags = sourceNodes[i].Flags,
                Path = sourceNodes[i].Path,
            };
            offset += sourceNodes[i].Size;
        }
        return nodes;
    }

    // Splits the uncompressed data into blocks, compresses them and writes the stored bytes to
    // dataStream back to back. Returns the block list for the metadata.
    static List<ArchiveStorageBlock> WriteBlocks(ArchiveContentReader source, ArchiveDirectoryNode[] nodes, RepackOptions options, Stream dataStream)
    {
        var blocks = new List<ArchiveStorageBlock>();
        var encoder = new BlockEncoder(options);
        var window = new byte[options.BlockSize];
        int windowCount = 0;
        long position = 0;

        foreach (var (_, segmentEnd) in GetSegments(nodes, options.SplitAtFileBoundaries))
        {
            while (position < segmentEnd)
            {
                int chunkLength = (int)Math.Min(options.BlockSize, segmentEnd - position);
                while (windowCount < chunkLength)
                {
                    int read = source.Read(window, windowCount, chunkLength - windowCount);
                    if (read == 0)
                        throw new InvalidDataException("The archive content ended earlier than its directory says.");
                    windowCount += read;
                }

                var encoded = encoder.EncodeLargestFittingPrefix(window, chunkLength);

                long blockStart = (dataStream.Position + options.BlockAlignment - 1) & ~((long)options.BlockAlignment - 1);
                ArchiveWriter.WritePadding(dataStream, blockStart - dataStream.Position);
                dataStream.Write(encoded.Data, 0, encoded.StoredSize);
                blocks.Add(new ArchiveStorageBlock
                {
                    UncompressedSize = (uint)encoded.Length,
                    CompressedSize = (uint)encoded.StoredSize,
                    Flags = (ushort)encoded.CompressionType,
                    Offset = blockStart,
                });

                // Keep whatever the block did not take for the next block.
                windowCount -= encoded.Length;
                Buffer.BlockCopy(window, encoded.Length, window, 0, windowCount);
                position += encoded.Length;
            }
        }

        return blocks;
    }

    // The ranges of the uncompressed data that blocks must not cross. With spanning there is only one;
    // with splitting each file starts a range, and alignment padding belongs to the file before it.
    static IEnumerable<(long start, long end)> GetSegments(ArchiveDirectoryNode[] nodes, bool splitAtFileBoundaries)
    {
        if (nodes.Length == 0)
            yield break;

        long totalSize = (long)(nodes[^1].DataOffset + nodes[^1].Size);
        if (!splitAtFileBoundaries)
        {
            yield return (0, totalSize);
            yield break;
        }

        for (int i = 0; i < nodes.Length; i++)
        {
            long start = (long)nodes[i].DataOffset;
            long end = i + 1 < nodes.Length ? (long)nodes[i + 1].DataOffset : totalSize;
            if (end > start)
                yield return (start, end);
        }
    }

    static void WriteArchive(string outputPath, ArchiveHeaderInfo sourceHeader, List<ArchiveStorageBlock> blocks,
        ArchiveDirectoryNode[] nodes, bool writeBlockOffsets, string dataPath)
    {
        var metadata = ArchiveWriter.SerializeMetadata(blocks, nodes, writeBlockOffsets);

        // Unity compresses the metadata with LZ4HC. Keep it uncompressed if that does not help.
        var compressedMetadata = new byte[LZ4Codec.MaximumOutputSize(metadata.Length)];
        int compressedLength = LZ4Codec.Encode(metadata, 0, metadata.Length, compressedMetadata, 0, compressedMetadata.Length, LZ4Level.L09_HC);
        uint metadataCompression = 3;
        byte[] storedMetadata;
        if (compressedLength > 0 && compressedLength < metadata.Length)
        {
            storedMetadata = compressedMetadata.AsSpan(0, compressedLength).ToArray();
        }
        else
        {
            storedMetadata = metadata;
            metadataCompression = 0;
        }

        // The metadata is always written directly after the header. Keep the padding flag of the
        // source, since it is what its Unity version writes.
        uint flags = ArchiveFlags.BlocksAndDirectoryInfoCombined
            | (sourceHeader.ArchiveFlagBits & ArchiveFlags.BlockInfoNeedPaddingAtStart)
            | metadataCompression;

        long dataOffset = ArchiveWriter.GetDataOffset(sourceHeader.Version, sourceHeader.UnityVersion, (uint)storedMetadata.Length, flags);
        long dataLength = new FileInfo(dataPath).Length;
        ulong fileSize = (ulong)(dataOffset + dataLength);

        var header = ArchiveWriter.SerializeHeader(sourceHeader.Version, sourceHeader.UnityVersion, fileSize,
            (uint)storedMetadata.Length, (uint)metadata.Length, flags);

        using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        output.Write(header);
        output.Write(storedMetadata);
        ArchiveWriter.WritePadding(output, dataOffset - output.Position);
        using (var data = new FileStream(dataPath, FileMode.Open, FileAccess.Read))
            data.CopyTo(output);
    }

    internal static string FormatCompression(int compressionType)
    {
        return compressionType switch
        {
            0 => "None",
            1 => "Lzma",
            2 => "Lz4",
            3 => "Lz4HC",
            _ => compressionType.ToString(),
        };
    }

    struct EncodedBlock
    {
        public byte[] Data;
        public int Length;
        public int StoredSize;
        public int CompressionType;
    }

    // Compresses one block and applies the compression-ratio fallback and the compressed size limit.
    class BlockEncoder
    {
        // When searching for a block length that fits MaxCompressedBlockSize, stop once the range is
        // this small. It bounds the number of extra compressions per block to about log2(BlockSize / 1 KB).
        const int SearchGranularity = 1024;

        readonly RepackOptions m_Options;
        readonly byte[] m_Compressed;

        public BlockEncoder(RepackOptions options)
        {
            m_Options = options;
            m_Compressed = new byte[LZ4Codec.MaximumOutputSize(options.BlockSize)];
        }

        public EncodedBlock EncodeLargestFittingPrefix(byte[] data, int length)
        {
            var full = Encode(data, length);
            if (!m_Options.MaxCompressedBlockSize.HasValue || full.StoredSize <= m_Options.MaxCompressedBlockSize.Value)
                return full;

            // A block stored uncompressed is never larger than its input, so a prefix as long as the
            // limit always fits. Binary search for a longer prefix that still fits.
            int limit = m_Options.MaxCompressedBlockSize.Value;
            int lo = Math.Min(length, limit);
            int hi = length;
            while (hi - lo > SearchGranularity)
            {
                int mid = lo + (hi - lo) / 2;
                if (Encode(data, mid).StoredSize <= limit)
                    lo = mid;
                else
                    hi = mid;
            }
            return Encode(data, lo);
        }

        EncodedBlock Encode(byte[] data, int length)
        {
            if (m_Options.Compression != RepackCompression.None)
            {
                var level = m_Options.Compression == RepackCompression.Lz4HC ? LZ4Level.L09_HC : LZ4Level.L00_FAST;
                int compressedLength = LZ4Codec.Encode(data, 0, length, m_Compressed, 0, m_Compressed.Length, level);
                if (compressedLength > 0 && compressedLength < length
                    && (double)length / compressedLength >= m_Options.MinCompressionRatio)
                {
                    return new EncodedBlock
                    {
                        Data = m_Compressed,
                        Length = length,
                        StoredSize = compressedLength,
                        CompressionType = m_Options.Compression == RepackCompression.Lz4HC ? 3 : 2,
                    };
                }
            }

            return new EncodedBlock { Data = data, Length = length, StoredSize = length, CompressionType = 0 };
        }
    }

    // Reads the uncompressed data of the new archive in order: the content of each file, with zero
    // padding wherever the new layout aligns the start of a file.
    class ArchiveContentReader : IDisposable
    {
        readonly IReadOnlyList<ArchiveNode> m_MountedNodes;
        readonly ArchiveDirectoryNode[] m_Layout;
        readonly byte[] m_ReadBuffer = new byte[256 * 1024];
        int m_NodeIndex = -1;
        UnityFile m_CurrentFile;
        long m_Position;
        long m_RemainingInFile;

        public ArchiveContentReader(IReadOnlyList<ArchiveNode> mountedNodes, ArchiveDirectoryNode[] layout)
        {
            m_MountedNodes = mountedNodes;
            m_Layout = layout;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (count > 0 && m_NodeIndex < m_Layout.Length)
            {
                if (m_NodeIndex < 0 || (m_RemainingInFile == 0 && m_Position >= (long)m_Layout[m_NodeIndex].DataOffset))
                {
                    OpenNextFile();
                    continue;
                }

                // Padding before the current file.
                long fileStart = (long)m_Layout[m_NodeIndex].DataOffset;
                if (m_Position < fileStart)
                {
                    int padding = (int)Math.Min(count, fileStart - m_Position);
                    Array.Clear(buffer, offset, padding);
                    offset += padding;
                    count -= padding;
                    total += padding;
                    m_Position += padding;
                    continue;
                }

                int toRead = (int)Math.Min(Math.Min(count, m_RemainingInFile), m_ReadBuffer.Length);
                long read = m_CurrentFile.Read(toRead, m_ReadBuffer);
                if (read <= 0)
                    throw new InvalidDataException($"Could not read \"{m_MountedNodes[m_NodeIndex].Path}\" from the archive.");
                Buffer.BlockCopy(m_ReadBuffer, 0, buffer, offset, (int)read);
                offset += (int)read;
                count -= (int)read;
                total += (int)read;
                m_Position += read;
                m_RemainingInFile -= read;
            }
            return total;
        }

        void OpenNextFile()
        {
            m_CurrentFile?.Dispose();
            m_CurrentFile = null;
            m_RemainingInFile = 0;

            if (++m_NodeIndex >= m_Layout.Length)
                return;

            var mounted = m_MountedNodes[m_NodeIndex];
            if ((ulong)mounted.Size != m_Layout[m_NodeIndex].Size)
                throw new InvalidDataException($"The size of \"{mounted.Path}\" differs between the archive directory and the mounted archive.");

            // Empty files take no space in the data, so there is nothing to open.
            if (mounted.Size > 0)
            {
                m_CurrentFile = UnityFileSystem.OpenFile(SourceMountPoint + mounted.Path);
                m_RemainingInFile = mounted.Size;
            }
        }

        public void Dispose()
        {
            m_CurrentFile?.Dispose();
        }
    }
}
