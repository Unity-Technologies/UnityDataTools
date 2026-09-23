# Analyzer Database Schema

Reference for the SQLite database produced by the [`analyze`](command-analyze.md) command. See
[Analyzer](analyzer.md) for an overview of the library, and [Example usage of
Analyze](analyze-examples.md) for worked queries.

Parts of the schema are documented on their own pages:

| Tables | Page |
|---|---|
| `content_layout*` | [ContentLayout in the Analyze Database](contentlayout-database.md) |
| `build_report*` | [BuildReport](buildreport.md) |
| `addressables_build*` | [Addressables Build Report Analysis](addressables-build-reports.md) |

## How to read this page

* **Start from the views.** They join the tables into the presentations you normally want, and a
  GUI tool such as [DB Browser for SQLite](https://sqlitebrowser.org/) can display them directly.
  `object_view` is the main entry point.
* **The database documents itself too.** Short notes for the non-obvious columns are stored inside
  the `CREATE` statements, so `sqlite3 Analysis.db ".schema objects"` shows them. This page is the
  full reference; the in-database notes are the reminder you get when you only have the `.db`.
* **Two columns hold `''` rather than NULL** when they have no value: `serialized_files.archive`
  and `objects.game_object`. `object_view` maps the former to NULL. Test for `''` when querying the
  tables directly.
* **Ids are analyzer-assigned.** `objects.id` and the ids that reference it exist only in the
  database. The Unity object id is `objects.object_id`.
* **Two options change what gets populated.** `--skip-references` leaves `refs`, `dangling_refs` and
  `script_object_view` empty. `--skip-crc` sets every `objects.crc32` to 0, which makes
  `view_potential_duplicates` report many false positives.

## At a glance

Core tables:

| Name | One row per |
|---|---|
| [`objects`](#objects) | Unity object found in the analyzed files |
| [`types`](#types) | distinct Unity type name |
| [`serialized_files`](#serialized_files) | SerializedFile analyzed |
| [`archives`](#archives) | Unity Archive analyzed |
| [`refs`](#refs) | reference from one object to another |
| [`dangling_refs`](#dangling_refs) | reference target that was never analyzed |
| [`property_names`](#property_names-and-property_types) | distinct property path used by `refs` |
| [`property_types`](#property_names-and-property_types) | distinct referenced type name used by `refs` |

Core views:

| Name | Purpose |
|---|---|
| [`object_view`](#object_view) | every object with its type, file and archive names resolved |
| [`refs_view`](#refs_view) | `refs` with the property strings resolved |
| [`dangling_refs_view`](#dangling_refs_view) | each unresolved target and the objects referencing it |
| [`view_breakdown_by_type`](#view_breakdown_by_type) | object count and total size per type |
| [`view_potential_duplicates`](#view_potential_duplicates) | objects that appear in more than one file |
| [`view_material_shader_refs`](#view_material_shader_refs-and-view_material_texture_refs) | each Material and its Shader |
| [`view_material_texture_refs`](#view_material_shader_refs-and-view_material_texture_refs) | each Material and its Textures |

AssetBundle and MonoScript:

| Name | Purpose |
|---|---|
| [`assetbundle_assets`](#assetbundle_assets) | assets explicitly assigned to an AssetBundle |
| [`assetbundle_asset_view`](#assetbundle_asset_view) | those assets with their object columns resolved |
| [`preload_dependencies`](#preload_dependencies) | objects Unity preloads alongside another object |
| [`preload_dependencies_view`](#preload_dependencies_view) | preload pairs with both sides resolved |
| [`monoscripts`](#monoscripts) | C# class behind each MonoBehaviour / ScriptableObject |
| [`monoscripts_view`](#monoscripts_view) | MonoScripts with their containing file |
| [`script_object_view`](#script_object_view) | MonoBehaviours and ScriptableObjects with their C# type |

Type-specific views, each `object_view` plus extra columns:

| Name | Type |
|---|---|
| [`animation_view`](#animation_view) | AnimationClip |
| [`audio_clip_view`](#audio_clip_view) | AudioClip |
| [`mesh_view`](#mesh_view) | Mesh |
| [`texture_view`](#texture_view) | Texture2D |
| [`shader_view`](#shader_view) | Shader |
| [`shader_subprogram_view`](#shader_subprogram_view) | Shader sub-programs |
| [`shader_keyword_ratios`](#shader_keyword_ratios) | Shader keyword impact on variant count |
| [`view_breakdowns_shaders`](#view_breakdowns_shaders) | Shaders aggregated by name |

---

# Core tables

## objects

One row per Unity object discovered during analysis, holding the properties common to all types.
Query [`object_view`](#object_view) instead unless you need the raw ids.

| Column | Type | Description |
|---|---|---|
| `id` | INTEGER | Analyzer-assigned id, unique across the database. Does not exist in the serialized data. Primary key. |
| `object_id` | INTEGER | The Unity local file id, serialized as `m_PathID` in object references (PPtrs). Signed 64-bit. Unique within its SerializedFile but not across files. |
| `serialized_file` | INTEGER | [`serialized_files.id`](#serialized_files) of the file containing the object. |
| `type` | INTEGER | [`types.id`](#types). |
| `name` | TEXT | The `Object.name` property. Empty for the many object types that have no name. |
| `game_object` | INTEGER | `objects.id` of the GameObject that owns this object. Set only for components; `''` otherwise. |
| `size` | INTEGER | Size in bytes, including any external `.resS` / `.resource` stream data belonging to the object. |
| `crc32` | INTEGER | CRC of the object's serialized state, including its external stream data. Useful for detecting whether an object changed between builds. `0` for every object when `--skip-crc` was used. |

## types

The Unity type names referenced by [`objects.type`](#objects).

| Column | Type | Description |
|---|---|---|
| `id` | INTEGER | The Unity class id. `-1` is a synthetic type added by analyze, see below. Primary key. |
| `name` | TEXT | The type name, e.g. `Texture2D`. |

Type `-1` is `Scene`. A scene has no single Unity object that represents it, so analyze inserts a
synthetic `Scene` object for scene bundles in order to have something for the AssetBundle container
entry and the scene's preload dependencies to hang off.

## serialized_files

One row per SerializedFile encountered during analysis.

| Column | Type | Description |
|---|---|---|
| `id` | INTEGER | Analyzer-assigned id. Primary key. |
| `archive` | INTEGER | [`archives.id`](#archives) of the Unity Archive containing this file, or `''` when the file is not inside an archive. [`object_view`](#object_view) maps `''` to NULL. |
| `name` | TEXT | The file's name on disk, see below. |

The name is usually a technical, hash-based string rather than a readable path, because that is how
the file is named on disk:

* **Regular AssetBundles**: `CAB-<MD4 hash of the AssetBundle name>`. This is MD4, not Unity's
  `Hash128` (spooky hash), and the optional AssetBundle-filename hash is not part of it.
* **Scene bundles** vary by build pipeline: `BuildPipeline.BuildAssetBundles` produces
  `BuildPlayer-<SceneName>`; the Scriptable Build Pipeline and Addressables produce
  `CAB-<hash of scene path>`; the Multi-Process Build Pipeline produces `CAB-<scene GUID>`.
* **Player builds** name scenes `level0`, `level1`, ... in scene-list order.

A file recorded because it holds a [dangling reference](#dangling_refs) target also appears here,
with `archive` NULL and no objects of its own. In that case the lowercased file name is all that is
known about it.

## archives

One row per Unity Archive encountered during analysis. AssetBundles are archives, and compressed
Player and ContentDirectory builds also keep their content in archives. A bare SerializedFile that
is not inside an archive has no row here.

| Column | Type | Description |
|---|---|---|
| `id` | INTEGER | Analyzer-assigned id. Primary key. |
| `name` | TEXT | The archive's name on the file system. UNIQUE. |
| `file_size` | INTEGER | Size of the archive file in bytes. |

`name` is UNIQUE and case-sensitive because analyze supports a single build at a time: two archives
with the same name would make every query ambiguous. A duplicate is detected while writing and
reported as an error; the constraint is the durable backstop. See
[Comparing Builds](comparing-builds.md) for how to analyze several builds.

## refs

Every reference between Unity objects (PPtrs), whether the two objects are in the same SerializedFile
or in different ones. On large builds this is by far the biggest table, so the property strings are
deduplicated into [`property_names`](#property_names-and-property_types) and
[`property_types`](#property_names-and-property_types) and only integer ids are stored here. Query
[`refs_view`](#refs_view) to get the strings back.

| Column | Type | Description |
|---|---|---|
| `object` | INTEGER | [`objects.id`](#objects) of the referencing object. |
| `referenced_object` | INTEGER | [`objects.id`](#objects) of the target, or [`dangling_refs.id`](#dangling_refs) when the target's file was not analyzed. |
| `property_path` | INTEGER | [`property_names.id`](#property_names-and-property_types). |
| `property_type` | INTEGER | [`property_types.id`](#property_names-and-property_types). |

Not populated when analyze is run with `--skip-references`.

## dangling_refs

When a reference points at an object whose SerializedFile was not part of the analyzed input, analyze
still assigns that target an object id but never writes an [`objects`](#objects) row for it.
`dangling_refs` records those targets, so the reference information is preserved and every object id
is accounted for: each id resolves to exactly one of `objects` or `dangling_refs`, never both.

| Column | Type | Description |
|---|---|---|
| `id` | INTEGER | The assigned object id. No `objects` row has this id. Primary key. |
| `object_id` | INTEGER | The target's local file id (`m_PathID`) within its SerializedFile. |
| `serialized_file` | INTEGER | [`serialized_files.id`](#serialized_files) of the un-analyzed file. That row has `archive` NULL and no objects of its own. |

Common causes:

* Analyzing a single AssetBundle or a partial subset of a bundle group, so cross-bundle references
  point at bundles that were not analyzed.
* A Player build referencing `unity default resources` - a built-in file that ships without
  TypeTrees and so cannot be analyzed. (`Resources/unity_builtin_extra` is built alongside your
  content and can be analyzed, but produces the same dangling references when it is not part of the
  analyzed set.)
* A ContentDirectory build analyzed without its `ContentLayout.json`, so the symbolic external
  references in the content files cannot be resolved. See
  [ContentLayout in the Analyze Database](contentlayout-database.md).

Not populated when analyze is run with `--skip-references`.

## property_names and property_types

Deduplicated lookup tables for the strings that [`refs`](#refs) would otherwise repeat on every row.
Both have the same shape:

| Column | Type | Description |
|---|---|---|
| `id` | INTEGER | Primary key, referenced by `refs`. |
| `name` | TEXT | The string. `property_names` holds property paths (e.g. `m_Shader`, `m_Materials[0]`); `property_types` holds referenced type names (e.g. `Texture2D`, `MonoScript`). |

Prefer [`refs_view`](#refs_view) over joining these by hand.

---

# Core views

## object_view

The main view: every object in the analyzed build output, with its type, SerializedFile and archive
names resolved. The output could be AssetBundles, a ContentDirectory build, a Player build, or
standalone SerializedFiles.

| Column | Type | Description |
|---|---|---|
| `id` | INTEGER | [`objects.id`](#objects). |
| `object_id` | INTEGER | The Unity object id. |
| `archive` | TEXT | Name of the Unity Archive containing the object, or NULL when the source file was a bare SerializedFile. |
| `serialized_file` | TEXT | Name of the SerializedFile containing the object. |
| `type` | TEXT | The object's type name. |
| `name` | TEXT | The object's name, if it had one. |
| `game_object` | INTEGER | `objects.id` of the containing GameObject, if any. Mostly for components. |
| `size` | INTEGER | Size in bytes, e.g. `3343772`. |
| `pretty_size` | TEXT | Size in a readable form, e.g. `3.2 MB`. |
| `crc32` | INTEGER | The object's CRC. |

## refs_view

[`refs`](#refs) with the `property_path` and `property_type` ids resolved to their strings. Use this
rather than joining the lookup tables yourself.

| Column | Type | Description |
|---|---|---|
| `object` | INTEGER | The referencing object's id. |
| `referenced_object` | INTEGER | The target's id. |
| `property_path` | TEXT | The property holding the reference, e.g. `m_Shader`. |
| `property_type` | TEXT | The referenced type, e.g. `Texture2D`. |

```sql
SELECT * FROM refs_view WHERE property_type = 'MonoScript';
```

## dangling_refs_view

Each [dangling target](#dangling_refs) joined back to the object or objects that reference it: one
row per (referencing object, dangling target) reference.

| Column | Type | Description |
|---|---|---|
| `source_id` | INTEGER | `objects.id` of the referencing object. |
| `source_serialized_file` | TEXT | The referencing object's file. |
| `source_object_id` | INTEGER | The referencing object's Unity object id. |
| `property_path` | TEXT | The property holding the reference. |
| `property_type` | TEXT | The referenced type. |
| `target_id` | INTEGER | `dangling_refs.id` of the unresolved target. |
| `target_serialized_file` | TEXT | Name of the file the target lives in. |
| `target_object_id` | INTEGER | The target's local file id. |

```sql
-- what does object 42 fail to resolve, and where should those objects have come from?
SELECT * FROM dangling_refs_view WHERE source_id = 42;
```

Because the view joins `refs`, it is empty when analyze is run with `--skip-references` (in that mode
neither `refs` nor `dangling_refs` are populated).

## view_breakdown_by_type

Total number and size of the objects, aggregated by type, largest first.

| Column | Type | Description |
|---|---|---|
| `type` | TEXT | The type name. |
| `count` | INTEGER | Number of objects of this type. |
| `byte_size` | INTEGER | Total size in bytes. |
| `pretty_size` | TEXT | Total size in a readable form. |

## view_potential_duplicates

Objects that are possibly included more than once in the build output. This happens when an asset is
referenced from multiple AssetBundles but is not assigned to one: Unity then copies it into every
bundle that references it.

| Column | Type | Description |
|---|---|---|
| `instances` | INTEGER | How many copies were found. |
| `name` | TEXT | The object's name. |
| `type` | TEXT | The object's type. |
| `pretty_total_size` | TEXT | Combined size of all copies, readable form. |
| `total_size` | INTEGER | Combined size of all copies in bytes. |
| `size` | INTEGER | Size of one copy in bytes. |
| `pretty_size` | TEXT | Size of one copy, readable form. |
| `in_files` | TEXT | The archives (or SerializedFiles) the copies were found in. |

Rows are grouped by name, type, size *and* `crc32`, so the view is normally very accurate. With
`--skip-crc` every `crc32` is 0 and the view reports many false positives.

## view_material_shader_refs and view_material_texture_refs

Each Material paired with the Shader it references, and with the Textures it references. Both
include the Material's AssetBundle container path when it has one.

`view_material_shader_refs`: `material_id`, `material_name`, `material_path`, `material_archive`,
`shader_id`, `shader_name`, `shader_archive`.

`view_material_texture_refs`: `material_id`, `material_name`, `material_path`, `material_archive`,
`texture_id`, `texture_name`, `texture_archive`.

---

# AssetBundle tables and views

## assetbundle_assets

The assets an AssetBundle explicitly exposes: one row per entry in the AssetBundle object's
`m_Container`. Dependencies that Unity pulled in automatically at build time are *not* listed here,
see [`preload_dependencies`](#preload_dependencies).

| Column | Type | Description |
|---|---|---|
| `object` | INTEGER | [`objects.id`](#objects) of the asset. For a scene bundle this is the synthetic [`Scene`](#types) object. |
| `name` | TEXT | The container path the asset is addressed by. |

This data comes from the AssetBundle Unity object, so the table is populated only for AssetBundle
builds. Player and ContentDirectory builds have no such object and it is empty for them.

For a scene bundle the container entry names the scene (its `.unity` path) and points at the
synthetic `Scene` object. This applies to both `BuildPipeline.BuildAssetBundles` and Scriptable
Build Pipeline / Addressables scene bundles.

## assetbundle_asset_view

[`assetbundle_assets`](#assetbundle_assets) joined to [`object_view`](#object_view): the columns of
`object_view` plus `asset_name`, the container path.

The join is an INNER JOIN, so a container entry whose object is not in the `objects` table is
omitted. In practice that only happens when the object was genuinely not analyzed, for example it
lives in a bundle that was not part of the analyzed set. The underlying table still holds those rows.

## preload_dependencies

Preload relationships as `object` to `dependency` pairs. A "dependency" here is an object that Unity
preloads or pulls in alongside another object.

| Column | Type | Description |
|---|---|---|
| `object` | INTEGER | [`objects.id`](#objects). What this is depends on the build type, see below. |
| `dependency` | INTEGER | [`objects.id`](#objects) of the preloaded object, or [`dangling_refs.id`](#dangling_refs) when it was not analyzed. |

The rows come from the AssetBundle and PreloadData Unity objects, so the table is populated for
AssetBundle **and Player** builds, but not ContentDirectory builds, which have neither object. What
the `object` side is depends on the build:

* **AssetBundle asset**: the explicitly-assigned asset, with its dependencies taken from the
  AssetBundle object's `m_PreloadTable`.
* **Scene bundle**: the synthetic [`Scene`](#types) object. Its dependencies are the scene's shared
  assets and the entries of the scene's PreloadData. This works for both
  `BuildPipeline.BuildAssetBundles` and Scriptable Build Pipeline / Addressables scene bundles. The
  scene's own content objects (GameObjects and so on) are not listed as dependencies, but they share
  the scene object's `serialized_file`.
* **Player build**: the `PreloadData` object itself, because a Player build has no scene object to
  attach the dependencies to. A Player build has one `PreloadData` per scene (in its
  `sharedassetsN.assets`) plus one in `globalgamemanagers.assets` for the always-loaded set.

The `dependency` side often references an object analyze never recorded, most commonly objects in
`unity default resources`. Those ids are catalogued in [`dangling_refs`](#dangling_refs).

## preload_dependencies_view

A convenience view over [`preload_dependencies`](#preload_dependencies) that resolves the ids: it
joins each row's `object` to [`assetbundle_asset_view`](#assetbundle_asset_view) and its `dependency`
to [`object_view`](#object_view). Columns: `id`, `asset_name`, `archive`, `type`, `dep_id`,
`dep_archive`, `dep_name`, `dep_type`.

Filter by `id` or `asset_name` for the dependencies of one asset, or by `dep_id` for everything that
depends on a given object - useful for working out why an object was included in a build.

Because of those inner joins the view is narrower than the underlying table:

* It only includes rows whose `object` is an AssetBundle asset or scene. Player-build rows hang off a
  `PreloadData` object rather than an AssetBundle asset, so they do not appear here - query the
  `preload_dependencies` table directly for those.
* It drops rows whose `dependency` is a dangling id, because those have no `object_view` row to join
  to.

---

# MonoScript tables and views

## monoscripts

The class information for all the C# types of MonoBehaviour objects in the build output, including
ScriptableObjects: assembly name, C# namespace and class name.

## monoscripts_view

`monoscripts` joined so you can see which AssetBundle or SerializedFile contains each MonoScript
object.

## script_object_view

All the MonoBehaviour and ScriptableObject objects in the build output with their location, size and
precise C# type, built from the `monoscripts` and [`refs`](#refs) tables. Empty when analyze is run
with `--skip-references`.

---

# Type-specific views

Each of these has the same columns as [`object_view`](#object_view) plus the ones listed.

## animation_view

AnimationClips.

| Column | Description |
|---|---|
| `legacy` | 1 if it is a legacy animation, 0 otherwise |
| `events` | the number of events |

## audio_clip_view

AudioClips.

| Column | Description |
|---|---|
| `bits_per_sample` | number of bits per sample |
| `frequency` | sampling frequency |
| `channels` | number of channels |
| `load_type` | `Compressed in Memory`, `Decompress on Load` or `Streaming` |
| `format` | compression format |

## mesh_view

Meshes.

| Column | Description |
|---|---|
| `sub_meshes` | the number of sub-meshes |
| `blend_shapes` | the number of blend shapes |
| `bones` | the number of bones |
| `indices` | the number of vertex indices |
| `vertices` | the number of vertices |
| `compression` | 1 if compressed, 0 otherwise |
| `rw_enabled` | 1 if the mesh has the *R/W Enabled* option, 0 otherwise |
| `vertex_size` | number of bytes used by each vertex |
| `channels` | name and type of the vertex channels |

## texture_view

Texture2Ds.

| Column | Description |
|---|---|
| `width` / `height` | texture resolution |
| `format` | compression format |
| `mip_count` | number of mipmaps |
| `rw_enabled` | 1 if the texture has the *R/W Enabled* option, 0 otherwise |

## shader_view

Shaders.

| Column | Description |
|---|---|
| `decompressed_size` | approximate size in bytes the shader needs at runtime when loaded |
| `sub_shaders` | the number of sub-shaders |
| `sub_programs` | the number of sub-programs (usually one per shader variant, stage and pass) |
| `unique_programs` | the number of unique programs (variants with identical programs share one program in memory) |
| `keywords` | list of all the keywords affecting the shader |

## shader_subprogram_view

One row per shader sub-program. Same columns as [`shader_view`](#shader_view) plus:

| Column | Description |
|---|---|
| `api` | the graphics API, e.g. DX11, Metal, GLES |
| `pass` | the pass number of the sub-program |
| `pass_name` | the pass name, if available |
| `hw_tier` | the hardware tier (as defined in the Graphics settings) |
| `shader_type` | the type of shader, e.g. vertex, fragment |
| `sub_program` | the sub-program index for this pass and shader type |
| `keywords` | the shader keywords specific to this sub-program |

## shader_keyword_ratios

Helps determine which shader keywords are causing a large number of variants. Define a "program" as a
unique combination of shader, sub-shader, hardware tier, pass number, API and shader type. Each row
of this view is one program paired with one of its keywords.

| Column | Description |
|---|---|
| `shader_id` | the shader id |
| `name` | the shader name |
| `sub_shader` | the sub-shader number |
| `hw_tier` | the hardware tier |
| `pass` | the pass number |
| `api` | the graphics API |
| `pass_name` | the pass name, if available |
| `shader_type` | the type of shader |
| `total_variants` | total number of variants for this program |
| `keyword` | one of the program's keywords |
| `variants` | number of variants including this keyword |
| `ratio` | `variants` / `total_variants` |

Use `ratio` to judge how a keyword affects the variant count. At 0.5 the keyword is in half the
variants, which means it is not being stripped at all: every variant exists both with and without
it. Keywords with a ratio close to 0.5 are therefore the best stripping targets. A ratio close to 0
or 1 means the keyword is in almost none or almost all of the variants, and stripping it will not
make much difference.

## view_breakdowns_shaders

All the shaders aggregated by name. `instances` indicates how many times the shader was found in the
data files. Also provides the total size per shader and the list of AssetBundles it was found in.

---

# Schema version

The database records which schema it was produced with in `PRAGMA user_version`. Commands that read
an existing database (currently only [`find-refs`](command-find-refs.md)) compare against it to give
a clean error instead of failing on a missing table or column.

Any schema change - a new or changed table, view or column - must bump the pragma in
`Analyzer/Resources/Init.sql` and add a row here. Databases produced before versioning report 0.

| Version | Change |
|---|---|
| 1 | Normalized `refs` table ([#44](https://github.com/Unity-Technologies/UnityDataTools/issues/44)) |
| 2 | Renamed `assets` / `asset_dependencies` to `assetbundle_assets` / `preload_dependencies` ([#82](https://github.com/Unity-Technologies/UnityDataTools/issues/82)) |
| 3 | Renamed the `asset_bundles` table to `archives`, and the `asset_bundle` column/alias to `archive` ([#68](https://github.com/Unity-Technologies/UnityDataTools/issues/68)) |
| 4 | `build_report_packed_asset_contents_view` type column changed from numeric id to type name ([#55](https://github.com/Unity-Technologies/UnityDataTools/issues/55)) |
| 5 | Added the `dangling_refs` table and view ([#85](https://github.com/Unity-Technologies/UnityDataTools/issues/85)) |
| 6 | `archives.name` is UNIQUE ([#51](https://github.com/Unity-Technologies/UnityDataTools/issues/51)) |
| 7 | Unity 6.6 `build_reports` columns and `build_report_content_*` tables ([#107](https://github.com/Unity-Technologies/UnityDataTools/issues/107)); `asset_name` / `asset_extension` columns on `build_report_source_assets` ([#110](https://github.com/Unity-Technologies/UnityDataTools/issues/110)) |
| 8 | `content_layout*` tables track the version 3 layout schema of Unity 6.7: loadables keyed by `loadable_index`, `stable_id` / `artifact_index` columns replace `cfid` / `content_hash`, `is_root_asset` records the root position, and the v2-only columns (`asset_path`, `source_lfid`) exist only in databases imported from a version 2 layout ([#131](https://github.com/Unity-Technologies/UnityDataTools/issues/131)) |

## Related documentation

| Topic | Page |
|---|---|
| [Analyzer](analyzer.md) | The library, and how to extend it |
| [analyze command](command-analyze.md) | Running `analyze` and its options |
| [Example usage of Analyze](analyze-examples.md) | Worked queries |
| [ContentLayout in the Analyze Database](contentlayout-database.md) | The `content_layout*` tables |
| [BuildReport](buildreport.md) | The `build_report*` tables |
| [Addressables Build Report Analysis](addressables-build-reports.md) | The `addressables_build*` tables |
| [Comparing Builds](comparing-builds.md) | Analyzing more than one build |
| [Using UnityDataTool with an AI Agent](agent-guide.md) | Agent-oriented workflow |
