using UnityEngine;
using UnityEngine.Playables;

namespace UndertaleLiDAR.Battle
{
    /// <summary>
    /// バトル内の時間。一時停止は全体、スローは弾と Timeline だけに効かせる（SOUL は通常速度で動ける）。
    /// Time.timeScale を使わないのは、UI やデバッグ表示まで止めないため。
    /// </summary>
    public class BattleClock : MonoBehaviour
    {
        [SerializeField] private PlayableDirector _director;
        [SerializeField, Range(0.05f, 1f)] private float _slowScale = 0.3f;

        public bool IsPaused { get; private set; }
        public bool IsSlow { get; private set; }

        public float DeltaTime => IsPaused ? 0f : Time.deltaTime;
        public float BulletDeltaTime => DeltaTime * BulletTimeScale;

        private float BulletTimeScale => IsSlow ? _slowScale : 1f;

        public void SetPaused(bool paused) => IsPaused = paused;
        public void SetSlow(bool slow) => IsSlow = slow;

        private void Update()
        {
            // Director は Play のたびに Graph を作り直すので、毎フレーム速度を合わせる。
            if (_director == null || !_director.playableGraph.IsValid()) return;
            _director.playableGraph.GetRootPlayable(0).SetSpeed(IsPaused ? 0f : BulletTimeScale);
        }
    }
}
