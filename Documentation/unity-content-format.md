# Overview of Unity Content 

This section gives an overview of the core Unity file types and how they are used in different types of builds. It also covers the important concept of "TypeTrees".  This gives context for understanding what UnityDataTools can and cannot do.

## File Formats

### SerializedFile

A SerializedFile is Unity's binary file format for serializing objects.  It is made up of a file header, then each object serialized one after another.  This binary format is the primary format loaded by the runtime, so when content is built for the Player this is the format that is produced.  The binary format is also available in the Editor, but typically user-visible project content uses the Unity YAML format instead of the binary format because the YAML format is more friendly for manual edits and source control operations.

The SerializedFiles in build output represent the project content, but optimized for the target platform.  Unity will combine objects from multiple source assets together into files, exclude certain objects (for example editor-only objects), and potentially split or duplicate objects across multiple output files.  This arrangement of objects is called the `build layout`.  Because of all this transformation, there is not a one-to-one mapping between the source assets and the SerializedFiles in the build output.

Use the [`serialized-file`](command-serialized-file.md) command to inspect SerializedFile headers, object lists, external references, and type metadata.

### Unity Archive

A Unity Archive is a container file (similar to a zip file).  Unity can `mount` this file, which makes the files inside it visible to Unity's loading system via the Unity "Virtual File System" (VFS).  Unity Archives often apply compression to the content, but it is also possible to create an uncompressed Archive.

Use the [`archive`](command-archive.md) command to inspect archive headers, data block layouts, and file listings, or to extract the contents of an archive.

## AssetBundles

[AssetBundles](https://docs.unity3d.com/Manual/AssetBundlesIntro.html) use the Unity Archive file format, with conventions for what to expect inside the archive.  The [Addressables](https://docs.unity3d.com/Manual/com.unity.addressables.html) package uses AssetBundles, so its build output is also made up of Unity Archive files.

AssetBundles always contain at least one SerializedFile.  In the case of an AssetBundle containing Scenes there will be multiple SerializedFiles.  AssetBundles can also contain auxiliary files, such as .resS files containing Textures and Meshes, and .resource files containing audio or video.

UnityDataTools supports opening Archive files, so it can analyze AssetBundles.

For a more technical, hands-on look at the internals of AssetBundles - the AssetBundle object, preload tables, and how scenes are laid out inside a bundle - see [AssetBundle Format](assetbundle-format.md).

## Player Builds

A Player build produces content alongside compiled code (assemblies, executables) and various configuration files.  UnityDataTool only concerns itself with the content portion of that output.

That content is made up of the same SerializedFiles and Unity Archives described above.  In brief:

* Each scene in the Scene List becomes a `level` file (`level0`, `level1`, …).
* Assets referenced from scenes are written to `sharedassets` files (`sharedassets0.assets`, …).
* The contents of `Resources` folders become `resources.assets`.
* Global project settings become `globalgamemanagers`, with their referenced assets in `globalgamemanagers.assets`.
* With compression enabled, all of these are packed into a single Unity Archive named `data.unity3d`.

By default Player builds omit TypeTrees, which limits what `analyze` and `dump` can do with them unless you rebuild with TypeTrees enabled (see [TypeTrees](#typetrees) below).

For a hands-on look at the Player build layout, the built-in resource files, compression, and how to inspect Player content (including platform-specific containers on Android and Web) with UnityDataTool, see [Player Build Format](playerbuild-format.md).  For Unity's official reference, see the Manual topic [Content output of a build](https://docs.unity3d.com/Manual/build-content-output.html).

## TypeTrees

The TypeTree is a data structure describing how objects have been serialized, i.e. the name, type, and
size of their properties. It is used by Unity when loading objects out of a SerializedFile that was built by a
different version of the Unity editor, or from different serialization layout for MonoBehaviours and ScriptableObjects in a project.

When Unity deserializes an object it checks whether the current type
definition exactly matches the type definition used when the object was serialized.  If they do not match,
Unity will attempt to match up the properties as best it can, based on the property names and structure
of the data.  This process is called a "Safe Binary Read".   This is significantly slower than the regular fast binary read path.

TypeTrees are important in the case of AssetBundles, to avoid rebuilding and redistributing all AssetBundles after each minor upgrade of Unity or after doing minor changes to your MonoBehaviour and ScriptableObject serialization.  However there can be a noticeable overhead to storing the TypeTrees in each AssetBundle, e.g. the header size of each SerializedFile is bigger.

TypeTrees also make it possible to load an AssetBundle in the Editor, when testing game play.

>[!NOTE]
>There is a flag available when building AssetBundles that will exclude TypeTrees, see [BuildAssetBundleOptions.DisableWriteTypeTree](https://docs.unity3d.com/6000.2/Documentation/ScriptReference/BuildAssetBundleOptions.DisableWriteTypeTree.html).  This has implications for future redistribution of your content, so use this flag with caution.

For Player Data the expectation is that you always rebuild all content together with each new build of the player.
So the assemblies and serialized objects will all have matching type definitions.  That is why, by default, TypeTrees are not included.

UnityDataTools relies on TypeTrees to understand the content of serialized objects for the `analyze` and `dump` command.  This approach means it does
not need to hard-code knowledge about the types and properties of each built-in Unity type
(for example Materials and Transforms).  It can also interpret serialized C# classes (e.g. MonoBehaviours, ScriptableObjects,
and objects serialized through the SerializeReference attribute).  This also means that UnityDataTools cannot understand
Player-built content, unless the Player was built with TypeTrees enabled.

Note: the `serialized-file` and `archive` command do not require TypeTrees.

>[!TIP]
>The `binary2text` tool supports an optional argument `-typeinfo` to enable dumping out the TypeTrees in a SerializedFile header.  That is a useful way to learn more about TypeTrees and to see exactly how Unity data is represented in the binary format.

#### Extracted Typetrees

Starting with Unity 6.5 and Addressables 2.9 it is possible to extract the TypeTrees from all the SerializedFiles in an Addressable build into a shared file.  This can reduce the size of the build output, because the TypeTree information is no longer duplicated in each file.

For details see [Addressable AssetBundle memory considerations](https://docs.unity3d.com/Packages/com.unity.addressables@2.9/manual/memory-assetbundles.html)

UnityDataTools commands that open serialized files using the UnityFileSystem API require access to the TypeTrees.  For example `dump` and `analyze`.  Use the `--typetree-data` option to specify the `.typetreedata` file when examining a build that has extracted TypeTrees.

## Mapping back to Source Assets

Because Unity rearranges objects in the build into a build layout there is no 1-1 mapping between the output files and the original source assets.  Only Scene files have a pretty direct mapping into the build output.

The UnityDataTool only looks at the output of the build, and has no information available about the source paths. This is expected, because the built output is optimized for speed and size, and there is no need to "leak" a lot of details about the source project in the data that gets shipped with the Player.

However in cases where you want to understand what contributes to the size of your build, or to confirm whether certain content is actually included, you may want to correlate the output back to the source assets in your project.

Often the source of content can be easily inferred, based on your own knowledge of your project, and the names of objects.  For example the name of a Shader should be unique, and typically has a filename that closely matches the Shader name.

You can include a Unity BuildReport file when running `UnityDataTools analyze`. This will import the PackedAsset information, tracking the source asset information for each object in the build output. See [Build Reports](./build-reports.md) for more information, including alternative ways to view the build report.

`UnityDataTools analyze` can also import Addressables build layout files, which include source asset information.  See [Addressable Build Reports](./addressables-build-reports.md).

For AssetBundles built by [BuildPipeline.BuildAssetBundles()](https://docs.unity3d.com/ScriptReference/BuildPipeline.BuildAssetBundles.html), Unity creates a .manifest file for each AssetBundle that has source information.  This is a text-based format.

For content directory builds, the [ContentLayout.json](contentlayout.md) file maps the build output back to the source assets in the project.
