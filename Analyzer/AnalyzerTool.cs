using Analyzer.SQLite.Parsers;
using Analyzer.SQLite.Writers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityDataTools.Analyzer.Build;
using UnityDataTools.Analyzer.SQLite.Handlers;
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
                    }
                    catch (Exception e)
                    {                        
                        EraseProgressLine();
                        Console.Error.WriteLine();
                        Console.Error.WriteLine($"Error processing file: {file}");
                        Console.WriteLine($"{e.GetType()}: {e.Message}");
                        if (m_Verbose)
                            Console.WriteLine(e.StackTrace);
                    }
                    ++i;
                }
            }
            if (!foundParser)
            {
                var relativePath = Path.GetRelativePath(path, file);

                if (m_Verbose)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Ignoring {relativePath}");
                }
                ++i;
            }
        }

        Console.WriteLine();
        Console.WriteLine("Finalizing database...");

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
            Console.Write($"\r{new string(' ', m_LastProgressMessageLength)}");
        else
            Console.WriteLine();
    }
}