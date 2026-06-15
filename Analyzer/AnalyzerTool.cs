using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityDataTools.Analyzer.SQLite.Handlers;
using UnityDataTools.Analyzer.SQLite.Parsers;
using UnityDataTools.Analyzer.SQLite.Writers;
using UnityDataTools.Models;
using UnityDataTools.BinaryFormat;
using UnityDataTools.FileSystem;

namespace UnityDataTools.Analyzer;

public class AnalyzerTool
{
    AnalyzeOptions m_Options;

    public List<ISQLiteFileParser> parsers = new List<ISQLiteFileParser>()
    {
        new AddressablesBuildLayoutParser(),
        new SerializedFileParser(),
    };

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
    }

    public int Analyze(AnalyzeOptions options)
    {
        m_Options = options;

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

        var files = CollectFiles();

        int countFailures = 0;
        int countSuccess = 0;
        int countIgnored = 0;
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
                    catch (SerializedFileOpenException e)
                    {
                        // Expected failure — the file content could not be parsed.
                        // Don't print a stack trace; it adds no value for this known failure mode.
                        EraseProgressLine();
                        Console.Error.WriteLine($"Failed to open: {relativePath}");
                        var hint = SerializedFileDetector.GetOpenFailureHint(e.FilePath);
                        if (hint != null)
                            Console.Error.WriteLine(hint);
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
        Console.WriteLine($"Finalizing database. Successfully processed files: {countSuccess}, Failed files: {countFailures}, Ignored files: {countIgnored}");

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
