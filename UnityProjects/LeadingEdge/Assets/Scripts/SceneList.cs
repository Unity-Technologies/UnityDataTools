using System.Collections.Generic;
using Unity.Loading;
using UnityEngine;

// References scenes through a serialized Dictionary of LoadableSceneId. The scenes are included in the
// build and can be loaded on demand by name through SceneManager.LoadSceneAsync(LoadableSceneId).
public class SceneList : ScriptableObject
{
    [SerializeField]
    public Dictionary<string, LoadableSceneId> scenes = new Dictionary<string, LoadableSceneId>();
}
