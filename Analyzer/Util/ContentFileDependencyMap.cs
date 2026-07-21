using System;
using System.Collections.Generic;

namespace UnityDataTools.Analyzer.Util;

// The file-to-file dependencies of a ContentDirectory build, populated from ContentLayout.json.
// In ContentDirectory builds the external reference table inside a SerializedFile holds symbolic
// .cfid placeholders instead of real filenames; the actual target of a reference is determined
// positionally through the layout's dependency list (see Documentation/contentdirectory-format.md).
// This map holds, for each content file (keyed by its normalized content-hash name, see
// NormalizeFileName), the resolved dependency names in external-reference-table order. An entry is
// null where the dependency is a built-in file (which has no content hash); such references fall
// back to the path from the external reference table.
public class ContentFileDependencyMap
{
    private Dictionary<string, string[]> m_Dependencies = new();

    public void Add(string fileName, string[] resolvedDependencies)
    {
        m_Dependencies[fileName] = resolvedDependencies;
    }

    // Returns the resolved dependency names for the given (normalized) content-file name, or null
    // if the file is not covered by an imported ContentLayout.
    public string[] GetDependencies(string fileName)
    {
        return m_Dependencies.TryGetValue(fileName, out var dependencies) ? dependencies : null;
    }

    // Content files are named by their content hash and may or may not carry the ".cf" extension:
    // loose builds and archived builds vary, and the extension is informational (see
    // Documentation/contentdirectory-format.md). Strip it so a content file resolves to the same
    // serialized-file id whether or not the extension is present on disk. Non-content files never
    // end in ".cf", so this is a no-op for them. The name's case is preserved, matching how
    // serialized-file names are keyed case-sensitively elsewhere.
    public static string NormalizeFileName(string fileName)
    {
        return fileName.EndsWith(".cf", StringComparison.OrdinalIgnoreCase) ? fileName[..^3] : fileName;
    }
}
