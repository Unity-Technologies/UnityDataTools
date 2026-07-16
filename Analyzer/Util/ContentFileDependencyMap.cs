using System.Collections.Generic;

namespace UnityDataTools.Analyzer.Util;

// The file-to-file dependencies of a ContentDirectory build, populated from ContentLayout.json.
// In ContentDirectory builds the external reference table inside a SerializedFile holds symbolic
// .cfid placeholders instead of real filenames; the actual target of a reference is determined
// positionally through the layout's dependency list (see Documentation/contentdirectory-format.md).
// This map holds, for each content file (keyed by its lowercased "<contenthash>.cf" filename),
// the resolved dependency filenames in external-reference-table order. An entry is null where the
// dependency is a built-in file (which has no content hash); such references fall back to the
// path from the external reference table.
public class ContentFileDependencyMap
{
    private Dictionary<string, string[]> m_Dependencies = new();

    public void Add(string fileName, string[] resolvedDependencies)
    {
        m_Dependencies[fileName] = resolvedDependencies;
    }

    // Returns the resolved dependency filenames for the given (lowercased) filename, or null if
    // the file is not covered by an imported ContentLayout.
    public string[] GetDependencies(string fileName)
    {
        return m_Dependencies.TryGetValue(fileName, out var dependencies) ? dependencies : null;
    }
}
