using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityDataTools.Analyzer.SQLite.Handlers;
using UnityDataTools.Analyzer.SQLite.Parsers;
using UnityDataTools.Analyzer.SQLite.Parsers.Models;
using UnityDataTools.Analyzer.SQLite.Writers;

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

        // TODO: skipReferences needs to be passed into AssetBundleWriter
        using SQLiteWriter writer = new (databaseName);

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
            foreach(var parser in parsers)
            {
                if (parser.CanParse(file))
                {
                    foundParser = true;
                    Console.Error.WriteLine(file);
                    try
                    {
                        parser.Parse(file);
                        ReportProgress(Path.GetRelativePath(path, file), i, files.Length);
                        countSuccess++;
                    }
                    catch (Exception e)
                    {                        
                        EraseProgressLine();
                        Console.Error.WriteLine();
                        Console.Error.WriteLine($"Error processing file: {file}");
                        Console.WriteLine($"{e.GetType()}: {e.Message}");
                        if (m_Verbose)
                            Console.WriteLine(e.StackTrace);
                        countFailures++;
                    }
                    ++i;
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
                ++i;
                countIgnored++;
                continue;
            }
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

    private bool ProcessFile(string file, string path, SQLiteWriter writer, int i, int length)
    {
        throw new NotImplementedException();
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