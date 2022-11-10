using System.IO;
using UnityEditor;
using UnityEngine;

public class BuildAssetBundles
{
    [MenuItem ("Tools/Generate AssetBundles")]
    static void GenerateAssetBundles ()
    {
        Directory.CreateDirectory("AssetBundles");

        BuildPipeline.BuildAssetBundles ("AssetBundles", BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX);
        
        var outPath = Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName, "TestCommon", "Data", "AssetBundles", Application.unityVersion);
        
        Directory.CreateDirectory(outPath);
        File.Copy(Path.Combine("AssetBundles", "assetbundle"), Path.Combine(outPath, "assetbundle"), true);
        File.Copy(Path.Combine("AssetBundles", "scenes"), Path.Combine(outPath, "scenes"), true);
    }
    
    [MenuItem ("Tools/Generate PlayerData")]
    static void GeneratePlayerData ()
    {
        if (!EditorUtility.DisplayDialog("Warning!",
                "Make sure that the \"ForceAlwaysWriteTypeTrees\" Diagnostic Switch is enabled in the Editor Preferences (Diagnostic/Editor section)",
                "OK", "Wait, let me do it!"))
        {
            return;
        }
        
        string[] levels = new string[] {"Assets/Scenes/OtherScene.unity"};
        BuildPipeline.BuildPlayer(levels, Path.Combine(Path.GetDirectoryName(Application.dataPath), "build", "game"), BuildTarget.StandaloneOSX, BuildOptions.None);
        
        var outPath = Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName, "TestCommon", "Data", "PlayerData", Application.unityVersion);
        
        Directory.CreateDirectory(outPath);
        File.Copy(Path.Combine("build", "game.app", "Contents", "Resources", "Data", "level0"), outPath, true);
    }
}
