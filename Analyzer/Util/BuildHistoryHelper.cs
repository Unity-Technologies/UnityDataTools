using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace UnityDataTools.Analyzer.Util;

// Identifies the ContentDirectory build present in the analyze input and locates its folder
// inside a build history (Library/BuildHistory, one folder per build). The folder of a build is
// found by matching the BuildManifestHash of its ContentLayout.json against the build's
// BuildManifestHash.txt. Pure file lookup; no knowledge of the parsers or the database.
public static class BuildHistoryHelper
{
    public const string HashFileName = "BuildManifestHash.txt";
    public const string LayoutFileName = "ContentLayout.json";
    public const string SummaryFileName = "BuildReportSummary.json";

    // Reads the BuildManifestHash.txt values identifying the ContentDirectory build(s) in the
    // input files: taken from the input itself when present, otherwise picked up next to the
    // .cf/.archive content files (the hash file is not always on the input, e.g. when specific
    // files are passed). More than one distinct hash means more than one build.
    public static List<string> FindBuildHashes(IEnumerable<string> files)
    {
        var fileList = files.ToList();

        var hashFiles = fileList
            .Where(f => string.Equals(Path.GetFileName(f), HashFileName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (hashFiles.Count == 0)
        {
            var contentDirectories = fileList
                .Where(f => HasExtension(f, ".cf") || HasExtension(f, ".archive"))
                .Select(f => Path.GetDirectoryName(Path.GetFullPath(f)))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            hashFiles.AddRange(contentDirectories
                .Select(dir => Path.Combine(dir, HashFileName))
                .Where(File.Exists));
        }

        return hashFiles.Select(f => File.ReadAllText(f).Trim()).Distinct().ToList();
    }

    // Fallback signal used when no BuildManifestHash.txt is available: a .cf file marks
    // ContentDirectory output. This is only a rough sanity check — the reverse does not hold, as
    // content files can be built without the extension or sit inside archives.
    public static bool HasContentFiles(IEnumerable<string> files)
    {
        return files.Any(f => HasExtension(f, ".cf"));
    }

    // Searches the history root and its direct child folders for the ContentLayout.json whose
    // BuildManifestHash matches the analyzed build. A build history is flat, so there is no
    // recursion — which also keeps the scan bounded if a large unrelated folder is passed. When
    // several folders match (rebuilds of identical content), the most recent build wins.
    // Returns the containing folder, or null when nothing matches.
    public static string LocateBuildFolder(string historyRoot, string buildHash)
    {
        var matches = Directory.EnumerateDirectories(historyRoot)
            .Prepend(historyRoot)
            .Where(dir => File.Exists(Path.Combine(dir, LayoutFileName)))
            .Where(dir => TryReadBuildManifestHash(Path.Combine(dir, LayoutFileName)) == buildHash);

        string selected = null;
        var selectedTime = default(DateTime);
        foreach (var folder in matches)
        {
            var startTime = TryReadBuildStartTime(folder);
            if (selected == null || startTime > selectedTime)
            {
                selected = folder;
                selectedTime = startTime;
            }
        }

        return selected;
    }

    // The subset of BuildReportSummary.json needed here.
    private class BuildReportSummary
    {
        public DateTime BuildStartedAt { get; set; }
    }

    // Reads the start time of the build from the BuildReportSummary.json of a build history
    // folder. The file is expected to always be present; returns a zero timestamp when it is
    // missing or unreadable.
    public static DateTime TryReadBuildStartTime(string buildFolder)
    {
        try
        {
            var json = File.ReadAllText(Path.Combine(buildFolder, SummaryFileName));
            var summary = JsonConvert.DeserializeObject<BuildReportSummary>(json);
            return summary?.BuildStartedAt ?? default;
        }
        catch (Exception)
        {
            return default;
        }
    }

    // The files of a build history folder that analyze imports: the layout and the build report.
    public static List<string> CollectBuildFiles(string buildFolder)
    {
        var files = new List<string> { Path.Combine(buildFolder, LayoutFileName) };
        files.AddRange(Directory.EnumerateFiles(buildFolder, "*.buildreport"));
        return files;
    }

    // Reads the top-level BuildManifestHash of a ContentLayout.json without parsing the whole
    // file (layouts of large builds are big; the hash is one of the first properties). Returns
    // null when the value cannot be found or the file is not valid json.
    public static string TryReadBuildManifestHash(string contentLayoutPath)
    {
        try
        {
            using var reader = new JsonTextReader(File.OpenText(contentLayoutPath));

            for (int i = 0; i < 64 && reader.Read(); ++i)
            {
                if (reader.TokenType == JsonToken.PropertyName && reader.Depth == 1 &&
                    "BuildManifestHash".Equals(reader.Value))
                {
                    return reader.ReadAsString();
                }
            }
        }
        catch (Exception)
        {
        }

        return null;
    }

    static bool HasExtension(string path, string extension)
    {
        return string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase);
    }
}
