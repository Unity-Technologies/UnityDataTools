using System;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class AssetBundleHandler : ISQLiteHandler
{
    SqliteCommand m_InsertCommand;
    private SqliteCommand m_InsertDepCommand;
    private Regex m_SceneNameRegex = new Regex(@"([^//]+)\.unity");

    public void Init(SqliteConnection db)
    {
        using var command = db.CreateCommand();
        command.CommandText = Properties.Resources.AssetBundle;
        command.ExecuteNonQuery();

        m_InsertCommand = db.CreateCommand();
        
        m_InsertCommand.CommandText = "INSERT INTO assets(object, name) VALUES(@object, @name)";
        m_InsertCommand.Parameters.Add("@object", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@name", SqliteType.Text);

        m_InsertDepCommand = db.CreateCommand();
        
        m_InsertDepCommand.CommandText = "INSERT INTO asset_dependencies(object, dependency) VALUES(@object, @dependency)";
        m_InsertDepCommand.Parameters.Add("@object", SqliteType.Integer);
        m_InsertDepCommand.Parameters.Add("@dependency", SqliteType.Integer);
    }

    public void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        var assetBundle = AssetBundle.Read(reader);
        
        foreach (var asset in assetBundle.Assets)
        {
            if (!assetBundle.IsSceneAssetBundle)
            {
                var fileId = ctx.LocalToDbFileId[asset.PPtr.FileId];
                var objId = ctx.ObjectIdProvider.GetId((fileId, asset.PPtr.PathId));
                m_InsertCommand.Transaction = ctx.Transaction;
                m_InsertCommand.Parameters["@object"].Value = objId;
                m_InsertCommand.Parameters["@name"].Value = asset.Name;
                m_InsertCommand.ExecuteNonQuery();

                for (int i = asset.PreloadIndex; i < asset.PreloadIndex + asset.PreloadSize; ++i)
                {
                    var dependency = assetBundle.PreloadTable[i];
                    var depFileId = ctx.LocalToDbFileId[dependency.FileId];
                    var depId = ctx.ObjectIdProvider.GetId((depFileId, dependency.PathId));
                    m_InsertDepCommand.Transaction = ctx.Transaction;
                    m_InsertDepCommand.Parameters["@object"].Value = objId;
                    m_InsertDepCommand.Parameters["@dependency"].Value = depId;
                    m_InsertDepCommand.ExecuteNonQuery();
                }
            }
            else
            {
                var match = m_SceneNameRegex.Match(asset.Name);

                if (match.Success)
                {
                    var sceneName = match.Groups[1].Value;
                    var objId = ctx.ObjectIdProvider.GetId((ctx.SerializedFileIdProvider.GetId(sceneName), 0));
                    m_InsertCommand.Transaction = ctx.Transaction;
                    m_InsertCommand.Parameters["@object"].Value = objId;
                    m_InsertCommand.Parameters["@name"].Value = asset.Name;
                    m_InsertCommand.ExecuteNonQuery();
                }
            }
        }

        name = assetBundle.Name;
        streamDataSize = 0;
    }

    public void Finalize(SqliteConnection db)
    {
        using var command = new SqliteCommand();
        command.Connection = db;
        command.CommandText = "CREATE INDEX asset_dependencies_object ON asset_dependencies(object)";
        command.ExecuteNonQuery();
        
        command.CommandText = "CREATE INDEX asset_dependencies_dependency ON asset_dependencies(dependency)";
        command.ExecuteNonQuery();
    }

    void IDisposable.Dispose()
    {
        m_InsertCommand?.Dispose();
        m_InsertDepCommand?.Dispose();
    }
}