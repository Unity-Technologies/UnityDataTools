using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityDataTools.Analyzer.SQLite.Handlers;
using UnityDataTools.Analyzer.SQLite.Parsers;
using UnityDataTools.Analyzer.SQLite.Writers;
using UnityDataTools.Analyzer.Util;
using UnityDataTools.FileSystem;
using UnityDataTools.Models;

namespace UnityDataTools.Analyzer;

public class AnalyzerTool
{
    AnalyzeOptions m_Options;

    // Shared between the ContentLayout import and the serialized-file analysis: both must agree
    // on the id assigned to each serialized file name, and the layout's dependency information
    // is what resolves the external references of ContentDirectory files (issue #99).
    private IdProvider<string> m_SerializedFileIdProvider = new();
    private ContentFileDependencyMap m_ContentFileDependencies = new();

    public List<ISQLiteFileParser> parsers;

    public AnalyzerTool()
    {
        parsers = new List<ISQLiteFileParser>()
        {
            new ContentLayoutParser(m_SerializedFileIdProvider, m_ContentFileDependencies),
            new AddressablesBuildLayoutParser(),
            new SerializedFileParser(m_SerializedFileIdProvider, m_ContentFileDependencies),
        };
    }

    public class AnalyzeOptions
    {
        // Each entry is a file or a directory. Directories are scanned using SearchPattern and
        // NoRecursion; files are always included regardless of SearchPattern.
        public IReadOnlyList<string> Paths { get; init; }
        public string DatabaseName { get; init; }
        public string SearchPattern { get; init; } = "*";
        public bool SkipReferences { get; init; }
        public bool SkipCrc { get; init; }
        public bool Verbose { get; init; }
        public bool NoRecursion { get; init; }
        // Build history folder (e.g. Library/BuildHistory); when set, the folder of the analyzed
        // build is located in it and its ContentLayout.json and build report join the input.
        public string BuildHistoryPath { get; init; }
    }

    public int Analyze(AnalyzeOptions options)
    {
        m_Options = options;

        var files = CollectFiles();

        // Validate the ContentDirectory-related inputs before creating the database, so an
        // invalid combination fails without leaving a partial database behind.
        if (!PrepareContentDirectoryInputs(files))
        {
            return 1;
        }

        using SQLiteWriter writer = new(m_Options.DatabaseName);

        try
        {
            writer.Begin();
            foreach (var parser in parsers)
            {
                parser.Verbose = m_Options.Verbose;
                parser.SkipReferences = m_Options.SkipReferences;
                parser.SkipCrc = m_Options.SkipCrc;
                parser.Init(writer.Connection);

            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Error creating database: {e.Message}");
            return 1;
        }

        var timer = new Stopwatch();
        timer.Start();

        int countFailures = 0;
        int countSuccess = 0;
        int countIgnored = 0;
        int countNoTypeTrees = 0;
        int i = 1;
        foreach (var (file, displayRoot) in files)
        {
            var relativePath = Path.GetRelativePath(displayRoot, file);
            bool foundParser = false;
            foreach (var parser in parsers)
            {
                if (parser.CanParse(file))
                {
                    foundParser = true;
                    try
                    {
                        parser.Parse(file);
                        ReportProgress(relativePath, i, files.Count);
                        countSuccess++;
                    }
                    catch (SerializedFileOpenException e) when (e.MissingTypeTrees)
                    {
                        // The file has no TypeTrees and was rejected before opening. This is an
                        // expected, distinct outcome — reported and counted separately so a large
                        // run can tell these apart from genuine failures.
                        EraseProgressLine();
                        Console.Error.WriteLine($"Skipped (no TypeTrees): {relativePath}");
                        countNoTypeTrees++;
                    }
                    catch (SerializedFileOpenException)
                    {
                        // Expected failure — the file content could not be parsed.
                        // Don't print a stack trace; it adds no value for this known failure mode.
                        EraseProgressLine();
                        Console.Error.WriteLine($"Failed to open: {relativePath}");
                        countFailures++;
                    }
                    catch (AnalyzeDuplicateException e)
                    {
                        // A file or archive with this name was already analyzed. Only a single build
                        // can be analyzed at a time; print a clear one-line message (always visible,
                        // not just with -v) and continue, counting this file as failed.
                        EraseProgressLine();
                        Console.Error.WriteLine($"Skipping {relativePath}: {e.Message}");
                        countFailures++;
                    }
                    catch (Exception e)
                    {
                        // Unexpected failure (SQL error, I/O error, bug, etc.) — print full details.
                        EraseProgressLine();
                        Console.Error.WriteLine($"Failed to process: {relativePath}");
                        if (m_Options.Verbose)
                        {
                            Console.Error.WriteLine($"  Exception: {e.GetType().Name}: {e.Message}");
                            if (e.InnerException != null)
                                Console.Error.WriteLine($"  Inner: {e.InnerException.Message}");
                            Console.Error.WriteLine(e.StackTrace);
                        }
                        countFailures++;
                    }
                }
            }
            if (!foundParser)
            {
                if (m_Options.Verbose)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Ignoring {relativePath}");
                }

                countIgnored++;
            }
            ++i;
        }

        Console.WriteLine();
        Console.WriteLine($"Finalizing database. Successfully processed files: {countSuccess}, Failed files: {countFailures}, Files without TypeTrees: {countNoTypeTrees}, Ignored files: {countIgnored}");

        // Record data that can only be determined once every file has been processed (e.g. which
        // referenced objects were never resolved) before the database is finalized.
        foreach (var parser in parsers)
        {
            parser.FinalizeDatabase();
        }

        writer.End();
        foreach (var parser in parsers)
        {
            parser.Dispose();
        }

        timer.Stop();
        Console.WriteLine();
        Console.WriteLine($"Total time: {(timer.Elapsed.TotalMilliseconds / 1000.0):F3} s");

        return 0;
    }

    // Validates the ContentDirectory-related inputs and prepares the file list (issue #99):
    // enforces that a single build is analyzed, adds the files of the matching build history
    // folder when --build-history is used, selects the ContentLayout.json whose
    // BuildManifestHash matches that build (dropping any others), and moves it to the front of
    // the list so it is imported before the content files whose references it resolves. Returns
    // false, after printing an error, when the input combination is invalid.
    bool PrepareContentDirectoryInputs(List<(string FullPath, string DisplayRoot)> files)
    {
        List<string> buildHashes;
        try
        {
            buildHashes = BuildHistoryHelper.FindBuildHashes(files.Select(f => f.FullPath));
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Error reading {BuildHistoryHelper.HashFileName}: {e.Message}");
            return false;
        }

        if (buildHashes.Count > 1)
        {
            Console.Error.WriteLine("The input contains more than one ContentDirectory build (different BuildManifestHash.txt values). Analyze a single build at a time.");
            return false;
        }

        var buildHash = buildHashes.Count == 1 ? buildHashes[0] : null;

        if (m_Options.BuildHistoryPath != null && !AddBuildHistoryFiles(buildHash, files))
        {
            return false;
        }

        var layoutCandidates = files.Where(f => ContentLayoutParser.IsContentLayoutFile(f.FullPath)).ToList();
        var hasContentDirectory = buildHash != null || BuildHistoryHelper.HasContentFiles(files.Select(f => f.FullPath));

        if (layoutCandidates.Count == 0)
        {
            if (hasContentDirectory)
            {
                Console.Error.WriteLine(
                    "Warning: analyzing ContentDirectory output without its ContentLayout.json. The analysis will be incomplete: " +
                    "references between content files cannot be resolved (they will appear in dangling_refs) and source asset " +
                    "information is unavailable. Re-run with the build's ContentLayout.json (found in its build report folder) " +
                    "included in the input paths.");
            }

            return true;
        }

        (string FullPath, string DisplayRoot) selected;

        if (hasContentDirectory)
        {
            if (buildHash == null)
            {
                Console.Error.WriteLine("A ContentLayout.json is in the input but no BuildManifestHash.txt was found for the ContentDirectory content, so the layout cannot be validated against the build.");
                return false;
            }

            // The hash match guarantees the layout describes exactly this build; a stale or
            // unrelated layout would silently produce misleading results.
            selected = layoutCandidates.FirstOrDefault(c => BuildHistoryHelper.TryReadBuildManifestHash(c.FullPath) == buildHash);

            if (selected.FullPath == null)
            {
                Console.Error.WriteLine($"No ContentLayout.json in the input matches the analyzed build (BuildManifestHash {buildHash}). Include the layout from the build report folder of this build.");
                return false;
            }
        }
        else if (layoutCandidates.Count == 1)
        {
            // A layout without its build content is a valid input (e.g. to query a large layout).
            selected = layoutCandidates[0];
        }
        else
        {
            Console.Error.WriteLine("The input contains multiple ContentLayout.json files but no ContentDirectory build to match them against. Only a single layout can be analyzed.");
            return false;
        }

        foreach (var candidate in layoutCandidates)
        {
            if (candidate != selected)
            {
                var reason = BuildHistoryHelper.TryReadBuildManifestHash(candidate.FullPath) == buildHash
                    ? "it duplicates the selected layout"
                    : "its BuildManifestHash does not match the analyzed build";
                Console.Error.WriteLine($"Ignoring \"{candidate.FullPath}\": {reason}.");
                files.Remove(candidate);
            }
        }

        // Import the layout before the content files it describes, so their references can be
        // resolved through it.
        files.Remove(selected);
        files.Insert(0, selected);

        return true;
    }

    // --build-history: locate the analyzed build's folder inside the build history and add its
    // ContentLayout.json and build report to the input. Purely additive — the added layout goes
    // through the same candidate validation as a manually passed one.
    bool AddBuildHistoryFiles(string buildHash, List<(string FullPath, string DisplayRoot)> files)
    {
        var historyPath = m_Options.BuildHistoryPath;

        if (!Directory.Exists(historyPath))
        {
            Console.Error.WriteLine($"--build-history path not found: {historyPath}");
            return false;
        }

        if (buildHash == null)
        {
            Console.Error.WriteLine("--build-history requires a ContentDirectory build in the input: no BuildManifestHash.txt was found to identify the build to match.");
            return false;
        }

        var buildFolder = BuildHistoryHelper.LocateBuildFolder(historyPath, buildHash);
        if (buildFolder == null)
        {
            Console.Error.WriteLine($"No build in \"{historyPath}\" matches the analyzed build (BuildManifestHash {buildHash}).");
            return false;
        }

        Console.WriteLine($"Using build history folder \"{buildFolder}\".");

        var newEntries = BuildHistoryHelper.CollectBuildFiles(buildFolder)
            .Where(file => !files.Any(f =>
                string.Equals(Path.GetFullPath(f.FullPath), Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase)))
            .Select(file => (file, buildFolder))
            .ToList();
        files.InsertRange(0, newEntries);

        return true;
    }

    // Expands the input paths into the concrete files to analyze. Each result pairs the file with the
    // root used to render its relative path in progress/error messages: the scanned directory for files
    // found by scanning, or the file's own directory for explicitly-named files. Duplicates reached via
    // more than one input are analyzed once.
    List<(string FullPath, string DisplayRoot)> CollectFiles()
    {
        var searchOption = m_Options.NoRecursion ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;
        var collected = new List<(string FullPath, string DisplayRoot)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var inputPath in m_Options.Paths)
        {
            if (Directory.Exists(inputPath))
            {
                foreach (var file in Directory.GetFiles(inputPath, m_Options.SearchPattern, searchOption))
                {
                    if (seen.Add(Path.GetFullPath(file)))
                        collected.Add((file, inputPath));
                }
            }
            else if (File.Exists(inputPath))
            {
                if (seen.Add(Path.GetFullPath(inputPath)))
                    collected.Add((inputPath, Path.GetDirectoryName(Path.GetFullPath(inputPath))));
            }
            else
            {
                Console.Error.WriteLine($"Warning: path not found, skipping: {inputPath}");
            }
        }

        return collected;
    }

    int m_LastProgressMessageLength = 0;

    void ReportProgress(string relativePath, int fileIndex, int cntFiles)
    {
        var message = $"Processing {fileIndex * 100 / cntFiles}% ({fileIndex}/{cntFiles}) {relativePath}";
        if (!m_Options.Verbose)
        {
            EraseProgressLine();
            Console.Write($"\r{message}");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine(message);
        }

        m_LastProgressMessageLength = message.Length;
    }

    void EraseProgressLine()
    {
        if (!m_Options.Verbose)
            Console.Write($"\r{new string(' ', m_LastProgressMessageLength)}\r");
        else
            Console.WriteLine();
    }
}
