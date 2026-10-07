using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LidarBattle.UI
{
    /// <summary>立ち絵の動き方。</summary>
    public enum PortraitMotion { Float, Sway, Shake }

    /// <summary>
    /// クリップの間だけ、立ち絵を元の位置のまわりで動かす。Ease In/Out を付けると動きが滑らかに始まる・終わる。
    /// 時間はクリップ内の時間を使うので、一時停止（Timeline の速度 0）中は止まる。
    /// </summary>
    public class PortraitMotionClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("Float = ふわふわ上下, Sway = 左右に往復, Shake = 小刻みに震える")]
        [SerializeField] private PortraitMotion _motion = PortraitMotion.Float;
        [Tooltip("動く幅（px、1920×1080 基準）")]
        [SerializeField] private float _amplitude = 12f;
        [Tooltip("1 秒あたりの往復回数（Shake は揺れの細かさ）")]
        [SerializeField] private float _frequency = 0.5f;

        public PortraitMotion Motion => _motion;

        public ClipCaps clipCaps => ClipCaps.Blending;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<PortraitMotionBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.Motion = _motion;
            behaviour.Amplitude = _amplitude;
            behaviour.Frequency = _frequency;
            return playable;
        }
    }

    public class PortraitMotionBehaviour : PlayableBehaviour
    {
        public PortraitMotion Motion;
        public float Amplitude;
        public float Frequency;

        /// <summary>クリップ内の時刻 t 秒での、元の位置からのずれ。</summary>
        public Vector2 Offset(float t)
        {
            float wave = Mathf.Sin(2f * Mathf.PI * Frequency * t) * Amplitude;
            return Motion switch
            {
                PortraitMotion.Float => new Vector2(0f, wave),
                PortraitMotion.Sway => new Vector2(wave, 0f),
                // 乱数ではなく Perlin ノイズなので、同じ時刻なら毎回同じ揺れになる。
                _ => new Vector2(Mathf.PerlinNoise(t * Frequency, 0.3f) - 0.5f,
                                 Mathf.PerlinNoise(0.7f, t * Frequency) - 0.5f) * (2f * Amplitude),
            };
        }
    }
}
