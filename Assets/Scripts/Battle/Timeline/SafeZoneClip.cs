using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LidarBattle.Battle
{
    /// <summary>クリップの間だけ安置のシートを出す。</summary>
    public class SafeZoneClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("安置の中心（盤面の正規化座標）。安置の外を埋める ShotClip の発射位置とそろえる")]
        [SerializeField] private Vector2 _center = new(0.5f, 0.5f);
        [Tooltip("安置の大きさ（ワールド単位）。FirePattern (Fill) の HoleSize とそろえる")]
        [SerializeField] private Vector2 _size = new(1.5f, 1f);

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<SafeZoneBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.Center = _center;
            behaviour.Size = _size;
            return playable;
        }
    }

    public class SafeZoneBehaviour : PlayableBehaviour
    {
        public Vector2 Center;
        public Vector2 Size;

        private SafeZoneView _view;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            // 表示は Play 中だけ。クリップに入った最初のフレームで 1 回だけ出す。
            if (!Application.isPlaying || _view != null || info.effectiveWeight <= 0f) return;
            if (playerData is not SafeZoneView view) return;

            _view = view;
            _view.Show(Center, Size);
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            if (_view == null) return;
            _view.Hide();
            _view = null;
        }
    }
}
