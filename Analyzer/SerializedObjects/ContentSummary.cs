using System.Collections.Generic;
using UnityDataTools.BinaryFormat;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SerializedObjects;

// A high level summary of the content included in a build, introduced in Unity 6.6. It carries
// cross-build totals plus per-type and per-source-asset breakdowns. See
// https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Build.Reporting.ContentSummary.html
public class ContentSummary
{
    public ulong SerializedFileSize { get; init; }
    public ulong ReusedSerializedFileSize { get; init; }
    public ulong ResourceDataSize { get; init; }
    public ulong HeaderSize { get; init; }
    public int SerializedFileCount { get; init; }
    public int ReusedSerializedFileCount { get; init; }
    public int ResourceFileCount { get; init; }
    public int ObjectCount { get; init; }
    public List<TypeStat> TypeStats { get; init; }
    public List<AssetStat> AssetStats { get; init; }

    private ContentSummary() { }

    public static ContentSummary Read(RandomAccessReader reader)
    {
        var typeStats = new List<TypeStat>(reader["m_typeStatsList"].GetArraySize());
        foreach (var element in reader["m_typeStatsList"])
        {
            typeStats.Add(new TypeStat
            {
                Type = element["classID"].GetValue<int>(),
                Size = element["size"].GetValue<ulong>(),
                ObjectCount = element["objectCount"].GetValue<int>(),
                ResourceCount = element["resourceCount"].GetValue<int>()
            });
        }

        var assetStats = new List<AssetStat>(reader["m_assetStatsList"].GetArraySize());
        foreach (var element in reader["m_assetStatsList"])
        {
            var guidData = element["sourceAssetGUID"];
            var guidString = GuidHelper.FormatUnityGuid(
                guidData["data[0]"].GetValue<uint>(),
                guidData["data[1]"].GetValue<uint>(),
                guidData["data[2]"].GetValue<uint>(),
                guidData["data[3]"].GetValue<uint>());

            assetStats.Add(new AssetStat
            {
                SourceAssetGUID = guidString,
                SourceAssetPath = element["sourceAssetPath"].GetValue<string>(),
                Size = element["size"].GetValue<ulong>(),
                ObjectCount = element["objectCount"].GetValue<int>(),
                ResourceCount = element["resourceCount"].GetValue<int>()
            });
        }

        return new ContentSummary()
        {
            SerializedFileSize = reader["m_serializedFileSize"].GetValue<ulong>(),
            ReusedSerializedFileSize = reader["m_reusedSerializedFileSize"].GetValue<ulong>(),
            ResourceDataSize = reader["m_resourceDataSize"].GetValue<ulong>(),
            HeaderSize = reader["m_headerSize"].GetValue<ulong>(),
            SerializedFileCount = reader["m_serializedFileCount"].GetValue<int>(),
            ReusedSerializedFileCount = reader["m_reusedSerializedFileCount"].GetValue<int>(),
            ResourceFileCount = reader["m_resourceFileCount"].GetValue<int>(),
            ObjectCount = reader["m_objectCount"].GetValue<int>(),
            TypeStats = typeStats,
            AssetStats = assetStats
        };
    }
}

public class TypeStat
{
    public int Type { get; init; }
    public ulong Size { get; init; }
    public int ObjectCount { get; init; }
    public int ResourceCount { get; init; }
}

public class AssetStat
{
    public string SourceAssetGUID { get; init; }
    public string SourceAssetPath { get; init; }
    public ulong Size { get; init; }
    public int ObjectCount { get; init; }
    public int ResourceCount { get; init; }
}
