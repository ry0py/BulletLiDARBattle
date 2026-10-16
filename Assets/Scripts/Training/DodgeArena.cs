using LidarBattle.Battle;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LidarBattle.Training
{
    /// <summary>
    /// 強化学習の「場」（盤面 1 つ）: 技 1 つ（8 秒の Timeline）を流し、SOUL を置き直す。
    /// 盤面を並べる・ML-Agents を 1 フレーム 1 ステップ進めるのは DodgeTrainingRunner。詳細は .claude/docs/rl-training.md。
    /// </summary>
    public class DodgeArena : MonoBehaviour
    {
        [SerializeField] private PlayableDirector _director;
        [SerializeField] private BattleClock _clock;
        [SerializeField] private BulletSystem _bullets;
        [SerializeField] private SoulController _soul;
        [Tooltip("技の Timeline。難易度 Easy → Medium → Hard の順に、同じ技の並びで入れる（Build RL Training Scene が設定する）")]
        [SerializeField] private TimelineAsset[] _timelines;

        private TimelineAsset _current;
        private float _elapsed;
        private SafeZoneView _safeZone;

        public int AttackCount => _timelines.Length / 3;
        /// <summary>今の技の進み具合 (0..1)。</summary>
        public float Progress => _current == null ? 0f : Mathf.Clamp01(_elapsed / (float)_current.duration);
        public bool Finished => _current != null && _elapsed >= _current.duration;
        public string CurrentName => _current == null ? "" : _current.name;
        public BulletSystem Bullets => _bullets;
        public SoulController Soul => _soul;
        // エージェントの初期化（OnEpisodeBegin → Restart）がこの Awake より先に来ることがあるので、最初に使うときに探す。
        public SafeZoneView SafeZone => _safeZone != null ? _safeZone : _safeZone = _bullets.Board.GetComponentInChildren<SafeZoneView>(true);

        private void Update()
        {
            if (_current != null) _elapsed += _clock.DeltaTime;
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
            SafeZone.Hide();
            _soul.Place(soulStart);

            _director.playableAsset = _current;
            foreach (var track in _current.GetOutputTracks())
            {
                if (track is ShotTrack) _director.SetGenericBinding(track, _bullets);
                else if (track is SafeZoneTrack) _director.SetGenericBinding(track, SafeZone);
            }
            _director.Play();
        }
    }
}
