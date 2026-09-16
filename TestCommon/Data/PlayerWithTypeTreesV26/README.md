# Test data description

This is the content output of a Player build, made with Unity 6000.7.0b2.
The diagnostic switch to enable TypeTrees was enabled when the build was performed.

It is a build of the same project as `PlayerWithTypeTrees` (Unity 6000.0.65f1), with the same two
scenes, so the two folders can be compared file by file. `PlayerWithTypeTrees` is SerializedFile
version 22 and this one is version 26, which makes the pair the reference for the Unity 6.7 format
changes: independently versioned TypeTrees, the `[SerializeReference]` registry frame, and shared
subtrees. For a version 23 comparison use `AssetBundleTypeTreeVariations/v23_Inline`.

## Content

See `../PlayerWithTypeTrees/README.md` for the scenes, the sharing arrangement and the scripting
types - they are the same here.

`resources.assets` is also included, which the version 23 folder does not have.
The `globalgamemanagers.assets.resS` splash screen data is not checked in, because it is 2.8 MB in
this build and nothing needs it.

## BuildReport

The LastBuild.buildreport file (created in the Library folder) has also been copied in.
