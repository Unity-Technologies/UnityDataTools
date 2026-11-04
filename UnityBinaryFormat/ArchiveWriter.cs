using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnityDataTools.BinaryFormat;

/// <summary>
/// Serializes the header and metadata sections of a "UnityFS" Unity Archive.
///
/// This is the inverse of ArchiveDetector: every field is written in the order and byte order
/// (big-endian) that ArchiveDetector reads, so an archive written here can be inspected with the
/// archive header / blocks / list commands. Only the combined BlocksInfo+DirectoryInfo layout with
/// the metadata directly after the header is written, which is what Unity itself writes for
/// AssetBundles.
/// </summary>
public static class ArchiveWriter
{
    const string Signature = "UnityFS";

    // Unity writes this fixed string in place of the old "UnityWebBundleVersion".
    const string UnusedVersionString = "5.x.x";

    /// <summary>
    /// Serializes the BlocksInfo followed by the DirectoryInfo. Block offsets are only part of the
    /// format from version 9, so writeBlockOffsets must match the version in the header.
    /// </summary>
    public static byte[] SerializeMetadata(IReadOnlyList<ArchiveStorageBlock> blocks, IReadOnlyList<ArchiveDirectoryNode> nodes, bool writeBlockOffsets)
    {
        using var stream = new MemoryStream();

        // The uncompressed data hash is not used by the runtime; Unity writes zeros as well.
        stream.Write(new byte[16]);
        WriteUInt32(stream, (uint)blocks.Count);
        foreach (var block in blocks)
        {
            WriteUInt32(stream, block.UncompressedSize);
            WriteUInt32(stream, block.CompressedSize);
            WriteUInt16(stream, block.Flags);
            if (writeBlockOffsets)
                WriteUInt64(stream, (ulong)block.Offset);
        }

        WriteUInt32(stream, (uint)nodes.Count);
        foreach (var node in nodes)
        {
            WriteUInt64(stream, node.DataOffset);
            WriteUInt64(stream, node.Size);
            WriteUInt32(stream, node.Flags);
            WriteNullTermString(stream, node.Path);
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Serializes the archive header, including the trailing alignment that format version 7 and
    /// later expect before the metadata section.
    /// </summary>
    public static byte[] SerializeHeader(uint version, string unityVersion, ulong fileSize,
        uint compressedMetadataSize, uint uncompressedMetadataSize, uint flags)
    {
        using var stream = new MemoryStream();
        WriteNullTermString(stream, Signature);
        WriteUInt32(stream, version);
        WriteNullTermString(stream, UnusedVersionString);
        WriteNullTermString(stream, unityVersion);
        WriteUInt64(stream, fileSize);
        WriteUInt32(stream, compressedMetadataSize);
        WriteUInt32(stream, uncompressedMetadataSize);
        WriteUInt32(stream, flags);

        if (version >= 7)
            WritePadding(stream, AlignTo16(stream.Length) - stream.Length);

        return stream.ToArray();
    }

    /// <summary>
    /// Size of the header that SerializeHeader would produce. It does not depend on the size fields,
    /// which lets a writer compute the file layout before it knows the final file size.
    /// </summary>
    public static int GetHeaderSize(uint version, string unityVersion)
    {
        return SerializeHeader(version, unityVersion, 0, 0, 0, 0).Length;
    }

    /// <summary>
    /// Offset of the data section from the start of the file, for an archive with the metadata
    /// directly after the header. Matches ArchiveDetector.GetDataOffset.
    /// </summary>
    public static long GetDataOffset(uint version, string unityVersion, uint compressedMetadataSize, uint flags)
    {
        long offset = GetHeaderSize(version, unityVersion);
        if ((flags & ArchiveFlags.BlockInfoNeedPaddingAtStart) != 0)
            offset += AlignTo16(compressedMetadataSize);
        else
            offset += compressedMetadataSize;
        return offset;
    }

    public static long AlignTo16(long value)
    {
        return (value + 15) & ~15L;
    }

    public static void WritePadding(Stream stream, long count)
    {
        for (long i = 0; i < count; i++)
            stream.WriteByte(0);
    }

    static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        stream.Write(buffer);
    }

    static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    static void WriteUInt64(Stream stream, ulong value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
        stream.Write(buffer);
    }

    // BinaryFileHelper.ReadNullTermString maps each byte to one char, so Latin1 is its exact inverse
    // and paths keep their original bytes (including any UTF-8 sequences) through a read and write.
    static void WriteNullTermString(Stream stream, string value)
    {
        stream.Write(Encoding.Latin1.GetBytes(value));
        stream.WriteByte(0);
    }
}
