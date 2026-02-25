using System;
using System.IO;

namespace UnityDataTools.Analyzer.Util;

/// <summary>
/// Information extracted from a Unity SerializedFile header.
/// </summary>
public class SerializedFileInfo
{
    public uint Version { get; set; }
    public ulong FileSize { get; set; }
    public ulong MetadataSize { get; set; }
    public ulong DataOffset { get; set; }
    public byte Endianness { get; set; }
    public bool IsLegacyFormat { get; set; }
}

/// <summary>
/// Utility for detecting Unity SerializedFile format by reading and validating the file header.
///
/// Unity SerializedFiles have evolved through several format versions:
///
/// Version < 9:
///   - 20-byte header (SerializedFileHeader32) with 32-bit offsets/sizes
///   - Layout: [header][data][metadata]
///   - Endianness byte stored at END of file, just before metadata
///
/// Version 9-21:
///   - 20-byte header (SerializedFileHeader32) with 32-bit offsets/sizes
///   - Layout: [header][metadata][data]
///   - Endianness byte at offset 16 in header
///   - Limited to 4GB file sizes
///
/// Version >= 22 (kLargeFilesSupport):
///   - 48-byte header (SerializedFileHeader) with 64-bit offsets/sizes
///   - Layout: [header][metadata][data]
///   - Endianness byte at offset 40 in header
///   - Supports files larger than 4GB
///
/// Important: The header itself is always stored in big-endian format on disk,
/// but the m_Endianness byte indicates the endianness of the actual data section.
///
/// DEPRECATION WARNING: The deprecation process for Version <18 (Unity 5.5 and earlier) has started in Unity 6.5.
/// Initially this will be a warning, but upcoming versions of UnityDataTool and UnityFileSystem can be expected
/// to lose the ability to open and read those files (apart from low level information exposed by the
/// "serialized-file header" command).
/// </summary>
public static class SerializedFileDetector
{
    // Version boundaries for format changes
    // NOTE: This version is so old that it is extremely unlikely it will work with modern versions of Unity,
    // we handle it just for the purpose of trying to report accurate information about the file.
    private const uint NewLayoutVersion = 9;           // Changed from [header][data][metadata] to [header][metadata][data]

    private const uint LargeFilesSupportVersion = 22;  // Changed to 64-bit header

    // Reasonable version range for SerializedFiles
    // Unity versions currently use values in the 20s-30s range
    private const uint MinVersion = 1;
    private const uint MaxVersion = 50;

    // Endianness values (only little-endian is supported in Unity 2023+)
    private const byte LittleEndian = 0;
    private const byte BigEndian = 1;

    // Header sizes
    private const int LegacyHeaderSize = 20;  // SerializedFileHeader32
    private const int ModernHeaderSize = 48;  // SerializedFileHeader

    /// <summary>
    /// Attempts to detect if a file is a Unity SerializedFile by reading and validating its header.
    /// Returns false immediately if the file doesn't match the expected format.
    /// </summary>
    /// <param name="filePath">Path to the file to check</param>
    /// <param name="info">If successful, contains header information</param>
    /// <returns>True if file appears to be a valid SerializedFile, false otherwise</returns>
    public static bool TryDetectSerializedFile(string filePath, out SerializedFileInfo info)
    {
        info = null;

        if (!File.Exists(filePath))
            return false;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            long fileLength = stream.Length;

            // Quick rejection: file must be at least large enough for the legacy header
            if (fileLength < LegacyHeaderSize)
                return false;

            // Read enough bytes to cover a modern header (48 bytes)
            // We'll determine which format to parse based on the version field
            byte[] headerBytes = new byte[ModernHeaderSize];
            int bytesRead = stream.Read(headerBytes, 0, headerBytes.Length);

            if (bytesRead < LegacyHeaderSize)
                return false;

            // ============================================================
            // STEP 1: Read version to determine header format
            // ============================================================

            // The version field is always at offset 8 in both header formats.
            // The header itself is always stored in big-endian format on disk.
            // On little-endian platforms (Windows, etc.), we need to swap the header fields.
            //
            // We try both interpretations to determine if swapping is needed:
            uint versionLE = BitConverter.ToUInt32(headerBytes, 8);
            uint versionBE = SwapUInt32(versionLE);

            // Determine which interpretation gives us a valid version number
            uint version;
            bool needsSwap;  // Whether header fields need byte swapping (expected to be true when running on most modern systems, which are little-endian)

            if (versionLE >= MinVersion && versionLE <= MaxVersion)
            {
                // Reading as little-endian gives valid version (header is in little-endian format)
                version = versionLE;
                needsSwap = false;
            }
            else if (versionBE >= MinVersion && versionBE <= MaxVersion)
            {
                // Reading as big-endian gives valid version (header is in big-endian format)
                version = versionBE;
                needsSwap = true;
            }
            else
            {
                // Neither interpretation gives a valid version
                return false;
            }

            // Determine header format based on version
            bool isLegacyFormat = version < LargeFilesSupportVersion;

            // ============================================================
            // STEP 2: Read endianness byte
            // ============================================================
            //
            // The m_Endianness byte indicates the endianness of the DATA section
            // (not the header, which is always big-endian on disk).
            // Location depends on version:
            // - Version < 9:   At end of file (before metadata) - we skip reading it for detection
            // - Version 9-21:  At offset 16 in the 20-byte header
            // - Version >= 22: At offset 40 in the 48-byte header
            //
            // The endianness byte is never swapped (it's a single byte).

            byte endianness;

            if (version < NewLayoutVersion)
            {
                // Version < 9: Endianness is at the end of the file
                // For detection purposes, we infer it from the header byte order
                // (though this is technically the header's endianness, not the data's)
                endianness = needsSwap ? BigEndian : LittleEndian;
            }
            else if (isLegacyFormat)
            {
                // Version 9-21: Endianness is at offset 16 in SerializedFileHeader32
                if (bytesRead >= 17)
                {
                    endianness = headerBytes[16];

                    // Validate endianness value
                    if (endianness != LittleEndian && endianness != BigEndian)
                        return false;
                }
                else
                {
                    return false; // File truncated
                }
            }
            else
            {
                // Version >= 22: Endianness is at offset 40 in SerializedFileHeader
                if (bytesRead >= 41)
                {
                    endianness = headerBytes[40];

                    // Validate endianness value
                    if (endianness != LittleEndian && endianness != BigEndian)
                        return false;
                }
                else
                {
                    return false; // File truncated
                }
            }

            // ============================================================
            // STEP 3: Parse the appropriate header format
            // ============================================================

            ulong metadataSize, fileSize, dataOffset;

            if (isLegacyFormat)
            {
                // SerializedFileHeader32 Layout (20 bytes total):
                // Offset 0-3:   UInt32 m_MetadataSize
                // Offset 4-7:   UInt32 m_FileSize
                // Offset 8-11:  UInt32 m_Version
                // Offset 12-15: UInt32 m_DataOffset
                // Offset 16:    UInt8  m_Endianness (only present for version >= 9)
                // Offset 17-19: UInt8  m_Reserved[3]
                //
                // Note: For version < 9, m_Endianness is NOT in the header.
                //       It's stored at the end of the file, just before metadata begins.

                uint metadataSize32 = ReadUInt32(headerBytes, 0, needsSwap);
                uint fileSize32 = ReadUInt32(headerBytes, 4, needsSwap);
                uint dataOffset32 = ReadUInt32(headerBytes, 12, needsSwap);

                // Convert to 64-bit for consistency
                metadataSize = metadataSize32;
                fileSize = fileSize32;
                dataOffset = dataOffset32;

                // Special case: Legacy format used UInt32.MaxValue to indicate "unknown" file size
                if (fileSize32 == uint.MaxValue)
                {
                    fileSize = ulong.MaxValue;
                }
            }
            else
            {
                // SerializedFileHeader Layout (48 bytes total):
                // Offset 0-7:   UInt8[8] m_Legacy (unused, allows struct alignment with SerializedFileHeader32)
                // Offset 8-11:  UInt32   m_Version
                // Offset 12-15: UInt8[4] m_Reserved0 (explicit padding)
                // Offset 16-23: UInt64   m_MetadataSize
                // Offset 24-31: UInt64   m_FileSize
                // Offset 32-39: UInt64   m_DataOffset
                // Offset 40:    UInt8    m_Endianness
                // Offset 41-47: UInt8[7] m_Reserved1

                metadataSize = ReadUInt64(headerBytes, 16, needsSwap);
                fileSize = ReadUInt64(headerBytes, 24, needsSwap);
                dataOffset = ReadUInt64(headerBytes, 32, needsSwap);
            }

            // ============================================================
            // STEP 4: Validate header consistency
            // ============================================================

            // MetadataSize must not be the sentinel value (indicates corruption)
            if (metadataSize == ulong.MaxValue)
                return false;

            // DataOffset must be within the file size
            if (fileSize != ulong.MaxValue && dataOffset > fileSize)
                return false;

            // FileSize should roughly match actual file size
            // Allow some tolerance for "stream files" which can have padding
            if (fileSize != ulong.MaxValue)
            {
                // File size should not exceed actual file size by more than 1KB (arbitrary tolerance)
                if (fileSize > (ulong)fileLength + 1024)
                    return false;
            }

            // MetadataSize should be reasonable (not larger than the file itself)
            if (metadataSize > (ulong)fileLength)
                return false;

            // ============================================================
            // STEP 5: Populate and return info
            // ============================================================

            info = new SerializedFileInfo
            {
                Version = version,
                FileSize = fileSize,
                MetadataSize = metadataSize,
                DataOffset = dataOffset,
                Endianness = endianness,
                IsLegacyFormat = isLegacyFormat
            };

            return true;
        }
        catch
        {
            // Any exception during reading/parsing means this isn't a valid SerializedFile
            return false;
        }
    }

    /// <summary>
    /// Reads a UInt32 from a byte array at the specified offset, optionally swapping endianness.
    /// </summary>
    private static uint ReadUInt32(byte[] buffer, int offset, bool swap)
    {
        uint value = BitConverter.ToUInt32(buffer, offset);
        return swap ? SwapUInt32(value) : value;
    }

    /// <summary>
    /// Reads a UInt64 from a byte array at the specified offset, optionally swapping endianness.
    /// </summary>
    private static ulong ReadUInt64(byte[] buffer, int offset, bool swap)
    {
        ulong value = BitConverter.ToUInt64(buffer, offset);
        return swap ? SwapUInt64(value) : value;
    }

    private static uint SwapUInt32(uint value)
    {
        return ((value & 0x000000FFU) << 24) |
               ((value & 0x0000FF00U) << 8) |
               ((value & 0x00FF0000U) >> 8) |
               ((value & 0xFF000000U) >> 24);
    }

    private static ulong SwapUInt64(ulong value)
    {
        return ((value & 0x00000000000000FFUL) << 56) |
               ((value & 0x000000000000FF00UL) << 40) |
               ((value & 0x0000000000FF0000UL) << 24) |
               ((value & 0x00000000FF000000UL) << 8) |
               ((value & 0x000000FF00000000UL) >> 8) |
               ((value & 0x0000FF0000000000UL) >> 24) |
               ((value & 0x00FF000000000000UL) >> 40) |
               ((value & 0xFF00000000000000UL) >> 56);
    }
}
