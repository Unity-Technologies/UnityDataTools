using System.Collections.Generic;
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
    public string AudioLanguages { get; init; }

    private VideoClip() { }

    public static VideoClip Read(RandomAccessReader reader)
    {
        // The audio tracks are described by parallel per-track arrays (m_AudioChannelCount,
        // m_AudioSampleRate, m_AudioLanguage). The audio itself is stored with the video data.
        var languages = new List<string>();
        foreach (var language in reader["m_AudioLanguage"])
            languages.Add(language.GetValue<string>());

        return new VideoClip()
        {
            Name = reader["m_Name"].GetValue<string>(),
            OriginalPath = reader["m_OriginalPath"].GetValue<string>(),
            Width = reader["Width"].GetValue<uint>(),
            Height = reader["Height"].GetValue<uint>(),
            FrameRate = reader["m_FrameRate"].GetValue<double>(),
            FrameCount = reader["m_FrameCount"].GetValue<ulong>(),
            AudioTracks = languages.Count,
            AudioLanguages = string.Join(",", languages),
            StreamDataSize = (long)reader["m_ExternalResources"]["m_Size"].GetValue<ulong>()
        };
    }
}
