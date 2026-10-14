using LidarBattle.Battle;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LidarBattle.Training
{
    /// <summary>
    /// 強化学習の「場」: 技 1 つ（8 秒の Timeline）を流し、SOUL を置き直す。ML-Agents の 1 ステップ = 1 フレームにそろえる。
    /// 学習中（mlagents-learn とつながっているとき）は 1 フレームを 1/60 秒に固定して、PC が出せる最高速で回す。
    /// 詳細は .claude/docs/rl-training.md。
    /// </summary>
    public class DodgeArena : MonoBehaviour
    {
        /// <summary>学習中の 1 フレームの長さ（秒）。弾の固定ステップと同じにする。</summary>
        private const float TrainingFrameSeconds = 1f / 60f;

        [SerializeField] private PlayableDirector _director;
        [SerializeField] private BattleClock _clock;
        [SerializeField] private BulletSystem _bullets;
        [SerializeField] private SoulController _soul;
        [Tooltip("技の Timeline。難易度 Easy → Medium → Hard の順に、同じ技の並びで入れる（Build RL Training Scene が設定する）")]
        [SerializeField] private TimelineAsset[] _timelines;

        private TimelineAsset _current;
        private float _elapsed;

        public int AttackCount => _timelines.Length / 3;
        /// <summary>今の技の進み具合 (0..1)。</summary>
        public float Progress => _current == null ? 0f : Mathf.Clamp01(_elapsed / (float)_current.duration);
        public bool Finished => _current != null && _elapsed >= _current.duration;
        public string CurrentName => _current == null ? "" : _current.name;
        public BulletSystem Bullets => _bullets;
        public SoulController Soul => _soul;

        private void Awake()
        {
            // 物理の FixedUpdate ではなく、このスクリプトの Update で 1 フレームに 1 回だけ進める。
            Academy.Instance.AutomaticSteppingEnabled = false;
            // 判断は 4 フレームに 1 回なので、学習済みの AI を見るとき（Python なし）も 60 fps にそろえる。
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            if (!Academy.Instance.IsCommunicatorOn) return;
            Time.captureDeltaTime = TrainingFrameSeconds;
            Application.targetFrameRate = -1;
            Application.runInBackground = true; // ウィンドウが裏に回っても学習を止めない
            AudioListener.volume = 0f;
        }

        private void OnDestroy()
        {
            if (Academy.IsInitialized) Academy.Instance.AutomaticSteppingEnabled = true;
            Time.captureDeltaTime = 0f;
        }

        private void Update()
        {
            // mlagents-learn の time_scale（既定 20）は 1 フレームを長くするだけで SOUL の動きが粗くなるので、1 に戻す。
            if (Academy.Instance.IsCommunicatorOn) Time.timeScale = 1f;
            if (_current != null) _elapsed += _clock.DeltaTime;
            Academy.Instance.EnvironmentStep();
        }

        /// <summary>技を最初から流し直す。difficulty は 0..2、attack は技の番号（範囲外ならランダム）。</summary>
        public void Restart(int difficulty, int attack, Vector2 soulStart)
        {
            difficulty = Mathf.Clamp(difficulty, 0, 2);
            if (attack < 0 || attack >= AttackCount) attack = Random.Range(0, AttackCount);
            _current = _timelines[difficulty * AttackCount + attack];
            _elapsed = 0f;

            _director.Stop();
            _bullets.ClearAll();
            _soul.Place(soulStart);

            _director.playableAsset = _current;
            foreach (var track in _current.GetOutputTracks())
            {
                if (track is ShotTrack) _director.SetGenericBinding(track, _bullets);
                else if (track is SafeZoneTrack) _director.SetGenericBinding(track, _bullets.Board.GetComponentInChildren<SafeZoneView>(true));
            }
            _director.Play();
        }
    }
}
