using System;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SerializedObjects;

public class VideoClip
{
    public string Name { get; init; }
    public double FrameRate { get; init; }
    public uint Width { get; init; }
    public uint Height { get; init; }
    public UInt64 FrameCount { get; init; }
    public long StreamDataSize { get; init; }
    
    private VideoClip() {}

    public static VideoClip Read(RandomAccessReader reader)
    {
        return new VideoClip()
        {
            Name = reader["m_Name"].GetValue<string>(),
            FrameRate = reader["m_FrameRate"].GetValue<double>(),
            Width = reader["Width"].GetValue<uint>(),
            Height = reader["Height"].GetValue<uint>(),
            FrameCount = reader["m_FrameCount"].GetValue<UInt64>(),
            StreamDataSize = reader["m_ExternalResources"]["m_Size"].GetValue<long>()
        };
    }
}