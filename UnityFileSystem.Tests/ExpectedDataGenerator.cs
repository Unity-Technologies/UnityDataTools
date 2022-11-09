using System;
using System.IO;
using System.Linq;
using UnityDataTools.TestCommon;

namespace UnityDataTools.FileSystem.Tests;

public static class ExpectedDataGenerator
{
    public static void Generate(Context context)
    {
        var expectedData = context.ExpectedData;

        UnityFileSystem.Init();
        using (var archive = UnityFileSystem.MountArchive(Path.Combine(context.UnityDataFolder, "assetbundle"), "/"))
        {
            expectedData.Add("NodeCount", archive.Nodes.Count);

            foreach (var n in archive.Nodes)
            {
                expectedData.Add(n.Path + "-Size", n.Size);
                expectedData.Add(n.Path + "-Flags", n.Flags);

                using (var f = UnityFileSystem.OpenFile("/" + n.Path))
                {
                    var buffer = new Byte[100];
                    f.Read(100, buffer);

                    expectedData.Add(n.Path + "-Data", buffer);
                }

                if (n.Flags.HasFlag(ArchiveNodeFlags.SerializedFile))
                {
                    using var f = UnityFileSystem.OpenSerializedFile("/" + n.Path);

                    expectedData.Add(n.Path + "-ObjCount", f.Objects.Count);
                    expectedData.Add(n.Path + "-ExtRefCount", f.ExternalReferences.Count);

                    int i = 0;
                    foreach (var extRef in f.ExternalReferences)
                    {
                        expectedData.Add($"{n.Path}-ExtRef{i}-Guid", extRef.Guid);
                        expectedData.Add($"{n.Path}-ExtRef{i}-Path", extRef.Path);
                        expectedData.Add($"{n.Path}-ExtRef{i}-Type", extRef.Type);
                        ++i;
                    }

                    var obj = f.Objects.First();
                    expectedData.Add($"{n.Path}-FirstObj-Id", obj.Id);
                    expectedData.Add($"{n.Path}-FirstObj-Offset", obj.Offset);
                    expectedData.Add($"{n.Path}-FirstObj-Size", obj.Size);
                    expectedData.Add($"{n.Path}-FirstObj-TypeId", obj.TypeId);

                    obj = f.Objects.Last();
                    expectedData.Add($"{n.Path}-LastObj-Id", obj.Id);
                    expectedData.Add($"{n.Path}-LastObj-Offset", obj.Offset);
                    expectedData.Add($"{n.Path}-LastObj-Size", obj.Size);
                    expectedData.Add($"{n.Path}-LastObj-TypeId", obj.TypeId);
                }
            }

            UnityFileSystem.Cleanup();
            
            var csprojFolder = Directory.GetParent(context.TestDataFolder).Parent.Parent.Parent.FullName;
            var outputFolder = Path.Combine(csprojFolder, "ExpectedData", context.UnityDataVersion);

            Directory.CreateDirectory(outputFolder);
            
            expectedData.Save(outputFolder);
        }
    }
}
