using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityDataTools.Analyzer.Build;
using UnityDataTools.Analyzer.SQLite;
using UnityDataTools.FileSystem;

namespace UnityDataTools.Analyzer;

public class AnalyzerTool
{
    bool m_Verbose = false;

    public int Analyze(
        string path,
        string databaseName,
        string searchPattern,
        bool skipReferences,
        bool verbose,
        bool noRecursion)
    {
        m_Verbose = verbose;

        using SQLiteWriter writer = new (databaseName, skipReferences);

        try
        {
            writer.Begin();
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
            if (Path.GetExtension(file) == ".json")
            {
                ProcessBuildLayout(file, writer, i, files.Length);
                ++i;
                continue;
            }
            if (ShouldIgnoreFile(file))
            {
                var relativePath = Path.GetRelativePath(path, file);

                if (m_Verbose)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Ignoring {relativePath}");
                }
                ++i;
                continue;
            }

            ProcessFile(file, path, writer, i, files.Length);
            ++i;
        }

        Console.WriteLine();
        Console.WriteLine("Finalizing database...");

        writer.End();

        timer.Stop();
        Console.WriteLine();
        Console.WriteLine($"Total time: {(timer.Elapsed.TotalMilliseconds / 1000.0):F3} s");

        return 0;
    }

    bool ShouldIgnoreFile(string file)
    {
        // Unfortunately there is no standard extension for AssetBundles, and SerializedFiles often have no extension at all.
        // Also there is also no distinctive signature at the start of a SerializedFile to immediately recognize it based on its first bytes.
        // This makes it difficult to use the "--search-pattern" argument to only pick those files.

        // Hence to reduce noise in UnityDataTool output we filter out files that we have a high confidence are
        // NOT SerializedFiles or Unity Archives.

        string fileName = Path.GetFileName(file);
        string extension = Path.GetExtension(file);

        return IgnoredFileNames.Contains(fileName) || IgnoredExtensions.Contains(extension);
    }

    // These lists are based on expected output files in Player, AssetBundle, Addressables and ECS builds.
    // However this is by no means exhaustive.
    private static readonly HashSet<string> IgnoredFileNames = new()
    {
        ".DS_Store", "boot.config", "archive_dependencies.bin", "scene_info.bin", "app.info", "link.xml",
        "catalog.bin", "catalog.hash"
    };

    private static readonly HashSet<string> IgnoredExtensions = new()
    {
        ".txt", ".resS", ".resource", ".json", ".dll", ".pdb", ".exe", ".manifest", ".entities", ".entityheader"
    };

    void ProcessFile(string file, string rootDirectory, SQLiteWriter writer, int fileIndex, int cntFiles)
    {
        try
        {
            UnityArchive archive = null;

            try
            {
                archive = UnityFileSystem.MountArchive(file, "archive:" + Path.DirectorySeparatorChar);
            }
            catch (NotSupportedException)
            {
                // It wasn't an AssetBundle, try to open the file as a SerializedFile.

                var relativePath = Path.GetRelativePath(rootDirectory, file);
                writer.WriteSerializedFile(relativePath, file, Path.GetDirectoryName(file));

                ReportProgress(relativePath, fileIndex, cntFiles);
            }

            if (archive != null)
            {
                try
                {
                    var assetBundleName = Path.GetRelativePath(rootDirectory, file);

                    writer.BeginAssetBundle(assetBundleName, new FileInfo(file).Length);
                    ReportProgress(assetBundleName, fileIndex, cntFiles);

                    foreach (var node in archive.Nodes)
                    {
                        if (node.Flags.HasFlag(ArchiveNodeFlags.SerializedFile))
                        {
                            try
                            {
                                writer.WriteSerializedFile(node.Path, "archive:/" + node.Path, Path.GetDirectoryName(file));
                            }
                            catch (Exception e)
                            {
                                EraseProgressLine();
                                Console.Error.WriteLine($"Error processing {node.Path} in archive {file}");
                                Console.Error.WriteLine(e);
                                Console.WriteLine();
                            }
                        }
                    }
                }
                finally
                {
                    writer.EndAssetBundle();
                    archive.Dispose();
                }
            }
            EraseProgressLine();
        }
        catch (NotSupportedException)
        {
            EraseProgressLine();
            Console.Error.WriteLine();
            //A "failed to load" error will already be logged by the UnityFileSystem library
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
    }



    void ProcessBuildLayout(string file, SQLiteWriter writer, int fileIndex, int cntFiles)
    {
        try
        {
            Console.Error.WriteLine(file);
            using (StreamReader reader = File.OpenText(file))
            {
                JsonSerializer serializer = new JsonSerializer();
                BuildLayout buildLayout = (BuildLayout)serializer.Deserialize(reader, typeof(BuildLayout));
                writer.WriteBuildLayout(file, buildLayout);
                ReportProgress(file, fileIndex, cntFiles);
            }
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