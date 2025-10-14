using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using UnityDataTools.Analyzer.SQLite.Parsers.Models;
using UnityDataTools.Analyzer.SQLite.Handlers;
using UnityDataTools.Analyzer.SQLite.Writers;

namespace UnityDataTools.Analyzer.SQLite.Parsers
{
    public class AddressablesBuildLayoutParser : ISQLiteFileParser
    {
        private AddressablesBuildLayoutSQLWriter m_Writer;

        public bool Verbose { get; set; }
        public bool SkipReferences { get; set; }

        public void Dispose()
        {
            m_Writer.Dispose();
        }
        public void Init(SqliteConnection db)
        {
            m_Writer = new AddressablesBuildLayoutSQLWriter(db);
            m_Writer.Init();
        }

        public bool CanParse(string filename)
        {

            if (Path.GetExtension(filename) != ".json")
                return false;

            // Read the first line of the JSON file and check if it contains BuildResultHash
            string firstLine = "";
            try
            {
                using (StreamReader reader = new StreamReader(filename))
                {
                    firstLine = reader.ReadLine();
                    if (firstLine != null)
                    {
                        // Remove trailing comma if present and add closing brace to make it valid JSON
                        if (firstLine.TrimEnd().EndsWith(","))
                        {
                            firstLine = firstLine.TrimEnd().TrimEnd(',') + "}";
                        }

                        using (JsonTextReader jsonReader = new JsonTextReader(new StringReader(firstLine)))
                        {
                            JsonSerializer serializer = new JsonSerializer();
                            var jsonObject = serializer.Deserialize<JObject>(jsonReader);

                            // If the file has BuildResultHash, process it as an Addressables build
                            if (jsonObject != null && jsonObject["BuildResultHash"] != null)
                            {
                                return true;
                            }
                        }
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
            using (StreamReader reader = File.OpenText(filename))
            {
                JsonSerializer serializer = new JsonSerializer();
                BuildLayout buildLayout = (BuildLayout)serializer.Deserialize(reader, typeof(BuildLayout));
                m_Writer.WriteAddressablesBuild(filename, buildLayout);
            }
        }
    }
}
