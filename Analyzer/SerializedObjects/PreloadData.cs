using System.Collections.Generic;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SerializedObjects;

public class PreloadData
{
    public IReadOnlyList<PPtr> Assets { get; init; }
    
    private PreloadData() {}

    public static PreloadData Read(RandomAccessReader reader)
    {
        var assets = new List<PPtr>(reader["m_Assets"].GetArraySize());
        
        foreach (var pptr in reader["m_Assets"])
        {
            assets.Add(PPtr.Read(pptr));
        }

        return new PreloadData()
        {
            Assets = assets
        };
    }
}
