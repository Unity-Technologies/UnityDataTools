using System;
using System.Collections.Generic;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SerializedObjects;

public class Mesh
{
    public enum ChannelUsage
    {
        Vertex,
        Normal,
        Tangent,
        Color,
        TexCoord0,
        TexCoord1,
        TexCoord2,
        TexCoord3,
        TexCoord4,
        TexCoord5,
        TexCoord6,
        TexCoord7,
        BlendWeights,
        BlendIndices,
    };
    
    public enum ChannelType
    {
        Float,
        Float16,
        UNorm8,
        SNorm8,
        UNorm16,
        SNorm16,
        UInt8,
        SInt8,
        UInt16,
        SInt16,
        UInt32,
        SInt32,
    };

    public class Channel
    {
        public ChannelUsage Usage;
        public ChannelType Type;
        public int Dimension;
    }

    public string Name { get; init; }
    public int StreamDataSize { get; init; }
    public int SubMeshes { get; init; }
    public int BlendShapes { get; init; }
    public int Bones { get; init; }
    public int Indices { get; init; }
    public int Vertices { get; init; }
    public int Compression { get; init; }
    public bool RwEnabled { get; init; }
    
    public IReadOnlyList<Channel> Channels { get; init; }
    
    public int VertexSize { get; init; }
    
    private static readonly int[] s_ChannelTypeSizes =
    {
        4,  // Float
        2,  // Float16
        1,  // UNorm8
        1,  // SNorm8
        2,  // UNorm16
        2,  // SNorm16
        1,  // UInt8
        1,  // SInt8
        2,  // UInt16
        2,  // SInt16
        4,  // UInt32
        4,  // SInt32
    };

    private Mesh() {}

    public static Mesh Read(RandomAccessReader reader)
    {
        var name = reader["m_Name"].GetValue<string>();
        var compression = reader["m_MeshCompression"].GetValue<byte>();
        var channels = new List<Channel>();
        int indices;
        int vertices;
        int streamDataSize = 0;
        int vertexSize = 0;

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

            int i = 0;
            foreach (var channel in reader["m_VertexData"]["m_Channels"])
            {
                int dimension = channel["dimension"].GetValue<byte>();

                if (dimension != 0)
                {
                    // The dimension can be padded. In that case, the real dimension
                    // is encoded in the top nibble.
                    int originalDim = (dimension >> 4) & 0xF;
                    if (originalDim != 0)
                    {
                        dimension = originalDim;
                    }

                    var c = new Channel()
                    {
                        Dimension = dimension,
                        Type = (ChannelType)channel["format"].GetValue<byte>(),
                        Usage = (ChannelUsage)i,
                    };
                    
                    channels.Add(c);
                    vertexSize += dimension * s_ChannelTypeSizes[(int)c.Type];
                }

                ++i;
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
            RwEnabled = reader["m_IsReadable"].GetValue<int>() != 0,
            Channels = channels,
            VertexSize = vertexSize,
        };
    }
}