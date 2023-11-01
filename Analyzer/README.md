# Analyzer

The Analyzer is a class library that can be used to analyze the content of Unity data files such
as AssetBundles and SerializedFiles. It iterates through all the serialized objects and uses the
TypeTree to extract information about these objects (e.g. name, size, etc.)

The extracted information is forwarded to an object implementing the [IWriter](./IWriter.cs)
interface. The library provides the [SQLiteWriter](./SQLite/SQLiteWriter.cs) implementation that
writes the data into a SQLite database.

It is possible to extract type-specific properties using classes inheriting from the
[SerializedObject](./SerializedObjects/SerializedObject.cs) class. The project already provides
the most common SerializedObject implementations in the [SerializedObjects](./SerializedObjects)
folder.

# How to use the library

The [AnalyzerTool](./AnalyzerTool.cs) class is the API entry point. The main method is called
Analyze. It is currently hardcoded to write using the [SQLiteWriter](./SQLite/SQLiteWriter.cs),
but it can easily be adapter to use another writer type. It takes four parameters:
* path (string): path of the folder where the files to analyze are located. It will be searched
  recursively.
* databaseName (string): database filename, it will be overwritten if it already exists.
* searchPattern (string): file search pattern (e.g. \*.bundle).
* skipReferences (bool): determines if the CRC calculation and references (PPtrs) extraction must
  skipped. This is faster, but the refs table will be empty and the duplicate assets won't be
  accurate.
Calling this method will create the SQLite output database and will recursively
process the files matching the search pattern in the provided path. It will add a row in
the 'objects' table for each serialized object. This table contain basic information such as the
size and the name of the object (if it has one).

# How to use the database

A tool such as the [DB Browser for SQLite](https://sqlitebrowser.org/) is required to look at the
content of the database. The database provides different views that can be used to easily find the
information you might need.

## object_view

This is the main view where the information about all the objects in the AssetBundles is available.
Its columns are:
* id: a unique id without any meaning outside of the database
* object_id: the Unity object id (unique inside its SerializedFile but not necessarily acros all
  AssetBundles)
* asset_bundle: the name of the AssetBundle containing the object (will be null if the source file
  was a SerializedFile and not an AssetBundle)
* serialized_file: the name of the SerializedFile containing the object
* type: the type of the object
* name: the name of the object, if it had one
* game_object: the id of the GameObject containing this object, if any (mostly for Components)
* size: the size of the object in bytes (e.g. 3343772)
* pretty_size: the size in an easier to read format (e.g. 3.2 MB)

## view_breakdown_by_type

This view lists the total number and size of the objects, aggregated by type.

## view_potential_duplicates

This view lists the objects that are possibly included more than once in the AssetBundles. This can
happen when an asset is referenced from multiple AssetBundles but is not assigned to one. In this
case, Unity will include the asset in all the AssetBundles with a reference to it. The
view_potential_duplicates provides the number of instances and the total size of the potentially
duplicated assets. It also lists all the AssetBundles where the asset was found.

If the skipReferences option is used, there will be a lot of false positives in that view. Otherwise,
it should be very accurate because CRCs are used to determine if objects are identical. 

## asset_view (AssetBundleProcessor)

This view lists all the assets that have been explicitly assigned to AssetBundles. The dependencies
that were automatically added by Unity at build time won't appear in this view. The columns are the
same as those in the *object_view* with the addition of the *asset_name* that contains the filename
of the asset.

## asset_dependencies_view  (AssetBundleProcessor)

This view lists the dependencies of all the assets. You can filter by id or asset_name to get all
the dependencies of an asset. Conversely, filtering by dep_id will return all the assets that
depend on this object. This can be useful to figure out why an asset was included in a build. 

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
