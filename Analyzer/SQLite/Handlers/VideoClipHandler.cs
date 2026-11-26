using System;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class VideoClipHandler : ISQLiteHandler
{
    private SqliteCommand m_InsertCommand;

    public void Init(SqliteConnection db)
    {
        using var command = db.CreateCommand();
        command.CommandText = Resources.VideoClip;
        command.ExecuteNonQuery();

        m_InsertCommand = db.CreateCommand();
        m_InsertCommand.CommandText = "INSERT INTO video_clips(id, width, height, frame_rate, frame_count, audio_tracks, audio_languages, original_path) VALUES(@id, @width, @height, @frame_rate, @frame_count, @audio_tracks, @audio_languages, @original_path)";
        m_InsertCommand.Parameters.Add("@id", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@width", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@height", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@frame_rate", SqliteType.Real);
        m_InsertCommand.Parameters.Add("@frame_count", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@audio_tracks", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@audio_languages", SqliteType.Text);
        m_InsertCommand.Parameters.Add("@original_path", SqliteType.Text);
    }

    public void Process(Context ctx, long objectId, RandomAccessReader reader, out string name)
    {
        var videoClip = VideoClip.Read(reader);
        m_InsertCommand.Transaction = ctx.Transaction;
        m_InsertCommand.Parameters["@id"].Value = objectId;
        m_InsertCommand.Parameters["@width"].Value = videoClip.Width;
        m_InsertCommand.Parameters["@height"].Value = videoClip.Height;
        m_InsertCommand.Parameters["@frame_rate"].Value = videoClip.FrameRate;
        m_InsertCommand.Parameters["@frame_count"].Value = (long)videoClip.FrameCount;
        m_InsertCommand.Parameters["@audio_tracks"].Value = videoClip.AudioTracks;
        m_InsertCommand.Parameters["@audio_languages"].Value = videoClip.AudioLanguages;
        m_InsertCommand.Parameters["@original_path"].Value = videoClip.OriginalPath;
        m_InsertCommand.ExecuteNonQuery();

        name = videoClip.Name;
    }

    void IDisposable.Dispose()
    {
        m_InsertCommand?.Dispose();
    }
}
