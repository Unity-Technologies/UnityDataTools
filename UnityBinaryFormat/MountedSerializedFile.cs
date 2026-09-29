using System;
using System.Collections.Generic;
using System.Linq;
using UnityDataTools.FileSystem;

namespace UnityDataTools.BinaryFormat;

// Gives access to one SerializedFile inside a Unity Archive without extracting it, by keeping the
// archive mounted until this object is disposed.
public sealed class MountedSerializedFile : IDisposable
{
    const string MountPoint = "/";

    UnityArchive m_Archive;

    // Path of the SerializedFile inside the archive, as shown by `archive list`, e.g. "CAB-<hash>".
    public string PathInArchive { get; }

    // Path to pass to UnityFileSystem / UnityFileStream while the archive is mounted.
    public string MountedPath => MountPoint + PathInArchive;

    MountedSerializedFile(UnityArchive archive, string pathInArchive)
    {
        m_Archive = archive;
        PathInArchive = pathInArchive;
    }

    // Mounts the archive and selects the SerializedFile at pathInArchive. When pathInArchive is null the
    // archive must contain exactly one SerializedFile. Throws SerializedFileSelectionException when
    // no SerializedFile can be selected, after unmounting the archive.
    public static MountedSerializedFile Open(string archivePath, string pathInArchive)
    {
        var archive = UnityFileSystem.MountArchive(archivePath, MountPoint);

        var nodes = archive.Nodes;
        var serializedFiles = nodes
            .Where(n => n.Flags.HasFlag(ArchiveNodeFlags.SerializedFile))
            .Select(n => n.Path)
            .ToList();

        SerializedFileSelectionFailure failure;
        if (serializedFiles.Count == 0)
            failure = SerializedFileSelectionFailure.NoSerializedFiles;
        else if (pathInArchive == null)
            failure = serializedFiles.Count == 1 ? SerializedFileSelectionFailure.None : SerializedFileSelectionFailure.MultipleSerializedFiles;
        else if (serializedFiles.Contains(pathInArchive))
            failure = SerializedFileSelectionFailure.None;
        else if (nodes.Any(n => n.Path == pathInArchive))
            failure = SerializedFileSelectionFailure.NotSerializedFile;
        else
            failure = SerializedFileSelectionFailure.NotFound;

        if (failure != SerializedFileSelectionFailure.None)
        {
            archive.Dispose();
            throw new SerializedFileSelectionException(failure, archivePath, pathInArchive, serializedFiles);
        }

        return new MountedSerializedFile(archive, pathInArchive ?? serializedFiles[0]);
    }

    public void Dispose()
    {
        m_Archive?.Dispose();
        m_Archive = null;
    }
}

public enum SerializedFileSelectionFailure
{
    None,
    NoSerializedFiles,
    MultipleSerializedFiles,
    NotFound,
    NotSerializedFile,
}

// Thrown by MountedSerializedFile.Open. Carries enough detail for the caller to explain how to
// select a SerializedFile in its own terms.
public class SerializedFileSelectionException : Exception
{
    public SerializedFileSelectionFailure Failure { get; }
    public string ArchivePath { get; }
    public string RequestedPath { get; }
    public IReadOnlyList<string> SerializedFiles { get; }

    public SerializedFileSelectionException(SerializedFileSelectionFailure failure, string archivePath, string requestedPath, IReadOnlyList<string> serializedFiles)
        : base(failure switch
        {
            SerializedFileSelectionFailure.NoSerializedFiles => "The archive contains no SerializedFiles.",
            SerializedFileSelectionFailure.MultipleSerializedFiles => $"The archive contains {serializedFiles.Count} SerializedFiles.",
            SerializedFileSelectionFailure.NotFound => $"\"{requestedPath}\" was not found in the archive.",
            SerializedFileSelectionFailure.NotSerializedFile => $"\"{requestedPath}\" in the archive is not a SerializedFile.",
            _ => "No SerializedFile could be selected in the archive.",
        })
    {
        Failure = failure;
        ArchivePath = archivePath;
        RequestedPath = requestedPath;
        SerializedFiles = serializedFiles;
    }
}
