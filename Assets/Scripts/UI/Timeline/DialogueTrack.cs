using UnityEngine.Timeline;

namespace UndertaleLiDAR.UI
{
    /// <summary>バトル中のセリフを並べるトラック。バインド先は DialogueBox。</summary>
    [TrackColor(0.3f, 0.6f, 1f)]
    [TrackClipType(typeof(DialogueClip))]
    [TrackBindingType(typeof(DialogueBox))]
    public class DialogueTrack : TrackAsset
    {
    }
}
