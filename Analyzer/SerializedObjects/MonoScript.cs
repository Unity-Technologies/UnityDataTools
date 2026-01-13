using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SerializedObjects;

public class MonoScript
{
    public string ClassName { get; init; }
    public string Namespace { get; init; }
    public string AssemblyName { get; init; }

    private MonoScript() { }

    public static MonoScript Read(RandomAccessReader reader)
    {
        return new MonoScript()
        {
            ClassName = reader["m_ClassName"].GetValue<string>(),
            Namespace = reader["m_Namespace"].GetValue<string>(),
            AssemblyName = reader["m_AssemblyName"].GetValue<string>()
        };
    }
}
