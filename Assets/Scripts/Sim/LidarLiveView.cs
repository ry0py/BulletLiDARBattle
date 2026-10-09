using System;
using LidarBattle.Config;
using LidarBattle.LiDAR;
using LidarBattle.Mapping;
using LidarBattle.Tracking;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LidarBattle.Sim
{
    /// <summary>
    /// 実機 LiDAR の動作確認用: 点群を描き、BattleScene と同じ検出器 (LidarTrackerChain: 円柱の検出・盤面の外の除外・平滑化) の結果をマーカーで示す。
    /// この GameObject の Transform をセンサー姿勢として使う (正面 +X をワールド上向きにするなら Z 90°)。
    /// マウスホイールでズーム。
    /// </summary>
    public sealed class LidarLiveView : MonoBehaviour
    {
        [SerializeField] private LidarSettings _settings;
        [SerializeField] private PointCloudView _view;
        [SerializeField] private SpriteRenderer _marker;
        [SerializeField] private TextMeshProUGUI _label;

        private HokuyoEthernetSensor _sensor;
        private SmoothedTracker _tracker;
        private string _error;
        private int _lastScanCount;
        private int _rateScanCount;
        private float _rateTime;
        private float _hz;

        private void Awake()
        {
            _sensor = new HokuyoEthernetSensor(_settings);
            _tracker = LidarTrackerChain.Create(_settings, out RegionFilterTracker region);
            var mapper = new RectCoordinateMapper(_settings.PhysicalMin, _settings.PhysicalMax,
                _settings.RotationDeg, _settings.InvertX, _settings.InvertY);
            float margin = _settings.HeartRadiusM; // LidarInputSource と同じく盤面を半径ぶん広げる
            region.Region = p => mapper.Contains(p, margin);
        }

        private void Start()
        {
            try { _sensor.Connect(); }
            catch (Exception e)
            {
                _error = e.Message;
                Debug.LogException(e);
            }
        }

        private void OnDestroy() => _sensor?.Dispose();

        private void Update()
        {
            HandleInput();
            UpdateRate();

            if (_sensor.ScanCount != _lastScanCount && _sensor.TryGetLatestScan(out LidarScan scan))
            {
                _lastScanCount = _sensor.ScanCount;
                _view.Show(scan, transform);

                bool detected = _tracker.TryTrack(scan, out Vector2 positionM);
                _marker.enabled = detected;
                if (detected) _marker.transform.position = transform.TransformPoint(positionM);
                UpdateLabel(scan.Count, positionM, detected);
            }
            else if (_error != null || !_sensor.IsConnected)
            {
                UpdateLabel(0, Vector2.zero, false);
            }
        }

        private void HandleInput()
        {
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse != null && cam != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f) cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * (scroll > 0f ? 0.9f : 1.1f), 0.2f, 20f);
            }
        }

        private void UpdateRate()
        {
            if (Time.unscaledTime - _rateTime < 1f) return;
            _hz = (_sensor.ScanCount - _rateScanCount) / (Time.unscaledTime - _rateTime);
            _rateScanCount = _sensor.ScanCount;
            _rateTime = Time.unscaledTime;
        }

        private void UpdateLabel(int points, Vector2 positionM, bool detected)
        {
            if (_label == null) return;
            string status = _error != null ? $"<color=red>接続失敗: {_error}</color>"
                : _sensor.IsConnected ? $"接続中 {_settings.HostName} | {_hz:F1} Hz | {points} 点"
                : "未接続";
            string heart = detected ? $"x {positionM.x * 1000f:F0} mm, y {positionM.y * 1000f:F0} mm (距離 {positionM.magnitude * 1000f:F0} mm)" : "未検出";
            _label.text = $"{status}\nハート: {heart}\n<color=#00FFFF>水色の線</color> = センサー正面 / 薄い線 = 取得範囲の端\nホイール: ズーム";
        }
    }
}
