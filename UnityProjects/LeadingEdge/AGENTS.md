# LeadingEdge test project

A Unity project that tracks the newest Unity version (currently 6000.6.0b3) and is updated proactively so UnityDataTools can be tested against the latest build features - for example Content Directory builds and serialized `Dictionary<,>` fields.

Its build scripts produce the reference output checked in at `TestCommon/Data/LeadingEdgeBuilds` (see that folder's `AGENTS.md`). To update it, rebuild with the scripts below and check in the results.

## Assets

Both builds start from a root ScriptableObject whose serialized dictionary maps names to other ScriptableObjects in the project. The referenced assets cover two scenarios:

1. **AudioClip references.** ScriptableObjects reference the project's two mp3 files. `a.mp3` is referenced from two different ScriptableObjects and `6.mp3` from one, demonstrating that all referenced content is included in the build exactly once, with no duplication.
2. **`SerializationDemo` asset.** A ScriptableObject with fields of many types, including a `[SerializeReference]` field. Useful for testing the `dump` command and for inspecting serialized field values directly.

## Editor scripts (`Assets/Editor`, `ContentDirectory` menu)

* `GenerateAssets.cs` - creates the ScriptableObject assets in `Assets/ScriptableObjects`, populating the serialized dictionaries before saving so the entries are serialized into the assets.
* `BuildAssetBundles.cs` - runs the AssetBundle build and copies its build report.
* `BuildContentDirectory.cs` - runs the Content Directory build and copies its build report folder.

Both build scripts write directly into `TestCommon/Data/LeadingEdgeBuilds` using paths relative to the project root.

## Content Directory Build

The root asset is `ContentDirectoryRoot.asset`. It directly references the `LoadableAudioClipReference` assets, so those are loaded automatically when the content directory is registered. The AudioClips themselves are referenced through `Loadable<T>`, so they are included in the build but loaded only on demand.

## AssetBundle Build

The root asset is `AssetBundleRoot.asset`. AssetBundles do not support `Loadable<T>`, so this build uses the direct-reference variants of the assets instead. Each asset is placed in its own bundle (named after the asset) - a highly granular layout that guarantees no content is duplicated across bundles.
