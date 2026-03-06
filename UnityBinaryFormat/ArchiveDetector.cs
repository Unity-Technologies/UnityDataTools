using System;
using System.IO;

namespace UnityDataTools.BinaryFormat;

/// <summary>
/// Utility for detecting Unity Archive (AssetBundle) files by reading their signature.
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
}
