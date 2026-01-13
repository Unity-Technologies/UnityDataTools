# Test data description

This is the content output of a Player build, made with Unity 6000.0.65f1.
The diagnostic switch to enable TypeTrees was enabled when the build was performed.

The project is very simple and intended to be used for precise tests of expected content.

## Content

The build includes two scene files:
* SceneWithReferences.unity (level0) uses MonoBehaviours to references BasicScriptableObject.asset and Asset2.asset
* SceneWithReferences2.unity (level1) uses MonoBehaviours to reference BasicScriptableObject.asset and ScriptableObjectWithSerializeReference.asset

Based on that sharing arrangement:
* sharedassets0.assets contains BasicScriptableObject.asset and Asset2.asset
* sharedassets1.assets contains ScriptableObjectWithSerializeReference.asset

There are also additional content 
* globalgamemanager with the preference objects
* globalgamemanager.assets with assets referenced from the globalgamemanager file
* globalgamemanagers.assets.resS containing the splash screen referenced from globalgamemanager.assets

Note: The binaries, json files and other output that were also output from the player build are not checked in, because they are not needed by UnityDataTool.

## BuildReport

The LastBuild.buildreport file (created in the Library folder) has also been copied in.

## Scripting types

The MonoBehaviour used in level0 and level1 to reference the ScriptableObject is of type MonoBehaviourWithReference.

* BasicScriptableObject.asset and Asset2.asset are instances of the BasicScriptableObject class.
* ScriptableObjectWithSerializeReference.asset is an instance of the MyNamespace.ScriptableObjectWithSerializeReference class.


