using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class AudioClipHandler : ISQLiteHandler, IDisposable
{
    SQLiteCommand m_InsertCommand;

    public void Init(SQLiteConnection db)
    {
        using var command = new SQLiteCommand(db);

        command.CommandText = Properties.Resources.AudioClip;
        command.ExecuteNonQuery();

        m_InsertCommand = new SQLiteCommand(db);
        m_InsertCommand.CommandText = "INSERT INTO audio_clips(id, bits_per_sample, frequency, channels, load_type, format) VALUES(@id, @bits_per_sample, @frequency, @channels, @load_type, @format)";
        m_InsertCommand.Parameters.Add("@id", DbType.Int64);
        m_InsertCommand.Parameters.Add("@bits_per_sample", DbType.Int32);
        m_InsertCommand.Parameters.Add("@frequency", DbType.Int32);
        m_InsertCommand.Parameters.Add("@channels", DbType.Int32);
        m_InsertCommand.Parameters.Add("@load_type", DbType.Int32);
        m_InsertCommand.Parameters.Add("@format", DbType.Int32);
    }

    public void Process(ObjectIdProvider idProvider, long objectId, Dictionary<int, int> localToDbFileId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        var audioClip = AudioClip.Read(reader);
        
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

    public void Finalize(SQLiteConnection db)
    {
    }

    void IDisposable.Dispose()
    {
        m_InsertCommand.Dispose();
    }
}