using System.Collections.Generic;
using UnityEngine;

// References other ScriptableObjects through a serialized Dictionary, creating direct (strong) references.
// Used as a root asset that aggregates several leaf reference assets.
public class DirectScriptableObjectReference : ScriptableObject
{
    [SerializeField]
    public Dictionary<string, ScriptableObject> references = new Dictionary<string, ScriptableObject>();
}
