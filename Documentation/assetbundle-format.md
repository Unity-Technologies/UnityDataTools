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

Because a bundle is just an archive, `analyze` records it in the generic `archives` table rather than
anything AssetBundle-specific: one row per bundle file (its name and size), and every SerializedFile
inside it points back through `serialized_files.archive`. In query results this surfaces as the
`archive` column of `object_view` (the containing bundle's name, or NULL for a bare SerializedFile).
The same table and column also represent Player and ContentDirectory archives, so a query written
against `archive` works across all of them. Data that is genuinely specific to the AssetBundle *object*
lives in the separate `assetbundle_assets` / `preload_dependencies` tables (see
[How `analyze` represents this](#how-analyze-represents-this)).

### SerializedFile names inside a bundle

The names of the SerializedFiles inside a bundle are technical and hash-based. You do not need to
understand them for normal use, but they show up throughout UnityDataTool output:

- **Regular (non-scene) bundles** contain one SerializedFile named `CAB-<hash>`, where the hash is
  the **MD4** hash of the AssetBundle name (not the `Hash128` / spooky hash exposed in the C# API).
- **Scene bundles** name their scene files differently depending on the build pipeline:
  - `BuildPipeline.BuildAssetBundles` uses `BuildPlayer-<SceneName>`.
  - The Scriptable Build Pipeline / Addressables uses `CAB-<hash of the scene path>`.
  - The Multi-Process Build Pipeline (2023.1+) uses `CAB-<scene GUID>`.

## Bundle dependencies and object references

AssetBundle dependencies exist at two related but different levels:

- The AssetBundle object records bundle-level dependencies in `m_Dependencies`: as AssetBundle
  names for `BuildPipeline.BuildAssetBundles`, or as internal SerializedFile names for Scriptable
  Build Pipeline / Addressables builds.
- Serialized objects record object-level references as `PPtr`s, which point to a file and a local
  object id.

The bundle filename itself is not written into every object reference. Instead, loading a bundle
mounts its archive and makes the SerializedFiles inside it available to Unity's object resolver.
When Unity follows a `PPtr`, it resolves the reference through the current SerializedFile and that
file's external reference table.

A `PPtr` has two important fields:

- `m_FileID` - `0` means the target object is in the same SerializedFile. Values `1`, `2`, and so on
  refer to entries in that SerializedFile's external reference table.
- `m_PathID` - the target object's local object id inside the resolved SerializedFile.

This means that two different bundles can have different filenames while still referencing each
other through the SerializedFiles mounted from those bundles. The application is still responsible
for loading dependency bundles. With `BuildPipeline.BuildAssetBundles`, the `AssetBundleManifest`
tells the application which bundles to load; with Addressables, the Addressables catalog does that
job. Unity's runtime does not auto-load bundles from `m_Dependencies`.

At runtime, `m_Dependencies` is used only in narrower lookup paths, such as searching already-loaded
dependency bundles for preload objects. If a named dependency bundle is not loaded, Unity skips it
rather than loading it automatically. Once the needed SerializedFiles are mounted, object references
point into those files by `m_FileID` and `m_PathID`.

For example, if an object in `characters.bundle` references a material in `materials.bundle`, the
object's `PPtr` does not contain the string `materials.bundle`. The source SerializedFile contains
an external reference entry that identifies the target SerializedFile by its internal path, for
example `archive:/CAB-<hash>/CAB-<hash>`. The dependency relationship in `m_Dependencies` helps
Unity search among already-loaded dependency bundles for preload objects, but the application still
uses the AssetBundleManifest or Addressables catalog to decide which archive files to load.

In UnityDataTool output, these layers appear in different places:

- The containing archive or bundle file appears as `archive` in `object_view`.
- The mounted SerializedFile appears as `serialized_file` in `object_view`.
- Object-to-object references appear in the `refs` table when `analyze` runs with reference
  extraction enabled.
- Preload relationships from the AssetBundle object and scene `PreloadData` appear in
  `preload_dependencies`.

Keeping those layers separate helps explain why a query over `refs` may show cross-bundle
relationships without directly mentioning an AssetBundle filename on each reference row.

## Built-in resources

Bundle content can reference objects in Unity's two built-in resource files (described in
[Built-in resource files](playerbuild-format.md#built-in-resource-files) on the Player build page).
`BuildPipeline.BuildAssetBundles` handles the two differently:

- References to **`Library/unity default resources`** objects (default meshes, the RenderSettings
  spot cookie, and so on) always stay external references. This is safe: that file is identical in
  every Player of the same Unity version, so the referenced objects are always available at runtime.
- Objects from **`Resources/unity_builtin_extra`** (built-in shaders and materials) are treated like
  user assets and copied into the bundle that uses them — with one exception: shaders in the
  project's **Always Included Shaders** list (Graphics Settings) are not copied, but referenced
  externally from `Resources/unity_builtin_extra`. A Player build writes exactly those shaders into
  its copy of that file, so the references resolve when the bundles and the Player are built from
  the same project settings.

For example, in the reference scene bundle in `TestCommon/Data/LeadingEdgeBuilds/AssetBundles`, the
`Sprites-Default` Material and the default reflection Cubemap are copied into the bundle's
`.sharedAssets` file, but the material's `Sprites/Default` *shader* stays an external reference into
`Resources/unity_builtin_extra`, because that shader is in the default Always Included Shaders list.

Those external shader references couple a bundle to the Player that loads it: if the Player was
built with a different Always Included Shaders list (a different project, or settings that changed
between builds), the bundle can reference a shader the Player does not contain. When you run
[`analyze`](command-analyze.md) on bundles, references into either built-in file show up in
`dangling_refs_view`, because the built-in files themselves are never part of the bundle build
output.

The Scriptable Build Pipeline avoids the coupling entirely: it never references the Player's
`unity_builtin_extra`, and instead copies the referenced built-in objects into each bundle that uses
them (references to `unity default resources` stay external, as above). The cost is duplication when
several bundles use the same built-in shader; the optional `CreateBuiltInBundle` build task removes
that by collecting the built-in objects into a single dedicated bundle. The Addressables package
enables that task in its default build, so Addressables content contains its own copy of the
built-in shaders it needs and does not depend on the Player's Always Included Shaders list.

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
- `m_Dependencies` - the other bundles or internal SerializedFiles this bundle depends on, stored
  as AssetBundle names for `BuildPipeline.BuildAssetBundles` or as internal SerializedFile names
  for Scriptable Build Pipeline / Addressables builds.
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

- Each object is linked to both its `serialized_file` and, when applicable, its containing `archive`
  in `object_view`.
- Object references extracted from `PPtr` fields become rows in `refs`; these rows are resolved to
  analyzer object ids rather than storing AssetBundle filenames directly.
- Each explicit `m_Container` asset becomes a row in `assetbundle_assets`, and its preload slice
  becomes rows in `preload_dependencies`.
- Because a scene has no Unity object, `analyze` synthesizes a "Scene" object per scene to stand in
  for it, so scenes appear in `assetbundle_asset_view` and their PreloadData dependencies appear in
  `preload_dependencies_view`.

See [Analyzer](analyzer.md) for the full schema and the exact behaviour of those views.
