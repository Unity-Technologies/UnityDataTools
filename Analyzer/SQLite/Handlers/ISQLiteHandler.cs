using System.Collections.Generic;
using System.Data.SQLite;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public interface ISQLiteHandler
{
    void Init(SQLiteConnection db);
    void Process(ObjectIdProvider idProvider, long objectId, Dictionary<int, int> localToDbFileId, RandomAccessReader reader, out string name, out long streamDataSize);
    void Finalize(SQLiteConnection db);
}