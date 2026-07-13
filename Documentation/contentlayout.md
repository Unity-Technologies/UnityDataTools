# ContentLayout.json

`ContentLayout.json` describes the content that a content directory build produced. It is written by [`BuildPipeline.BuildContentDirectory`](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/BuildPipeline.BuildContentDirectory.html) into the build report directory, alongside the other build report files. For an overview of the build report directory and the other files it contains, see [Build report and build history](https://docs.unity3d.com/6000.6/Documentation/Manual/build-history.html) in the Unity Manual.

This page explains what the file contains conceptually to aid in creation of build-analysis tooling or inspection of content directory build output.  The C# types that define the schema are published alongside this documentation in [`ContentLayout.cs`](../UnityDataModels/ContentLayout.cs), which is the authoritative reference for the individual fields.

## What it is for

Use `ContentLayout.json` to:

* Find which source assets a build included, and which serialized file each one ended up in.
* See the dependencies between the files the build produced.
* Understand what contributes to the size or loading footprint of the content in the runtime.

## Relationship to the build manifest

`ContentLayout.json` is a superset of the build manifest that Unity ships with the content. The build manifest is a minimal summary that contains only what is required to load the content at runtime. `ContentLayout.json` is not shipped, and adds a more complete picture, including a mapping from the built content back to the source assets in the project.

## What it does not contain

* **Packaging information.** It does not record how the content is stored, for example whether the artifacts are packed inside Unity Archive files. It describes the logical content, not its on-disk packaging.
* **Object-level detail.** It does not describe individual Unity objects inside the files, and contains no type information. This is intentional, to keep the file size reasonable. For object-level analysis, run [`UnityDataTool analyze`](command-analyze.md) on the build output. For per-type and per-asset size statistics, use the `ContentSummary` of the [BuildReport](buildreport.md).
* **Non-deterministic data.** To avoid non-deterministic data this file contains no timestamps, and no information about the build process that created it. Two builds that produce identical content produce an identical `ContentLayout.json`.

## Terminology

The file uses a few terms consistently:

* **Artifact** — a unit of build output. The term is used instead of "file" because the build output is not necessarily written as individual files (for example, it could be packed inside Unity Archives, or served from key/value storage).
* **SerializedFile** — a Unity binary file containing serialized objects (the `.cf` content files of the build). See [Unity Content Format](unity-content-format.md).
* **Loadable** — an object that can be loaded on demand, identified independently of the serialized file that happens to contain it.  Loadable will reference a specific object, for example the root GameObject of a prefab, but loading that object will load the entire SerializedFile.
* **LoadableSceneId** — Similar to a Loadable, but referencing a Unity Scene.
* **Source asset** — an asset in the source project (identified by GUID and asset path) that contributed to the build output.

## Top-level structure

`ContentLayout.json` is a single JSON object with the following members. Entries in several of the arrays cross-reference each other by array index or by hash, so the file describes a graph rather than a flat list.

| Member | Description |
|--------|-------------|
| `Version` | Schema version of the file. See [Schema versioning](#schema-versioning). |
| `BuildManifestHash` | Hash of the build manifest this layout corresponds to. |
| `SerializedFiles` | One entry per serialized file in the build. Each entry records the source assets it contains, its content hash, and its dependencies on other serialized files, loadables, and loadable scenes. The same source asset can appear in more than one serialized file (for example, a single FBX file can be split into multiple output files). |
| `RootAssets` | The `ObjectIdHash` of each root asset the build was made from. Each one has a corresponding entry in `LoadableObjectIds`. |
| `LoadableObjectIds` | The objects that can be loaded on demand. Each entry records its `ObjectIdHash`, where the object lives in the built content (which serialized file) and where it came from in the source project (GUID, asset path, local file ID, and identifier type). |
| `LoadableSceneIds` | The scenes in the build, each with its source project path and GUID, and the serialized file that contains it. |
| `BinaryArtifacts` | The artifacts that make up the build output. See [Binary artifacts](#binary-artifacts). |

### Binary artifacts

`BinaryArtifacts` is essentially the list of files in the build output. Each entry has a `Category` and a `Size`, and lists its direct dependencies in `ArtifactReferences`:

* The entry with category `manifest` is the root of the graph.
* Entries with category `contentfile` each have a matching entry in `SerializedFiles` (matched by content hash).
* `BinaryArtifacts` also reports the additional data files that hold audio, video, texture, and mesh data (the `.resource` and `.resS` files).
* BinaryArtifacts are identified by the hash of their content.  When saved as a file, the filename is the hash and the file extension is based on the category.

`ArtifactReferences` lists only direct dependencies. Dependencies that go through a loadable or loadable scene are not included, and the dependency graph is never cyclical. Together, this makes it possible to see every artifact required to load a particular serialized file, excluding data that is loaded on demand through a loadable or loadable scene.

## Schema versioning

The schema is subject to change. The `Version` field records the schema version of the file, independently of the Unity version that produced it. When the schema changes, the version number increments.

[`ContentLayout.cs`](../UnityDataModels/ContentLayout.cs) always represents the latest schema version (currently version 1).

## Related documentation

| Topic | Description |
|-------|-------------|
| [Content Directory Format](contentdirectory-format.md) | Content directory builds and inspecting them with UnityDataTool. |
| [Build report and build history](https://docs.unity3d.com/6000.6/Documentation/Manual/build-reporting.html) | The build report directory and the files in it (Unity Manual). |
| [BuildReport Support](buildreport.md) | Analyzing Unity build report files with UnityDataTool. |
| [Unity Content Format](unity-content-format.md) | SerializedFiles, Unity Archives, and how build output maps back to source assets. |
| [`analyze` command](command-analyze.md) | Object-level analysis of build output. |
