using System;
using System.IO;
using K4os.Compression.LZ4;

namespace UnityDataTools.BinaryFormat;

/// <summary>
/// Parsed header information from a Unity Archive file.
///
/// A Unity Archive consists of three sections:
/// - Header: A small uncompressed header with version info, sizes, and flags.
/// - Metadata: An index section containing the Block List (sizes and compression of each
///   data block) and the Directory (paths, sizes, and flags of files inside the archive).
///   This section may be compressed; the header's compression bits and size fields describe
///   its on-disk vs uncompressed size.
/// - Data: One or more blocks of file content. Each block has its own compression type
///   recorded in its per-block flags. The metadata section is required to interpret the data.
///   A single file can span multiple blocks, and a single block can contain data for multiple files.
///   The blocks account for every byte of the uncompressed data (there are no offsets stored - no
///   overlapping or gaps can be expressed).  However the files could have padding between them, and
///   when the BlockPaddingBetweenChunks flag is set the on-disk blocks are separated by alignment
///   padding that belongs to no block.
///
/// The metadata can appear directly after the header (default layout) or at the end of the
/// file after the data (indicated by the BlocksInfoAtTheEnd flag).
/// </summary>
public class ArchiveHeaderInfo
{
    public string Signature { get; set; }
    public uint Version { get; set; }

    /// <summary>
    /// Unused legacy field (formerly "UnityWebBundleVersion"). Always "5.x.x".
    /// </summary>
    public string Unused { get; set; }

    public string UnityVersion { get; set; }
    public ulong Size { get; set; }
    public uint CompressedMetadataSize { get; set; }
    public uint UncompressedMetadataSize { get; set; }
    public uint Flags { get; set; }

    /// <summary>
    /// Compression type used for the metadata section (bits 0-5 of Flags).
    /// </summary>
    public int MetadataCompressionType => (int)(Flags & 0x3F);

    /// <summary>
    /// Archive flag bits (bits 6+ of Flags), with compression bits masked out.
    /// </summary>
    public uint ArchiveFlagBits => Flags & ~0x3Fu;

    /// <summary>
    /// True when each chunk-based (non-streamed) block is followed by padding up to the next
    /// 16-byte boundary of the data section. Introduced with archive format version 9.
    /// </summary>
    public bool HasBlockPaddingBetweenChunks => (Flags & ArchiveFlags.BlockPaddingBetweenChunks) != 0;
}

/// <summary>
/// Archive header flag bits (bits 6 and up of the header Flags field). Bits 0-5 hold the
/// metadata CompressionType instead.
/// </summary>
public static class ArchiveFlags
{
    public const uint BlocksAndDirectoryInfoCombined = 0x40;
    public const uint BlocksInfoAtTheEnd = 0x80;
    public const uint OldWebPluginCompatibility = 0x100;
    public const uint BlockInfoNeedPaddingAtStart = 0x200;
    public const uint BlockPaddingBetweenChunks = 0x400;
}

public class ArchiveStorageBlock
{
    public uint UncompressedSize { get; set; }
    public uint CompressedSize { get; set; }
    public ushort Flags { get; set; }
    public int CompressionType => Flags & 0x3F;
    public bool IsStreamed => (Flags & 0x40) != 0;

    /// <summary>
    /// Offset of this block from the start of the archive file.
    /// Calculated after parsing — not stored in the serialized data.
    /// </summary>
    public long FileOffset { get; set; }

    /// <summary>
    /// Offset of this block's uncompressed data relative to the start of the
    /// full uncompressed data (all blocks concatenated).
    /// Calculated after parsing — not stored in the serialized data.
    /// </summary>
    public long DataOffset { get; set; }
}

public class ArchiveBlocksInfo
{
    public byte[] UncompressedDataHash { get; set; } // Unused

    // Archives with no compression or LZMA will have a single block,
    // except when the data exceeds 4GB (because the size fields in ArchiveStorageBlock are 32-bit).
    public ArchiveStorageBlock[] Blocks { get; set; }
}

public class ArchiveDirectoryNode
{
    /// <summary>
    /// Offset within the uncompressed data (all blocks concatenated).
    /// </summary>
    public ulong DataOffset { get; set; }
    public ulong Size { get; set; }
    public uint Flags { get; set; }

    /// <summary>
    /// Path of the file within the archive, using '/' as a separator.
    /// Although Flags has a Directory flag, in practice nodes are only created for files,
    /// and directories are implied by the paths.
    /// </summary>
    public string Path { get; set; }
}

public class ArchiveDirectoryInfo
{
    public ArchiveDirectoryNode[] Nodes { get; set; }
}

public class ArchiveMetadata
{
    public ArchiveBlocksInfo BlocksInfo { get; set; }
    public ArchiveDirectoryInfo DirectoryInfo { get; set; }
}

/// <summary>
/// Utility for detecting and parsing Unity Archive (AssetBundle) file headers.
/// </summary>
public static class ArchiveDetector
{
    private static readonly string[] Signatures = { "UnityFS", "UnityWeb", "UnityRaw", "UnityArchive" };
    private const int MaxSignatureLength = 12; // "UnityArchive".Length

    /// <summary>
    /// Checks if a file is a Unity Archive (AssetBundle) by reading its signature.
    /// Supports UnityFS, UnityWeb, UnityRaw, and UnityArchive formats.
    /// </summary>
    /// <param name="filePath">Path to the file to check</param>
    /// <returns>True if file appears to be a Unity Archive, false otherwise</returns>
    public static bool IsUnityArchive(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Read the first bytes to check for known signatures
            byte[] buffer = new byte[MaxSignatureLength];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);

            if (bytesRead < Signatures[0].Length) // "UnityFS" is the shortest at 7 bytes
                return false;

            // Check against all known archive signatures
            foreach (var signature in Signatures)
            {
                if (bytesRead >= signature.Length)
                {
                    bool match = true;
                    for (int i = 0; i < signature.Length; i++)
                    {
                        if (buffer[i] != signature[i])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                        return true;
                }
            }

            return false;
        }
        catch
        {
            // If we can't read the file, it's not a valid archive
            return false;
        }
    }

    /// <summary>
    /// Reads a null-terminated signature string, with a length limit to avoid reading
    /// deep into non-archive files that don't contain an early null byte.
    /// </summary>
    /// Note: this is used for a very similar purpose to IsUnityArchive(). But IsUnityArchive() is
    /// optimized to quickly check a file whereas this one is used when we are actually parsing
    /// the file.  The two could potentially be merged.
    static string ReadSignature(BinaryReader reader)
    {
        const int maxLength = 20; // Longest valid signature is "UnityArchive" (12 chars)
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < maxLength; i++)
        {
            byte b = reader.ReadByte(); // Throws EndOfStreamException on EOF
            if (b == 0)
                return sb.ToString();
            sb.Append((char)b);
        }
        // No null terminator found within the limit — not a valid archive signature.
        return sb.ToString();
    }

    /// <summary>
    /// Attempts to read and parse the header of a Unity Archive file.
    /// Only the "UnityFS" format is supported. Other archive signatures will produce
    /// an error message identifying the unsupported signature.
    /// </summary>
    public static bool TryReadArchiveHeader(string filePath, out ArchiveHeaderInfo info, out string errorMessage)
    {
        info = null;
        errorMessage = null;

        if (!File.Exists(filePath))
        {
            errorMessage = $"File not found: \"{filePath}\".";
            return false;
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(stream);

            string signature;
            try
            {
                signature = ReadSignature(reader);
            }
            catch (EndOfStreamException)
            {
                errorMessage = "File is not a Unity Archive.";
                return false;
            }

            if (signature != "UnityFS")
            {
                // Check if it's a recognized but unsupported legacy signature.
                if (signature == "UnityWeb" || signature == "UnityRaw" || signature == "UnityArchive")
                    errorMessage = $"Unsupported archive signature: \"{signature}\". Only \"UnityFS\" is supported.";
                else
                    errorMessage = "File is not a Unity Archive.";
                return false;
            }

            // All numeric fields are big-endian (swap = true).
            var version = BinaryFileHelper.ReadUInt32(reader, true);
            var unused = BinaryFileHelper.ReadNullTermString(reader);
            var unityVersion = BinaryFileHelper.ReadNullTermString(reader);
            var size = BinaryFileHelper.ReadUInt64(reader, true);
            var compressedMetadataSize = BinaryFileHelper.ReadUInt32(reader, true);
            var uncompressedMetadataSize = BinaryFileHelper.ReadUInt32(reader, true);
            var flags = BinaryFileHelper.ReadUInt32(reader, true);

            if (compressedMetadataSize > uncompressedMetadataSize)
                throw new InvalidDataException("Compressed metadata size exceeds uncompressed size. The file may be corrupt.");

            if (size == 0)
                throw new InvalidDataException("Archive size is zero. The file may be corrupt.");

            info = new ArchiveHeaderInfo
            {
                Signature = signature,
                Version = version,
                Unused = unused,
                UnityVersion = unityVersion,
                Size = size,
                CompressedMetadataSize = compressedMetadataSize,
                UncompressedMetadataSize = uncompressedMetadataSize,
                Flags = flags,
            };

            return true;
        }
        catch (Exception ex) when (ex is EndOfStreamException || ex is InvalidDataException)
        {
            errorMessage = $"Error reading archive header: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Reads and parses the metadata section (BlocksInfo and DirectoryInfo) from a Unity Archive.
    /// The header must have been successfully read first via TryReadArchiveHeader.
    /// Only the combined BlocksInfo+DirectoryInfo layout is supported.
    /// </summary>
    public static bool TryReadArchiveMetadata(string filePath, ArchiveHeaderInfo header, out ArchiveMetadata metadata, out string errorMessage)
    {
        metadata = null;
        errorMessage = null;

        if ((header.ArchiveFlagBits & ArchiveFlags.BlocksAndDirectoryInfoCombined) == 0)
        {
            errorMessage = "This archive does not use the combined BlocksInfo+DirectoryInfo layout. Only the combined layout is supported.";
            return false;
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Calculate where the metadata section starts.
            long metadataOffset;
            if ((header.ArchiveFlagBits & ArchiveFlags.BlocksInfoAtTheEnd) != 0)
                metadataOffset = (long)(header.Size - header.CompressedMetadataSize);
            else
                metadataOffset = GetHeaderSize(header);

            stream.Seek(metadataOffset, SeekOrigin.Begin);

            // Read the metadata bytes (which may be compressed)
            var compressedData = new byte[header.CompressedMetadataSize];
            int bytesRead = stream.Read(compressedData, 0, compressedData.Length);
            if (bytesRead != compressedData.Length)
                throw new InvalidDataException("Could not read the full metadata section from the file.");

            // Decompress if needed.
            byte[] uncompressedData;
            if (header.MetadataCompressionType == 0)
            {
                uncompressedData = compressedData;
            }
            else if (header.MetadataCompressionType == 2 || header.MetadataCompressionType == 3)
            {
                // LZ4 and LZ4HC use the same decompression algorithm.
                uncompressedData = new byte[header.UncompressedMetadataSize];
                int decoded = LZ4Codec.Decode(compressedData, 0, compressedData.Length,
                    uncompressedData, 0, uncompressedData.Length);
                if (decoded != header.UncompressedMetadataSize)
                    throw new InvalidDataException($"LZ4 decompression produced {decoded} bytes, expected {header.UncompressedMetadataSize}.");
            }
            else if (header.MetadataCompressionType == 1)
            {
                errorMessage = "LZMA compression for archive metadata is not supported.";
                return false;
            }
            else
            {
                errorMessage = $"Unknown metadata compression type: {header.MetadataCompressionType}.";
                return false;
            }

            // Parse BlocksInfo and DirectoryInfo from the uncompressed buffer.
            using var memStream = new MemoryStream(uncompressedData);
            using var reader = new BinaryReader(memStream);

            var blocksInfo = ParseBlocksInfo(reader);
            var directoryInfo = ParseDirectoryInfo(reader);

            // Populate calculated offsets on each block. Block positions are not stored, so they are
            // accumulated from the block sizes. When BlockPaddingBetweenChunks is set, each chunk-based
            // block is followed by padding that belongs to no block, so the running offset is aligned
            // after each such block. The alignment is relative to the start of the data section, which
            // is itself 16-byte aligned in every version that can set this flag, so the blocks are also
            // aligned within the file - that is what keeps chunk positions stable for binary patching.
            long dataSectionStart = GetDataOffset(header);
            long blockOffset = 0;
            long dataOffset = 0;
            foreach (var block in blocksInfo.Blocks)
            {
                block.FileOffset = dataSectionStart + blockOffset;
                block.DataOffset = dataOffset;
                blockOffset += block.CompressedSize;
                if (header.HasBlockPaddingBetweenChunks && !block.IsStreamed)
                    blockOffset = AlignTo16(blockOffset);
                dataOffset += block.UncompressedSize;
            }

            ValidateMetadata(blocksInfo, directoryInfo);

            metadata = new ArchiveMetadata
            {
                BlocksInfo = blocksInfo,
                DirectoryInfo = directoryInfo,
            };

            return true;
        }
        catch (Exception ex) when (ex is EndOfStreamException || ex is InvalidDataException)
        {
            errorMessage = $"Error reading archive metadata: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Calculates the data section offset from the start of the archive file.
    /// This is the byte position where the first data block begins.
    /// </summary>
    public static long GetDataOffset(ArchiveHeaderInfo header)
    {
        long offset = GetHeaderSize(header);

        if ((header.ArchiveFlagBits & ArchiveFlags.BlocksInfoAtTheEnd) == 0)
        {
            if ((header.ArchiveFlagBits & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
                offset += AlignTo16(header.CompressedMetadataSize);
            else
                offset += header.CompressedMetadataSize;
        }

        return offset;
    }

    /// <summary>
    /// Validates consistency between BlocksInfo and DirectoryInfo.
    ///
    /// Directory nodes represent files laid out sequentially in the uncompressed data
    /// (all blocks concatenated). Nodes must be in non-decreasing offset order and must
    /// not overlap, though padding between them is permitted. Every file byte must be
    /// covered by block data — the total uncompressed block size must reach at least
    /// the end of the last file.
    /// </summary>
    static void ValidateMetadata(ArchiveBlocksInfo blocksInfo, ArchiveDirectoryInfo directoryInfo)
    {
        var nodes = directoryInfo.Nodes;
        var blocks = blocksInfo.Blocks;

        if (nodes.Length == 0 || blocks.Length == 0)
            return;

        // Verify directory nodes are in order and non-overlapping.
        for (int i = 1; i < nodes.Length; i++)
        {
            ulong prevEnd = nodes[i - 1].DataOffset + nodes[i - 1].Size;
            if (nodes[i].DataOffset < prevEnd)
                throw new InvalidDataException(
                    $"Directory node \"{nodes[i].Path}\" at data offset {nodes[i].DataOffset} overlaps with " +
                    $"previous node \"{nodes[i - 1].Path}\" which ends at {prevEnd}. The file may be corrupt.");
        }

        // Verify that the blocks cover all file data. The last block's end must reach
        // at least the end of the last file. (It may exceed it due to padding.)
        var lastBlock = blocks[blocks.Length - 1];
        long blocksEnd = lastBlock.DataOffset + lastBlock.UncompressedSize;

        var lastNode = nodes[nodes.Length - 1];
        ulong filesEnd = lastNode.DataOffset + lastNode.Size;

        if ((ulong)blocksEnd < filesEnd)
            throw new InvalidDataException(
                $"Block data ends at offset {blocksEnd} but directory entries extend to {filesEnd}. " +
                $"The file may be corrupt.");
    }

    static int GetHeaderSize(ArchiveHeaderInfo header)
    {
        int size;
        if ((header.ArchiveFlagBits & ArchiveFlags.OldWebPluginCompatibility) != 0)
            size = 10; // Legacy web plugin signature portion
        else
            size = header.Signature.Length + 1;

        size += 4; // version
        size += header.Unused.Length + 1;
        size += header.UnityVersion.Length + 1;
        size += 8; // size (UInt64)
        size += 4; // compressedMetadataSize
        size += 4; // uncompressedMetadataSize
        size += 4; // flags

        if (header.Version >= 7)
            size = (int)AlignTo16((uint)size);

        return size;
    }

    static long AlignTo16(long value)
    {
        return (value + 15) & ~15L;
    }

    static ArchiveBlocksInfo ParseBlocksInfo(BinaryReader reader)
    {
        var hash = reader.ReadBytes(16);
        var blockCount = BinaryFileHelper.ReadUInt32(reader, true);

        var blocks = new ArchiveStorageBlock[blockCount];
        for (int i = 0; i < blockCount; i++)
        {
            blocks[i] = new ArchiveStorageBlock
            {
                UncompressedSize = BinaryFileHelper.ReadUInt32(reader, true),
                CompressedSize = BinaryFileHelper.ReadUInt32(reader, true),
                Flags = BinaryFileHelper.ReadUInt16(reader, true),
            };
        }

        return new ArchiveBlocksInfo
        {
            UncompressedDataHash = hash,
            Blocks = blocks,
        };
    }

    static ArchiveDirectoryInfo ParseDirectoryInfo(BinaryReader reader)
    {
        var nodeCount = BinaryFileHelper.ReadUInt32(reader, true);

        var nodes = new ArchiveDirectoryNode[nodeCount];
        for (int i = 0; i < nodeCount; i++)
        {
            nodes[i] = new ArchiveDirectoryNode
            {
                DataOffset = BinaryFileHelper.ReadUInt64(reader, true),
                Size = BinaryFileHelper.ReadUInt64(reader, true),
                Flags = BinaryFileHelper.ReadUInt32(reader, true),
                Path = BinaryFileHelper.ReadNullTermString(reader),
            };
        }

        return new ArchiveDirectoryInfo
        {
            Nodes = nodes,
        };
    }
}
