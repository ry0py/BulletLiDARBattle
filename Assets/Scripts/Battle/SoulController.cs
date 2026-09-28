using LidarBattle.Input;
using UnityEngine;

namespace LidarBattle.Battle
{
    /// <summary>SOUL の移動・当たり判定サイズ・被弾後の無敵時間。</summary>
    public class SoulController : MonoBehaviour
    {
        [Tooltip("IHeartInputSource を実装したコンポーネント")]
        [SerializeField] private MonoBehaviour _inputSource;
        [SerializeField] private BulletBoard _board;
        [SerializeField] private BattleClock _clock;
        [SerializeField] private float _halfSize = 0.1f;
        [SerializeField] private float _hitRadius = 0.05f;
        [SerializeField] private float _grazeRadius = 0.25f;
        [SerializeField] private float _invincibleDuration = 1f;

        private IHeartInputSource _input;
        private float _invincibleTimer;

        public Vector2 Position => transform.position;
        public float HitRadius => _hitRadius;
        public float GrazeRadius => _grazeRadius;
        public bool IsInvincible => _invincibleTimer > 0f;

        public void StartInvincibility() => _invincibleTimer = _invincibleDuration;

        private void Awake()
        {
            _input = _inputSource as IHeartInputSource;
            if (_input == null) Debug.LogError($"{name}: Input Source に IHeartInputSource がありません", this);
        }

        private void Update()
        {
            float dt = _clock.DeltaTime;
            if (dt <= 0f || _input == null) return;

            _invincibleTimer -= dt;
            var target = _input.ReadTarget(_board.WorldToNormalized(Position), dt);
            transform.position = _board.Clamp(_board.NormalizedToWorld(target), _halfSize);
        }

        private void OnValidate()
        {
            if (_inputSource != null && _inputSource is not IHeartInputSource)
            {
                Debug.LogError($"{_inputSource.GetType().Name} は IHeartInputSource ではありません", this);
                _inputSource = null;
            }
        }
    }
}
