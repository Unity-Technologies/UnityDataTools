using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
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
    public SqliteTransaction Transaction { get; set; }
}

public interface ISQLiteHandler : IDisposable
{
    void Init(Microsoft.Data.Sqlite.SqliteConnection db);
    void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize);
    void Finalize(Microsoft.Data.Sqlite.SqliteConnection db);
}