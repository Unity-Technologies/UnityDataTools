using System;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SerializedObjects;

public class BuildReport
{
    public string Name { get; init; }
    public string BuildGuid { get; init; }
    public string PlatformName { get; init; }
    public int Subtarget { get; init; }
    public string StartTime { get; init; }
    public string EndTime { get; init; }
    public int Options { get; init; }
    public int AssetBundleOptions { get; init; }
    public string OutputPath { get; init; }
    public uint Crc { get; init; }
    public ulong TotalSize { get; init; }
    public int TotalTimeSeconds { get; init; }
    public int TotalErrors { get; init; }
    public int TotalWarnings { get; init; }
    public int BuildType { get; init; }
    public string BuildResult { get; init; }

    private BuildReport() { }

    public static BuildReport Read(RandomAccessReader reader)
    {
        var summary = reader["m_Summary"];

        // Read the GUID (4 unsigned ints)
        // Unity's GUID format reverses nibbles within each uint32
        var guidData = summary["buildGUID"];
        var guid0 = guidData["data[0]"].GetValue<uint>();
        var guid1 = guidData["data[1]"].GetValue<uint>();
        var guid2 = guidData["data[2]"].GetValue<uint>();
        var guid3 = guidData["data[3]"].GetValue<uint>();
        var guidString = FormatUnityGuid(guid0, guid1, guid2, guid3);

        // Convert build start time from ticks to ISO 8601 UTC format
        var startTimeTicks = summary["buildStartTime"]["ticks"].GetValue<long>();
        var startTime = new DateTime(startTimeTicks, DateTimeKind.Utc).ToString("o");

        // Convert ticks to seconds (TimeSpan.TicksPerSecond = 10,000,000)
        var totalTimeTicks = summary["totalTimeTicks"].GetValue<ulong>();
        var totalTimeSeconds = (int)Math.Round(totalTimeTicks / 10000000.0);

        var endTime = new DateTime(startTimeTicks + (long)totalTimeTicks, DateTimeKind.Utc).ToString("o");

        return new BuildReport()
        {
            Name = reader["m_Name"].GetValue<string>(),
            BuildGuid = guidString,
            PlatformName = summary["platformName"].GetValue<string>(),
            Subtarget = summary["subtarget"].GetValue<int>(),
            StartTime = startTime,
            EndTime = endTime,
            Options = summary["options"].GetValue<int>(),
            AssetBundleOptions = summary["assetBundleOptions"].GetValue<int>(),
            OutputPath = summary["outputPath"].GetValue<string>(),
            Crc = summary["crc"].GetValue<uint>(),
            TotalSize = summary["totalSize"].GetValue<ulong>(),
            TotalTimeSeconds = totalTimeSeconds,
            TotalErrors = summary["totalErrors"].GetValue<int>(),
            TotalWarnings = summary["totalWarnings"].GetValue<int>(),
            BuildType = summary["buildType"].GetValue<int>(),
            BuildResult = GetBuildResultString(summary["buildResult"].GetValue<int>())
        };
    }

    // Converts Unity GUID data array to string format matching Unity's GUIDToString function.
    // Unity stores GUIDs as 4 uint32 values and converts them to a 32-character hex string
    // with a specific byte ordering that differs from standard GUID/UUID formatting.
    // Example: data[0]=3856716653 (0xe60765cd) becomes "d63d0e5e"
    private static string FormatUnityGuid(uint data0, uint data1, uint data2, uint data3)
    {
        char[] result = new char[32];
        FormatUInt32Reversed(data0, result, 0);
        FormatUInt32Reversed(data1, result, 8);
        FormatUInt32Reversed(data2, result, 16);
        FormatUInt32Reversed(data3, result, 24);
        return new string(result);
    }

    // Formats a uint32 as 8 hex digits matching Unity's GUIDToString logic.
    // Unity's implementation extracts nibbles from most significant to least significant
    // (j=7 down to j=0) and writes them to output positions in the same order (offset+7 to offset+0),
    // which reverses the byte order compared to standard hex formatting.
    // For example: 0xe60765cd becomes "d63d0e5e" (bytes reversed: cd,65,07,e6 → e6,07,65,cd)
    private static void FormatUInt32Reversed(uint value, char[] output, int offset)
    {
        const string hexChars = "0123456789abcdef";
        for (int j = 7; j >= 0; j--)
        {
            uint nibble = (value >> (j * 4)) & 0xF;
            output[offset + j] = hexChars[(int)nibble];
        }
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

    public static string GetBuildResultString(int buildResult)
    {
        return buildResult switch
        {
            0 => "Unknown",
            1 => "Succeeded",
            2 => "Failed",
            3 => "Cancelled",
            4 => "Pending",
            _ => buildResult.ToString()
        };
    }
}
