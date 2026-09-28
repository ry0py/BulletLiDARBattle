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

        private float _timer;

        public Difficulty Difficulty => _difficulty;
        public bool IsSelected => _timer >= _holdSeconds;

        private void Update()
        {
            var offset = _soul.Position - (Vector2)transform.position;
            bool inside = Mathf.Abs(offset.x) <= _size.x * 0.5f && Mathf.Abs(offset.y) <= _size.y * 0.5f;
            _timer = inside ? _timer + Time.deltaTime : 0f;

            if (_gauge != null)
            {
                var scale = _gauge.localScale;
                scale.x = Mathf.Clamp01(_timer / _holdSeconds);
                _gauge.localScale = scale;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, _size);
        }
    }
}
