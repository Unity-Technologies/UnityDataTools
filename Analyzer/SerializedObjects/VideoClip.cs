using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SerializedObjects;

public class VideoClip
{
    public string Name { get; init; }
    public string OriginalPath { get; init; }
    public long StreamDataSize { get; init; }
    public uint Width { get; init; }
    public uint Height { get; init; }
    public double FrameRate { get; init; }
    public ulong FrameCount { get; init; }
    public int AudioTracks { get; init; }

    private VideoClip() { }

    public static VideoClip Read(RandomAccessReader reader)
    {
        return new VideoClip()
        {
            Name = reader["m_Name"].GetValue<string>(),
            OriginalPath = reader["m_OriginalPath"].GetValue<string>(),
            Width = reader["Width"].GetValue<uint>(),
            Height = reader["Height"].GetValue<uint>(),
            FrameRate = reader["m_FrameRate"].GetValue<double>(),
            FrameCount = reader["m_FrameCount"].GetValue<ulong>(),
            // One entry per audio track; the clip's audio is stored with its video data.
            AudioTracks = reader["m_AudioChannelCount"].GetArraySize(),
            StreamDataSize = (long)reader["m_ExternalResources"]["m_Size"].GetValue<ulong>()
        };
    }
}
