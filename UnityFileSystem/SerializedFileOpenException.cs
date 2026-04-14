using System;

namespace UnityDataTools.FileSystem;

/// <summary>
/// Thrown when a SerializedFile cannot be opened, typically due to a content-related
/// parsing failure (e.g. type mismatch, missing TypeTrees) rather than an I/O error.
/// </summary>
public class SerializedFileOpenException : Exception
{
    public string FilePath { get; }

    public SerializedFileOpenException(string filePath)
        : base($"Failed to open serialized file: \"{filePath}\"")
        => FilePath = filePath;
}
