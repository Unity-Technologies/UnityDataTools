using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SerializedObjects;

public class Mesh
{
    public string Name { get; init; }
    public int StreamDataSize { get; init; }
    public int SubMeshes { get; init; }
    public int BlendShapes { get; init; }
    public int Bones { get; init; }
    public int Indices { get; init; }
    public int Vertices { get; init; }
    public int Compression { get; init; }
    public bool RwEnabled { get; init; }

    private Mesh() {}

    public static Mesh Read(RandomAccessReader reader)
    {
        var name = reader["m_Name"].GetValue<string>();
        var compression = reader["m_MeshCompression"].GetValue<byte>();
        int indices;
        int vertices;
        int streamDataSize = 0;

        if (compression == 0)
        {
            var bytesPerIndex = reader["m_IndexFormat"].GetValue<int>() == 0 ? 2 : 4;

            indices = reader["m_IndexBuffer"].GetArraySize() / bytesPerIndex;
            vertices = reader["m_VertexData"]["m_VertexCount"].GetValue<int>();
                
            // If vertex data size is 0, data is stored in a stream file.
            if (reader["m_VertexData"]["m_DataSize"].GetArraySize() == 0)
            {
                streamDataSize = reader["m_StreamData"]["size"].GetValue<int>();
            }
        }
        else
        {
            vertices = reader["m_CompressedMesh"]["m_Vertices"]["m_NumItems"].GetValue<int>() / 3;
            indices = reader["m_CompressedMesh"]["m_Triangles"]["m_NumItems"].GetValue<int>();
        }
        
        return new Mesh()
        {
            Name = name,
            Vertices = vertices,
            Indices = indices,
            StreamDataSize = streamDataSize,
            SubMeshes = reader["m_SubMeshes"].GetArraySize(),
            BlendShapes = reader["m_Shapes"]["shapes"].GetArraySize(),
            Bones = reader["m_BoneNameHashes"].GetArraySize(),
            RwEnabled = reader["m_IsReadable"].GetValue<int>() != 0
        };
    }
}