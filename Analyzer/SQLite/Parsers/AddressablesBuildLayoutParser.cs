using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityDataTools.Analyzer.SQLite.Handlers;
using UnityDataTools.Analyzer.SQLite.Writers;
using UnityDataTools.Models;

namespace UnityDataTools.Analyzer.SQLite.Parsers
{
    public class AddressablesBuildLayoutParser : ISQLiteFileParser
    {
        private AddressablesBuildLayoutSQLWriter m_Writer;

        public bool Verbose { get; set; }
        public bool SkipReferences { get; set; }
        public bool SkipCrc { get; set; }

        public void Dispose()
        {
            m_Writer.Dispose();
        }

        public void FinalizeDatabase()
        {
            // Addressables build reports don't produce dangling object references.
        }

        public void Init(SqliteConnection db)
        {
            m_Writer = new AddressablesBuildLayoutSQLWriter(db);
            m_Writer.Verbose = Verbose;
        }

        public bool CanParse(string filename)
        {

            if (Path.GetExtension(filename) != ".json")
                return false;

            // Read enough content to check if it contains BuildResultHash
            // This handles both minified and pretty-printed JSON
            try
            {
                using (StreamReader reader = new StreamReader(filename))
                {
                    // Read first 4KB which should be enough to find BuildResultHash near the start
                    char[] buffer = new char[4096];
                    int charsRead = reader.Read(buffer, 0, buffer.Length);
                    string content = new string(buffer, 0, charsRead);

                    // Check if BuildResultHash appears in the content
                    if (content.Contains("\"BuildResultHash\""))
                    {
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                if (Verbose)
                {
                    Console.Error.WriteLine($"Error reading JSON file {filename}: {e.Message}");
                }
            }
            return false;
        }

        public void Parse(string filename)
        {
            // only init our writer if we are actually parsing a file
            m_Writer.Init();
            using (StreamReader reader = File.OpenText(filename))
            {
                JsonSerializer serializer = new JsonSerializer();
                BuildLayout buildLayout = (BuildLayout)serializer.Deserialize(reader, typeof(BuildLayout));
                m_Writer.WriteAddressablesBuild(filename, buildLayout);
            }
        }
    }
}
