# Analyzer

The Analyzer is a class library that can be used to analyze the content of Unity data files such
as AssetBundles and SerializedFiles. It iterates through all the serialized objects and uses the
TypeTree to extract information about these objects (e.g. name, size, etc.)

The most common use of this library is through the [analyze](unitydatatool.md#analyzeanalyse)
command of the UnityDataTool.  This uses the Analyze library to generate a SQLite database.

Once generated, a tool such as the [DB Browser for SQLite](https://sqlitebrowser.org/), or the command line `sqlite3` tool, can be used to look at the content of the database.

# Example usage

See [this topic](analyze-examples.md) for examples of how to use the SQLite output of the UnityDataTool Analyze command.

# Database Schema Reference

The database provides different tables and views.  The views join multiple tables together to provide useful presentations of the data.  With GUI-based SQLite tools it is often possible to skip writing your own SQL queries, and simply view the content of these pre-defined queries.

This section gives an overview of the main tables and views.

## object_view

This is the main view, providing information about all the objects in the analyzed build output.
That output could be AssetBundles, a ContentDirectory build, a Player build, or standalone
SerializedFiles.
Its columns are:
* id: a unique id without any meaning outside of the database
* object_id: the Unity object id (unique inside its SerializedFile but not necessarily across all
  the files in the build)
* archive: the name of the Unity Archive containing the object (will be null if the source file
  was a bare SerializedFile and not inside an archive). Archives back AssetBundles, ContentDirectory
  builds and Player builds.
* serialized_file: the name of the SerializedFile containing the object
* type: the type of the object
* name: the name of the object, if it had one
* game_object: the id of the GameObject containing this object, if any (mostly for Components)
* size: the size of the object in bytes (e.g. 3343772)
* pretty_size: the size in an easier to read format (e.g. 3.2 MB)

## archives

One row per Unity Archive encountered during analysis, holding the archive file's `name` and
`file_size`. AssetBundle files are recorded here. Compressed Player and other builds may also have content inside archive files. Each SerializedFile links to its archive through the `serialized_files.archive`
column, and `object_view` surfaces the archive name as its `archive` column. A bare SerializedFile that is not inside an archive has no row here.

## view_breakdown_by_type

This view lists the total number and size of the objects, aggregated by type.

## view_potential_duplicates

This view lists the objects that are possibly included more than once in the AssetBundles. This can
happen when an asset is referenced from multiple AssetBundles but is not assigned to one. In this
case, Unity will include the asset in all the AssetBundles with a reference to it. The
view_potential_duplicates provides the number of instances and the total size of the potentially
duplicated assets. It also lists all the AssetBundles where the asset was found.

If the `--skip-crc` option is used, there will be a lot of false positives in that view. Otherwise,
it should be very accurate because CRCs are used to determine if objects are identical.

## assetbundle_asset_view (AssetBundleProcessor)

Lists the assets that were explicitly assigned to AssetBundles, one row per entry in the AssetBundle
object's `m_Container`. Dependencies that Unity pulled in automatically at build time are not listed
here (see `preload_dependencies_view`). The columns are those of *object_view* plus `asset_name`,
the container path of the asset.

This data comes from the AssetBundle Unity object, so the view is only populated for AssetBundle
builds; Player and ContentDirectory builds have no such object and it is empty for them. For a scene
bundle the container entry names the scene (its `.unity` path) and points at a synthetic object of
type `Scene` (there is no single Unity object that represents a scene). Scene objects are created
for both BuildPipeline.BuildAssetBundles and Scriptable Build Pipeline / Addressables scene bundles.

The view is built with an INNER JOIN to *object_view*, so a container entry whose object is not in
the `objects` table is omitted. In practice that only happens if the object was genuinely not
analyzed (for example it lives in a bundle that was not part of the analyzed set); the underlying
`assetbundle_assets` table still holds those rows.

## preload_dependencies

This table records preload relationships as `object` -> `dependency` id pairs, where both are
`objects.id` values. A "dependency" is an object that Unity preloads / pulls in alongside another
object. The rows come from the AssetBundle and PreloadData Unity objects, so the table is populated
for AssetBundle and Player builds but not ContentDirectory builds (which have neither object).

What the `object` is depends on the build:

- **AssetBundle asset**: the explicitly-assigned asset, with its dependencies taken from the
  AssetBundle object's preload table.
- **Scene bundle**: a synthetic `Scene` object (a scene has no single Unity object). Its
  dependencies are the scene's shared assets and the entries of the scene's PreloadData. This works
  for both BuildPipeline.BuildAssetBundles and Scriptable Build Pipeline / Addressables scene
  bundles. The scene's own content objects (GameObjects, etc.) are not listed as dependencies but
  share the scene object's `serialized_file`.
- **Player build**: the `PreloadData` object itself, because a player build has no scene object to
  attach the dependencies to. A player build has one `PreloadData` per scene (in its
  `sharedassetsN.assets`) plus one in `globalgamemanagers.assets` for the always-loaded set.

The `dependency` side can reference an object that analyze never recorded, leaving a "dangling" id
with no matching `objects` row (such ids are catalogued in the `dangling_refs` table). The most
common case is objects in `unity default resources`, the
built-in resource file that ships with the Unity Editor without TypeTrees and so cannot be analyzed
without a specially built copy. (`Resources/unity_builtin_extra` is built alongside your content and
can be analyzed, but produces the same dangling references when it is not part of the analyzed set.)

## preload_dependencies_view  (AssetBundleProcessor)

A convenience view over `preload_dependencies` that resolves the ids to readable columns: it joins
each row's `object` to `assetbundle_asset_view` and its `dependency` to *object_view*. Filter by `id`
or `asset_name` for the dependencies of one asset, or by `dep_id` for everything that depends on a
given object (useful for figuring out why an object was included in a build).

Because of those inner joins the view is narrower than the underlying table:

- It only includes rows whose `object` is an AssetBundle asset or scene (i.e. present in
  `assetbundle_asset_view`). Player-build rows hang off a `PreloadData` object rather than an
  AssetBundle asset, so they do not appear here - query the `preload_dependencies` table directly.
- It drops rows whose `dependency` is a dangling id (see above), because those have no *object_view*
  row to join to.

## monoscripts

Show the class information for all the C# types of MonoBehaviour objects in the build output (including ScriptableObjects).

This includes the assembly name, C# namespace and class name.

## monoscripts_view

This view is a convenient view for seeing which AssetBundle / SerializedFile contains each MonoScript object.

## script_object_view

This view lists all the MonoBehaviour and ScriptableObject objects in the build output, with their location, size and precise C# type (using the `monoscripts` and `refs` tables).   This view is not populated if analyze is run with the `--skip-references` option.

## animation_view (AnimationClipProcessor)

This provides additional information about AnimationClips. The columns are the same as those in
the *object_view*, with the addition of:
* legacy: 1 if it's a legacy animation, 0 otherwise
* events: the number of events

## audio_clip_view (AudioClipProcessor)

This provides additional information about AudioClips. The columns are the same as those in
the *object_view*, with the addition of:
* bits_per_sample: number of bits per sample
* frequency: sampling frequency
* channels: number of channels
* load_type: either *Compressed in Memory*, *Decompress on Load* or *Streaming*
* format: compression format

## mesh_view (MeshProcessor)

This provides additional information about Meshes. The columns are the same as those in
the *object_view*, with the addition of:
* sub_meshes: the number of sub-meshes
* blend_shapes: the number of blend shapes
* bones: the number of bones
* indices: the number of vertex indices
* vertices: the number of vertices
* compression: 1 if compressed, 0 otherwise
* rw_enabled: 1 if the mesh has the *R/W Enabled* option, 0 otherwise
* vertex_size: number of bytes used by each vertex
* channels: name and type of the vertex channels

## texture_view (Texture2DProcessor)

This provides additional information about Texture2Ds. The columns are the same as those in
the *object_view*, with the addition of:
* width/height: texture resolution
* format: compression format
* mip_count: number of mipmaps
* rw_enabled:  1 if the mesh has the *R/W Enabled* option, 0 otherwise

## shader_view (ShaderProcessor)

This provides additional information about Shaders. The columns are the same as those in
the *object_view*, with the addition of:
* decompressed_size: the approximate size in bytes that this shader will need at runtime when
  loaded
* sub_shaders: the number of sub-shaders
* sub_programs: the number of sub-programs (usually one per shader variant, stage and pass)
* unique_programs: the number of unique program (variants with identical programs will share the
  same program in memory)
* keywords: list of all the keywords affecting the shader

## shader_subprogram_view (ShaderProcessor)

This view lists all the shader sub-programs and has the same columns as the *shader_view* with the
addition of:
* api: the API of the shader (e.g. DX11, Metal, GLES, etc.)
* pass: the pass number of the sub-program
* pass_name: the pass name, if available
* hw_tier: the hardware tier of the sub-program (as defined in the Graphics settings)
* shader_type: the type of shader (e.g. vertex, fragment, etc.)
* sub_program: the subprogram index for this pass and shader type
* keywords: the shader keywords specific to this sub-program

## shader_keyword_ratios

This view can help to determine which shader keywords are causing a large number of variants.  To
understand how it works, let's define a "program" as a unique combination of shader, subshader,
hardware tier, pass number, API (DX, Metal, etc.), and shader type (vertex, fragment, etc).

Each row of the view corresponds to a combination of one program and one of its keywords. The
columns are:

* shader_id: the shader id
* name: the shader name
* sub_shader: the sub-shader number
* hw_tier: the hardware tier of the sub-program (as defined in the Graphics settings)
* pass: the pass number of the sub-program
* api: the API of the shader (e.g. DX11, Metal, GLES, etc.)
* pass_name: the pass name, if available
* shader_type: the type of shader (e.g. vertex, fragment, etc.)
* total_variants: total number of variants for this program.
* keyword: one of the program's keywords
* variants: number of variants including this keyword.
* ratio: variants/total_variants

The ratio can be used to determine how a keyword affects the number of variants. When it is equal
to 0.5, it means that it is in half of the variants. Basically, that means that it is not stripped
at all because each of the program's variants has a version with and without that keyword.
Therefore, keywords with a ratio close to 0.5 are good targets for stripping. When the ratio is
close to 0 or 1, it means that the keyword is in almost none or almost all of the variants and
stripping it won't make a big difference.

## view_breakdowns_shaders (ShaderProcessor)

This view lists all the shaders aggregated by name. The *instances* column indicates how many time
the shader was found in the data files. It also provides the total size per shader and the list of
AssetBundles in which they were found.

## refs / refs_view

The `refs` table records the references between objects: for each reference it stores the source
`object`, the `referenced_object`, and the property that holds the reference. On large builds this
table dominates the database size, so the property strings are deduplicated into two lookup tables
and `refs` stores integer ids into them:

* `property_names`: distinct property paths (e.g. `m_Shader`, `m_Materials[0]`).
* `property_types`: distinct referenced types (e.g. `Texture2D`, `MonoScript`).

The `refs_view` rejoins these so the original strings are available directly. Query `refs_view`
(columns `object`, `referenced_object`, `property_path`, `property_type`) rather than joining the
lookup tables by hand:

```sql
SELECT * FROM refs_view WHERE property_type = 'MonoScript';
```

These tables are not populated when analyze is run with `--skip-references`.

## dangling_refs / dangling_refs_view

When a reference points to an object whose serialized file was not part of the analyzed input,
analyze still assigns that target an object id but never writes an `objects` row for it. `dangling_refs`
records those targets so the reference information is preserved and every object id is accounted for
(each id resolves to exactly one of `objects` or `dangling_refs`, never both). Common causes:

* Analyzing a single AssetBundle or a partial subset of a bundle group, so cross-bundle references
  point at bundles that were not analyzed.
* A Player build referencing `unity default resources` (and `Resources/unity_builtin_extra` when it
  is not part of the analyzed set) - built-in files that ship without TypeTrees and are not analyzed.
* A ContentDirectory build analyzed without its `ContentLayout.json`, so references cannot be resolved.

The columns mirror the `objects` table: `id` (the assigned object id, absent from `objects`),
`object_id` (the target's local file id / LFID) and `serialized_file`. The target file is recorded in
`serialized_files` (with `archive` NULL and no objects of its own) so its name is available; the
lowercased file name is all that is known about it - there is no type, size, or other detail.

`dangling_refs_view` joins each dangling target back to the object(s) that reference it, one row per
reference, with columns `source_id`, `source_serialized_file`, `source_object_id`, `property_path`,
`property_type`, `target_id`, `target_serialized_file`, `target_object_id`.

```sql
-- what does object 42 fail to resolve, and where should those objects have come from?
SELECT * FROM dangling_refs_view WHERE source_id = 42;
```

Because the view joins `refs`, it is not populated when analyze is run with `--skip-references`
(in that mode neither `refs` nor `dangling_refs` are populated).

## ContentLayout (content_layout tables)

When the analyzed input includes the `ContentLayout.json` of a ContentDirectory build (see
[contentlayout.md](contentlayout.md)), its content is imported into a set of `content_layout*`
tables. These tables are only created when a layout is actually imported, and a single layout per
database is supported. The tables mirror the json structure, with the sentinel values mapped to
SQL NULL (`-1` for "dropped from build", the missing content hash of built-in entries) and the
top-level `RootAssets` list folded into the `is_root_asset` flag:

* `content_layout`: one row identifying the imported file (`name`, `version`, `build_manifest_hash`).
* `content_layout_serialized_files`: one row per serialized file (.cf Content File) of the build,
  keyed by `file_index` (the json array index). Records the symbolic `cfid`, `is_builtin`, and the
  `content_hash` that gives the filename (`content_hash || '.cf'`). The `serialized_file` column
  references the core `serialized_files` table when the build content is part of the analyzed
  input, connecting the layout to the analyzed objects.
* `content_layout_source_assets`: the source assets included in each serialized file.
* `content_layout_serialized_file_dependencies`: file-to-file dependency edges. The 1-based
  `position` column preserves the json array order, which is how PPtr `m_FileID` values resolve
  (see [contentdirectory-format.md](contentdirectory-format.md)).
* `content_layout_loadable_dependencies` / `content_layout_loadable_scene_dependencies`: the
  loadables and scenes referenced from each serialized file.
* `content_layout_loadable_objects`: the objects loadable on demand, with their source project
  identity (`guid`, `asset_path`, `lfid`) and output location (`serialized_file_index`,
  `output_lfid`).
* `content_layout_loadable_scenes`: the loadable scenes.
* `content_layout_binary_artifacts`: every artifact of the build output (serialized files, .resS
  and .resource data files, the manifest) with its `category` and `size`.
* `content_layout_artifact_references`: direct references between artifacts.

Views join the pieces together: `content_layout_serialized_files_view` (files with derived
filename, size, and core-table link), `content_layout_source_assets_view` (source asset → built
files), `content_layout_serialized_file_dependencies_view` (dependency edges with resolved
filenames), `content_layout_loadable_objects_view` (loadables resolved to their analyzed object),
`content_layout_binary_artifacts_view` (artifacts with derived filenames), and
`content_layout_data_files_view` (the .resS/.resource data files each serialized file uses).

```sql
-- which files was this source asset built into?
SELECT * FROM content_layout_source_assets_view WHERE asset_path = 'Assets/Textures/GreenStatic.png';
```

## BuildReport

See [BuildReport.md](buildreport.md) for details of the tables and views related to analyzing BuildReport files.

# Advanced

## Using the library

The [AnalyzerTool](../Analyzer/AnalyzerTool.cs) class is the API entry point. The main method is called
Analyze. It is currently hard coded to write using the [SQLiteWriter](../Analyzer/SQLite/SQLiteWriter.cs),
but this approach could be extended to add support for other outputs.

Calling this method processes the provided paths, which can be individual files or directories.
Directories are scanned recursively for files matching the search pattern (unless recursion is
disabled). It will add a row in the 'objects' table for each serialized object. This table contains
basic information such as the size and the name of the object (if it has one).

## Extending the Library

The extracted information is forwarded to an object implementing the [IWriter](../Analyzer/IWriter.cs)
interface. The library provides the [SQLiteWriter](../Analyzer/SQLite/SQLiteWriter.cs) implementation that
writes the data into a SQLite database.

The core properties that apply to all Unity Objects are extracted into the `objects` table.
However much of the most useful Analyze functionality comes by virtue of the type-specific information that is extracted for
important types like Meshes, Shaders, Texture2D and AnimationClips.  For example, when a Mesh object is encountered in a Serialized
File, then rows are added to both the `objects` table and the `meshes` table.  The meshes table contains columns that only apply to Mesh objects, for example the number of vertices, indices, bones, and channels.  The `mesh_view` is a view that joins the `objects` table with the `meshes` table, so that you can see all the properties of a Mesh object in one place.

Each supported Unity object type follows the same pattern:
* A Handler class in the SQLite/Handlers, e.g. [MeshHandler.cs](../Analyzer/SQLite/Handler/MeshHandler.cs).
* The registration of the handler in the m_Handlers dictionary in [SerializedFileSQLiteWriter.cs](../Analyzer/SQLite/Writers/SerializedFileSQLiteWriter.cs).
* SQL statements defining extra tables and views associated with the type, e.g. [Mesh.sql](../Analyzer/SQLite/Resources/Mesh.sql).
* A Reader class that uses RandomAccessReader to read properties from the serialized object, e.g. [Mesh.cs](../Analyzer/SerializedObjects/Mesh.cs).

It would be possible to extend the Analyze library to add additional columns for the existing types, or by following the same pattern to add additional types.  The [dump](unitydatatool.md#dump) feature of UnityDataTool is a useful way to see the property names and other details of the serialization for a type.  Based on that information, code in the Reader class can use the RandomAccessReader to retrieve those properties to bring them into the SQLite database.

## Supporting Other File Formats

Another direction of possible extension is to support analyzing additional file formats, beyond Unity SerializedFiles.

This the approach taken to analyze Addressables Build Layout files, which are JSON files using the format defined in [BuildLayout.cs](../Analyzer/SQLite/Parsers/Models/BuildLayout.cs).

Support for another file format could be added by deriving an additional class from SQLiteWriter and implementing a class derived from ISQLiteFileParser.  Then follow the existing code structure convention to add new Commands (derived from AbstractCommand) and Resource .sql files to establish additional tables in the database.

An example of another file format that could be useful to support, as the tool evolves, are the yaml [.manifest files](https://docs.unity3d.com/Manual/assetbundles-file-format.html), generated by BuildPipeline.BuildAssetBundles().
