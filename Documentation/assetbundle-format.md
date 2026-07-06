# AssetBundle Format

This topic digs into the internal layout of AssetBundles, with a focus on the parts that are useful
when inspecting an AssetBundle with UnityDataTool. It complements the higher-level
[Overview of Unity Content](unity-content-format.md), which introduces SerializedFiles and Unity
Archives.

For Unity's official reference on the container format, see
[AssetBundle file format](https://docs.unity3d.com/Manual/assetbundles-file-format.html). 
This page builds on that information with more technical information and examples of how 
these data structures can be examined.

## AssetBundles are Unity Archives

An AssetBundle is a [Unity Archive](unity-content-format.md#unity-archive) with some conventions for
what lives inside. The [Addressables](https://docs.unity3d.com/Manual/com.unity.addressables.html)
package builds its content as AssetBundles too, so the same layout applies there.

An AssetBundle always contains at least one SerializedFile, and may contain auxiliary files such as
`.resS` (Textures and Meshes) and `.resource` (audio/video).

### SerializedFile names inside a bundle

The names of the SerializedFiles inside a bundle are technical and hash-based. You do not need to
understand them for normal use, but they show up throughout UnityDataTool output:

- **Regular (non-scene) bundles** contain one SerializedFile named `CAB-<hash>`, where the hash is
  the **MD4** hash of the AssetBundle name (not the `Hash128` / spooky hash exposed in the C# API).
- **Scene bundles** name their scene files differently depending on the build pipeline:
  - `BuildPipeline.BuildAssetBundles` uses `BuildPlayer-<SceneName>`.
  - The Scriptable Build Pipeline / Addressables uses `CAB-<hash of the scene path>`.
  - The Multi-Process Build Pipeline (2023.1+) uses `CAB-<scene GUID>`.

## Inspecting a bundle with UnityDataTool

The [`archive`](command-archive.md) command lists or extracts the files inside a bundle, and
[`dump`](command-dump.md) / [`serialized-file`](command-serialized-file.md) inspect the
SerializedFiles. A typical workflow is to extract the bundle into a folder and then dump specific
objects:

```
UnityDataTool archive extract mybundle.bundle -o extracted
cd extracted
UnityDataTool sf objectlist CAB-<hash>
UnityDataTool dump --stdout CAB-<hash> --type AssetBundle
```

## The AssetBundle object

Every bundle has one **AssetBundle** object (ClassID 142). It records which assets the bundle
exposes and what each of them needs preloaded. Its important fields are:

- `m_Container` - a map from an asset's address/path to the object it maps to, plus the
  `preloadIndex`/`preloadSize` range describing that asset's dependencies (see below).
- `m_PreloadTable` - a flat list of `PPtr`s. Each container entry references a contiguous slice of
  this list.
- `m_Dependencies` - the names of other bundles this bundle depends on.
- `m_IsStreamedSceneAssetBundle` - true for scene bundles.
- `m_SceneHashes` - for scene bundles, a map from each scene path to the SerializedFile that holds
  it (populated by SBP/Addressables only; see [Scenes in AssetBundles](#scenes-in-assetbundles)).

In a regular bundle the AssetBundle object is typically at local file id (LFID) 1. In a scene bundle
it is at LFID 2, because the PreloadData object takes LFID 1.

### Preload for assets (non-scene bundles)

For a regular asset, `m_Container` names the asset and points at its main object, and
`preloadIndex` + `preloadSize` select the slice of `m_PreloadTable` listing every object that must
be loaded for that asset (including objects found in SerializedFiles in other dependent AssetBundles). This is how Unity knows the full set of objects to load in order to fully load an asset.

For example, a bundle whose single ScriptableObject references an AudioClip stored in another
bundle:

```
ID: 1 (ClassID: 142) AssetBundle
  m_Name (string) directaudioclipreference
  m_PreloadTable (vector)
    Array<PPtr<Object>>[4]
      data[0] (PPtr<Object>)
        m_FileID (int) 1
        m_PathID (SInt64) -895285400835485219
      data[1] (PPtr<Object>)
        m_FileID (int) 0
        m_PathID (SInt64) -602721743313719314
      data[2] (PPtr<Object>)
        m_FileID (int) 0
        m_PathID (SInt64) 8127315791103748070
      data[3] (PPtr<Object>)
        m_FileID (int) 2
        m_PathID (SInt64) -5151917721481642524
  m_Container (map)
    Array<pair>[1]
      data[0] (pair)
        first (string) assets/scriptableobjects/directaudioclipreference.asset
        second (AssetInfo)
          preloadIndex (int) 0
          preloadSize (int) 4
          asset (PPtr<Object>)
            m_FileID (int) 0
            m_PathID (SInt64) -602721743313719314
  m_Dependencies (vector)
    Array<string>[2]
      data[0] (string) 6
      data[1] (string) a
  m_IsStreamedSceneAssetBundle (bool) False
```

Here the single asset `directaudioclipreference.asset` has `preloadIndex 0` and `preloadSize 4`, so
its dependencies are `m_PreloadTable[0..3]`. Those `PPtr`s use `m_FileID` to say which file each
object lives in: `0` is this file, and `1`/`2` are entries in the file's external reference table -
in this case objects in the dependency bundles `6` and `a` listed in `m_Dependencies`.

> [!NOTE]
> `m_Container` only records the assets that were **explicitly** added to the bundle. Assets that
> are pulled in implicitly (because an explicit asset references them) appear in `m_PreloadTable`
> and the dependency bundles, but are not listed as their own container entries.

## Scenes in AssetBundles

Scenes are a special case, where the component hierarchy requires a dedicated serialized file that is entirely
loaded each time a scene is loaded. Unity's build pipeline emits **two SerializedFiles
per scene**:

- `<scene>` - the scene's own contents (the GameObject/Transform hierarchy, RenderSettings,
  LightmapSettings, and so on).
- `<scene>.sharedAssets` - the objects referenced by the scene (Materials, Meshes, MonoScripts,
  etc.). To avoid duplication, a scene's files may reference objects in *other* scenes' `.sharedAssets`
  files, but there is never an external reference **into** a scene's own contents file.  Note: in some cases a scene will have no additional external references apart from the references already saved in other .sharedAssets files. So in that case there will be no sharedAssets file for that scene.

Extracting a simple single-scene bundle shows the pair:

```
UnityDataTool archive list scene1.bundle
```
```
BuildPlayer-Scene1.sharedAssets
  ...
  Flags: SerializedFile

BuildPlayer-Scene1
  ...
  Flags: SerializedFile
```

A scene bundle contains exactly **one AssetBundle object**, in one of the `.sharedAssets` files.
Its `m_Container` lists every scene in the bundle by `.unity` path, and each scene's container entry
has a null `asset` PPtr (`m_FileID 0`, `m_PathID 0`) because there is no object to point at:

```
UnityDataTool dump --stdout BuildPlayer-Scene1.sharedAssets --type AssetBundle
```
```
ID: 2 (ClassID: 142) AssetBundle
  m_Name (string) scene1.bundle
  m_PreloadTable (vector)
    Array<PPtr<Object>>[0]
  m_Container (map)
    Array<pair>[1]
      data[0] (pair)
        first (string) Assets/AssetDuplication/Scene1.unity
        second (AssetInfo)
          preloadIndex (int) 0
          preloadSize (int) 0
          asset (PPtr<Object>)
            m_FileID (int) 0
            m_PathID (SInt64) 0
  m_IsStreamedSceneAssetBundle (bool) True
  m_SceneHashes (map)
    Array<pair>[0]
```

Note: the layout for scenes inside an AssetBundle is basically the same as how a Player build builds scenes (except different naming convention for the serialized files). In fact BuildPipeline.BuildAssetBundles and BuildPipeline.BuildPlayer() share much of the same code for processing scenes.

### m_SceneHashes: mapping a scene to its SerializedFile

Because SBP/Addressables name their scene files `CAB-<hash>`, the file name alone does not reveal
which scene it holds. The AssetBundle object records the mapping in `m_SceneHashes`, from the scene
path to the scene's SerializedFile name:

```
  m_SceneHashes (map)
    Array<pair>[145]
      data[0] (pair)
        first (string) Assets/Scenes/Dungeons/BaneChamber/DUN_BaneChamber_DESIGN.unity
        second (string) CAB-256771f7b55e388852b84baa22aeb5b2
```

`BuildPipeline.BuildAssetBundles` leaves `m_SceneHashes` **empty** (as in the single-scene example
above) and instead encodes the scene name directly in the file name (`BuildPlayer-<SceneName>`).

### PreloadData

Each scene's `.sharedAssets` file contains a **PreloadData** object (ClassID 150) at LFID 1. Its
`m_Assets` vector lists the `PPtr`s of everything the scene depends on; at runtime Unity walks this
list to load all required objects before the scene loads.

```
UnityDataTool dump --stdout BuildPlayer-Scene1.sharedAssets --type PreloadData
```
```
ID: 1 (ClassID: 150) PreloadData
  m_Name (string)
  m_Assets (vector)
    Array<PPtr<Object>>[2]
      data[0] (PPtr<Object>)
        m_FileID (int) 1
        m_PathID (SInt64) 10001
      data[1] (PPtr<Object>)
        m_FileID (int) 2
        m_PathID (SInt64) 4900368479417156912
  m_Dependencies (vector)
    Array<string>[1]
      data[0] (string) imagelist.bundle
```

As with the AssetBundle preload table, each `m_Assets` entry's `m_FileID` selects the file: `0` is
this file and `1`, `2`, ... are entries in the file's external reference table (other bundles, or
Unity's built-in resource files). Here `data[1]` points into another bundle (`imagelist.bundle`).

Because PreloadData maps out the whole dependency graph of the objects referenced from a scene, its
size grows with the number of dependencies. Projects with very large or numerous hard references
(for example a MonoBehaviour in a scene directly referencing thousands of prefabs) can end up with
PreloadData tables containing huge numbers of entries, adding significant metadata overhead to the
build. If PreloadData is contributing excessive size or load time, the usual fix is to break up
large dependency graphs by loading some assets on demand (via Addressables, AssetBundles, or
`Resources.Load`) instead of using direct hard references.

## How `analyze` represents this

The [`analyze`](analyzer.md) command turns the above into queryable tables:

- Each explicit `m_Container` asset becomes a row in `assetbundle_assets`, and its preload slice
  becomes rows in `preload_dependencies`.
- Because a scene has no Unity object, `analyze` synthesizes a "Scene" object per scene to stand in
  for it, so scenes appear in `assetbundle_asset_view` and their PreloadData dependencies appear in
  `preload_dependencies_view`.

See [Analyzer](analyzer.md) for the full schema and the exact behaviour of those views.
