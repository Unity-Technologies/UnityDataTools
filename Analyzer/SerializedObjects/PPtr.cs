using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SerializedObjects;

public class PPtr
{
    public int FileId { get; init; }
    public long PathId { get; init; }
    
    private PPtr() {}

    public static PPtr Read(RandomAccessReader reader)
    {
        return new PPtr()
        {
            FileId = reader["m_FileID"].GetValue<int>(),
            PathId = reader["m_PathID"].GetValue<long>()
        };
    }
}
