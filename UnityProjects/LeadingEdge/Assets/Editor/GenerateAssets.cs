using System.Collections.Generic;
using System.IO;
using Unity.Loading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Generates the scenes and ScriptableObject assets used as build sources. The dictionaries are populated before
// the assets are saved, so the entries are serialized into the asset files.
//
// Leaf assets reference AudioClips (directly or via Loadable). The single-clip variants reference only a.mp3,
// which is also referenced by the all-clips variants, demonstrating an AudioClip shared between two assets.
// Two small scenes each show the shared GreenStatic texture through a SpriteRenderer, with distinct GameObject
// names so the scenes have different content. The SceneList asset references both scenes by LoadableSceneId.
// The root assets (DirectScriptableObjectReference) directly reference the leaf assets and act as build roots.
public static class GenerateAssets
{
    const string AudioFolder = "Assets/Audio";
    const string OutputFolder = "Assets/ScriptableObjects";
    const string TexturePath = "Assets/Textures/GreenStatic.png";

    public static readonly string[] ScenePaths =
    {
        "Assets/Scenes/Scene1.unity",
        "Assets/Scenes/Scene2.unity"
    };

    [MenuItem("ContentDirectory/Generate Assets")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");

        GenerateScenes();

        // All AudioClips keyed by filename without extension, e.g. "6.mp3" -> "6".
        var allClips = new Dictionary<string, AudioClip>();
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            allClips[Path.GetFileNameWithoutExtension(path)] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // The single-clip assets reference only a.mp3, which is also part of the all-clips assets.
        var singleClip = new Dictionary<string, AudioClip> { { "a", allClips["a"] } };

        var direct = CreateDirect("DirectAudioClipReference", allClips);
        var loadable = CreateLoadable("LoadableAudioClipReference", allClips);
        var singleDirect = CreateDirect("SingleAudioClipDirectReference", singleClip);
        var singleLoadable = CreateLoadable("SingleAudioClipLoadableReference", singleClip);

        // Serialization demo asset, referenced from both build roots so it ends up in both build outputs.
        var serializationDemo = ScriptableObject.CreateInstance<SerializationDemo>();
        serializationDemo.data = new SerializationDemo.SerializedData();
        AssetDatabase.CreateAsset(serializationDemo, $"{OutputFolder}/SerializationDemo.asset");

        var sceneList = CreateSceneList("SceneList");

        CreateReference("AssetBundleRoot", direct, singleDirect, serializationDemo);
        CreateReference("ContentDirectoryRoot", loadable, singleLoadable, serializationDemo, sceneList);

        AssetDatabase.SaveAssets();

        Debug.Log("GenerateAssets: created scenes, leaf reference assets and AssetBundleRoot / ContentDirectoryRoot.");
    }

    // Each scene contains a single GameObject showing the shared texture through a SpriteRenderer. The
    // GameObject names differ per scene so the scenes have different content (and different content hashes).
    static void GenerateScenes()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturePath);

        foreach (var scenePath in ScenePaths)
        {
            var sceneName = Path.GetFileNameWithoutExtension(scenePath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject($"{sceneName}Sprite");
            go.AddComponent<SpriteRenderer>().sprite = sprite;
            EditorSceneManager.SaveScene(scene, scenePath);
        }
    }

    static SceneList CreateSceneList(string name)
    {
        var asset = ScriptableObject.CreateInstance<SceneList>();
        foreach (var scenePath in ScenePaths)
            asset.scenes[Path.GetFileNameWithoutExtension(scenePath)] =
                LoadableSceneIdEditorUtility.CreateLoadableSceneId(scenePath);
        AssetDatabase.CreateAsset(asset, $"{OutputFolder}/{name}.asset");
        return asset;
    }

    static DirectAudioClipReference CreateDirect(string name, Dictionary<string, AudioClip> clips)
    {
        var asset = ScriptableObject.CreateInstance<DirectAudioClipReference>();
        foreach (var kvp in clips)
            asset.clips[kvp.Key] = kvp.Value;
        AssetDatabase.CreateAsset(asset, $"{OutputFolder}/{name}.asset");
        return asset;
    }

    static LoadableAudioClipReference CreateLoadable(string name, Dictionary<string, AudioClip> clips)
    {
        var asset = ScriptableObject.CreateInstance<LoadableAudioClipReference>();
        foreach (var kvp in clips)
            asset.clips[kvp.Key] = new Loadable<AudioClip>(LoadableObjectIdEditorUtility.CreateLoadableObjectId(kvp.Value));
        AssetDatabase.CreateAsset(asset, $"{OutputFolder}/{name}.asset");
        return asset;
    }

    static void CreateReference(string name, params ScriptableObject[] targets)
    {
        var asset = ScriptableObject.CreateInstance<DirectScriptableObjectReference>();
        foreach (var target in targets)
            asset.references[target.name] = target;
        AssetDatabase.CreateAsset(asset, $"{OutputFolder}/{name}.asset");
    }
}
