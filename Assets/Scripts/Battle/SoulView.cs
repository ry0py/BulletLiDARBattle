using UnityEngine;

namespace LidarBattle.Battle
{
    /// <summary>
    /// SOUL の見た目: グレイズ範囲を示す輪の脈動、無敵中の点滅、グレイズしたときの輪のフラッシュ。
    /// 見た目だけを担当し、ゲームの状態は持たない。
    /// </summary>
    public class SoulView : MonoBehaviour
    {
        [SerializeField] private SoulController _soul;
        [Tooltip("弾の無いシーン（選択画面）では未設定でよい")]
        [SerializeField] private BulletSystem _bullets;
        [Tooltip("グレイズ範囲の輪")]
        [SerializeField] private SpriteRenderer _ring;
        [Tooltip("無敵中に点滅させるレンダラー")]
        [SerializeField] private SpriteRenderer[] _blinkTargets;
        [SerializeField] private float _pulseSeconds = 1.6f;
        [SerializeField, Range(0f, 1f)] private float _ringAlpha = 0.35f;
        [SerializeField] private float _grazeFlashSeconds = 0.25f;
        [SerializeField] private float _blinkHz = 10f;

        private float _grazeTimer;
        private Vector3 _ringScale;

        private void Awake() => _ringScale = _ring.transform.localScale;

        private void OnEnable()
        {
            if (_bullets != null) _bullets.Grazed += OnGrazed;
        }

        private void OnDisable()
        {
            if (_bullets != null) _bullets.Grazed -= OnGrazed;
        }

        private void OnGrazed(Bullet _) => _grazeTimer = _grazeFlashSeconds;

        private void Update()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time / _pulseSeconds * Mathf.PI * 2f);
            _grazeTimer -= Time.deltaTime;
            float flash = Mathf.Clamp01(_grazeTimer / _grazeFlashSeconds);

            var color = _ring.color;
            color.a = Mathf.Lerp(_ringAlpha * (0.6f + 0.4f * pulse), 1f, flash);
            _ring.color = color;
            _ring.transform.localScale = _ringScale * (1f + 0.04f * pulse + 0.15f * flash);

            bool visible = !_soul.IsInvincible || Mathf.FloorToInt(Time.time * _blinkHz) % 2 == 0;
            foreach (var r in _blinkTargets) r.enabled = visible;
        }
    }
}
