using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using NUnit.Framework;
using UnityDataTools.FileSystem;

namespace UnityDataTools.UnityDataTool.Tests
{
    public static class TestData
    {
        public class ExpectedValue
        {
            public ExpectedValue()
            {
            }
            
            public ExpectedValue(string key, object value)
            {
                Key = key;
                Value = value;
            }
            
            public string Key;
            public object Value;
        }

        static TestData()
        {
            ExpectedValues = new Dictionary<string, Dictionary<string, object>>();
        }

        public static Dictionary<string, Dictionary<string, object>> ExpectedValues
        {
            get;
        }
        
        static public IEnumerable<string> GetTestFolders()
        {
            return Directory.EnumerateDirectories(Path.Combine(TestContext.CurrentContext.TestDirectory, "data"));
        }

        static public void GenerateTestData()
        {
            var expectedValues = new List<ExpectedValue>();
            
            foreach (var folder in GetTestFolders())
            {
                expectedValues.Clear();
                
                UnityFileSystem.Init();
                using (var archive = UnityFileSystem.MountArchive(Path.Combine(folder, "AssetBundles", "assetbundle"), "/"))
                {
                    expectedValues.Add(new ExpectedValue("NodeCount", archive.Nodes.Count));
                    
                    foreach (var n in archive.Nodes)
                    {
                        expectedValues.Add(new ExpectedValue(n.Path + "-Size", n.Size));
                        expectedValues.Add(new ExpectedValue(n.Path + "-Flags", n.Flags));
                    }
                }
                UnityFileSystem.Cleanup();
                
                Program.Main(new string[] { "analyze", Path.Combine(folder, "AssetBundles"), "-r" });
                
                using var db = new SQLiteConnection($"Data Source={Path.Combine(Directory.GetCurrentDirectory(), "database.db")};Version=3;New=True;Foreign Keys=False;");
                db.Open();

                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText =
                        @"SELECT 
                        (SELECT COUNT(*) FROM animation_clips),
                        (SELECT COUNT(*) FROM asset_bundles),
                        (SELECT COUNT(*) FROM assets),
                        (SELECT COUNT(*) FROM audio_clips),
                        (SELECT COUNT(*) FROM meshes),
                        (SELECT COUNT(*) FROM objects),
                        (SELECT COUNT(*) FROM refs),
                        (SELECT COUNT(*) FROM serialized_files),
                        (SELECT COUNT(*) FROM shader_subprograms),
                        (SELECT COUNT(*) FROM shaders),
                        (SELECT COUNT(*) FROM shader_keywords),
                        (SELECT COUNT(*) FROM shader_subprogram_keywords),
                        (SELECT COUNT(*) FROM textures),
                        (SELECT COUNT(*) FROM types)";

                    using var reader = cmd.ExecuteReader();

                    reader.Read();

                    expectedValues.Add(new ExpectedValue("animation_clips_count", reader.GetInt32(0)));
                    expectedValues.Add(new ExpectedValue("asset_bundles_count", reader.GetInt32(1)));
                    expectedValues.Add(new ExpectedValue("assets_count", reader.GetInt32(2)));
                    expectedValues.Add(new ExpectedValue("audio_clips_count", reader.GetInt32(3)));
                    expectedValues.Add(new ExpectedValue("meshes_count", reader.GetInt32(4)));
                    expectedValues.Add(new ExpectedValue("objects_count", reader.GetInt32(5)));
                    expectedValues.Add(new ExpectedValue("refs_count", reader.GetInt32(6)));
                    expectedValues.Add(new ExpectedValue("serialized_files_count", reader.GetInt32(7)));
                    expectedValues.Add(new ExpectedValue("shader_subprograms_count", reader.GetInt32(8)));
                    expectedValues.Add(new ExpectedValue("shaders_count", reader.GetInt32(9)));
                    expectedValues.Add(new ExpectedValue("shader_keywords_count", reader.GetInt32(10)));
                    expectedValues.Add(new ExpectedValue("shader_subprogram_keywords_count", reader.GetInt32(11)));
                    expectedValues.Add(new ExpectedValue("textures_count", reader.GetInt32(12)));
                    expectedValues.Add(new ExpectedValue("types_count", reader.GetInt32(13)));
                }

                var di = new DirectoryInfo(folder);
                var unityVersion = di.Name;
                var outputPath = Path.Combine(di.Parent.Parent.Parent.Parent.Parent.FullName, "data", unityVersion);
                Directory.CreateDirectory(outputPath);

                var dumpPath = Path.Combine(outputPath, "ExpectedData", "dump");
                Directory.CreateDirectory(dumpPath);
                Program.Main(new string[] { "dump", Path.Combine(folder, "AssetBundles", "assetbundle"), "-o", dumpPath });
                
                dumpPath = Path.Combine(outputPath, "ExpectedData", "dump-s");
                Directory.CreateDirectory(dumpPath);
                Program.Main(new string[] { "dump", Path.Combine(folder, "AssetBundles", "assetbundle"), "-o", dumpPath, "-s" });
                
                XmlSerializer s = new XmlSerializer(typeof(List<ExpectedValue>));
                TextWriter writer = new StreamWriter(Path.Combine(outputPath, "ExpectedValues.xml"));
                s.Serialize(writer, expectedValues);
                writer.Close();
            }
        }

        public static void LoadTestData()
        {
            var expectedValues = new List<ExpectedValue>();
            
            foreach (var folder in GetTestFolders())
            {
                expectedValues.Clear();
                
                XmlSerializer s = new XmlSerializer(typeof(List<ExpectedValue>));
                TextReader reader = new StreamReader(Path.Combine(folder, "ExpectedValues.xml"));
                expectedValues = (List<ExpectedValue>)s.Deserialize(reader);
                reader.Close();

                ExpectedValues[folder] = new Dictionary<string, object>();
                foreach (var e in expectedValues)
                {
                    ExpectedValues[folder][e.Key] = e.Value;
                }
            }
        }
    }
}
