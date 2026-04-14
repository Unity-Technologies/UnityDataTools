# AssetBundle TypeTree Variations

This folder contains variations of the TypeTree representations in the newest SerializedFile formats.

- **v22** is used in recent versions of Unity
- **v23** is introduced in Unity 6.5

## Folder Overview

| Folder | Format | TypeTree Mode | Build Method | Built With |
|--------|--------|---------------|--------------|------------|
| `v22/` | v22 | Inline | Addressables | Unity 6000.0.65f1 |
| `v23_extracted/` | v23 | Extracted to separate file | Addressables | Unity 6000.6.0a1 |
| `v23_Inline/` | v23 | Inline | Addressables | Unity 6000.6.0a1 |
| `AssetBundle-NoTypeTree/` | v22 | Disabled (`DisableWriteTypeTree`) | `BuildPipeline.BuildAssetBundles` | Unity 6000.0.65f1 |
| `AssetBundle-NoTypeTreeNoVersion/` | v22 | Disabled (`DisableWriteTypeTree` + `AssetBundleStripUnityVersion`) | `BuildPipeline.BuildAssetBundles` | Unity 6000.0.65f1 |

## Addressable Builds (v22, v23_extracted, v23_Inline)

These three folders are builds of the same tiny Addressables project. They each contain the following files:

### packedassets_assets_all.bundle

- Main AssetBundle.
- Builds a simple prefab that includes a MonoBehaviour (an instance of the `MyScripts.Data` class).
- The MonoBehaviour has a `SerializedReference` field that stores an instance of a scripting class.

### MonoScript_monoscripts_dde848dc9848681e340a8b4fa9bd7578.bundle

- Auto-generated AssetBundle.
- Contains the MonoScript that tracks the MonoBehaviour type.

### AssetBundle.typetreedata

- An archive file with the TypeTree info from both bundles.
- **Only present in `v23_extracted/`** (when Addressables is built with *Extract Typetrees* enabled).

### prefab_with_serializedreference.serializedfile

- Serialized file extracted from `packedassets_assets_all.bundle`.
- Actual name inside AssetBundle: `CAB-394ff12e47c27ee4c30d41d2747acd4b`
- Contains: 2 GameObjects, 2 Transforms, a MonoBehaviour, and an AssetBundle
  (visible using `UnityDataTool sf objectlist prefab_with_serializedreference.serializedfile`).
- The TypeTree array has type info for AssetBundle (142), Transform (4), GameObject (1), MonoBehaviour (114).
- The TypeTree for the MonoBehaviour is specific to `MyScripts.Data` — it includes the `SerializedReference` field `MyData`, which is not part of other `MonoBehaviour`-derived classes. The `ScriptID` hash distinguishes the precise type when multiple MonoBehaviours are referenced in the same file.
- The SerializedReference TypeTree array references C# type `Assembly: Assembly-CSharp, NameSpace: MyScripts, Class: Data`.

### monoscriptbundle.serializedfile

- Serialized file extracted from `MonoScript_monoscripts_dde848dc9848681e340a8b4fa9bd7578.bundle`.
- Actual name inside AssetBundle: `CAB-d57a1d89ac0708bf030936c59479c685`

## Built-in AssetBundle Builds (AssetBundle-NoTypeTree, AssetBundle-NoTypeTreeNoVersion)

These are builds made by Unity 6000.0.65f1 of a single small ScriptableObject asset (from the BuildReportInspector package test project). The built-in AssetBundle support was used (`BuildPipeline.BuildAssetBundles`). The archive files have LZMA compression.

Each folder contains `small.bundle` (the AssetBundle) and associated manifest files.

| Folder | Build Flags |
|--------|-------------|
| `AssetBundle-NoTypeTree/` | `BuildAssetBundleOptions.DisableWriteTypeTree` |
| `AssetBundle-NoTypeTreeNoVersion/` | `BuildAssetBundleOptions.DisableWriteTypeTree` + `AssetBundleStripUnityVersion` |

## binary2text Tips

- The `-typeinfo` argument shows the actual contents of the TypeTrees.
- To use binary2text with the extracted versions of the serialized files, you must pass in `AssetBundle.typetreedata` using the `-typetreefile` argument.

Example:
```
binary2text v23_extracted/prefab_with_serializedreference.serializedfile -typetreefile v23_extracted/AssetBundle.typetreedata -typeinfo
```
