using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SQLite.Handlers;
using UnityDataTools.Analyzer.SQLite.Writers;
using UnityDataTools.FileSystem;

namespace UnityDataTools.Analyzer.SQLite.Parsers
{
    public class SerializedFileParser : ISQLiteFileParser
    {
        private SerializedFileSQLiteWriter m_Writer;

        public bool Verbose { get; set; }
        public bool SkipReferences { get; set; }

        public bool CanParse(string filename)
        {
            return ShouldIgnoreFile(filename) == false;
        }


        public void Dispose()
        {
            m_Writer.Dispose();
        }

        public void Init(SqliteConnection db)
        {
            m_Writer = new SerializedFileSQLiteWriter(db, SkipReferences);
        }

        public void Parse(string filename)
        {
            // only init our writer if we are actually parsing a file
            m_Writer.Init();
            bool successful = ProcessFile(filename, Path.GetDirectoryName(filename));
            if (!successful)
            {
                throw new Exception($"Failed to process file: {filename}");
            }
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
        ".ini", ".config", ".hash", ".md"
    };

        bool ProcessFile(string file, string rootDirectory)
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

                            m_Writer.BeginAssetBundle(assetBundleName, new FileInfo(file).Length);

                            foreach (var node in archive.Nodes)
                            {
                                if (node.Flags.HasFlag(ArchiveNodeFlags.SerializedFile))
                                {
                                    try
                                    {
                                        m_Writer.WriteSerializedFile(node.Path, "archive:/" + node.Path, Path.GetDirectoryName(file));
                                    }
                                    catch (Exception e)
                                    {
                                        // the most likely exception here is Microsoft.Data.Sqlite.SqliteException,
                                        // for example 'UNIQUE constraint failed: serialized_files.id'.
                                        // or 'UNIQUE constraint failed: objects.id' which can happen
                                        // if AssetBundles from different builds are being processed by a single call to Analyze
                                        // or if there is a Unity Data Tool bug.
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
                            m_Writer.EndAssetBundle();
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
                    m_Writer.WriteSerializedFile(relativePath, file, Path.GetDirectoryName(file));
                }
            }
            catch (NotSupportedException)
            {
                Console.Error.WriteLine();
                //A "failed to load" error will already be logged by the UnityFileSystem library

                successful = false;
            }
            catch (Exception)
            {
                // Don't log the error here - it will be logged by AnalyzerTool
                // Just mark as unsuccessful and let the exception propagate up via Parse()
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
    }
}
