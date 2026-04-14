using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityDataTools.Analyzer.SQLite.Handlers;
using UnityDataTools.Analyzer.SQLite.Parsers;
using UnityDataTools.Analyzer.SQLite.Parsers.Models;
using UnityDataTools.Analyzer.SQLite.Writers;
using UnityDataTools.BinaryFormat;
using UnityDataTools.FileSystem;

namespace UnityDataTools.Analyzer;

public class AnalyzerTool
{
    bool m_Verbose = false;

    public List<ISQLiteFileParser> parsers = new List<ISQLiteFileParser>()
    {
        new AddressablesBuildLayoutParser(),
        new SerializedFileParser(),
    };

    public int Analyze(
        string path,
        string databaseName,
        string searchPattern,
        bool skipReferences,
        bool verbose,
        bool noRecursion)
    {
        m_Verbose = verbose;

        using SQLiteWriter writer = new(databaseName);

        try
        {
            writer.Begin();
            foreach (var parser in parsers)
            {
                parser.Verbose = verbose;
                parser.SkipReferences = skipReferences;
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

        var files = Directory.GetFiles(
            path,
            searchPattern,
            noRecursion ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories);

        int countFailures = 0;
        int countSuccess = 0;
        int countIgnored = 0;
        int i = 1;
        foreach (var file in files)
        {
            bool foundParser = false;
            foreach (var parser in parsers)
            {
                if (parser.CanParse(file))
                {
                    foundParser = true;
                    try
                    {
                        parser.Parse(file);
                        ReportProgress(Path.GetRelativePath(path, file), i, files.Length);
                        countSuccess++;
                    }
                    catch (SerializedFileOpenException e)
                    {
                        // Expected failure — the file content could not be parsed.
                        // Don't print a stack trace; it adds no value for this known failure mode.
                        EraseProgressLine();
                        var relativePath = Path.GetRelativePath(path, file);
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
                        var relativePath = Path.GetRelativePath(path, file);
                        Console.Error.WriteLine($"Failed to process: {relativePath}");
                        if (m_Verbose)
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
                if (m_Verbose)
                {
                    var relativePath = Path.GetRelativePath(path, file);
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

    int m_LastProgressMessageLength = 0;

    void ReportProgress(string relativePath, int fileIndex, int cntFiles)
    {
        var message = $"Processing {fileIndex * 100 / cntFiles}% ({fileIndex}/{cntFiles}) {relativePath}";
        if (!m_Verbose)
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
        if (!m_Verbose)
            Console.Write($"\r{new string(' ', m_LastProgressMessageLength)}\r");
        else
            Console.WriteLine();
    }
}
