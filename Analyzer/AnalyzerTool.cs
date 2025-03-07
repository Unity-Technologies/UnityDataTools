﻿using System;
using System.Diagnostics;
using System.IO;
using UnityDataTools.Analyzer.SQLite;
using UnityDataTools.FileSystem;

namespace UnityDataTools.Analyzer;

public class AnalyzerTool
{
    public int Analyze(string path, string databaseName, string searchPattern, bool skipReferences)
    {
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

        var files = Directory.GetFiles(path, searchPattern, SearchOption.AllDirectories);
        int i = 1;
        int lastLength = 0;
        foreach (var file in files)
        {
            // Automatically ignore these annoying OS X style files meta files.
            if (Path.GetFileName(file) == ".DS_Store")
                continue;

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
                    
                    var relativePath = Path.GetRelativePath(path, file);

                    writer.WriteSerializedFile(relativePath, file, Path.GetDirectoryName(file));

                    var message = $"Processing {i * 100 / files.Length}% ({i}/{files.Length}) {file}";
                    Console.Write($"\rProcessing {i * 100 / files.Length}% ({i}/{files.Length}) {file}");
                    lastLength = message.Length;
                }

                if (archive != null)
                {
                    try
                    {
                        var assetBundleName = Path.GetRelativePath(path, file);
                        
                        writer.BeginAssetBundle(assetBundleName, new FileInfo(file).Length);
                        
                        var message = $"Processing {i * 100 / files.Length}% ({i}/{files.Length}) {assetBundleName}";
                        Console.Write($"\r{message}{new string(' ', Math.Max(0, lastLength - message.Length))}");
                        lastLength = message.Length;

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
                                    Console.Error.WriteLine($"\rError processing {node.Path} in archive {file}{new string(' ', Math.Max(0, lastLength - message.Length))}");
                                    Console.Error.WriteLine(e);
                                    Console.WriteLine();
                                }
                            }
                        }
                    }
                    finally
                    {
                        Console.Write($"\r{new string(' ', lastLength)}");
                        writer.EndAssetBundle();
                        archive.Dispose();
                    }
                }
            }
            catch(NotSupportedException) {
                Console.Error.WriteLine();
                //Console.Error.WriteLine($"File not supported: {file}"); // This is commented out because another codepath will output "failed to load"
            }
            catch (Exception e) 
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"Error processing file: {file}");
                Console.Write($"{e.GetType()}: ");
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }

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
}
