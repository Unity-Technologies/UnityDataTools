using System;
using System.Data;
using System.Data.SQLite;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class VideoClipHandler : ISQLiteHandler
{
    SQLiteCommand m_InsertCommand;

    public void Init(SQLiteConnection db)
    {
        using var command = new SQLiteCommand(db);

        command.CommandText = Properties.Resources.VideoClip;
        command.ExecuteNonQuery();

        m_InsertCommand = new SQLiteCommand(db);
        m_InsertCommand.CommandText = "INSERT INTO video_clips(id, width, height, frame_rate, frame_count) VALUES(@id, @width, @height, @frame_rate, @frame_count)";
        m_InsertCommand.Parameters.Add("@id", DbType.Int64);
        m_InsertCommand.Parameters.Add("@width", DbType.UInt32);
        m_InsertCommand.Parameters.Add("@height", DbType.UInt32);
        m_InsertCommand.Parameters.Add("@frame_rate", DbType.Double);
        m_InsertCommand.Parameters.Add("@frame_count", DbType.UInt64);
    }

    public void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        var videoClip = VideoClip.Read(reader);

        m_InsertCommand.Parameters["@id"].Value = objectId;
        m_InsertCommand.Parameters["@width"].Value = videoClip.Width;
        m_InsertCommand.Parameters["@height"].Value = videoClip.Height;
        m_InsertCommand.Parameters["@frame_rate"].Value = videoClip.FrameRate;
        m_InsertCommand.Parameters["@frame_count"].Value = videoClip.FrameCount;

        m_InsertCommand.ExecuteNonQuery();

        streamDataSize = (long)videoClip.StreamDataSize;
        name = videoClip.Name;
    }

    public void Finalize(SQLiteConnection db)
    {
    }

    void IDisposable.Dispose()
    {
        m_InsertCommand.Dispose();
    }
}