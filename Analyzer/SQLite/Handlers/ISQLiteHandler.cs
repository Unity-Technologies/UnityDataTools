using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class Context
{
    public int ArchiveId { get; init; }
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
}

public interface ISQLiteFileParser : IDisposable
{
    void Init(SqliteConnection db);
    bool CanParse(string filename);

    // rootDirectory is the scanned input path the file was found under; names recorded in the
    // database are relative to it, so same-named files in different sub-folders stay distinct.
    void Parse(string filename, string rootDirectory);

    // Called once after all files have been parsed, so a parser can write data that can only be
    // determined from the complete set (e.g. dangling references). No-op for parsers that don't
    // need it.
    void FinalizeDatabase();

    public bool Verbose { get; set; }
    public bool SkipReferences { get; set; }
    public bool SkipCrc { get; set; }
}
