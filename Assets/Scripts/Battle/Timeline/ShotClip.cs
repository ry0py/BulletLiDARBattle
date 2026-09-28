using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace UndertaleLiDAR.Battle
{
    /// <summary>「どの弾を・どう飛ばすか・どこから・どの間隔で」を持つクリップ。クリップの長さ＝撃ち続ける時間。</summary>
    public class ShotClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] private BulletType _bulletType;
        [SerializeField] private FirePattern _pattern;
        [Tooltip("発射位置（盤面の正規化座標。(0.5, 1) = 枠の上辺中央。枠外も可）")]
        [SerializeField] private Vector2 _position = new(0.5f, 1f);
        [Tooltip("発射間隔（秒）。0 ならクリップの頭で 1 回だけ撃つ")]
        [SerializeField, Min(0f)] private float _interval = 0.5f;
        [SerializeField] private bool _useFixedSeed;
        [SerializeField] private int _seed;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<ShotBehaviour>.Create(graph);
            playable.GetBehaviour().Setup(_bulletType, _pattern, _position, _interval,
                _useFixedSeed ? new System.Random(_seed) : new System.Random());
            return playable;
        }
    }
}
