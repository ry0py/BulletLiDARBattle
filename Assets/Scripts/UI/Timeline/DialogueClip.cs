using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LidarBattle.UI
{
    /// <summary>クリップの間だけセリフを表示する。</summary>
    public class DialogueClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField, TextArea] private string _text;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<DialogueBehaviour>.Create(graph);
            playable.GetBehaviour().Text = _text;
            return playable;
        }
    }
}
