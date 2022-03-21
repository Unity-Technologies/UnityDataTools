# ReferenceFinder

The ReferenceFinder is a library providing an API that can be used to find reference chains leading to specific objects.

## How to use

The API consists of a single class called ReferenceFinder. It requires a database that was previously created by the [Analyzer](../Analyzer/README.md) with extractReferences option. It takes an object id or name as input and will find reference chains originating from a root asset to the specified object(s). A root asset is an asset that was explicitely added to an AssetBundle at build time.

The ReferenceFinder has two public methods named FindReferences, one taking an object id and the other an object name and type. They both have these additional parameters:
* databasePath (string): path of the source database.
* outputFile (string): output filename.
* findAll (bool): determines if the method should find all reference chains leading to a single object or if it should stop at the first one.

## How to interpret the output file

The content of the output file looks like this:

    Reference chains to 
      ID:             1234
      Type:           Transform
      AssetBundle:    asset_bundle_name
      SerializedFile: CAB-353837edf22eb1c4d651c39d27a233b7

    Found reference in:
    MyPrefab.prefab
    (AssetBundle = MyAssetBundle; SerializedFile = CAB-353837edf22eb1c4d651c39d27a233b7)
      GameObject (id=1348) MyPrefab
        ↓ m_Component.Array[0].component
        RectTransform (id=721) [Component of MyPrefab (id=1348)]
          ↓ m_Children.Array[9]
          RectTransform (id=1285) [Component of MyButton (id=1284)]
            ↓ m_GameObject
            GameObject (id=1284) MyButton
              ↓ m_Component.Array[3].component
              MonoBehaviour (id=1288) [Script = Button] [Component of MyButton (id=1284)]
                ↓ m_OnClick.m_PersistentCalls.m_Calls.Array[0].m_Target
                MonoBehaviour (id=1347) [Script = MyButtonEffect] [Component of MyPrefab (id=1348)]
                  ↓ effectText
                  MonoBehaviour (id=688) [Script = TextMeshProUGUI] [Component of MyButtonText (id=588)]
                    ↓ m_GameObject
                    GameObject (id=588) MyButtonText
                      ↓ m_Component.Array[0].component
                      RectTransform (id=587) [Component of MyButtonText (id=588)]
                        ↓ m_Father
                        RectTransform (id=589) [Component of MyButtonImage (id=944)]
                          ↓ m_Children.Array[10]
                          Transform (id=1234) [Component of MyButtonEffectLayer (1) (id=938)]
                            ↓ m_GameObject
                            GameObject (id=938) MyButtonEffectLayer (1)
                              ↓ m_Component.Array[0].component
                              Transform (id=1234) [Component of MyButtonEffectLayer (1) (id=938)]

    Analyzed 266 object(s).
    Found 1 reference chain(s).

For each object matching the id or name and type provided, the output file will provide the information related to it. In this case, it was a Transform in the AssetBundle named MyAssetBundle. It will then list all the root objects having at least one reference chain leading to that object. In this case, there was a prefab named MyPrefab that had a hierarchy of GameObjects where one had a reference on the Transform.

For each reference in the chain, the name of the property is provided. For example, we can see that the first reference in the chain is from the m_Component.Array\[0\].component property of the MyPrefab GameObject. This is the first item in the array of Components of the GameObject and it points to a RectTransform. When the referenced object is a Component, the corresponding GameObject name is also provided (in this case, it's obviously MyPrefab). When MonoBehaviour are encountered, the name of the corresponding Script is provided too (because MonoBehaviour names are empty for some reason). The last item in the chain is the object that was provided as input.
