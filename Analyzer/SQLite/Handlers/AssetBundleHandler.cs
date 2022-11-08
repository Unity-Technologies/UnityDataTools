using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class AssetBundleHandler : ISQLiteHandler, IDisposable
{
    SQLiteCommand m_InsertCommand;

    public void Init(SQLiteConnection db)
    {
        using var command = new SQLiteCommand(db);

        command.CommandText = Properties.Resources.AssetBundle;
        command.ExecuteNonQuery();

        m_InsertCommand = new SQLiteCommand(db);
        m_InsertCommand.CommandText = "INSERT INTO assets(object, name) VALUES(@object, @name)";
        m_InsertCommand.Parameters.Add("@object", DbType.Int64);
        m_InsertCommand.Parameters.Add("@name", DbType.String);
    }

    public void Process(ObjectIdProvider idProvider, long objectId, Dictionary<int, int> localToDbFileId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        var assetBundle = AssetBundle.Read(reader);
        
        foreach (var asset in assetBundle.Assets)
        {
            var fileId = localToDbFileId[asset.PPtr.FileId];
            m_InsertCommand.Parameters["@object"].Value = idProvider.GetId((fileId, asset.PPtr.PathId));
            m_InsertCommand.Parameters["@name"].Value = asset.Name;
            
            m_InsertCommand.ExecuteNonQuery();
        }

        name = assetBundle.Name;
        streamDataSize = 0;
    }

    public void Finalize(SQLiteConnection db)
    {
    }

    void IDisposable.Dispose()
    {
        m_InsertCommand.Dispose();
    }
}