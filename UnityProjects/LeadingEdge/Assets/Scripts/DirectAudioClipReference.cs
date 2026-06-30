using System.Collections.Generic;
using UnityEngine;

// References AudioClips through a serialized Dictionary, creating direct (strong) references.
// All referenced clips are loaded immediately when this asset is loaded.
public class DirectAudioClipReference : ScriptableObject
{
    [SerializeField]
    public Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
}
