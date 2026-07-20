using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SQLite.Handlers;
using UnityDataTools.Analyzer.SQLite.Writers;
using UnityDataTools.BinaryFormat;
using UnityDataTools.FileSystem;

namespace UnityDataTools.Analyzer.SQLite.Parsers
{
    public class SerializedFileParser : ISQLiteFileParser
    {
        private SerializedFileSQLiteWriter m_Writer;
        private Util.IdProvider<string> m_SerializedFileIdProvider;
        private Util.ContentFileDependencyMap m_ContentFileDependencies;

        public bool Verbose { get; set; }
        public bool SkipReferences { get; set; }
        public bool SkipCrc { get; set; }

        public SerializedFileParser(Util.IdProvider<string> serializedFileIdProvider, Util.ContentFileDependencyMap contentFileDependencies)
        {
            m_SerializedFileIdProvider = serializedFileIdProvider;
            m_ContentFileDependencies = contentFileDependencies;
        }

        public bool CanParse(string filename)
        {
            // First check if the file is in the ignore list (by extension or filename)
            if (ShouldIgnoreFile(filename))
                return false;

            // Then validate that it's actually a Unity file by checking its format
            // This prevents ugly exceptions when processing non-Unity files
            return ArchiveDetector.IsUnityArchive(filename)
                || SerializedFileDetector.TryDetectSerializedFile(filename, out _);
        }


        public void Dispose()
        {
            m_Writer.Dispose();
        }

        public void FinalizeDatabase()
        {
            // m_Writer is only Init'd once a file is actually parsed; nothing to finalize otherwise.
            m_Writer.FinalizeDatabase();
        }

        public void Init(SqliteConnection db)
        {
            m_Writer = new SerializedFileSQLiteWriter(db, SkipReferences, SkipCrc,
                m_SerializedFileIdProvider, m_ContentFileDependencies);
        }

        public void Parse(string filename)
        {
            // only init our writer if we are actually parsing a file
            m_Writer.Init();
            ProcessFile(filename, Path.GetDirectoryName(filename));
        }

        bool ShouldIgnoreFile(string file)
        {
            // Filter out common non-Unity files by extension or filename.
            // This is a fast initial filter before we perform format detection.
            //
            // Note: AssetBundles have no standard extension, and SerializedFiles often have no extension at all.
            // Format detection (via ArchiveDetector and SerializedFileDetector) is performed after this filter
            // to definitively identify Unity files.

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

        void ProcessFile(string file, string rootDirectory)
        {
            if (ArchiveDetector.IsUnityArchive(file))
            {
                bool archiveHadErrors = false;
                bool archiveHadMissingTypeTrees = false;
                using (UnityArchive archive = UnityFileSystem.MountArchive(file, "archive:" + Path.DirectorySeparatorChar))
                {
                    if (archive == null)
                        throw new FileLoadException($"Failed to mount archive: {file}");

                    try
                    {
                        var archiveName = Path.GetRelativePath(rootDirectory, file);

                        m_Writer.BeginArchive(archiveName, new FileInfo(file).Length);

                        foreach (var node in archive.Nodes)
                        {
                            if (node.Flags.HasFlag(ArchiveNodeFlags.SerializedFile))
                            {
                                try
                                {
                                    m_Writer.WriteSerializedFile(node.Path, "archive:/" + node.Path, Path.GetDirectoryName(file));
                                }
                                catch (SerializedFileOpenException e) when (e.MissingTypeTrees)
                                {
                                    // The file has no TypeTrees and was rejected before opening. This is
                                    // tracked separately so it isn't lumped with genuine processing errors.
                                    archiveHadMissingTypeTrees = true;
                                }
                                catch (AnalyzeDuplicateException e)
                                {
                                    // A SerializedFile with this name was already analyzed (e.g. two
                                    // differently-named bundles containing the same CAB). Report the
                                    // self-contained message rather than a raw SQLite constraint error.
                                    Console.Error.WriteLine($"Skipping {node.Path} in archive {archiveName}: {e.Message}");
                                    archiveHadErrors = true;
                                }
                                catch (Exception e)
                                {
                                    // An unexpected error, for example a Unity Data Tool bug. Duplicate
                                    // names (the common 'UNIQUE constraint failed' case) are handled above.
                                    Console.Error.WriteLine($"Error processing {node.Path} in archive {archiveName}");
                                    Console.Error.WriteLine(e.Message);
                                    Console.Error.WriteLine();

                                    // It is possible some files inside an archive will pass and others will fail, to have a partial analyze.
                                    // Overall that is reported as a failure
                                    archiveHadErrors = true;
                                }
                            }
                        }
                    }
                    finally
                    {
                        m_Writer.EndArchive();
                    }
                }

                // Genuine errors take precedence over missing TypeTrees when reporting the archive's outcome.
                if (archiveHadErrors)
                {
                    throw new Exception("One or more files in the archive failed to process");
                }

                if (archiveHadMissingTypeTrees)
                {
                    throw new SerializedFileOpenException(file, missingTypeTrees: true);
                }
            }
            else
            {
                // This isn't a Unity Archive file, so process it as a SerializedFile.
                // Note: The file has already been validated in CanParse() via SerializedFileDetector,
                // so we're confident it's a valid SerializedFile at this point.
                var relativePath = Path.GetRelativePath(rootDirectory, file);
                m_Writer.WriteSerializedFile(relativePath, file, Path.GetDirectoryName(file));
            }
        }
    }
}
