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
    // Documentation/contentlayout-database.md for the resulting schema).
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
            ContentLayout layout;
            using (var reader = File.OpenText(filename))
            {
                var serializer = new JsonSerializer();
                layout = (ContentLayout)serializer.Deserialize(reader, typeof(ContentLayout));
            }

            // The tool's failure summary only includes exception details in verbose mode, so
            // report the reason for these expected failures directly.
            if (layout == null)
            {
                Fail($"\"{filename}\" does not contain a ContentLayout.");
            }

            if (layout.Version != ContentLayout.CurrentVersion)
            {
                Fail($"Unsupported ContentLayout.json version {layout.Version} (this version of UnityDataTool supports version {ContentLayout.CurrentVersion}).");
            }

            if (m_ImportedLayout != null)
            {
                Fail($"Only a single ContentLayout.json can be analyzed (already imported \"{m_ImportedLayout}\").");
            }

            // Only create the content_layout tables when a layout is actually imported.
            m_Writer.Init();
            m_Writer.WriteContentLayout(filename, layout);
            m_ImportedLayout = filename;
        }

        private static void Fail(string message)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(message);
            throw new Exception(message);
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
