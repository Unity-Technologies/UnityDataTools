using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem;
using UnityDataTools.FileSystem.TypeTreeReaders;
using UnityDataTools.TestCommon;

namespace UnityDataTools.UnityDataTool.Tests;

public static class ExpectedDataGenerator
{
    public static void GenerateAll()
    {
        foreach (var context in Context.GetAll())
        {
            Generate(context);
        }
    }
    
    public static void Generate(Context context)
    {
        UnityFileSystem.Init();
        using var archive = UnityFileSystem.MountArchive(Path.Combine(context.UnityDataFolder, "AssetBundles", "assetbundle"), "/");

        using var serializedFile = UnityFileSystem.OpenSerializedFile("/CAB-5d40f7cad7c871cf2ad2af19ac542994");
        using var fileReader = new UnityFileReader("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994", 1024*1024);
        
        var objectInfo = serializedFile.Objects.First(x => x.Id == -4850512016903265157);
        var node = serializedFile.GetTypeTreeRoot(objectInfo.Id);
        var reader = new RandomAccessReader(serializedFile, node, fileReader, objectInfo.Offset);
        var shader = Shader.Read(reader);

        context.ExpectedData.Add("Shader", shader);
        
        objectInfo = serializedFile.Objects.First(x => x.Id == -9023202112035587373);
        node = serializedFile.GetTypeTreeRoot(objectInfo.Id);
        reader = new RandomAccessReader(serializedFile, node, fileReader, objectInfo.Offset);
        var texture2D = Texture2D.Read(reader);
        
        context.ExpectedData.Add("Texture2D", texture2D);

        var di = new DirectoryInfo(context.TestDataFolder);
        var outputFolder = Path.Combine(di.Parent.Parent.Parent.Parent.FullName, "ExpectedData", context.UnityDataVersion);

        Directory.CreateDirectory(outputFolder);
        context.ExpectedData.Save(outputFolder);
    }
}
