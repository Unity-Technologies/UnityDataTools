# ContentLayout in the Analyze Database

When the input of the [`analyze`](command-analyze.md) command includes the `ContentLayout.json` of a content directory build, its content is imported into the database tables and views documented on this page. This makes the layout of the build queryable with SQL — which source assets each file was built from, the dependencies between the files, the loadable objects and scenes, and the size of every artifact — and connects that information to the objects and references that `analyze` extracts from the build content itself.

The layout is also what makes reference information correct for a ContentDirectory build: the external reference tables inside the build's files hold symbolic placeholders, and analyze resolves them through the layout's dependency lists. With the layout on the input, references between content files land in the regular `refs` table (and [`find-refs`](command-find-refs.md) works across files); without it they are recorded in [`dangling_refs`](analyzer.md#dangling_refs--dangling_refs_view).

For what `ContentLayout.json` contains conceptually, see [ContentLayout.json](contentlayout.md); for the build output format it describes and the reference-resolution mechanics, see [Content Directory Format](contentdirectory-format.md). For the input combinations analyze accepts (including how the layout is matched to the build), see [the `analyze` command](command-analyze.md#contentdirectory-builds). Example queries: [Example queries for ContentDirectory builds](analyze-examples-contentlayout.md).

## General notes

* The `content_layout*` tables and views are only created when a `ContentLayout.json` is actually part of the analyzed input, so databases produced from AssetBundles or Player builds are not cluttered with empty tables.
* A single layout per database is supported. When several `ContentLayout.json` files are on the input, the one whose `BuildManifestHash` matches the analyzed build is selected and the rest are ignored (see [the `analyze` command](command-analyze.md#contentdirectory-builds)).
* The tables mirror the json structure. Entries that cross-reference each other by array index in the json do the same in the database: `file_index` and `artifact_index` columns hold the json array indexes.
* Two adjustments are made so the data is natural to query:
  * The json's sentinel values are stored as SQL `NULL`: a `SerializedFile` value of `-1` (an object dropped from the build) and the missing `ContentHash` of built-in entries.
  * The top-level `RootAssets` list is folded into the `is_root_asset` flag of `content_layout_loadable_objects`.
* The `.sql` files in [`Analyzer/Resources`](../Analyzer/Resources) (`ContentLayout*.sql`) are the authoritative reference for the exact columns, in the same way [`ContentLayout.cs`](../UnityDataModels/ContentLayout.cs) is for the json schema.

## Tables

### content_layout

One row identifying the imported layout: `name` (the path of the imported file), `version` (the json schema version) and `build_manifest_hash`.

### content_layout_serialized_files

One row per serialized file (`.cf` Content File) of the build, keyed by `file_index`. Records the symbolic `cfid` reference string, the `is_builtin` flag, and the `content_hash` that gives the filename (`content_hash || '.cf'`).

The `serialized_file` column references the core `serialized_files` table, connecting the layout to the analyzed objects (see [below](#analyzing-the-layout-with-or-without-the-build-content) for what the referenced row contains when the build content was not analyzed).

### content_layout_source_assets

The source assets included in each serialized file, one row per (`serialized_file_index`, `asset_path`) pair. The same asset path can appear in more than one file (for example, a single FBX split into multiple output files).

### content_layout_serialized_file_dependencies

File-to-file dependency edges: the other serialized files that must be loaded before this one (`serialized_file_index` → `dependency_index`). The 1-based `position` column preserves the json array order, which is significant: a PPtr's `m_FileID` inside the file resolves positionally through this list (see [Content Directory Format](contentdirectory-format.md)).

### content_layout_loadable_dependencies / content_layout_loadable_scene_dependencies

The loadables (`object_id_hash`) and scenes (`scene_path`) referenced from each serialized file.

### content_layout_loadable_objects

The objects that can be loaded on demand, keyed by `object_id_hash`. Each row records where the object came from in the source project (`guid`, `asset_path`, `lfid`, `identifier_type`) and where it lives in the built content (`serialized_file_index`, `output_lfid`). `is_root_asset` marks the root assets the build was made from.

### content_layout_loadable_scenes

The scenes exposed as loadable in the build: `guid`, `path` and the `serialized_file_index` of the file containing the scene.

### content_layout_binary_artifacts

Every artifact of the build output, keyed by `artifact_index`: the serialized files themselves (`category = 'contentfile'`), the streamed texture/mesh data (`'texture'`, `'mesh'` → `.resS` files), the audio/video data (`'audio'`, `'video'` → `.resource` files) and the manifest (`'manifest'`). This is the standard place to find artifact sizes, including the sizes of the serialized files, which no core table records.

### content_layout_artifact_references

Direct references between artifacts (`artifact_index` → `referenced_artifact_index`), e.g. a serialized file referencing its data files. References that go through a loadable are not included, and the graph is never cyclical. References to other serialized files are not recorded here either — those are in `content_layout_serialized_file_dependencies`.

## Views

| View | Description |
|------|-------------|
| `content_layout_serialized_files_view` | One row per layout serialized file with the derived filename, its artifact size, and the core-table link (`serialized_file`, `archive`). Built-in entries show their path as the filename. |
| `content_layout_source_assets_view` | Source asset → the file(s) it was built into. |
| `content_layout_serialized_file_dependencies_view` | The dependency edges with filenames resolved on both sides. |
| `content_layout_loadable_objects_view` | The loadables resolved to their analyzed object (`object`, `type`, `name`, `size`). |
| `content_layout_binary_artifacts_view` | The artifacts with their derived on-disk filename (content hash + category-based extension). |
| `content_layout_resource_files_view` | The `.resS`/`.resource` data files each serialized file uses, derived from the artifact graph.  Note: the same resource can be referenced by multiple serialized file. |

```sql
-- which files was this source asset built into?
SELECT * FROM content_layout_source_assets_view WHERE asset_path = 'Assets/Textures/GreenStatic.png';

-- build size by artifact category
SELECT category, COUNT(*) AS count, SUM(size) AS bytes
FROM content_layout_binary_artifacts GROUP BY category ORDER BY bytes DESC;
```

## Analyzing the layout with or without the build content

A `ContentLayout.json` can be analyzed on its own — useful for running SQL queries against a large layout — or together with the build output it describes. The layout tables themselves are identical in both cases; what differs is what the `serialized_file` link points at:

* When the build content is part of the analyzed input, each `content_layout_serialized_files` row links to its analyzed file, and the views resolve across that link (e.g. `content_layout_loadable_objects_view` shows the object, type, name and size of each loadable).
* When a file was not part of the analyzed input (a layout-only analyze, or a subset of a build), its layout entry links to a placeholder `serialized_files` row instead: the row holds only the filename, with `archive` NULL and no objects. The link column is therefore always valid, and whether a file was actually analyzed is visible through its objects: `EXISTS (SELECT 1 FROM objects o WHERE o.serialized_file = f.serialized_file)`. In a layout-only database, expect NULL in the `archive` column of `content_layout_serialized_files_view` and in the `object`, `type`, `name` and `size` columns of `content_layout_loadable_objects_view`.
* Built-in entries (`is_builtin = 1`) always have a NULL `content_hash` and `serialized_file` — they are not files produced by the build.

## Related documentation

| Topic | Description |
|-------|-------------|
| [ContentLayout.json](contentlayout.md) | What the file contains and its json schema. |
| [Content Directory Format](contentdirectory-format.md) | Content directory builds and inspecting them with UnityDataTool. |
| [`analyze` command](command-analyze.md) | The command that imports the layout, and the accepted input combinations. |
| [Example queries for ContentDirectory builds](analyze-examples-contentlayout.md) | Example SQL queries against these tables. |
| [Analyzer](analyzer.md) | The core database schema (objects, serialized files, references). |
