using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using UnityDataTools.Analyzer.SQLite.Handlers;
using UnityDataTools.Analyzer.SQLite.Writers;
using UnityDataTools.Analyzer.Util;
using UnityDataTools.Models;

namespace UnityDataTools.Analyzer.SQLite.Parsers
{
    // Imports the ContentLayout.json produced by BuildPipeline.BuildContentDirectory into the
    // content_layout* tables (see Documentation/contentlayout.md for the file, and
    // Documentation/contentlayout-database.md for the resulting schema). Version 2 files
    // (Unity 6.6) are upgraded to the current in-memory model on the way in, so a single write
    // path serves both supported versions.
    public class ContentLayoutParser : ISQLiteFileParser
    {
        private ContentLayoutSQLWriter m_Writer;
        private string m_ImportedLayout;
        private IdProvider<string> m_SerializedFileIdProvider;
        private ContentFileDependencyMap m_ContentFileDependencies;

        public bool Verbose { get; set; }
        public bool SkipReferences { get; set; }
        public bool SkipCrc { get; set; }

        public ContentLayoutParser(IdProvider<string> serializedFileIdProvider, ContentFileDependencyMap contentFileDependencies)
        {
            m_SerializedFileIdProvider = serializedFileIdProvider;
            m_ContentFileDependencies = contentFileDependencies;
        }

        public void Init(SqliteConnection db)
        {
            m_Writer = new ContentLayoutSQLWriter(db, m_SerializedFileIdProvider, m_ContentFileDependencies);
        }

        // Unity always writes this exact filename into the build report directory, so unlike the
        // Addressables build reports (whose filenames can embed timestamps) no content sniffing
        // is needed.
        public static bool IsContentLayoutFile(string filename)
        {
            return string.Equals(Path.GetFileName(filename), "ContentLayout.json", StringComparison.OrdinalIgnoreCase);
        }

        public bool CanParse(string filename)
        {
            return IsContentLayoutFile(filename);
        }

        public void Parse(string filename)
        {
            var version = BuildHistoryHelper.TryReadLayoutVersion(filename);

            // The tool's failure summary only includes exception details in verbose mode, so
            // report the reason for these expected failures directly.
            if (version == null)
            {
                throw Fail($"\"{filename}\" does not contain a ContentLayout.");
            }

            // The upgrader and the writer throw InvalidDataException for dangling references in
            // the layout (an ObjectIdHash, ContentHash or ArtifactIndex with no target).
            try
            {
                ContentLayout layout;
                LoadableObjectV2Data[] v2Data = null;

                switch (version)
                {
                    case Models.V2.ContentLayout.SchemaVersion:
                        layout = ContentLayoutV2Upgrader.Upgrade(
                            Deserialize<Models.V2.ContentLayout>(filename), out v2Data);
                        break;

                    case ContentLayout.CurrentVersion:
                        layout = Deserialize<ContentLayout>(filename);
                        break;

                    default:
                        throw Fail($"Unsupported ContentLayout.json version {version} (this version of UnityDataTool supports versions {Models.V2.ContentLayout.SchemaVersion} and {ContentLayout.CurrentVersion}).");
                }

                if (m_ImportedLayout != null)
                {
                    throw Fail($"Only a single ContentLayout.json can be analyzed (already imported \"{m_ImportedLayout}\").");
                }

                // Only create the content_layout tables when a layout is actually imported. The v2
                // variant of the loadable-objects table carries extra source columns.
                m_Writer.Init(v2Data != null);
                m_Writer.WriteContentLayout(filename, layout, v2Data);
                m_ImportedLayout = filename;
            }
            catch (InvalidDataException e)
            {
                throw Fail($"\"{filename}\" is not a valid ContentLayout: {e.Message}");
            }
        }

        private T Deserialize<T>(string filename) where T : class
        {
            using var reader = File.OpenText(filename);
            var serializer = new JsonSerializer();
            var layout = (T)serializer.Deserialize(reader, typeof(T));

            if (layout == null)
            {
                throw Fail($"\"{filename}\" does not contain a ContentLayout.");
            }

            return layout;
        }

        private static Exception Fail(string message)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(message);
            return new Exception(message);
        }

        // Called after all files are processed, so the analyzed .cf files all have their
        // serialized_files rows and the layout entries can be linked to them.
        public void FinalizeDatabase()
        {
            if (m_ImportedLayout != null)
            {
                m_Writer.LinkSerializedFiles();
            }
        }

        public void Dispose()
        {
            m_Writer.Dispose();
        }
    }
}
