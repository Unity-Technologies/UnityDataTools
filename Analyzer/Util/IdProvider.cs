using System.Collections.Generic;

namespace UnityDataTools.Analyzer.Util;

// Assigns a unique, sequential integer id to each distinct key and returns the same id when a
// key is seen again. Analyze always builds a fresh database, so rather than relying on SQLite
// to auto-assign row ids the writer allocates them here: e.g. each serialized file name maps to
// one row id in the serialized_files table. Ids are 0-based and dense (the next id is simply the
// current key count).
public class IdProvider<Key>
{
    private Dictionary<Key, int> m_Ids = new();

    // Exposes the key->id assignments so callers can iterate or invert the mapping (used at
    // finalize to map a dangling object id back to its (fileId, pathId) or file name).
    public IReadOnlyDictionary<Key, int> Entries => m_Ids;

    public int GetId(Key key)
    {
        if (m_Ids.TryGetValue(key, out var id))
        {
            return id;
        }

        id = m_Ids.Count;
        m_Ids.Add(key, id);

        return id;
    }
}

// Assigns the objects-table row id for each Unity object, keyed by (fileId, pathId) where:
//   - fileId is the *global* serialized file id (from the serialized-file IdProvider), NOT a
//     PPtr's local m_FileID. A local m_FileID (0 = same file, 1..N = an entry in that file's
//     external reference table) is only meaningful within one serialized file, so callers must
//     translate it to a global file id (via LocalToDbFileId) before calling GetId.
//   - pathId is the object's local id (LFID) within its serialized file.
// Because the fileId is global, a PPtr in file A and file B's own object list resolve to the
// same row id. That shared id is what lets the refs table link objects across files.
public class ObjectIdProvider : IdProvider<(int fileId, long pathId)>
{
}
