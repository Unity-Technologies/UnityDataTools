# LeadingEdge test project

A Unity project that tracks the newest Unity version (currently 6000.7.0f1) and is updated proactively so UnityDataTools can be tested against the latest build features - for example Content Directory builds and serialized `Dictionary<,>` fields.

Its build scripts produce the reference output checked in at `TestCommon/Data/LeadingEdgeBuilds` (see that folder's `AGENTS.md`). To update it, rebuild with the scripts below and check in the results.

## Assets

Both builds start from a root ScriptableObject whose serialized dictionary maps names to other ScriptableObjects in the project. The referenced assets cover these scenarios:

1. **AudioClip references.** ScriptableObjects reference the project's two mp3 files. `a.mp3` is referenced from two different ScriptableObjects and `6.mp3` from one, demonstrating that all referenced content is included in the build exactly once, with no duplication.
2. **`SerializationDemo` asset.** A ScriptableObject with fields of many types, including a `[SerializeReference]` field. Useful for testing the `dump` command and for inspecting serialized field values directly.
3. **Scenes.** Two small scenes (`Assets/Scenes/Scene1.unity`, `Scene2.unity`) that both show the shared `GreenStatic.png` texture through a SpriteRenderer, with distinct GameObject names so their content differs. The `SceneList` asset references them by name through a serialized dictionary of `LoadableSceneId`.
4. **VideoClips.** Two tiny clips in `Assets/Video`, kept small for repo size. The `VideoClipReference` ScriptableObject references every clip in that folder, and is included in both build roots. Each clip's mp4 is stored unchanged in a `.resource` file, together with its audio.
   * `basic_clip_no_audio_160_100_1_1_50_100_H264.mp4` - 160x100, 50 fps, 100 frames, no audio.
   * `clip_audio_eng_stereo_fra_mono_160_100_1_1_25_50_H264.mp4` - 160x100, 25 fps, 50 frames, with two audio tracks so the VideoClip's per-track arrays have entries: English stereo 48 kHz and French mono 44.1 kHz. Unity records the languages as `en` and `fr`. It is synthetic (ffmpeg's `testsrc2` pattern and sine tones), generated with:

         ffmpeg -f lavfi -i "testsrc2=size=160x100:rate=25:duration=2" -f lavfi -i "sine=frequency=440:sample_rate=48000:duration=2" -f lavfi -i "sine=frequency=660:sample_rate=44100:duration=2" -filter_complex "[1:a]pan=stereo|c0=c0|c1=c0[en]" -map 0:v -map "[en]" -map 2:a -c:v libx264 -profile:v baseline -pix_fmt yuv420p -preset veryslow -crf 35 -c:a aac -b:a:0 48k -b:a:1 32k -ac:a:1 1 -metadata:s:a:0 language=eng -metadata:s:a:0 title=English -metadata:s:a:1 language=fra -metadata:s:a:1 title=French -disposition:a:0 default -disposition:a:1 0 -map_metadata -1 -fflags +bitexact -flags:v +bitexact -flags:a +bitexact -movflags +faststart <output>.mp4

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

The root asset is `ContentDirectoryRoot.asset`. It directly references the `LoadableAudioClipReference` assets, so those are loaded automatically when the content directory is registered. The AudioClips themselves are referenced through `Loadable<T>`, so they are included in the build but loaded only on demand. It also directly references the `SceneList` asset, whose `LoadableSceneId` entries pull the two scenes into the build; each scene becomes its own Content File. `VideoClipReference` is referenced directly, and pulls the VideoClips into the build.

## AssetBundle Build

The root asset is `AssetBundleRoot.asset`. AssetBundles do not support `Loadable<T>`, so this build uses the direct-reference variants of the assets instead. Each asset, including the mp3s and the mp4s, is placed in its own bundle (named after the asset) - a highly granular layout that guarantees no content is duplicated across bundles. The two scenes are placed together in a `scenes` bundle, since AssetBundles require scenes and assets in separate bundles.

The default build uses LZMA, which produces a single streamed data block per archive. The `Build AssetBundles (LZ4)` menu item repeats the same bundle layout with chunk-based compression, which produces archives with many small blocks. The writer leaves padding between those blocks, so they exercise the per-block `StorageBlock.Offset` added in archive format version 9.
