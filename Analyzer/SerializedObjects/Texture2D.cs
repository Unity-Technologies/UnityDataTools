using UnityDataTools.FileSystem.TypeTreeReaders;
using System.Text.Json;
namespace UnityDataTools.Analyzer.SerializedObjects;

public class Texture2D
{
    public string Name { get; init; }
    public int StreamDataSize { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int Format { get; init; }
    public int MipCount { get; init; }
    public bool RwEnabled { get; init; }

    private Texture2D() {}
    
    public static Texture2D Read(RandomAccessReader reader)
    {
        return new Texture2D()
        {
            Name = reader["m_Name"].GetValue<string>(),
            Width = reader["m_Width"].GetValue<int>(),
            Height = reader["m_Height"].GetValue<int>(),
            Format = reader["m_TextureFormat"].GetValue<int>(),
            RwEnabled = reader["m_IsReadable"].GetValue<int>() != 0,
            MipCount = reader["m_MipCount"].GetValue<int>(),
            StreamDataSize = reader["image data"].GetArraySize() == 0 ? reader["m_StreamData"]["size"].GetValue<int>() : 0
        };
    }
}
