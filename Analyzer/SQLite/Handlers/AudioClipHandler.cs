using System;
using Microsoft.Data.Sqlite;
using System.Data;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class AudioClipHandler : ISQLiteHandler
{
    private SqliteCommand m_InsertCommand;

    public void Init(SqliteConnection db)
    {
        using var command = db.CreateCommand();
        command.CommandText = Properties.Resources.AudioClip;
        command.ExecuteNonQuery();

        m_InsertCommand = db.CreateCommand();
        m_InsertCommand.CommandText = "INSERT INTO audio_clips(id, bits_per_sample, frequency, channels, load_type, format) VALUES(@id, @bits_per_sample, @frequency, @channels, @load_type, @format)";
        m_InsertCommand.Parameters.Add("@id", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@bits_per_sample", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@frequency", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@channels", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@load_type", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@format", SqliteType.Integer);
    }

    public void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        var audioClip = AudioClip.Read(reader);
        m_InsertCommand.Transaction = ctx.Transaction;
        m_InsertCommand.Parameters["@id"].Value = objectId;
        m_InsertCommand.Parameters["@bits_per_sample"].Value = audioClip.BitsPerSample;
        m_InsertCommand.Parameters["@frequency"].Value = audioClip.Frequency;
        m_InsertCommand.Parameters["@channels"].Value = audioClip.Channels;
        m_InsertCommand.Parameters["@load_type"].Value = audioClip.LoadType;
        m_InsertCommand.Parameters["@format"].Value = audioClip.Format;

        m_InsertCommand.ExecuteNonQuery();

        streamDataSize = audioClip.StreamDataSize;
        name = audioClip.Name;
    }

    public void Finalize(SqliteConnection db)
    {
    }

    void IDisposable.Dispose()
    {
        m_InsertCommand?.Dispose();
    }
}