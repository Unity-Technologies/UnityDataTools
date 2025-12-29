using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SerializedObjects;

public class BuildReport
{
    public string Name { get; init; }
    public string BuildGuid { get; init; }
    public string PlatformName { get; init; }
    public int Subtarget { get; init; }
    public int Options { get; init; }
    public int AssetBundleOptions { get; init; }
    public string OutputPath { get; init; }
    public uint Crc { get; init; }
    public ulong TotalSize { get; init; }
    public ulong TotalTimeTicks { get; init; }
    public int TotalErrors { get; init; }
    public int TotalWarnings { get; init; }
    public int BuildType { get; init; }
    public int BuildResult { get; init; }

    private BuildReport() { }

    public static BuildReport Read(RandomAccessReader reader)
    {
        var summary = reader["m_Summary"];

        // Read the GUID (4 unsigned ints)
        var guidData = summary["buildGUID"];
        var guid0 = guidData["data[0]"].GetValue<uint>();
        var guid1 = guidData["data[1]"].GetValue<uint>();
        var guid2 = guidData["data[2]"].GetValue<uint>();
        var guid3 = guidData["data[3]"].GetValue<uint>();
        var guidString = $"{guid0:x8}{guid1:x8}{guid2:x8}{guid3:x8}";

        return new BuildReport()
        {
            Name = reader["m_Name"].GetValue<string>(),
            BuildGuid = guidString,
            PlatformName = summary["platformName"].GetValue<string>(),
            Subtarget = summary["subtarget"].GetValue<int>(),
            Options = summary["options"].GetValue<int>(),
            AssetBundleOptions = summary["assetBundleOptions"].GetValue<int>(),
            OutputPath = summary["outputPath"].GetValue<string>(),
            Crc = summary["crc"].GetValue<uint>(),
            TotalSize = summary["totalSize"].GetValue<ulong>(),
            TotalTimeTicks = summary["totalTimeTicks"].GetValue<ulong>(),
            TotalErrors = summary["totalErrors"].GetValue<int>(),
            TotalWarnings = summary["totalWarnings"].GetValue<int>(),
            BuildType = summary["buildType"].GetValue<int>(),
            BuildResult = summary["buildResult"].GetValue<int>()
        };
    }

    public static string GetBuildTypeString(int buildType)
    {
        return buildType switch
        {
            1 => "Player",
            2 => "AssetBundle",
            3 => "Player, AssetBundle",
            _ => buildType.ToString()
        };
    }
}
