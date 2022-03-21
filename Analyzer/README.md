# Analyzer

The Analyzer is an API that can be used to analyze the content of Unity data files such as AssetBundles and SerializedFiles. It iterates through all the serialized objects and uses the TypeTree to extract useful information about these objects (e.g. name, size, etc.) The extracted information is stored in a SQLite database. It is possible to extract type-specific properties using specialized processors and some are included in this library.

# How to use the library

The AnalyzerTool class is the API entry point. The main method is called Analyze. It takes four parameters:
* path (string): path of the folder where the files to analyze are located. It will be searched recursively.
* databaseName (string): database filename, it will be overwritten if it already exists.
* searchPattern (string): file search pattern (e.g. \*.bundle).
* extractReferences (bool): determines if the references (PPtrs) must be extracted and saved in the 'refs' table.
Calling this method will create the SQLite output database and will recursively process the files matching the search pattern in the provided path. It will add a row in the 'objects' table for each serialized object. This table contain basic information such as the size and the name of the object (if it has one).

It is possible to extract more information from specific types of objects by adding processors using the AddProcessor method before calling Analyze. The processors must implement the IProcessor interface. Some processors are provided in the Processors folder:
* AnimationClipProcessor
* AssetBundleProcessor
* AudioClipProcessor
* MeshProcessor
* ShaderProcessor
* Texture2DProcessor
They are not added by default when creating a new instance of the AnalyzerTool class.

# How to use the database

A tool such as the [DB Browser for SQLite](https://sqlitebrowser.org/) is required to look at the content of the database. The database provides different views that can be used to easily find the information you might need.

## object_view

This is the main view where the information about all the objects in the AssetBundles is available. Its columns are:
* id: a unique id without any meaning outside of the database
* object_id: the Unity object id (unique inside its SerializedFile but not necessarily acros all AssetBundles)
* asset_bundle: the name of the AssetBundle containing the object (will be null if the source file was a SerializedFile and not an AssetBundle)
* serialized_file: the name of the SerializedFile containing the object
* type: the type of the object
* name: the name of the object, if it had one
* game_object: the id of the GameObject containing this object, if any (mostly for Components)
* size: the size of the object in bytes (e.g. 3343772)
* pretty_size: the size in an easier to read format (e.g. 3.2 MB)

## view_breakdown_by_type

This view lists the total number and size of the objects, aggregated by type.

## view_potential_duplicates

This view lists the objects that are possibly included more than once in the AssetBundles. This can happen when an is referenced from multiple AssetBundles but not assigned to one. In this case, Unity will include the asset in all the AssetBundles with a reference to it. The view provides the number of instances and the total size of the potentially duplicated assets. It also lists all the AssetBundles where the asset was found.

It is important to understand that there is a lot of false positives in that view. All the objects having an identical name, size and type are reported as potential duplicates. For example, if several animated characters have a bone GameObject named "Hand_L" they will all be reported as potential duplicates even if they are not part of the same object.

## asset_view (AssetBundleProcessor)

This view lists all the assets that have been explicitely assigned to AssetBundles. The dependencies that were automatically added by Unity at build time won't appear in this view. The columns are the same as those in the *object_view* with the addition of the *asset_name* that contains the filename of the asset.

## animation_view (AnimationClipProcessor)

This provides additional information about AnimationClips. The columns are the same as those in the *object_view*, with the addition of:
* legacy: 1 if it's a legacy animation, 0 otherwise
* events: the number of events

## audio_clip_view (AudioClipProcessor)

This provides additional information about AudioClips. The columns are the same as those in the *object_view*, with the addition of:
* bits_per_sample: number of bits per sample
* frequency: sampling frequency
* channels: number of channels
* load_type: either *Compressed in Memory*, *Decompress on Load* or *Streaming*
* format: compression format

## mesh_view (MeshProcessor)

This provides additional information about Meshes. The columns are the same as those in the *object_view*, with the addition of:
* sub_meshes: the number of sub-meshes
* blend_shapes: the number of blend shapes
* bones: the number of bones
* indices: the number of vertex indices
* vertices: the number of vertices
* compression: 1 if compressed, 0 otherwise
* rw_enabled: 1 if the mesh has the *R/W Enabled* option, 0 otherwise

## texture_view (Texture2DProcessor)

This provides additional information about Texture2Ds. The columns are the same as those in the *object_view*, with the addition of:
* width/height: texture resolution
* format: compression format
* mip_count: number of mipmaps
* rw_enabled:  1 if the mesh has the *R/W Enabled* option, 0 otherwise

## shader_view (ShaderProcessor)

This provides additional information about Shaders. The columns are the same as those in the *object_view*, with the addition of:
* decompressed_size: the size in bytes that this shader will need at runtime when loaded
* sub_shaders: the number of sub-shaders
* sub_programs: the number of sub-programs (usually one per shader variant, stage and pass)
* unique_programs: the number of unique program (variants with identical programs will share the same program in memory)
* keywords: list of all the keywords affecting the shader

## shader_subprogram_view (ShaderProcessor)

This view lists all the shader sub-programs and has the same columns as the *shader_view* with the addition of:
* api: the API of the shader (e.g. DX11, Metal, GLES, etc.)
* pass: the pass number of the sub-program
* hw_tier: the hardware tier of the sub-program (as defined in the Graphics settings)
* shader_type: the type of shader (e.g. vertex, fragment, etc.)
* prog_keywords: the shader keywords specific to this sub-program

## view_breakdowns_shaders (ShaderProcessor)

This view lists all the shaders aggregated by name. The *instances* column indicates how many time the shader was found in the data files. It also provides the total size per shader and the list of AssetBundles in which they were found.
