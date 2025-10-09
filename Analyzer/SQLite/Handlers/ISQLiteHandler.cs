using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using UnityDataTools.FileSystem;
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

public abstract class SQLiteHandlerBase : IDisposable
{
    public abstract void Init(SqliteConnection db);

    // Override if you want object-level processing
    public virtual void ProcessObject(Context ctx, long objectId, RandomAccessReader reader,
                                      out string name, out long streamDataSize)
    {
        name = string.Empty;
        streamDataSize = 0;
    }

    // Override if you want SerializedFile-level processing
    public virtual void ProcessSerializedFile(SerializedFile sf, SqliteTransaction transaction) { }

    public virtual void Finalize(SqliteConnection db) { }

    public abstract void Dispose();
}

// Keep old interface for backward compatibility during migration
public interface ISQLiteHandler : IDisposable
{
    void Init(Microsoft.Data.Sqlite.SqliteConnection db);
    void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize);
    void Finalize(Microsoft.Data.Sqlite.SqliteConnection db);
}