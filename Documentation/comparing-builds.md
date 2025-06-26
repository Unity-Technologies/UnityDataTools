# Comparing Builds

When working with Unity typically many builds will be performed, so that the content can be tested in the player or released.  Each time a build is performed it is likely that some content will changed. Normally the change should be predictable, based on changes made to assets, scenes, scripts, packages or any upgrades to the Unity Editor.  But in other cases there may be a problem of "non-determinism", where the build changes each time it is run, or has different output from different identical build machines.  So a common question is "what changed" between builds?  This question arises most frequently in the area of AssetBundles, so the examples here focus on that type of build.  But the same principals can also apply to Player builds.

This topic gives examples using several tools and techniques to compare build output files.

* [comparebuilds.ps1](../scripts/comparebuilds.ps1)  An example script using UnityDataTool to compare two builds at the object level
* [comparebundles.ps1](../scripts/comparebundles.ps1) An example script using UnityDatatTool to compare two versions of a build file.
* A diff tool for comparing directories, binary files and text files.  These tools are readily available on Windows, Mac and Linux.  For example Beyond Compare, WinMerge, and kdiff3.
* WebExtract for extracting contents of an AssetBundle (WebExtract is shipped as part of the Unity Editor installation).
* `UnityDataTool dump` to create a text representation of the content of a Unity Serialized File.

The [Overview of Unity Content](./unity-content-format.md) topic gives useful background for the file formats and concepts that are discussed in this topic.

# Example 1 - Changes in a texture

As an example suppose that two builds of the same project are located side-by-side in two directories, /build1 and /build2.

This build includes an AssetBundle called "sprites.bundle" that contains 3 textures ("red.png", "Snow.png" and "Snow 1.png").

## File-level comparison

A quick way to compare two builds is to do a file-level comparison, e.g. using a diff tool such as `WinMerge` to compare build1 and build2.

![](./AssetBundleBuildComparison.png)

This will quickly narrow down which AssetBundle files have changed.  But AssetBundles files are binary archive files, so this won't show what changed inside the files.

## UnityDataTool object comparison

UnityDataTools does not natively support comparing two builds, but it can be done by analyzing each build individually into separate SQLite databases, then running queries to compare the contents of the two databases.

For example two database could be generated as follows:

```
UnityDataTool.exe analyze -o build1.db .\Build1\
UnityDataTool.exe analyze -o build2.db .\Build2\
```

Running the following PowerShell script would print all the objects, with info about whether they match between the two builds.  Objects are matched primarily based on the AssetBundle and local file ID (object_id) matching.  If there is a change in content then the CRC should change. The object size is also shown (which includes the size of data that is stored in the side-car .resS file).

[comparebuilds.ps1](../scripts/comparebuilds.ps1)

This is a truncated example output, where the "red.png" image has been changed between builds:


```
asset_bundle    object_id             type                 name                 status     size_build1  size_build2  crc32_build1  crc32_build2
--------------  --------------------  -------------------  -------------------  ---------  -----------  -----------  ------------  ------------
AssetBundles    1                     AssetBundle                               Same       104          104          241569179     241569179
AssetBundles    2                     AssetBundleManifest  AssetBundleManifest  Different  184          184          4124235088    3102991602
audio.bundle    -1630896013228033972  AudioClip            audio                Same       18656        18656        883020518     883020518
audio.bundle    1                     AssetBundle          audio.bundle         Same       144          144          2644028121    2644028121
sprites.bundle  -4266742476527514910  Sprite               Snow 1               Same       464          464          2360191667    2360191667
sprites.bundle  -39415655269619539    Texture2D            Snow 1               Same       524496       524496       3893000759    3893000759
sprites.bundle  -3600607445234681765  Texture2D            red                  Different  152079       152079       3533099562    3115177070
sprites.bundle  -1350043613627603771  Texture2D            Snow                 Same       524492       524492       3894005184    3894005184
sprites.bundle  1                     AssetBundle          sprites.bundle       Same       460          460          245831303     245831303
```

The output pinpoints that "red" has changed. The AssetBundleManifest object also changes, which is expected because it lists AssetBundle content hashes.

### Comparing Individual AssetBundles

A variation of comparing entire builds is to compare two versions of an individual AssetBundle.

The script [comparebundles.ps1](../scripts/comparebundles.ps1) is an example of this approach.  It creates temporary sqlite databases, so that the comparison is a convenient one-step process.

For example, to analyze the two versions of sprites.bundle it could be invoked like this:

```
comparebundles.ps1 .\Build1\sprites.bundle .\Build2\sprites.bundle
```

The output from this example would be:


```
serialized_file                       object_id             type       name  status     size_build1  size_build2  crc32_build1  crc32_build2
------------------------------------  --------------------  ---------  ----  ---------  -----------  -----------  ------------  ------------
CAB-6b49068aebcf9d3b05692c8efd933167  -3600607445234681765  Texture2D  red   Different  152079       152079       3533099562    3115177070
```

### Analyzing Differences in .ResS Files

UnityDataTool helps pinpoint which AssetBundle objects have changed between builds.  But to actually understand "what" has changed it is necessary to look deeper into the content of the AssetBundles and how Unity serializes data.

We already know that sprites.bundle has changed between builds, and the script pinpoints "red" as the object that changed, whereas "Snow" and "Snow 1" are unchanged.  So how can we determine more information about what has changed in the build of "red.png"?

To go deeper we can extract the content of each build of sprites.bundle.  The **WebExtract** tool that is shipped with Unity can be used to do this.  When run on an AssetBundle it creates a subdirectory with all the contents of the AssetBundle expanded as individual files.

```
cd Build1
WebExtract.exe sprites.bundle
cd ..\Build2
WebExtract.exe sprites.bundle
```

When WebExtract has been run on both copies of sprites.bundle the diff tool can be used to compare the contents of the AssetBundle:

![](./AssetBundleContentComparison.png)

In this case we see that the AssetBundle contained the following content:

![](./SpritesBundleContent.png)

And, based on the diff, we see that the SerializedFile is unchanged between builds, but the .resS file is different.  This means that the Texture2D object has the exact same properties (including dimensions, format etc), but the pixel data is different.

For the sake of further illustration, we can go deeper and look at how the .resS file relates to the 3 textures in sprites.bundle. 

When a binary diff is performed on the two verions of the .resS file we can see that all the differences are located near the start of the file, finishing before address 0x25150 (151,888 in decimal).  The rest of the file is identical.

![](./ResFileBinaryDiff.png)

We know from our UnityDataTool queries that "red" is the only texture that changed, so we can surmise that the "red" texture is at the start of the .resS file.  Its possible to confirm this by further analysis of the AssetBundle contents.

To understand the content of a resS file we have to look at the associated SerializedFile.  E.g. to understand what is contained inside `CAB-6b49068aebcf9d3b05692c8efd933167.resS` we need to look inside `CAB-6b49068aebcf9d3b05692c8efd933167`.

Because the SerializedFile is a binary format, we first need to convert it to text.  We can do this using the `dump` feature of UnityDataTools.  We can run this on the WebExtract output from either build1 or build2 (because the file is identical from both builds).

```
UnityDataTool dump CAB-6b49068aebcf9d3b05692c8efd933167
```

Inside this file we can search for all mentions of "CAB-6b49068aebcf9d3b05692c8efd933167.resS". This search discovers 3 Texture2D objects.  These are the relevant parts of the output file:

```
ID: -3600607445234681765 (ClassID: 28) Texture2D
  m_Name (string) red
  ...
  m_StreamData (StreamingInfo)
    offset (UInt64) 0
    size (unsigned int) 151875
    path (string) archive:/CAB-6b49068aebcf9d3b05692c8efd933167/CAB-6b49068aebcf9d3b05692c8efd933167.resS
```

```
ID: -1350043613627603771 (ClassID: 28) Texture2D
  m_Name (string) Snow
  ...
  m_StreamData (StreamingInfo)
    offset (UInt64) 151888
    size (unsigned int) 524288
    path (string) archive:/CAB-6b49068aebcf9d3b05692c8efd933167/CAB-6b49068aebcf9d3b05692c8efd933167.resS
```

```
ID: -39415655269619539 (ClassID: 28) Texture2D
  m_Name (string) Snow 1
  ...
  m_StreamData (StreamingInfo)
    offset (UInt64) 676176
    size (unsigned int) 524288
    path (string) archive:/CAB-6b49068aebcf9d3b05692c8efd933167/CAB-6b49068aebcf9d3b05692c8efd933167.resS

```

The resS file is a simple format with no header.  It is literally just the binary data of textures or meshes, concatenated together (sometimes with extra padding bytes between entries).  The m_StreamData describes each range of bytes inside the .resS file.  The total file size on disk is 1200463 bytes, so every byte of the file is accounted for based on the three objects.

This diagram shows the structure and relation ship between the objects inside the Serialized file and the content of the .resS file.

![](./SpritesBundleDetailedContent.png)

Based on this analysis we have confirmed that the range information for "red" exactly matches the changes we observed in the binary diff.  So this confirms our understanding that pixel data inside "red.png" is what caused the AssetBundle content to change.

This same approach can be used to analyze mesh data inside .resS files.  And also for Audio and Video inside .resource files.
