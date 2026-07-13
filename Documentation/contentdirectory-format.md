# Content Directory Format

Content directories are a build pipeline introduced in Unity 6.6 for shipping a project's assets as
separate content builds that load alongside a Player build. They are designed as a replacement for
[AssetBundles](assetbundle-format.md), with automatic de-duplication of shared content and per-asset
dependency tracking. Content directories support **local** content only. For the full picture — what
they are, how to build them, and the APIs for loading content — see Unity's Manual topic
[Use content directories to load assets at runtime](https://docs.unity3d.com/6000.6/Documentation/Manual/content-directories.html).

This page focuses on the parts that matter when inspecting a content directory build with
UnityDataTool. It complements the higher-level [Overview of Unity Content](unity-content-format.md),
which introduces SerializedFiles and Unity Archives. It does **not** repeat the Manual: it does not
cover the APIs for building or loading content directories.

## What a content directory build produces

A content directory build produces the same kinds of data as any other Unity build — SerializedFiles
holding the serialized objects, plus companion `.resS` (texture/mesh) and `.resource` (audio/video)
files — together with a build manifest that records what is needed to load the content at runtime.

The output can be written as loose files or packed into a Unity Archive, so UnityDataTool opens it the
same way it opens Player and AssetBundle content. The reference build checked in at
`TestCommon/Data/LeadingEdgeBuilds/ContentDirectory` is an uncompressed build, so its files are loose.

The files use technical, content-hash-based names rather than the familiar `level0` / `sharedassets`
names of a Player build. A detailed explanation of that naming is planned for a future revision of
this page.

## Build history

Every Player and content directory build in Unity 6.6 and later records a **build history** — a set
of files, in a per-build directory, that describe how the build ran and what it produced. By default
these live under `Library/BuildHistory`. The build history is separate from the shipped content and
is not distributed with your application. For the full picture, see the Manual topic
[Analyze builds](https://docs.unity3d.com/6000.6/Documentation/Manual/build-analyze-builds.html).

Most of the files in a build history directory (the Trace Event Profile, `BuildLog.jsonl`,
`ScriptsOnlyCache.yaml`, `ContentSizeSummary.txt`, `BuildReportSummary.json`, and others) are outside
the scope of UnityDataTool and are documented in the Manual's
[Build history file reference](https://docs.unity3d.com/6000.6/Documentation/Manual/build-history-file-reference.html).
Two files in the build history are directly relevant here:

* **The BuildReport file** — the build report for the build, in the same SerializedFile format that
  UnityDataTool reads for Player and AssetBundle builds. In the build history it is named after the
  build session GUID (rather than the fixed `LastBuild.buildreport` name), so reports from multiple
  builds can sit side by side and be analyzed together. See [BuildReport Support](buildreport.md).
* **[`ContentLayout.json`](contentlayout.md)** — maps the built content back to the source assets in
  the project and describes the dependencies between the produced files. It is the key file for
  understanding a content directory build. UnityDataTool support for `ContentLayout.json` is a work in
  progress; for now, see the dedicated [ContentLayout.json](contentlayout.md) page for its structure.

## Inspecting content directory output with UnityDataTool

When you run [`analyze`](command-analyze.md) on a content directory build, analyze the **build output
folder and its matching build history folder together**, in a single `analyze` call, by passing both
paths:

```bash
UnityDataTool analyze /path/to/ContentDirectory /path/to/Library/BuildHistory/<build-directory>
```

Analyzing the build output alone records the objects but not where they came from. The build report
in the build history adds the source-asset mapping (the PackedAssets data), so analyzing the two
together gives a database that ties each built object back to its source asset. See
[BuildReport Support](buildreport.md) for how the build report data is stored and queried.

## Related documentation

| Topic | Description |
|-------|-------------|
| [Use content directories to load assets at runtime](https://docs.unity3d.com/6000.6/Documentation/Manual/content-directories.html) | Unity Manual: what content directories are, and the APIs to build and load them. |
| [Analyze builds](https://docs.unity3d.com/6000.6/Documentation/Manual/build-analyze-builds.html) | Unity Manual: the build history and the build report. |
| [ContentLayout.json](contentlayout.md) | The build layout file that maps content directory output back to source assets. |
| [BuildReport Support](buildreport.md) | Analyzing Unity build report files with UnityDataTool. |
| [Overview of Unity Content](unity-content-format.md) | SerializedFiles, Unity Archives, and TypeTrees. |
| [AssetBundle Format](assetbundle-format.md) | The system that content directories replace. |
