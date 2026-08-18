# LeadingEdge reference builds

The checked-in build output of the `UnityProjects/LeadingEdge` project, for use in automated and ad hoc tests. See that project's `AGENTS.md` for the test scenarios and how the assets are set up.

The LeadingEdge build scripts regenerate this folder directly, so to update it, rebuild in that project and check in the results.

## Layout

* `AssetBundles/` - the AssetBundle build: one bundle per asset (named after the asset) plus the `AssetBundles` manifest bundle. LZMA compressed, so each archive has a single data block.
* `AssetBundlesLz4/` - the same bundle layout built with chunk-based (LZ4) compression, giving archives with multiple data blocks separated by alignment padding (archive format version 9).
* `ContentDirectory/` - the Content Directory build: content (`.cf`) files, `.resource` files and the build manifest.
* `BuildReport-AssetBundles/LastBuild.buildreport` - the AssetBundle build report.
* `BuildReport-ContentDirectory/` - the Content Directory build report folder, including `ContentLayout.json`.
