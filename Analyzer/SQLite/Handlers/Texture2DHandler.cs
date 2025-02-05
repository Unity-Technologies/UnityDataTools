using System;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class Texture2DHandler : ISQLiteHandler
{
    SqliteCommand m_InsertCommand = new SqliteCommand();

    public void Init(SqliteConnection db)
    {
        using var command = db.CreateCommand();
        command.CommandText = Properties.Resources.Texture2D;
        command.ExecuteNonQuery();

        m_InsertCommand = db.CreateCommand();
        m_InsertCommand.CommandText = "INSERT INTO textures(id, width, height, format, rw_enabled, mip_count) VALUES(@id, @width, @height, @format, @rw_enabled, @mip_count)";
        m_InsertCommand.Parameters.Add("@id", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@width", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@height", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@format", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@rw_enabled", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@mip_count", SqliteType.Integer);
    }

    public void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        var texture2d = Texture2D.Read(reader);
        m_InsertCommand.Transaction = ctx.Transaction;
        m_InsertCommand.Parameters["@id"].Value = objectId;
        m_InsertCommand.Parameters["@width"].Value = texture2d.Width;
        m_InsertCommand.Parameters["@height"].Value = texture2d.Height;
        m_InsertCommand.Parameters["@format"].Value = texture2d.Format;
        m_InsertCommand.Parameters["@rw_enabled"].Value = texture2d.RwEnabled;
        m_InsertCommand.Parameters["@mip_count"].Value = texture2d.MipCount;
        m_InsertCommand.ExecuteNonQuery();

        name = texture2d.Name;
        streamDataSize = texture2d.StreamDataSize;
    }

    public void Finalize(SqliteConnection db)
    {
    }

    void IDisposable.Dispose()
    {
        m_InsertCommand?.Dispose();
    }
}