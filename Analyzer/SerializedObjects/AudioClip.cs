using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SerializedObjects;

public class AudioClip
{
    public string Name { get; init; }
    public int StreamDataSize { get; init; }
    public int BitsPerSample { get; init; }
    public int Frequency { get; init; }
    public int Channels { get; init; }
    public int LoadType { get; init; }
    public int Format { get; init; }

    private AudioClip() {}

    public static AudioClip Read(RandomAccessReader reader)
    {
        return new AudioClip()
        {
            Name = reader["m_Name"].GetValue<string>(),
            Channels = reader["m_Channels"].GetValue<int>(),
            Format = reader["m_CompressionFormat"].GetValue<int>(),
            Frequency = reader["m_Frequency"].GetValue<int>(),
            LoadType = reader["m_LoadType"].GetValue<int>(),
            BitsPerSample = reader["m_BitsPerSample"].GetValue<int>(),
            StreamDataSize = reader["m_Resource"]["m_Size"].GetValue<int>()
        };
    }
}