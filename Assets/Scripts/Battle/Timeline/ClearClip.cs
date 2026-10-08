using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LidarBattle.Battle
{
    /// <summary>クリップの頭で盤面の弾を全部消す（技の切り替わりで前の技の弾を残さないため）。ShotTrack に置く。</summary>
    public class ClearClip : PlayableAsset, ITimelineClipAsset
    {
        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner) =>
            ScriptPlayable<ClearBehaviour>.Create(graph);
    }

    public class ClearBehaviour : PlayableBehaviour
    {
        private bool _done;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!Application.isPlaying || _done || info.effectiveWeight <= 0f) return;
            if (playerData is not BulletSystem system) return;
            system.ClearAll();
            _done = true;
        }
    }
}
