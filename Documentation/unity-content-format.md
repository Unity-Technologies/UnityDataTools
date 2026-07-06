# Overview of Unity Content 

This section gives an overview of the core Unity file types and how they are used in different types of builds. It also covers the important concept of "TypeTrees".  This gives context for understanding what UnityDataTools can and cannot do.

## File Formats

### SerializedFile

A SerializedFile is Unity's binary file format for serializing objects.  It is made up of a file header, then each object serialized one after another.  This binary format is also available in the Editor, but typically Editor content uses the Unity YAML format instead.

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

A player build produces content as well as compiled code (assemblies, executables) and various configuration files.  UnityDataTool only concerns itself with the content portion of that output.

The content comprises the scenes in the Scene List, the contents of Resources folders, content from the Project Preferences (the "GlobalGameManagers"), and all Assets referenced from those root inputs.  This translates into SerializedFiles in the build output.

The SerializedFiles are named in a predictable way.  This is a very quick summary: 

* Each scene in the SceneList becomes a "level" file, e.g. "level0", "level1".
* Assets referenced from Scenes becomes "sharedAssets" files, e.g. "sharedAssets0.assets", "sharedAssets1.assets".  Scenes are processed in order of the scene list and assets are stored in the sharedasset file corresponding to the scene where they are first encountered.  This means that a level file may reference multiple sharedasset files, but only the ones at the same number and lower.  For example the 3rd scene, level2 can reference "sharedAssets2.assets", "sharedAssets1.assets" and "sharedAssets0.assets" but never "sharedAssets3.assets".
* The contents of the Resources folder becomes "resources.assets".
* The Preferences become "globalgamemanager".  Assets referenced from "globalgamemanager" are saved in "globalgamemanager.assets".

If [compression](https://docs.unity3d.com/6000.2/Documentation/ScriptReference/BuildOptions.CompressWithLz4HC.html) is enabled, the Player build will compress all the serialized files into a single Unity Archive file, called `data.unity3d`.

### Enabling TypeTrees in the Player

UnityDataTools supports Player build output, because that uses the same SerializedFiles and Archives that AssetBundles use.  But often its output is not very useful. That is because, by default, Player builds do not include TypeTrees.

>[!IMPORTANT]
>It is possible to generate TypeTrees for the Player data, starting in Unity 2021.2.
>This makes that output compatible with UnityDataTool, but it is not a recommended flag to enable for your production builds.

To do so, the **ForceAlwaysWriteTypeTrees** Diagnostic Switch must be enabled in the Editor Preferences (Diagnostics->Editor section).

![](./TypeTreeForPlayer.png)


Note: The `Resources\unity default resources` file is shipped with the Unity Editor and is not rebuilt when doing a Player Build.  It does not have TypeTrees.  Hence it is normal that this file emits errors when analyzing a player build, even after rebuilding with TypeTrees enabled.  For example:

```
Error processing file: C:\TestProject\CompressedPlayer\TestProject_Data\Resources\unity default resources
System.ArgumentException: Invalid object id.
```

For more information about TypeTrees see the following section.

## TypeTrees

The TypeTree is a data structure describing how objects have been serialized, i.e. the name, type, and
size of their properties. It is used by Unity when loading a SerializedFile that was built by a
different Unity version.  When Unity deserializes an object it checks whether the current type
definition exactly matches the type definition used when the object was serialized.  If they do not match,
Unity will attempt to match up the properties as best it can, based on the property names and structure
of the data.  This process is called a "Safe Binary Read" and is somewhat slower than the regular fast binary read path.

TypeTrees are important in the case of AssetBundles, to avoid rebuilding and redistributing all AssetBundles after each minor upgrade of Unity or after doing minor changes to your MonoBehaviour and ScriptableObject serialization.  However there can be a noticeable overhead to storing the TypeTrees in each AssetBundle, e.g. the header size of each SerializedFile is bigger.

TypeTrees also make it possible to load an AssetBundle in the Editor, when testing game play.

>[!NOTE]
>There is a flag available when building AssetBundles that will exclude TypeTrees, see [BuildAssetBundleOptions.DisableWriteTypeTree](https://docs.unity3d.com/6000.2/Documentation/ScriptReference/BuildAssetBundleOptions.DisableWriteTypeTree.html).  This has implications for future redistribution of your content, so use this flag with caution.

For Player Data the expectation is that you always rebuild all content together with each new build of the player.
So the assemblies and serialized objects will all have matching type definitions.  That is why, by default, TypeTrees are not included.

UnityDataTools relies on TypeTrees to understand the content of serialized objects.  This approach means it does
not need to hard-code knowledge about the types and properties of each built-in Unity type
(for example Materials and Transforms).  It can also interpret serialized C# classes (e.g. MonoBehaviours, ScriptableObjects,
and objects serialized through the SerializeReference attribute).  This also means that UnityDataTools cannot understand
Player-built content unless the Player was built with TypeTrees enabled.

>[!TIP]
>The `binary2text` tool supports an optional argument `-typeinfo` to enable dumping out the TypeTrees in a SerializedFile header.  That is a useful way to learn more about TypeTrees and to see exactly how Unity data is represented in the binary format.

#### Extracted Typetrees

Starting with Unity 6.5 and Addressables 2.9 it is possible to extract the TypeTrees from all the SerializedFiles in an Addressable build into a shared file.  This can reduce the size of the build output, because the TypeTree information is no longer duplicated in each file.

For details see [Addressable AssetBundle memory considerations](https://docs.unity3d.com/Packages/com.unity.addressables@2.9/manual/memory-assetbundles.html)

UnityDataTools commands that open serialized files using the UnityFileSystem API require access to the TypeTrees.  For example `dump` and `analyze`.  Use the `--typetree-data` option to specify the `.typetreedata` file when examining a build that has extracted TypeTrees.

### Platform details for using UnityDataTool with Player Data

The output structure and file formats for a Unity Player build are quite platform specific.

On some platforms the content is packaged into platform-specific container files, for example Android builds use .apk and .obb files.  So accessing the actual SerializedFiles may involve mounting or extracting the content of those files, and possibly also opening a data.unity3d file inside them.

UnityDataTools directly supports opening the `.data` container file format used in Player builds that target Web platforms (e.g. WebGL).  Specifically the [`archive list`](command-archive.md#list) and [`archive extract`](command-archive.md#extract) commands work with that format.  Once extracted, you can run other UnityDataTool commands on the output.

Android APK files are not difficult to open and expand using freely available utilities.  For example on Windows they can be opened using 7-zip.  Once the content is extracted you can run UnityDataTool commands on the output.

## Mapping back to Source Assets

Because Unity rearranges objects in the build into a build layout there is no 1-1 mapping between the output files and the original source assets.  Only Scene files have a pretty direct mapping into the build output.

The UnityDataTool only looks at the output of the build, and has no information available about the source paths. This is expected, because the built output is optimized for speed and size, and there is no need to "leak" a lot of details about the source project in the data that gets shipped with the Player.

However in cases where you want to understand what contributes to the size of your build, or to confirm whether certain content is actually included, you may want to correlate the output back to the source assets in your project.

Often the source of content can be easily inferred, based on your own knowledge of your project, and the names of objects.  For example the name of a Shader should be unique, and typically has a filename that closely matches the Shader name.

You can include a Unity BuildReport file when running `UnityDataTools analyze`. This will import the PackedAsset information, tracking the source asset information for each object in the build output. See [Build Reports](./build-reports.md) for more information, including alternative ways to view the build report.

`UnityDataTools analyze` can also import Addressables build layout files, which include source asset information.  See [Addressable Build Reports](./addressables-build-reports.md).

For AssetBundles built by [BuildPipeline.BuildAssetBundles()](https://docs.unity3d.com/ScriptReference/BuildPipeline.BuildAssetBundles.html), Unity creates a .manifest file for each AssetBundle that has source information.  This is a text-based format.

For content directory builds, the [ContentLayout.json](contentlayout.md) file maps the build output back to the source assets in the project.
