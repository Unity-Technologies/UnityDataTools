using UnityEngine;
using UnityEngine.Video;

// Directly references the VideoClips so they are pulled into both the AssetBundle and the Content Directory build.
public class VideoClipReference : ScriptableObject
{
    public VideoClip[] clips;
}
