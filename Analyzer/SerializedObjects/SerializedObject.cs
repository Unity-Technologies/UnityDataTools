using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SerializedObjects;

public abstract class SerializedObject
{
    protected SerializedObject() {}
    protected SerializedObject(RandomAccessReader reader) {}
}
