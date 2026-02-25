using System;
using System.IO;
using System.Text;

namespace UnityDataTools.Analyzer.Util;

/// <summary>
/// Utility for detecting YAML-format Unity SerializedFiles.
///
/// Unity SerializedFiles can be stored in two formats:
/// 1. Binary format (produced by builds - read by Unity Runtime, also used for imported artifacts etc) - detected by SerializedFileDetector
/// 2. YAML format (text format used in Editor for .asset, .prefab, .unity files) - detected by this class
///
/// YAML SerializedFiles begin with the magic string "%YAML 1.1", optionally preceded by
/// a UTF-8 BOM (byte order mark: 0xEF 0xBB 0xBF).
/// </summary>
public static class YamlSerializedFileDetector
{
    private const string UnityTextMagicString = "%YAML 1.1";
    private static readonly byte[] Utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF };

    public static bool IsYamlSerializedFile(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Unity checks for UTF-8 BOM (3 bytes) + magic string (9 bytes) = 12 bytes total
            const int bomLength = 3;
            int magicLength = UnityTextMagicString.Length;
            int bufferSize = bomLength + magicLength;

            if (stream.Length < magicLength)
                return false;

            byte[] buffer = new byte[bufferSize];
            int bytesRead = stream.Read(buffer, 0, Math.Min(bufferSize, (int)stream.Length));

            if (bytesRead < magicLength)
                return false;

            int offset = 0;
            if (bytesRead >= bomLength && HasUtf8Bom(buffer))
            {
                offset = bomLength;
            }

            // Check for magic string after BOM (if present)
            if (bytesRead - offset < magicLength)
                return false;

            string fileStart = Encoding.ASCII.GetString(buffer, offset, magicLength);
            return fileStart == UnityTextMagicString;
        }
        catch
        {
            // Any exception during file reading means this isn't a valid YAML file
            return false;
        }
    }

    private static bool HasUtf8Bom(byte[] buffer)
    {
        if (buffer.Length < Utf8Bom.Length)
            return false;

        for (int i = 0; i < Utf8Bom.Length; i++)
        {
            if (buffer[i] != Utf8Bom[i])
                return false;
        }

        return true;
    }
}
