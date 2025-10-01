using Analyzer.SQLite.Writers;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityDataTools.Analyzer.SQLite.Handlers;
using UnityDataTools.FileSystem;

namespace Analyzer.SQLite.Parsers
{
    public class SerializedFileParser : ISQLiteFileParser
    {
        private AssetBundleSQLiteWriter m_Writer;

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
            m_Writer = new AssetBundleSQLiteWriter(db, SkipReferences);
            m_Writer.Init();
        }

        public void Parse(string filename)
        {
            ProcessFile(filename, Path.GetDirectoryName(filename));
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


        void ProcessFile(string file, string rootDirectory)
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
                    m_Writer.WriteSerializedFile(relativePath, file, Path.GetDirectoryName(file));
                }

                if (archive != null)
                {
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
                                    Console.Error.WriteLine($"Error processing {node.Path} in archive {file}");
                                    Console.Error.WriteLine(e);
                                    Console.WriteLine();
                                }
                            }
                        }
                    }
                    finally
                    {
                        m_Writer.EndAssetBundle();
                        archive.Dispose();
                    }
                }
            }
            catch (NotSupportedException)
            {
                Console.Error.WriteLine();
                //A "failed to load" error will already be logged by the UnityFileSystem library
            }
            catch (Exception e)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"Error processing file: {file}");
                Console.WriteLine($"{e.GetType()}: {e.Message}");
                if (Verbose)
                    Console.WriteLine(e.StackTrace);
            }
        }
    }
}
