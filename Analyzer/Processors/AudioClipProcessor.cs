using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.Processors
{
    public class AudioClipProcessor : IProcessor, IDisposable
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

        public void Process(AnalyzerTool analyzer, long objectId, Dictionary<int, int> localToDbFileId, RandomAccessReader reader, out string name, out long streamedDataSize)
        {
            streamedDataSize = reader["m_Resource"]["m_Size"].GetValue<int>();

            m_InsertCommand.Parameters["@id"].Value = objectId;
            m_InsertCommand.Parameters["@bits_per_sample"].Value = reader["m_BitsPerSample"].GetValue<int>();
            m_InsertCommand.Parameters["@frequency"].Value = reader["m_Frequency"].GetValue<int>();
            m_InsertCommand.Parameters["@channels"].Value = reader["m_Channels"].GetValue<int>();
            m_InsertCommand.Parameters["@load_type"].Value = reader["m_LoadType"].GetValue<int>();
            m_InsertCommand.Parameters["@format"].Value = reader["m_CompressionFormat"].GetValue<int>();

            m_InsertCommand.ExecuteNonQuery();

            name = reader["m_Name"].GetValue<string>();
        }

        void IDisposable.Dispose()
        {
            m_InsertCommand.Dispose();
        }
    }
}
