# ContentLayout in the Analyze Database

When the input of the [`analyze`](command-analyze.md) command includes the `ContentLayout.json` of a content directory build, its content is imported into the database tables and views documented on this page. This makes the layout of the build queryable with SQL — which source assets each file was built from, the dependencies between the files, the loadable objects and scenes, and the size of every artifact — and connects that information to the objects and references that `analyze` extracts from the build content itself.

The layout is also what makes reference information correct for a ContentDirectory build: the external reference tables inside the build's files hold symbolic placeholders, and analyze resolves them through the layout's dependency lists. With the layout on the input, references between content files land in the regular `refs` table (and [`find-refs`](command-find-refs.md) works across files); without it they are recorded in [`dangling_refs`](analyzer-schema.md#dangling_refs).

For what `ContentLayout.json` contains conceptually, see [ContentLayout.json](contentlayout.md); for the build output format it describes and the reference-resolution mechanics, see [Content Directory Format](contentdirectory-format.md). For the input combinations analyze accepts (including how the layout is matched to the build), see [the `analyze` command](command-analyze.md#contentdirectory-builds). Example queries: [Example queries for ContentDirectory builds](analyze-examples-contentlayout.md).

## General notes

* The `content_layout*` tables and views are only created when a `ContentLayout.json` is actually part of the analyzed input, so databases produced from AssetBundles or Player builds are not cluttered with empty tables.
* A single layout per database is supported. When several `ContentLayout.json` files are on the input, the one whose `BuildManifestHash` matches the analyzed build is selected and the rest are ignored (see [the `analyze` command](command-analyze.md#contentdirectory-builds)).
* The tables mirror the json structure of the current layout version (version 3, Unity 6.7). Entries that cross-reference each other by array index in the json do the same in the database: `file_index`, `artifact_index` and `loadable_index` columns hold the json array indexes.
* Version 2 layouts (Unity 6.6) are upgraded on import, so both versions produce the same schema and the same queries work on both. See [Layout version differences](#layout-version-differences).
* Two adjustments are made so the data is natural to query:
  * The json's `-1` sentinels are stored as SQL `NULL`: the `SerializedFile` of an object dropped from the build, and the `ArtifactIndex` of the built-in entry.
  * The top-level `RootAssets` list is folded into the `is_root_asset` column of `content_layout_loadable_objects`, which records each root's 1-based position in the list (0 for non-roots), preserving the root input order.
* This page is the reference for the columns; [`ContentLayout.cs`](../UnityDataModels/ContentLayout.cs) is the reference for the json schema. A produced database also carries short notes on its non-obvious columns inside the `CREATE` statements, so `sqlite3 Analysis.db ".schema content_layout_serialized_files"` describes them without needing this page.

## Tables

### content_layout

One row identifying the imported layout.

| Column | Type | Description |
|---|---|---|
| `id` | INTEGER | Always 0 — a single layout per database. Primary key. |
| `name` | TEXT | Path of the imported `ContentLayout.json`. |
| `version` | INTEGER | Schema version of the json file. |
| `build_manifest_hash` | TEXT | The hash used to match the layout to its build. |

### content_layout_serialized_files

One row per serialized file (`.cf` Content File) of the build.

| Column | Type | Description |
|---|---|---|
| `file_index` | INTEGER | The json array index. How every other `content_layout` table names a file. Primary key. |
| `stable_id` | TEXT | The identity hash used to reference this file from other files (see [Stable ids](contentlayout.md#stable-ids)). Stored without the `.cfid` extension that v2 layouts carry. For built-ins, the built-in path. |
| `is_builtin` | INTEGER | 1 for a built-in entry, which is not a file produced by this build. |
| `artifact_index` | INTEGER | References `content_layout_binary_artifacts.artifact_index`, whose `content_hash` gives the filename (`content_hash \|\| '.cf'` — `content_layout_serialized_files_view` derives it). NULL for built-ins. |
| `serialized_file` | INTEGER | [`serialized_files.id`](analyzer-schema.md#serialized_files), connecting the layout to the analyzed objects. NULL for built-ins. See [below](#analyzing-the-layout-with-or-without-the-build-content) for what the referenced row contains when the build content was not analyzed. |

### content_layout_source_assets

The source assets included in each serialized file. The same asset path can appear in more than one file (for example, a single FBX split into multiple output files), so this is many-to-many.

| Column | Type | Description |
|---|---|---|
| `serialized_file_index` | INTEGER | References `content_layout_serialized_files.file_index`. |
| `asset_path` | TEXT | Project path of the source asset. |

### content_layout_serialized_file_dependencies

File-to-file dependency edges: the other serialized files that must be loaded before this one.

| Column | Type | Description |
|---|---|---|
| `serialized_file_index` | INTEGER | References `content_layout_serialized_files.file_index`. |
| `position` | INTEGER | 1-based position in the json array. Significant: a PPtr's `m_FileID` inside the file resolves positionally through this list (see [Content Directory Format](contentdirectory-format.md)). |
| `dependency_index` | INTEGER | References `content_layout_serialized_files.file_index`. |

`serialized_file_index` and `position` together are the primary key.

### content_layout_loadable_objects

The objects that can be loaded on demand, identified independently of the serialized file that contains them. Each row also records where the object came from in the source project.

| Column | Type | Description |
|---|---|---|
| `loadable_index` | INTEGER | The json array index. Primary key, and how `content_layout_loadable_dependencies` names a loadable. |
| `guid` | TEXT | AssetDatabase GUID of the source asset. |
| `lfid` | INTEGER | Local file id of the object in its output serialized file when placed, otherwise the source local file id. This is the join key to the analyzed [`objects`](analyzer-schema.md#objects) (`objects.object_id`). |
| `identifier_type` | INTEGER | Distinguishes the kinds of identifier a loadable can have. |
| `serialized_file_index` | INTEGER | References `content_layout_serialized_files.file_index`. NULL when the object was dropped from the build (json value `-1`, e.g. server-build shader references). |
| `is_root_asset` | INTEGER | The 1-based position in the layout's `RootAssets` (the root input order); 0 when not a root asset. |
| `asset_path` | TEXT | **v2 layouts only.** Project path of the source asset. |
| `source_lfid` | INTEGER | **v2 layouts only.** Local file id of the object within the source asset. |

The last two columns exist only in a database imported from a version 2 layout — see [Layout version differences](#layout-version-differences).

### content_layout_loadable_scenes

The scenes exposed as loadable in the build.

| Column | Type | Description |
|---|---|---|
| `guid` | TEXT | AssetDatabase GUID of the scene. Primary key. |
| `path` | TEXT | Project path of the scene. |
| `serialized_file_index` | INTEGER | References `content_layout_serialized_files.file_index`. NULL when dropped from the build. |

### content_layout_loadable_dependencies

The loadable objects each serialized file references.

| Column | Type | Description |
|---|---|---|
| `serialized_file_index` | INTEGER | References `content_layout_serialized_files.file_index`. |
| `loadable_index` | INTEGER | References `content_layout_loadable_objects.loadable_index`. |

### content_layout_loadable_scene_dependencies

The scenes each serialized file references.

| Column | Type | Description |
|---|---|---|
| `serialized_file_index` | INTEGER | References `content_layout_serialized_files.file_index`. |
| `scene_path` | TEXT | Matches `content_layout_loadable_scenes.path`. |

### content_layout_binary_artifacts

Every artifact of the build output. This is the standard place to find artifact sizes, including the sizes of the serialized files, which no core table records.

| Column | Type | Description |
|---|---|---|
| `artifact_index` | INTEGER | The json array index. Primary key. |
| `content_hash` | TEXT | The on-disk filename is this plus an extension derived from `category`; `content_layout_binary_artifacts_view` adds it. |
| `category` | TEXT | One of `texture`, `mesh` (both `.resS`), `audio`, `video` (both `.resource`), `contentfile` (`.cf`) or `manifest` (`.json`). |
| `size` | INTEGER | Size of the artifact in bytes. |

### content_layout_artifact_references

Direct references between artifacts, for example a serialized file referencing its data files. References that go through a loadable are not included, and the graph is never cyclical. References to other serialized files are not recorded here either — those are in `content_layout_serialized_file_dependencies`.

| Column | Type | Description |
|---|---|---|
| `artifact_index` | INTEGER | References `content_layout_binary_artifacts.artifact_index`. |
| `referenced_artifact_index` | INTEGER | References `content_layout_binary_artifacts.artifact_index`. |

Both columns together are the primary key.

## Views

| View | Description |
|------|-------------|
| `content_layout_serialized_files_view` | One row per layout serialized file with its content hash, derived filename, artifact size, and the core-table link (`serialized_file`, `archive`), resolved through the artifact link. Built-in entries show their path as the filename. |
| `content_layout_source_assets_view` | Source asset → the file(s) it was built into. |
| `content_layout_serialized_file_dependencies_view` | The dependency edges with filenames resolved on both sides. |
| `content_layout_loadable_objects_view` | The loadables resolved to their analyzed object (`object`, `type`, `name`, `size`). In a database from a v2 layout it also carries the `asset_path` and `source_lfid` columns. |
| `content_layout_binary_artifacts_view` | The artifacts with their derived on-disk filename (content hash + category-based extension). |
| `content_layout_resource_files_view` | The `.resS`/`.resource` data files each serialized file uses, derived from the artifact graph.  Note: the same resource can be referenced by multiple serialized files. |

```sql
-- which files was this source asset built into?
SELECT * FROM content_layout_source_assets_view WHERE asset_path = 'Assets/Textures/GreenStatic.png';

-- build size by artifact category
SELECT category, COUNT(*) AS count, SUM(size) AS bytes
FROM content_layout_binary_artifacts GROUP BY category ORDER BY bytes DESC;
```

Indexes are created after the tables are populated, so importing a very large layout stays fast. The indexes serve reverse lookups such as "who depends on X", "which loadables live in file Y" and "which artifact has this hash".

## Layout version differences

The [`analyze` command](command-analyze.md) accepts layout versions 2 (Unity 6.6) and 3 (Unity 6.7 and later; see [Schema versioning](contentlayout.md#schema-versioning)). A version 2 file is upgraded to the version 3 structure during import: the `.cfid` extension is stripped from the stable ids, and its hash-based references (`ObjectIdHash` strings in `LoadableDependencies` and `RootAssets`, `ContentHash` on the files) are resolved to the `loadable_index` / `artifact_index` values the tables store. The original file version stays queryable in `content_layout.version`.

Version 2 layouts record two things about a loadable that newer versions no longer contain, and these become extra columns of `content_layout_loadable_objects` (and its view) that exist **only** in a database imported from a v2 file:

* `asset_path` — the loadable's source asset path. In v2 builds a serialized file could combine several source assets (clustering), so the path identified which one the loadable came from. Unity 6.7 builds don't remap, and the layout only records the per-file `SourceAssets` lists.
* `source_lfid` — the object's local file id within its source asset. In v2 builds the id in the output file (`lfid`) could differ from the source id; in 6.7 output they match (MonoScripts excepted), so only `lfid` exists.

The `ObjectIdHash` concept was removed in version 3 and is not stored for either version — loadables are keyed by their array index.

## Analyzing the layout with or without the build content

A `ContentLayout.json` can be analyzed on its own — useful for running SQL queries against a large layout — or together with the build output it describes. The layout tables themselves are identical in both cases; what differs is what the `serialized_file` link points at:

* When the build content is part of the analyzed input, each `content_layout_serialized_files` row links to its analyzed file, and the views resolve across that link (e.g. `content_layout_loadable_objects_view` shows the object, type, name and size of each loadable).
* When a file was not part of the analyzed input (a layout-only analyze, or a subset of a build), its layout entry links to a placeholder `serialized_files` row instead: the row holds only the filename, with `archive` NULL and no objects. The link column is therefore always valid, and whether a file was actually analyzed is visible through its objects: `EXISTS (SELECT 1 FROM objects o WHERE o.serialized_file = f.serialized_file)`. In a layout-only database, expect NULL in the `archive` column of `content_layout_serialized_files_view` and in the `object`, `type`, `name` and `size` columns of `content_layout_loadable_objects_view`.
* Built-in entries (`is_builtin = 1`) always have a NULL `artifact_index` and `serialized_file` — they are not files produced by the build.

## Related documentation

| Topic | Description |
|-------|-------------|
| [ContentLayout.json](contentlayout.md) | What the file contains and its json schema. |
| [Content Directory Format](contentdirectory-format.md) | Content directory builds and inspecting them with UnityDataTool. |
| [`analyze` command](command-analyze.md) | The command that imports the layout, and the accepted input combinations. |
| [Example queries for ContentDirectory builds](analyze-examples-contentlayout.md) | Example SQL queries against these tables. |
| [Analyzer Database Schema](analyzer-schema.md) | The core database schema (objects, serialized files, references). |
