using System;

namespace UnityDataTools.FileSystem;

/// <summary>
/// Thrown when a SerializedFile cannot be opened, typically due to a content-related
/// parsing failure (e.g. type mismatch, missing TypeTrees) rather than an I/O error.
/// </summary>
public class SerializedFileOpenException : Exception
{
    public string FilePath { get; }

    /// <summary>
    /// True when the file was not opened because it has no TypeTrees. This is detected before
    /// handing the file to the native loader, which would otherwise emit misleading version
    /// mismatch errors or crash. Callers use it to report and count these files distinctly.
    /// </summary>
    public bool MissingTypeTrees { get; }

    public SerializedFileOpenException(string filePath, bool missingTypeTrees = false)
        : base(missingTypeTrees
            ? $"Serialized file has no TypeTrees: \"{filePath}\""
            : $"Failed to open serialized file: \"{filePath}\"")
    {
        FilePath = filePath;
        MissingTypeTrees = missingTypeTrees;
    }
}
