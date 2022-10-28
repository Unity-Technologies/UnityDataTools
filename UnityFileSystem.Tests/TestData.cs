using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using NUnit.Framework;

namespace UnityDataTools.FileSystem.Tests
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
                        
                        using (var f = UnityFileSystem.OpenFile("/" + n.Path))
                        {
                            var buffer = new Byte[100];
                            f.Read(100, buffer);

                            expectedValues.Add(new ExpectedValue(n.Path + "-Data", buffer));
                        }

                        if (n.Flags.HasFlag(ArchiveNodeFlags.SerializedFile))
                        {
                            using var f = UnityFileSystem.OpenSerializedFile("/" + n.Path);
                            
                            expectedValues.Add(new ExpectedValue(n.Path + "-ObjCount", f.Objects.Count));
                            expectedValues.Add(new ExpectedValue(n.Path + "-ExtRefCount", f.ExternalReferences.Count));

                            int i = 0;
                            foreach (var extRef in f.ExternalReferences)
                            {
                                expectedValues.Add(new ExpectedValue($"{n.Path}-ExtRef{i}-Guid", extRef.Guid));
                                expectedValues.Add(new ExpectedValue($"{n.Path}-ExtRef{i}-Path", extRef.Path));
                                expectedValues.Add(new ExpectedValue($"{n.Path}-ExtRef{i}-Type", extRef.Type));
                                ++i;
                            }

                            var obj = f.Objects.First();
                            expectedValues.Add(new ExpectedValue($"{n.Path}-FirstObj-Id", obj.Id));
                            expectedValues.Add(new ExpectedValue($"{n.Path}-FirstObj-Offset", obj.Offset));
                            expectedValues.Add(new ExpectedValue($"{n.Path}-FirstObj-Size", obj.Size));
                            expectedValues.Add(new ExpectedValue($"{n.Path}-FirstObj-TypeId", obj.TypeId));
                            
                            obj = f.Objects.Last();
                            expectedValues.Add(new ExpectedValue($"{n.Path}-LastObj-Id", obj.Id));
                            expectedValues.Add(new ExpectedValue($"{n.Path}-LastObj-Offset", obj.Offset));
                            expectedValues.Add(new ExpectedValue($"{n.Path}-LastObj-Size", obj.Size));
                            expectedValues.Add(new ExpectedValue($"{n.Path}-LastObj-TypeId", obj.TypeId));
                        }
                    }
                }
                UnityFileSystem.Cleanup();
                
                var di = new DirectoryInfo(folder);
                var unityVersion = di.Name;
                var outputPath = Path.Combine(di.Parent.Parent.Parent.Parent.Parent.FullName, "data", unityVersion);
                Directory.CreateDirectory(outputPath);
                
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
