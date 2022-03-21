using UnityEditor;
using UnityEngine;

public class BuildAssetBundles
{
    [MenuItem ("Assets/Build AssetBundles")]
    static void BuildAllAssetBundles ()
    {
        BuildPipeline.BuildAssetBundles ("AssetBundles", BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
    }
}
