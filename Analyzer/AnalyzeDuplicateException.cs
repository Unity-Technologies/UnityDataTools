using System;

namespace UnityDataTools.Analyzer;

// Thrown when analyze encounters a second SerializedFile or archive with a name it has already
// processed. Only a single build can be analyzed at a time: a SerializedFile is referenced by name,
// so two files sharing a name are indistinguishable to Unity's cross-file references. The message
// is self-contained and leads with a distinctive phrase that is documented in command-analyze.md.
public class AnalyzeDuplicateException : Exception
{
    public string DuplicateName { get; }
    public bool IsArchive { get; }

    public AnalyzeDuplicateException(string duplicateName, bool isArchive)
        : this(duplicateName, isArchive, isArchive
            ? $"Duplicate archive name '{duplicateName}'. Each analyzed archive must have a unique name; only a single build can be analyzed at a time."
            : $"Duplicate SerializedFile name '{duplicateName}'. Only a single build can be analyzed at a time; the same SerializedFile name cannot be analyzed twice.")
    {
    }

    private AnalyzeDuplicateException(string duplicateName, bool isArchive, string message)
        : base(message)
    {
        DuplicateName = duplicateName;
        IsArchive = isArchive;
    }

    // AssetBundle variants ("ui.hd", "ui.sd") contain a SerializedFile with the same name by design,
    // so hitting one is a known limitation rather than a sign of mixing builds.
    public static AnalyzeDuplicateException AssetBundleVariant(string serializedFileName, string analyzedArchive)
    {
        return new AnalyzeDuplicateException(serializedFileName, isArchive: false,
            $"AssetBundle variant of '{analyzedArchive}', which was already analyzed (both contain SerializedFile '{serializedFileName}'). Only one variant of each bundle can be analyzed.");
    }
}
