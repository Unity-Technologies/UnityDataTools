using System.Collections.Generic;
using UnityDataTools.Analyzer.Util;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SerializedObjects;

public class PackedAssets
{
    public string Path { get; init; }
    public ulong FileHeaderSize { get; init; }
    public List<PackedAssetInfo> Contents { get; init; }

    private PackedAssets() { }

    public static PackedAssets Read(RandomAccessReader reader)
    {
        var path = reader["m_ShortPath"].GetValue<string>();
        var fileHeaderSize = reader["m_Overhead"].GetValue<ulong>();

        var contentsList = new List<PackedAssetInfo>(reader["m_Contents"].GetArraySize());

        foreach (var element in reader["m_Contents"])
        {
            // Read GUID (4 unsigned ints)
            var guidData = element["sourceAssetGUID"];
            var guid0 = guidData["data[0]"].GetValue<uint>();
            var guid1 = guidData["data[1]"].GetValue<uint>();
            var guid2 = guidData["data[2]"].GetValue<uint>();
            var guid3 = guidData["data[3]"].GetValue<uint>();
            var guidString = GuidHelper.FormatUnityGuid(guid0, guid1, guid2, guid3);

            contentsList.Add(new PackedAssetInfo
            {
                ObjectID = element["fileID"].GetValue<long>(),
                Type = element["classID"].GetValue<int>(),
                Size = element["packedSize"].GetValue<ulong>(),
                Offset = element["offset"].GetValue<ulong>(),
                SourceAssetGUID = guidString,
                BuildTimeAssetPath = element["buildTimeAssetPath"].GetValue<string>()
            });
        }

        return new PackedAssets()
        {
            Path = path,
            FileHeaderSize = fileHeaderSize,
            Contents = contentsList
        };
    }
}

public class PackedAssetInfo
{
    public long ObjectID { get; init; }
    public int Type { get; init; }
    public ulong Size { get; init; }
    public ulong Offset { get; init; }
    public string SourceAssetGUID { get; init; }
    public string BuildTimeAssetPath { get; init; }
}

