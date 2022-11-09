using System.IO;
using UnityEditor;
using UnityEngine;

public class BuildAssetBundles
{
    [MenuItem ("Tools/Generate Test Data")]
    static void GenerateTestData ()
    {
        Directory.CreateDirectory("AssetBundles");

        BuildPipeline.BuildAssetBundles ("AssetBundles", BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX);

        CopyTo("TestCommon");
        
        string[] levels = new string[] {"Assets/Scenes/OtherScene.unity"};
        BuildPipeline.BuildPlayer(levels, Path.Combine(Path.GetDirectoryName(Application.dataPath), "build", "game"), BuildTarget.StandaloneOSX, BuildOptions.None);
        var outPath = Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName, "TestCommon", "data",
            "PlayerData");
        Directory.CreateDirectory(outPath);
        File.Copy(Path.Combine("build", "game.app", "Contents", "Resources", "Data", "level0"), outPath, true);
    }

    static void CopyTo(string project)
    {
        var outPath = Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName, project, "data", Application.unityVersion, "AssetBundles");
        Directory.CreateDirectory(outPath);
        
        File.Copy(Path.Combine("AssetBundles", "assetbundle"), Path.Combine(outPath, "assetbundle"), true);
        File.Copy(Path.Combine("AssetBundles", "scenes"), Path.Combine(outPath, "scenes"), true);
    }
}
