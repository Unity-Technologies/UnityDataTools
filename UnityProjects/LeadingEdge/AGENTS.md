# LeadingEdge test project

A Unity project that tracks the newest Unity version (currently 6000.7.0f1) and is updated proactively so UnityDataTools can be tested against the latest build features - for example Content Directory builds and serialized `Dictionary<,>` fields.

Its build scripts produce the reference output checked in at `TestCommon/Data/LeadingEdgeBuilds` (see that folder's `AGENTS.md`). To update it, rebuild with the scripts below and check in the results.

## Assets

Both builds start from a root ScriptableObject whose serialized dictionary maps names to other ScriptableObjects in the project. The referenced assets cover these scenarios:

1. **AudioClip references.** ScriptableObjects reference the project's two mp3 files. `a.mp3` is referenced from two different ScriptableObjects and `6.mp3` from one, demonstrating that all referenced content is included in the build exactly once, with no duplication.
2. **`SerializationDemo` asset.** A ScriptableObject with fields of many types, including a `[SerializeReference]` field. Useful for testing the `dump` command and for inspecting serialized field values directly.
3. **Scenes.** Two small scenes (`Assets/Scenes/Scene1.unity`, `Scene2.unity`) that both show the shared `GreenStatic.png` texture through a SpriteRenderer, with distinct GameObject names so their content differs. The `SceneList` asset references them by name through a serialized dictionary of `LoadableSceneId`.
4. **VideoClip.** `Assets/Video/basic_clip_no_audio_160_100_1_1_50_100_H264.mp4`, a tiny clip (160x100, 50 fps, 100 frames, no audio) kept small for repo size. The `VideoClipReference` ScriptableObject references it directly from both build roots. Its mp4 data is stored unchanged in a `.resource` file.

## Editor scripts (`Assets/Editor`, `ContentDirectory` menu)

* `GenerateAssets.cs` - creates the ScriptableObject assets in `Assets/ScriptableObjects`, populating the serialized dictionaries before saving so the entries are serialized into the assets.
* `BuildAssetBundles.cs` - runs the AssetBundle build and copies its build report. It has a second entry point, `BuildLz4`, that repeats the build with chunk-based (LZ4) compression into a separate folder.
* `BuildContentDirectory.cs` - runs the Content Directory build and copies its build report folder.

Both build scripts write directly into `TestCommon/Data/LeadingEdgeBuilds` using paths relative to the project root.

## Regenerating the reference builds

Run the editor scripts in batchmode with an editor matching the project version:

    Unity.exe -batchmode -quit -projectPath UnityProjects/LeadingEdge -executeMethod <GenerateAssets.Generate|BuildAssetBundles.Build|BuildAssetBundles.BuildLz4|BuildContentDirectory.Build> -logFile <log>

* `ProjectVersion.txt`, `Packages/` and `Library/` are gitignored; the version above is the tracked one. Opening the project with an older editor than the one that last opened it fails to resolve packages, so use the tracked version.
* Builds are deterministic: rebuilding without asset changes leaves the output byte-identical.
* `GenerateAssets` rewrites `SerializationDemo.asset` with a new random `[SerializeReference]` `rid`, and the two scenes with new random fileIDs (content unchanged). Revert those files unless they actually changed, otherwise the scene bundles change for no reason.

## Content Directory Build

The root asset is `ContentDirectoryRoot.asset`. It directly references the `LoadableAudioClipReference` assets, so those are loaded automatically when the content directory is registered. The AudioClips themselves are referenced through `Loadable<T>`, so they are included in the build but loaded only on demand. It also directly references the `SceneList` asset, whose `LoadableSceneId` entries pull the two scenes into the build; each scene becomes its own Content File. `VideoClipReference` is referenced directly, and pulls the VideoClip into the build.

## AssetBundle Build

The root asset is `AssetBundleRoot.asset`. AssetBundles do not support `Loadable<T>`, so this build uses the direct-reference variants of the assets instead. Each asset, including the mp3s and the mp4, is placed in its own bundle (named after the asset) - a highly granular layout that guarantees no content is duplicated across bundles. The two scenes are placed together in a `scenes` bundle, since AssetBundles require scenes and assets in separate bundles.

The default build uses LZMA, which produces a single streamed data block per archive. The `Build AssetBundles (LZ4)` menu item repeats the same bundle layout with chunk-based compression, which produces archives with many small blocks. The writer leaves padding between those blocks, so they exercise the per-block `StorageBlock.Offset` added in archive format version 9.
