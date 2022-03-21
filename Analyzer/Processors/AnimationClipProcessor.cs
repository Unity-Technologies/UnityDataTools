using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.Processors
{
    public class AnimationClipProcessor : IProcessor, IDisposable
    {
        SQLiteCommand m_InsertCommand;

        public void Init(SQLiteConnection db)
        {
            using var command = new SQLiteCommand(db);

            command.CommandText = Properties.Resources.AnimationClip;
            command.ExecuteNonQuery();

            m_InsertCommand = new SQLiteCommand(db);
            m_InsertCommand.CommandText = "INSERT INTO animation_clips(id, legacy, events) VALUES(@id, @legacy, @events)";
            m_InsertCommand.Parameters.Add("@id", DbType.Int64);
            m_InsertCommand.Parameters.Add("@legacy", DbType.Int32);
            m_InsertCommand.Parameters.Add("@events", DbType.Int32);
        }

        public void Process(AnalyzerTool analyzer, long objectId, Dictionary<int, int> localToDbFileId, RandomAccessReader reader, out string name, out long streamedDataSize)
        {
            streamedDataSize = 0;

            m_InsertCommand.Parameters["@id"].Value = objectId;
            m_InsertCommand.Parameters["@legacy"].Value = reader["m_Legacy"].GetValue<byte>();
            m_InsertCommand.Parameters["@events"].Value = reader["m_Events"].GetArraySize();
            m_InsertCommand.ExecuteNonQuery();

            name = reader["m_Name"].GetValue<string>();
        }

        void IDisposable.Dispose()
        {
            m_InsertCommand.Dispose();
        }
    }
}
