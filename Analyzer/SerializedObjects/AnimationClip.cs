using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SerializedObjects;

public class AnimationClip
{
    public string Name { get; init; }
    public bool Legacy { get; init; }
    public int Events { get; init; }
    
    private AnimationClip() {}

    public static AnimationClip Read(RandomAccessReader reader)
    {
        return new AnimationClip()
        {
            Name = reader["m_Name"].GetValue<string>(),
            Legacy = reader["m_Legacy"].GetValue<bool>(),
            Events = reader["m_Events"].GetArraySize()
        };
    }
}