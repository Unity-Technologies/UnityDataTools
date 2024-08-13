using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class Texture2DHandler : ISQLiteHandler
{
    SQLiteCommand m_InsertCommand;
    bool m_skipCreateDatabase;

    public Texture2DHandler(bool skipCreateDatabase)
    {
        m_skipCreateDatabase = skipCreateDatabase;
    }

    public void Init(SQLiteConnection db)
    {
        if (!m_skipCreateDatabase)
        {
            using var command = new SQLiteCommand(db);

            command.CommandText = Properties.Resources.Texture2D;
            command.ExecuteNonQuery();
        }

        m_InsertCommand = new SQLiteCommand(db);
        m_InsertCommand.CommandText = "INSERT INTO textures(id, width, height, image_count, format, rw_enabled, mip_count) VALUES(@id, @width, @height, @image_count, @format, @rw_enabled, @mip_count)";
        m_InsertCommand.Parameters.Add("@id", DbType.Int64);
        m_InsertCommand.Parameters.Add("@width", DbType.Int32);
        m_InsertCommand.Parameters.Add("@height", DbType.Int32);
        m_InsertCommand.Parameters.Add("@image_count", DbType.Int32);
        m_InsertCommand.Parameters.Add("@format", DbType.Int32);
        m_InsertCommand.Parameters.Add("@rw_enabled", DbType.Int32);
        m_InsertCommand.Parameters.Add("@mip_count", DbType.Int32);
    }

    public void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        var texture2d = Texture2D.Read(reader);
        
        m_InsertCommand.Parameters["@id"].Value = objectId;
        m_InsertCommand.Parameters["@width"].Value = texture2d.Width;
        m_InsertCommand.Parameters["@height"].Value = texture2d.Height;
        m_InsertCommand.Parameters["@image_count"].Value = texture2d.ImageCount;
        m_InsertCommand.Parameters["@format"].Value = texture2d.Format;
        m_InsertCommand.Parameters["@rw_enabled"].Value = texture2d.RwEnabled;
        m_InsertCommand.Parameters["@mip_count"].Value = texture2d.MipCount;

        m_InsertCommand.ExecuteNonQuery();

        name = texture2d.Name;
        streamDataSize = texture2d.StreamDataSize;
    }

    public void Finalize(SQLiteConnection db)
    {
    }

    void IDisposable.Dispose()
    {
        m_InsertCommand.Dispose();
    }
}