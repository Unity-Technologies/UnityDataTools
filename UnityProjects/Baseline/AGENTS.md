# Baseline test project

A Unity project that generates test data for the UnityDataTools test suites. It intentionally tracks older broadly-used Unity features and is upgraded only when necessary, it should be updated with the oldest currently supported LTS version.

## Assets

A deliberately broad mix of asset types, so the tool is exercised against many serialized object types: a shader, animation clips (including a legacy animation), an animator controller, a material, a prefab, FBX models, TIFF/JPG textures, a WAV clip and several scenes. `SerializeReferencePolymorphismExample.cs` demonstrates polymorphic `[SerializeReference]` fields.

## Editor scripts (`Assets/Editor`, `Tools` menu)

* `BuildAssetBundles.cs`
  * **Generate AssetBundles** - builds the `assetbundle` and `scenes` bundles (StandaloneOSX) and copies them to `TestCommon/Data/AssetBundles/<unityVersion>`.
  * **Generate PlayerData** - builds a player and copies its `level0` to `TestCommon/Data/PlayerData/<unityVersion>`. Requires the `ForceAlwaysWriteTypeTrees` diagnostic switch (Editor Preferences > Diagnostic/Editor).
* `TypeIdRegistryGenerator.cs` - regenerates `UnityFileSystem/TypeIdRegistry.cs` from the live engine type list. Run via **Tools > Generate TypeIdRegistry** or headless with `-executeMethod TypeIdRegistryGenerator.Generate`.

Output is written under a per-Unity-version subfolder so data from multiple versions can coexist.
