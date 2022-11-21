using System.Collections.Generic;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SerializedObjects;

public class AssetBundle
{   
    public string Name { get; init; }
    public IReadOnlyList<Asset> Assets { get; init; }
    public IReadOnlyList<PPtr> PreloadTable { get; init; }
    public bool IsSceneAssetBundle { get; init; }

    public class Asset
    {
        public string Name { get; init; }
        public PPtr PPtr { get; init; }
        public int PreloadIndex { get; init; }
        public int PreloadSize { get; init; }

        private Asset() {}

        public static Asset Read(RandomAccessReader reader)
        {
            return new Asset()
            {
                Name = reader["first"].GetValue<string>(),
                PPtr = PPtr.Read(reader["second"]["asset"]),
                PreloadIndex = reader["second"]["preloadIndex"].GetValue<int>(),
                PreloadSize = reader["second"]["preloadSize"].GetValue<int>()
            };
        }
    }
    
    private AssetBundle() {}
    
    public static AssetBundle Read(RandomAccessReader reader)
    {
        var name = reader["m_Name"].GetValue<string>();
        var assets = new List<Asset>(reader["m_Container"].GetArraySize());
        var preloadTable = new List<PPtr>(reader["m_PreloadTable"].GetArraySize());
        var isSceneAssetBundle = reader["m_IsStreamedSceneAssetBundle"].GetValue<bool>();
        
        foreach (var pptr in reader["m_PreloadTable"])
        {
            preloadTable.Add(PPtr.Read(pptr));
        }
        
        foreach (var asset in reader["m_Container"])
        {
            assets.Add(Asset.Read(asset));
        }

        return new AssetBundle()
        {
            Name = name,
            Assets = assets,
            PreloadTable = preloadTable,
            IsSceneAssetBundle = isSceneAssetBundle
        };
    }
}