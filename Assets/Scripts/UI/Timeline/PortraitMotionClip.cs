using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LidarBattle.UI
{
    /// <summary>立ち絵の動き方。</summary>
    public enum PortraitMotion { Float, Sway, Shake, Roam, Climb }

    /// <summary>
    /// クリップの間だけ、立ち絵を元の位置のまわりで動かす。Ease In/Out を付けると動きが滑らかに始まる・終わる。
    /// 時間はクリップ内の時間を使うので、一時停止（Timeline の速度 0）中は止まる。
    /// </summary>
    public class PortraitMotionClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("Float = ふわふわ上下, Sway = 左右に往復, Shake = 小刻みに震える, Roam = 左右を大きく行き来しながら跳ねる, Climb = Roam の上下版")]
        [SerializeField] private PortraitMotion _motion = PortraitMotion.Float;
        [Tooltip("動く幅（px、1920×1080 基準）")]
        [SerializeField] private float _amplitude = 12f;
        [Tooltip("1 秒あたりの往復回数（Shake は揺れの細かさ）")]
        [SerializeField] private float _frequency = 0.5f;
        [Tooltip("動きの中心の、元の位置からのずれ（px）。Ease In/Out の間に元の位置との間を移動する")]
        [SerializeField] private Vector2 _center;

        public PortraitMotion Motion => _motion;

        public ClipCaps clipCaps => ClipCaps.Blending;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<PortraitMotionBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.Motion = _motion;
            behaviour.Amplitude = _amplitude;
            behaviour.Frequency = _frequency;
            behaviour.Center = _center;
            return playable;
        }
    }

    public class PortraitMotionBehaviour : PlayableBehaviour
    {
        public PortraitMotion Motion;
        public float Amplitude;
        public float Frequency;
        public Vector2 Center;

        /// <summary>クリップ内の時刻 t 秒での、元の位置からのずれ。</summary>
        public Vector2 Offset(float t)
        {
            float phase = 2f * Mathf.PI * Frequency * t;
            float wave = Mathf.Sin(phase) * Amplitude;
            return Center + Motion switch
            {
                PortraitMotion.Float => new Vector2(0f, wave),
                PortraitMotion.Sway => new Vector2(wave, 0f),
                // 速さに緩急をつけるため、速い波を少し混ぜる。上下は小さく跳ねる。
                PortraitMotion.Roam => new Vector2(
                    (0.75f * Mathf.Sin(phase) + 0.25f * Mathf.Sin(2.3f * phase + 1f)) * Amplitude,
                    Mathf.Abs(Mathf.Sin(3f * phase)) * Amplitude * 0.04f),
                PortraitMotion.Climb => new Vector2(
                    Mathf.Abs(Mathf.Sin(3f * phase)) * Amplitude * 0.04f,
                    (0.75f * Mathf.Sin(phase) + 0.25f * Mathf.Sin(2.3f * phase + 1f)) * Amplitude),
                // 乱数ではなく Perlin ノイズなので、同じ時刻なら毎回同じ揺れになる。
                _ => new Vector2(Mathf.PerlinNoise(t * Frequency, 0.3f) - 0.5f,
                                 Mathf.PerlinNoise(0.7f, t * Frequency) - 0.5f) * (2f * Amplitude),
            };
        }
    }
}
