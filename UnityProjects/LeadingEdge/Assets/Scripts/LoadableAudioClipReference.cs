using System.Collections.Generic;
using Unity.Loading;
using UnityEngine;

// References AudioClips through a serialized Dictionary of Loadable<AudioClip>, creating on-demand (weak)
// references. The clips are included in the build but loaded only when code requests them.
public class LoadableAudioClipReference : ScriptableObject
{
    [SerializeField]
    public Dictionary<string, Loadable<AudioClip>> clips = new Dictionary<string, Loadable<AudioClip>>();
}
