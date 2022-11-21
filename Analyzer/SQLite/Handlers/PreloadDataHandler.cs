using System;
using System.Data;
using System.Data.SQLite;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class PreloadDataHandler : ISQLiteHandler
{
    SQLiteCommand m_InsertDepCommand;

    public void Init(SQLiteConnection db)
    {
        using var command = new SQLiteCommand(db);
        
        m_InsertDepCommand = new SQLiteCommand(db);
        m_InsertDepCommand.CommandText = "INSERT INTO asset_dependencies(object, dependency) VALUES(@object, @dependency)";
        m_InsertDepCommand.Parameters.Add("@object", DbType.Int64);
        m_InsertDepCommand.Parameters.Add("@dependency", DbType.Int64);
    }

    public void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        var preloadData = PreloadData.Read(reader);

        m_InsertDepCommand.Parameters["@object"].Value = ctx.SceneId;

        foreach (var asset in preloadData.Assets)
        {
            var fileId = ctx.LocalToDbFileId[asset.FileId];
            var objId = ctx.ObjectIdProvider.GetId((fileId, asset.PathId));
            
            m_InsertDepCommand.Parameters["@dependency"].Value = objId;
            m_InsertDepCommand.ExecuteNonQuery();
        }

        name = "";
        streamDataSize = 0;
    }

    public void Finalize(SQLiteConnection db)
    {
    }

    void IDisposable.Dispose()
    {
        m_InsertDepCommand.Dispose();
    }
}