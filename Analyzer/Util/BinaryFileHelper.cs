using System;
using System.IO;
using System.Text;

namespace UnityDataTools.Analyzer.Util;

/// <summary>
/// A 128-bit hash stored as four 32-bit unsigned integers, matching Unity's Hash128 binary layout.
/// </summary>
public readonly struct UnityHash128
{
    public uint Data0 { get; init; }
    public uint Data1 { get; init; }
    public uint Data2 { get; init; }
    public uint Data3 { get; init; }

    public bool IsZero => Data0 == 0 && Data1 == 0 && Data2 == 0 && Data3 == 0;

    public override string ToString() => $"{Data0:x8}{Data1:x8}{Data2:x8}{Data3:x8}";
}

/// <summary>
/// Helpers for reading primitive types and Unity-specific data from binary streams and byte
/// arrays, with optional endianness swapping.
/// </summary>
public static class BinaryFileHelper
{
    // -----------------------------------------------------------------------
    // Stream / BinaryReader helpers
    // -----------------------------------------------------------------------

    /// <summary>Advances the stream to the next 4-byte boundary measured from <paramref name="baseOffset"/>.</summary>
    public static void AlignTo4(Stream stream, long baseOffset)
    {
        long rel = stream.Position - baseOffset;
        long aligned = (rel + 3) & ~3L;
        stream.Position = baseOffset + aligned;
    }

    /// <summary>Reads a null-terminated ASCII string from the stream.</summary>
    public static string ReadNullTermString(BinaryReader reader)
    {
        var sb = new StringBuilder();
        byte b;
        while ((b = reader.ReadByte()) != 0)
            sb.Append((char)b);
        return sb.ToString();
    }

    public static int ReadInt32(BinaryReader reader, bool swap)
    {
        uint raw = reader.ReadUInt32();
        return (int)(swap ? SwapUInt32(raw) : raw);
    }

    public static short ReadInt16(BinaryReader reader, bool swap)
    {
        ushort raw = reader.ReadUInt16();
        if (swap)
            raw = (ushort)((raw << 8) | (raw >> 8));
        return (short)raw;
    }

    public static uint ReadUInt32(BinaryReader reader, bool swap)
    {
        uint raw = reader.ReadUInt32();
        return swap ? SwapUInt32(raw) : raw;
    }

    public static ulong ReadUInt64(BinaryReader reader, bool swap)
    {
        ulong raw = reader.ReadUInt64();
        return swap ? SwapUInt64(raw) : raw;
    }

    public static long ReadInt64(BinaryReader reader, bool swap)
    {
        ulong raw = reader.ReadUInt64();
        return (long)(swap ? SwapUInt64(raw) : raw);
    }

    public static UnityHash128 ReadHash128(BinaryReader reader, bool swap)
    {
        return new UnityHash128
        {
            Data0 = ReadUInt32(reader, swap),
            Data1 = ReadUInt32(reader, swap),
            Data2 = ReadUInt32(reader, swap),
            Data3 = ReadUInt32(reader, swap),
        };
    }

    // -----------------------------------------------------------------------
    // Byte-array helpers
    // -----------------------------------------------------------------------

    /// <summary>Reads a UInt32 from a byte array at the specified offset, optionally swapping endianness.</summary>
    public static uint ReadUInt32(byte[] buffer, int offset, bool swap)
    {
        uint value = BitConverter.ToUInt32(buffer, offset);
        return swap ? SwapUInt32(value) : value;
    }

    /// <summary>Reads a UInt64 from a byte array at the specified offset, optionally swapping endianness.</summary>
    public static ulong ReadUInt64(byte[] buffer, int offset, bool swap)
    {
        ulong value = BitConverter.ToUInt64(buffer, offset);
        return swap ? SwapUInt64(value) : value;
    }

    // -----------------------------------------------------------------------
    // Byte-swap utilities
    // -----------------------------------------------------------------------

    public static uint SwapUInt32(uint value)
    {
        return ((value & 0x000000FFU) << 24) |
               ((value & 0x0000FF00U) << 8) |
               ((value & 0x00FF0000U) >> 8) |
               ((value & 0xFF000000U) >> 24);
    }

    public static ulong SwapUInt64(ulong value)
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
