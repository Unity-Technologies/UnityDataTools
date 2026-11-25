using UnityEngine;
using UnityEngine.Video;

// Directly references a VideoClip so the clip is pulled into both the AssetBundle and the Content Directory build.
public class VideoClipReference : ScriptableObject
{
    public VideoClip clip;
}
