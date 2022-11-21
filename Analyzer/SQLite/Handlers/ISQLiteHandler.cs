using System;
using System.Collections.Generic;
using System.Data.SQLite;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class Context
{
    public int AssetBundleId { get; init; }
    public int SerializedFileId { get; init; }
    public int SceneId { get; init; }
    public Util.ObjectIdProvider ObjectIdProvider { get; init; }
    public Util.IdProvider<string> SerializedFileIdProvider { get; init; }
    public Dictionary<int, int> LocalToDbFileId { get; init; }
}

public interface ISQLiteHandler : IDisposable
{
    void Init(SQLiteConnection db);
    void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize);
    void Finalize(SQLiteConnection db);
}