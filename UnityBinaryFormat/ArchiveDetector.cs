using System;
using System.IO;

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

            var signature = BinaryFileHelper.ReadNullTermString(reader);

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
}
