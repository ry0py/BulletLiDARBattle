using UnityEngine;
using UnityEngine.UI;

namespace UndertaleLiDAR.Battle
{
    /// <summary>
    /// 被弾したと分かるように、カメラを短く揺らし、画面全体を赤くフラッシュする。
    /// 見た目だけを担当し、ゲームの状態は持たない。BulletSystem.Hit を購読する。
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        [SerializeField] private BulletSystem _bullets;
        [SerializeField] private Transform _camera;
        [Tooltip("画面全体を覆う Image。色は _flashColor で上書きするので何色でもよい")]
        [SerializeField] private Image _overlay;
        [SerializeField] private float _shakeSeconds = 0.25f;
        [Tooltip("揺れ幅（ワールド単位）。時間とともに小さくなる")]
        [SerializeField] private float _shakeAmplitude = 0.15f;
        [Tooltip("フラッシュの色。a が最初の濃さで、そこから 0 へ薄くなる")]
        [SerializeField] private Color _flashColor = new(1f, 0f, 0f, 0.35f);
        [SerializeField] private float _flashSeconds = 0.3f;

        private Vector3 _cameraOrigin;
        private float _shakeTimer;
        private float _flashTimer;

        private void Awake()
        {
            _cameraOrigin = _camera.position;
            SetOverlayAlpha(0f);
        }

        private void OnEnable() => _bullets.Hit += OnHit;
        private void OnDisable() => _bullets.Hit -= OnHit;

        private void OnHit(Bullet _)
        {
            _shakeTimer = _shakeSeconds;
            _flashTimer = _flashSeconds;
        }

        private void Update()
        {
            if (_shakeTimer > 0f)
            {
                _shakeTimer -= Time.deltaTime;
                // 残り時間に比例して弱める。0 になったフレームで元の位置に戻る。
                float strength = _shakeAmplitude * Mathf.Clamp01(_shakeTimer / _shakeSeconds);
                _camera.position = _cameraOrigin + (Vector3)(Random.insideUnitCircle * strength);
            }

            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                SetOverlayAlpha(_flashColor.a * Mathf.Clamp01(_flashTimer / _flashSeconds));
            }
        }

        private void SetOverlayAlpha(float alpha)
        {
            var color = _flashColor;
            color.a = alpha;
            _overlay.color = color;
            _overlay.enabled = alpha > 0f; // 透明なときは描画しない
        }
    }
}
