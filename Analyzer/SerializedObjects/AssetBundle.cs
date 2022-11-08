using System.Collections.Generic;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SerializedObjects;

public class AssetBundle
{   
    public string Name { get; init; }
    public IReadOnlyList<Asset> Assets { get; init; }

    public class Asset
    {
        public string Name { get; init; }
        public PPtr PPtr { get; init; }

        private Asset() {}

        public static Asset Read(RandomAccessReader reader)
        {
            return new Asset()
            {
                Name = reader["first"].GetValue<string>(),
                PPtr = PPtr.Read(reader["second"]["asset"])
            };
        }
    }
    
    private AssetBundle() {}
     
    public static AssetBundle Read(RandomAccessReader reader)
    {
        var name = reader["m_Name"].GetValue<string>();
        var assets = new List<Asset>(reader["m_Container"].GetArraySize());
        
        foreach (var asset in reader["m_Container"])
        {
            assets.Add(Asset.Read(asset));
        }

        return new AssetBundle()
        {
            Name = name,
            Assets = assets
        };
    }
}