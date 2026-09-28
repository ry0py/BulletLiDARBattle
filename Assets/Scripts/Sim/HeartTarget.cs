using UnityEngine;
using UnityEngine.InputSystem;

namespace UndertaleLiDAR.Sim
{
    /// <summary>
    /// 真値となるハート。マウス追従か Lissajous 自動移動で範囲内を動く。
    /// コライダ (ハート形状＋手) は Editor 拡張が付与し、ここは位置だけを扱う。
    /// </summary>
    public sealed class HeartTarget : MonoBehaviour
    {
        public enum MoveMode { Mouse, Auto }

        [SerializeField] private MoveMode _mode = MoveMode.Mouse;
        [Tooltip("移動範囲 [m, ワールド] の最小")] [SerializeField] private Vector2 _areaMin = new Vector2(-0.4f, 0.2f);
        [Tooltip("移動範囲 [m, ワールド] の最大")] [SerializeField] private Vector2 _areaMax = new Vector2(0.4f, 0.95f);
        [Tooltip("自動移動の角速度 [rad/s]")] [SerializeField] private float _autoSpeed = 0.8f;

        private Camera _camera;
        private float _phase;

        public MoveMode Mode
        {
            get => _mode;
            set => _mode = value;
        }

        /// <summary>真値 (ハート中心のワールド座標 [m])。</summary>
        public Vector2 TruePosition => transform.position;

        private void Awake() => _camera = Camera.main;

        private void Update()
        {
            Vector2 target = TruePosition;
            if (_mode == MoveMode.Mouse)
            {
                if (Mouse.current != null && _camera != null)
                    target = _camera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            }
            else
            {
                _phase += Time.deltaTime * _autoSpeed;
                Vector2 center = (_areaMin + _areaMax) * 0.5f;
                Vector2 amplitude = (_areaMax - _areaMin) * 0.45f;
                target = center + new Vector2(amplitude.x * Mathf.Sin(_phase), amplitude.y * Mathf.Sin(2f * _phase + 1f));
            }

            transform.position = new Vector3(
                Mathf.Clamp(target.x, _areaMin.x, _areaMax.x),
                Mathf.Clamp(target.y, _areaMin.y, _areaMax.y),
                0f);
        }
    }
}
