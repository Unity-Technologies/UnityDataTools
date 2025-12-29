namespace UnityDataTools.Analyzer.Util;

/// <summary>
/// Helper class for converting Unity GUID data to string format.
/// </summary>
public static class GuidHelper
{
    /// <summary>
    /// Converts Unity GUID data array to string format matching Unity's GUIDToString function.
    /// Unity stores GUIDs as 4 uint32 values and converts them to a 32-character hex string
    /// with a specific byte ordering that differs from standard GUID/UUID formatting.
    /// </summary>
    /// <example>
    /// data[0]=3856716653 (0xe60765cd) becomes "d63d0e5e"
    /// </example>
    public static string FormatUnityGuid(uint data0, uint data1, uint data2, uint data3)
    {
        char[] result = new char[32];
        FormatUInt32Reversed(data0, result, 0);
        FormatUInt32Reversed(data1, result, 8);
        FormatUInt32Reversed(data2, result, 16);
        FormatUInt32Reversed(data3, result, 24);
        return new string(result);
    }

    /// <summary>
    /// Formats a uint32 as 8 hex digits matching Unity's GUIDToString logic.
    /// Unity's implementation extracts nibbles from most significant to least significant
    /// (j=7 down to j=0) and writes them to output positions in the same order (offset+7 to offset+0),
    /// which reverses the byte order compared to standard hex formatting.
    /// </summary>
    /// <example>
    /// For example: 0xe60765cd becomes "d63d0e5e" (bytes reversed: cd,65,07,e6 → e6,07,65,cd)
    /// </example>
    private static void FormatUInt32Reversed(uint value, char[] output, int offset)
    {
        const string hexChars = "0123456789abcdef";
        for (int j = 7; j >= 0; j--)
        {
            uint nibble = (value >> (j * 4)) & 0xF;
            output[offset + j] = hexChars[(int)nibble];
        }
    }
}

