using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LidarBattle.UI
{
    /// <summary>クリップの間だけ、立ち絵をこの表情にする。</summary>
    public class PortraitClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] private PortraitExpression _expression = PortraitExpression.Talk;

        public PortraitExpression Expression => _expression;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<PortraitBehaviour>.Create(graph);
            playable.GetBehaviour().Expression = _expression;
            return playable;
        }
    }

    public class PortraitBehaviour : PlayableBehaviour
    {
        public PortraitExpression Expression;
    }
}
