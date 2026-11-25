# LeadingEdge reference builds

The checked-in build output of the `UnityProjects/LeadingEdge` project, for use in automated and ad hoc tests. See that project's `AGENTS.md` for the test scenarios and how the assets are set up.

The LeadingEdge build scripts regenerate this folder directly, so to update it, rebuild in that project and check in the results.

This data is rebuilt with each new Unity version, so tests must not hard-code values that a rebuild changes: content hashes, the build report's GUID-based name, ContentLayout indices, file sizes and object counts. Read them from the data instead (`UnityDataTool.Tests/LeadingEdgeTestData.cs`), or cross-check one source against another. Values fixed by the project's assets (asset GUIDs from `.meta` files, names, serialized field values, the mp4's size) are fine to assert, and change only when the assets do. Rebuilding without asset changes should need no test edits. The frozen fixtures from older Unity versions elsewhere in `TestCommon/Data` are not rebuilt, so tests can check their exact values.

## Layout

* `AssetBundles/` - the AssetBundle build: one bundle per asset (named after the asset) plus the `AssetBundles` manifest bundle. LZMA compressed, so each archive has a single data block.
* `AssetBundlesLz4/` - the same bundle layout built with chunk-based (LZ4) compression, giving archives with multiple data blocks. The writer leaves padding between the blocks, so these exercise the `StorageBlock.Offset` field added in archive format version 9.
* `ContentDirectory/` - the Content Directory build: content (`.cf`) files, `.resource` files and the build manifest.
* `BuildReport-AssetBundles/LastBuild.buildreport` - the AssetBundle build report.
* `BuildReport-ContentDirectory/` - the Content Directory build report folder, including `ContentLayout.json`.
