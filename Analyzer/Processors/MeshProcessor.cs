using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.Processors
{
    public class MeshProcessor : IProcessor, IDisposable
    {
        SQLiteCommand m_InsertCommand;

        public void Init(SQLiteConnection db)
        {
            using var command = new SQLiteCommand(db);

            command.CommandText = Properties.Resources.Mesh;
            command.ExecuteNonQuery();

            m_InsertCommand = new SQLiteCommand(db);
            m_InsertCommand.CommandText = "INSERT INTO meshes(id, sub_meshes, blend_shapes, bones, indices, vertices, compression, rw_enabled) VALUES(@id, @sub_meshes, @blend_shapes, @bones, @indices, @vertices, @compression, @rw_enabled)";
            m_InsertCommand.Parameters.Add("@id", DbType.Int64);
            m_InsertCommand.Parameters.Add("@sub_meshes", DbType.Int32);
            m_InsertCommand.Parameters.Add("@blend_shapes", DbType.Int32);
            m_InsertCommand.Parameters.Add("@bones", DbType.Int32);
            m_InsertCommand.Parameters.Add("@indices", DbType.Int32);
            m_InsertCommand.Parameters.Add("@vertices", DbType.Int32);
            m_InsertCommand.Parameters.Add("@compression", DbType.Int32);
            m_InsertCommand.Parameters.Add("@rw_enabled", DbType.Int32);
        }

        public void Process(AnalyzerTool analyzer, long objectId, Dictionary<int, int> localToDbFileId, RandomAccessReader reader, out string name, out long streamedDataSize)
        {
            streamedDataSize = 0;

            var compression = reader["m_MeshCompression"].GetValue<byte>();

            if (compression == 0)
            {
                var bytesPerIndex = reader["m_IndexFormat"].GetValue<int>() == 0 ? 2 : 4;

                m_InsertCommand.Parameters["@indices"].Value = reader["m_IndexBuffer"].GetArraySize() / bytesPerIndex;
                m_InsertCommand.Parameters["@vertices"].Value = reader["m_VertexData"]["m_VertexCount"].GetValue<int>();
                
                // If vertex data size is 0, data is stored in a stream file.
                if (reader["m_VertexData"]["m_DataSize"].GetArraySize() == 0)
                {
                    streamedDataSize = reader["m_StreamData"]["size"].GetValue<int>();
                }
            }
            else
            {
                m_InsertCommand.Parameters["@indices"].Value = reader["m_CompressedMesh"]["m_Vertices"]["m_NumItems"].GetValue<int>() / 3;
                m_InsertCommand.Parameters["@indices"].Value = reader["m_CompressedMesh"]["m_Triangles"]["m_NumItems"].GetValue<int>();
            }
            
            m_InsertCommand.Parameters["@id"].Value = objectId;
            m_InsertCommand.Parameters["@sub_meshes"].Value = reader["m_SubMeshes"].GetArraySize();
            m_InsertCommand.Parameters["@blend_shapes"].Value = reader["m_Shapes"]["shapes"].GetArraySize();
            m_InsertCommand.Parameters["@bones"].Value = reader["m_BoneNameHashes"].GetArraySize();
            m_InsertCommand.Parameters["@compression"].Value = compression;
            m_InsertCommand.Parameters["@rw_enabled"].Value = reader["m_IsReadable"].GetValue<int>();

            m_InsertCommand.ExecuteNonQuery();

            name = reader["m_Name"].GetValue<string>();
        }

        void IDisposable.Dispose()
        {
            m_InsertCommand.Dispose();
        }
    }
}
