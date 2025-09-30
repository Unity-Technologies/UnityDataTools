using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        int countFailures = 0;
        int countSuccess = 0;
        int countIgnored = 0;
        int i = 1;
        foreach (var file in files)
        {
            if (ShouldIgnoreFile(file))
            {
                if (m_Verbose)
                {
                    var relativePath = Path.GetRelativePath(path, file);
                    Console.WriteLine();
                    Console.WriteLine($"Ignoring {relativePath}");
                }
                countIgnored++;
            }
            else if (!ProcessFile(file, path, writer, i, files.Length))
            {
                countFailures++;
            }
            else
            {
                countSuccess++;
            }
            ++i;
        }

        Console.WriteLine();
        Console.WriteLine($"Finalizing database. Successfully processed files: {countSuccess}, Failed files: {countFailures}, Ignored files: {countIgnored}");

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
        ".txt", ".resS", ".resource", ".json", ".dll", ".pdb", ".exe", ".manifest", ".entities", ".entityheader",
        ".ini", ".config"
    };

    bool ProcessFile(string file, string rootDirectory, SQLiteWriter writer, int fileIndex, int cntFiles)
    {
        bool successful = true;
        try
        {
            if (IsUnityArchive(file))
            {
                using (UnityArchive archive = UnityFileSystem.MountArchive(file, "archive:" + Path.DirectorySeparatorChar))
                {
                    if (archive == null)
                        throw new FileLoadException($"Failed to mount archive: {file}");

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
                                    // the most likely exception here is Microsoft.Data.Sqlite.SqliteException,
                                    // for example 'UNIQUE constraint failed: serialized_files.id'.
                                    // or 'UNIQUE constraint failed: objects.id' which can happen
                                    // if AssetBundles from different builds are being processed by a single call to Analyze
                                    // or if there is a Unity Data Tool bug.
                                    EraseProgressLine();
                                    Console.Error.WriteLine($"Error processing {node.Path} in archive {file}");
                                    Console.Error.WriteLine(e.Message);
                                    Console.WriteLine();

                                    // It is possible some files inside an archive will pass and others will fail, to have a partial analyze.
                                    // Overall that is reported as a failure
                                    successful = false;
                                }
                            }
                        }
                    }
                    finally
                    {
                        writer.EndAssetBundle();
                    }
                }
            }
            else
            {
                // This isn't a Unity Archive file.  Try to open it as a SerializedFile.
                // Unfortunately there is no standard file extension, or clear signature at the start of the file,
                // to test if it truly is a SerializedFile.  So this will process files that are clearly not unity build files,
                // and there is a chance for crashes and freezes if the parser misinterprets the file content.
                var relativePath = Path.GetRelativePath(rootDirectory, file);
                writer.WriteSerializedFile(relativePath, file, Path.GetDirectoryName(file));

                ReportProgress(relativePath, fileIndex, cntFiles);
            }

            EraseProgressLine();
        }
        catch (NotSupportedException)
        {
            EraseProgressLine();
            Console.Error.WriteLine();
            //A "failed to load" error will already be logged by the UnityFileSystem library

            successful = false;
        }
        catch (Exception e)
        {
            EraseProgressLine();
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Error processing file: {file}");
            Console.WriteLine($"{e.GetType()}: {e.Message}");
            if (m_Verbose)
                Console.WriteLine(e.StackTrace);

            successful = false;
        }

        return successful;
    }

    private static bool IsUnityArchive(string filePath)
    {
        // Check whether a file is a Unity Archive (AssetBundle) by looking for known signatures at the start of the file.
        // "UnifyFS" is the current signature, but some older formats of the file are still supported
        string[] signatures = { "UnityFS", "UnityWeb", "UnityRaw", "UnityArchive" };
        int maxLen = 12; // "UnityArchive".Length
        byte[] buffer = new byte[maxLen];

        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            int read = fs.Read(buffer, 0, buffer.Length);
            foreach (var sig in signatures)
            {
                if (read >= sig.Length)
                {
                    bool match = true;
                    for (int i = 0; i < sig.Length; ++i)
                    {
                        if (buffer[i] != sig[i])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                        return true;
                }
            }
            return false;
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
            Console.Write($"\r{new string(' ', m_LastProgressMessageLength)}\r");
        else
            Console.WriteLine();
    }
}