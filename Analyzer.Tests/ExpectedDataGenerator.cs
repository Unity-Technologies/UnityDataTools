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
        
        AddObject(-4850512016903265157, "Shader", serializedFile, fileReader, context, Shader.Read);
        AddObject(-9023202112035587373, "Texture1", serializedFile, fileReader, context, Texture2D.Read);
        AddObject(404836592933730457, "Texture2", serializedFile, fileReader, context, Texture2D.Read);
        AddObject(2152370074763270995, "AnimationClip", serializedFile, fileReader, context, AnimationClip.Read);
        AddObject(4693305862354978555, "Mesh", serializedFile, fileReader, context, Mesh.Read);
        AddObject(-8074603400156879931, "AudioClip", serializedFile, fileReader, context, AudioClip.Read);
        AddObject(1, "AssetBundle", serializedFile, fileReader, context, AssetBundle.Read);

        var di = new DirectoryInfo(context.TestDataFolder);
        var outputFolder = Path.Combine(di.Parent.Parent.Parent.Parent.FullName, "ExpectedData", context.UnityDataVersion);

        Directory.CreateDirectory(outputFolder);
        context.ExpectedData.Save(outputFolder);
    }

    static void AddObject<T>(long id, string name, SerializedFile serializedFile, UnityFileReader fileReader, Context context, Func<RandomAccessReader, T> creator)
    {
        var objectInfo = serializedFile.Objects.First(x => x.Id == id);
        var node = serializedFile.GetTypeTreeRoot(objectInfo.Id);
        var reader = new RandomAccessReader(serializedFile, node, fileReader, objectInfo.Offset);
        var obj = creator(reader);
        
        context.ExpectedData.Add(name, obj);
    }
}
