using System;
using Microsoft.Data.Sqlite;
using System.Text;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class MeshHandler : ISQLiteHandler
{
    SqliteCommand m_InsertCommand;

    public void Init(SqliteConnection db)
    {
        using var command = db.CreateCommand();
        command.CommandText = Properties.Resources.Mesh;
        command.ExecuteNonQuery();

        m_InsertCommand = db.CreateCommand();
        m_InsertCommand.CommandText = "INSERT INTO meshes(id, sub_meshes, blend_shapes, bones, indices, vertices, compression, rw_enabled, vertex_size, channels) VALUES(@id, @sub_meshes, @blend_shapes, @bones, @indices, @vertices, @compression, @rw_enabled, @vertex_size, @channels)";
        m_InsertCommand.Parameters.Add("@id", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@sub_meshes", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@blend_shapes", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@bones", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@indices", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@vertices", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@compression", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@rw_enabled", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@vertex_size", SqliteType.Integer);
        m_InsertCommand.Parameters.Add("@channels", SqliteType.Text);
    }

    public void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        var mesh = Mesh.Read(reader);
        m_InsertCommand.Transaction = ctx.Transaction;
        m_InsertCommand.Parameters["@id"].Value = objectId;
        m_InsertCommand.Parameters["@indices"].Value = mesh.Indices;
        m_InsertCommand.Parameters["@vertices"].Value = mesh.Vertices;
        m_InsertCommand.Parameters["@sub_meshes"].Value = mesh.SubMeshes;
        m_InsertCommand.Parameters["@blend_shapes"].Value = mesh.BlendShapes;
        m_InsertCommand.Parameters["@bones"].Value = mesh.Bones;
        m_InsertCommand.Parameters["@compression"].Value = mesh.Compression;
        m_InsertCommand.Parameters["@rw_enabled"].Value = mesh.RwEnabled;
        m_InsertCommand.Parameters["@vertex_size"].Value = mesh.VertexSize;

        StringBuilder channels = new StringBuilder();
        foreach (var channel in mesh.Channels)
        {
            channels.Append(channel.Usage.ToString());
            channels.Append(' ');
            channels.Append(channel.Type.ToString());
            channels.Append('[');
            channels.Append(channel.Dimension);
            channels.AppendLine("]");
        }
        
        m_InsertCommand.Parameters["@channels"].Value = channels.ToString();

        m_InsertCommand.ExecuteNonQuery();

        streamDataSize = mesh.StreamDataSize;
        name = mesh.Name;
    }

    public void Finalize(SqliteConnection db)
    {
    }

    void IDisposable.Dispose()
    {
        m_InsertCommand?.Dispose();
    }
}