using System.IO;
using UnityEditor;
using UnityEngine;

public class BuildAssetBundles
{
    [MenuItem ("Assets/Build AssetBundles")]
    static void BuildAllAssetBundles ()
    {
        Directory.CreateDirectory("AssetBundles");

        BuildPipeline.BuildAssetBundles ("AssetBundles", BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX);

        CopyTo("UnityFileSystem.Tests");
        CopyTo("UnityDataTool.Tests");
    }

    static void CopyTo(string project)
    {
        var outPath = Path.Combine(Directory.GetParent(Application.dataPath).Parent.FullName, project, "data", Application.unityVersion, "AssetBundles");
        Directory.CreateDirectory(outPath);
        
        File.Copy(Path.Combine("AssetBundles", "assetbundle"), Path.Combine(outPath, "assetbundle"), true);
        File.Copy(Path.Combine("AssetBundles", "scenes"), Path.Combine(outPath, "scenes"), true);
    }
}
