using LidarBattle.Audio;
using LidarBattle.Battle;
using UnityEngine;

namespace LidarBattle.Flow
{
    /// <summary>難易度の選択肢。SOUL が枠内に holdSeconds 居続けたら選択済みになる。外れたらリセット。</summary>
    public class DifficultyOption : MonoBehaviour
    {
        [SerializeField] private Difficulty _difficulty;
        [SerializeField] private SoulController _soul;
        [SerializeField] private Vector2 _size = new(2f, 1.2f);
        [SerializeField] private float _holdSeconds = 2f;
        [Tooltip("進み具合に合わせて横方向に伸ばすゲージ（pivot を左端にしておく）")]
        [SerializeField] private Transform _gauge;
        [Tooltip("選択中の音を鳴らす間隔（秒）")]
        [SerializeField] private float _tickInterval = 0.2f;
        [Tooltip("ゲージが満ちたときの選択中の音のピッチ（始まりは 1）")]
        [SerializeField] private float _tickMaxPitch = 2f;

        private float _timer;
        private float _nextTick;

        public Difficulty Difficulty => _difficulty;
        public bool IsSelected => _timer >= _holdSeconds;

        private void Update()
        {
            var offset = _soul.Position - (Vector2)transform.position;
            bool inside = Mathf.Abs(offset.x) <= _size.x * 0.5f && Mathf.Abs(offset.y) <= _size.y * 0.5f;
            _timer = inside ? _timer + Time.deltaTime : 0f;
            UpdateTick(inside);

            if (_gauge != null)
            {
                var scale = _gauge.localScale;
                scale.x = Mathf.Clamp01(_timer / _holdSeconds);
                _gauge.localScale = scale;
            }
        }

        /// <summary>乗っている間、ゲージが溜まるほど高い音で「チッ、チッ」と鳴らす。</summary>
        private void UpdateTick(bool inside)
        {
            if (!inside)
            {
                _nextTick = 0f;
                return;
            }
            if (IsSelected || _timer < _nextTick) return;
            _nextTick += _tickInterval;
            GameAudio.PlaySelectTick(Mathf.Lerp(1f, _tickMaxPitch, _timer / _holdSeconds));
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, _size);
        }
    }
}
