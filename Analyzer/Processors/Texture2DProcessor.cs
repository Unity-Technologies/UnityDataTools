using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.Processors
{
    public class Texture2DProcessor : IProcessor, IDisposable
    {
        SQLiteCommand m_InsertCommand;

        public void Init(SQLiteConnection db)
        {
            using var command = new SQLiteCommand(db);

            command.CommandText = Properties.Resources.Texture2D;
            command.ExecuteNonQuery();

            m_InsertCommand = new SQLiteCommand(db);
            m_InsertCommand.CommandText = "INSERT INTO textures(id, width, height, format, rw_enabled, mip_count) VALUES(@id, @width, @height, @format, @rw_enabled, @mip_count)";
            m_InsertCommand.Parameters.Add("@id", DbType.Int64);
            m_InsertCommand.Parameters.Add("@width", DbType.Int32);
            m_InsertCommand.Parameters.Add("@height", DbType.Int32);
            m_InsertCommand.Parameters.Add("@format", DbType.Int32);
            m_InsertCommand.Parameters.Add("@rw_enabled", DbType.Int32);
            m_InsertCommand.Parameters.Add("@mip_count", DbType.Int32);
        }

        public void Process(AnalyzerTool analyzer, long objectId, Dictionary<int, int> localToDbFileId, RandomAccessReader reader, out string name, out long streamedDataSize)
        {
            m_InsertCommand.Parameters["@id"].Value = objectId;
            m_InsertCommand.Parameters["@width"].Value = reader["m_Width"].GetValue<int>();
            m_InsertCommand.Parameters["@height"].Value = reader["m_Height"].GetValue<int>();
            m_InsertCommand.Parameters["@format"].Value = reader["m_TextureFormat"].GetValue<int>();
            m_InsertCommand.Parameters["@rw_enabled"].Value = reader["m_IsReadable"].GetValue<int>();
            m_InsertCommand.Parameters["@mip_count"].Value = reader["m_MipCount"].GetValue<int>();

            m_InsertCommand.ExecuteNonQuery();

            name = reader["m_Name"].GetValue<string>();

            // If size is 0, data is stored in a stream file.
            if (reader["image data"].GetArraySize() == 0)
            {
                streamedDataSize = reader["m_StreamData"]["size"].GetValue<int>();
            }
            else
            {
                streamedDataSize = 0;
            }
        }

        void IDisposable.Dispose()
        {
            m_InsertCommand.Dispose();
        }
    }
}
