using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityDataTools.Analyzer.Util;
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
    public List<BuildFile> Files { get; init; }

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
        var guidString = GuidHelper.FormatUnityGuid(guid0, guid1, guid2, guid3);

        // Convert build start time from ticks to ISO 8601 UTC format
        var startTimeTicks = summary["buildStartTime"]["ticks"].GetValue<long>();
        var startTime = new DateTime(startTimeTicks, DateTimeKind.Utc).ToString("o");

        // Convert ticks to seconds (TimeSpan.TicksPerSecond = 10,000,000)
        var totalTimeTicks = summary["totalTimeTicks"].GetValue<ulong>();
        var totalTimeSeconds = (int)Math.Round(totalTimeTicks / 10000000.0);

        var endTime = new DateTime(startTimeTicks + (long)totalTimeTicks, DateTimeKind.Utc).ToString("o");

        // Read the files array
        var filesList = new List<BuildFile>(reader["m_Files"].GetArraySize());
        foreach (var fileElement in reader["m_Files"])
        {
            filesList.Add(new BuildFile
            {
                Id = fileElement["id"].GetValue<uint>(),
                Path = fileElement["path"].GetValue<string>(),
                Role = fileElement["role"].GetValue<string>(),
                Size = fileElement["totalSize"].GetValue<ulong>()
            });
        }

        TrimCommonPathPrefix(filesList);

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
            BuildResult = GetBuildResultString(summary["buildResult"].GetValue<int>()),
            Files = filesList
        };
    }

    // Currently the file list has the absolute paths of the build output, but what we really want is the relative path.
    // This code reuses the approach taken in the build report inspector of automatically stripping off whatever part of the path
    // is repeated as the prefix on each file, which effectively finds the relative output path.
    static void TrimCommonPathPrefix(List<BuildFile> files)
    {
        if (files.Count == 0)
            return;
        string longestCommonRoot = files[0].Path;
        foreach (var file in files)
        {
            for (var i = 0; i < longestCommonRoot.Length && i < file.Path.Length; i++)
            {
                if (longestCommonRoot[i] == file.Path[i])
                    continue;
                longestCommonRoot = longestCommonRoot.Substring(0, i);
            }
        }

        foreach (var file in files)
        {
            file.Path = file.Path.Substring(longestCommonRoot.Length);
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

public class BuildFile
{
    public uint Id { get; init; }
    public string Path { get; set; }
    public string Role { get; init; }
    public ulong Size { get; init; }
}
