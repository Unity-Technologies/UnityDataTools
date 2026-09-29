using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityDataTools.FileSystem;

namespace UnityDataTools.BinaryFormat;

// A SerializedFile inside a mounted Unity Archive, so that commands can read it without extracting
// the archive. The archive stays mounted until this object is disposed.
public sealed class ArchiveSerializedFile : IDisposable
{
    const string MountPoint = "/";

    UnityArchive m_Archive;

    // Path of the SerializedFile inside the archive, as shown by `archive list`.
    public string Name { get; }

    // Path to pass to UnityFileSystem / UnityFileStream while the archive is mounted.
    public string MountedPath => MountPoint + Name;

    ArchiveSerializedFile(UnityArchive archive, string name)
    {
        m_Archive = archive;
        Name = name;
    }

    // Mounts the archive and selects the SerializedFile named by entry. When entry is null the archive
    // must contain exactly one SerializedFile. On failure the archive is unmounted and errorMessage
    // explains the problem, listing the SerializedFiles that can be chosen.
    public static bool TryOpen(string archivePath, string entry, out ArchiveSerializedFile result, out string errorMessage)
    {
        result = null;
        var archive = UnityFileSystem.MountArchive(archivePath, MountPoint);

        var nodes = archive.Nodes;
        var serializedFiles = nodes
            .Where(n => n.Flags.HasFlag(ArchiveNodeFlags.SerializedFile))
            .Select(n => n.Path)
            .ToList();

        errorMessage = null;
        if (serializedFiles.Count == 0)
        {
            errorMessage = "Error: The archive contains no SerializedFiles.";
        }
        else if (entry != null)
        {
            if (serializedFiles.Contains(entry))
                result = new ArchiveSerializedFile(archive, entry);
            else if (nodes.Any(n => n.Path == entry))
                errorMessage = FormatError($"\"{entry}\" in the archive is not a SerializedFile.", serializedFiles);
            else
                errorMessage = FormatError($"\"{entry}\" was not found in the archive.", serializedFiles);
        }
        else if (serializedFiles.Count == 1)
        {
            result = new ArchiveSerializedFile(archive, serializedFiles[0]);
        }
        else
        {
            errorMessage = FormatError(
                $"The archive contains {serializedFiles.Count} SerializedFiles. Choose one with --entry, for example --entry \"{serializedFiles[0]}\".",
                serializedFiles);
        }

        if (result == null)
            archive.Dispose();

        return result != null;
    }

    // Error for --entry given with a file that is not a Unity Archive.
    public static string EntryWithoutArchiveError(string filePath) =>
        $"Error: --entry can only be used with a Unity Archive, and this file is not one.{Environment.NewLine}File: {filePath}";

    static string FormatError(string message, List<string> serializedFiles)
    {
        var sb = new StringBuilder();
        sb.Append("Error: ").AppendLine(message);
        sb.Append("SerializedFiles in the archive:");
        foreach (var name in serializedFiles)
            sb.AppendLine().Append("  ").Append(name);
        return sb.ToString();
    }

    public void Dispose()
    {
        m_Archive?.Dispose();
        m_Archive = null;
    }
}
