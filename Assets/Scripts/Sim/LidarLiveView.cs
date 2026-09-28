using System;
using LidarBattle.Config;
using LidarBattle.LiDAR;
using LidarBattle.Tracking;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LidarBattle.Sim
{
    /// <summary>
    /// 実機 LiDAR の動作確認用: 点群を描き、BattleScene と同じ検出器チェーン (背景差分 + 円当てはめ) の結果をマーカーで示す。
    /// この GameObject の Transform をセンサー姿勢として使う (正面 +X をワールド上向きにするなら Z 90°)。
    /// [B] ハートを外した状態で背景を学習 / [C] 背景を消去 / マウスホイールでズーム。
    /// </summary>
    public sealed class LidarLiveView : MonoBehaviour
    {
        [SerializeField] private LidarSettings _settings;
        [SerializeField] private PointCloudView _view;
        [SerializeField] private SpriteRenderer _marker;
        [SerializeField] private TextMeshProUGUI _label;

        private HokuyoEthernetSensor _sensor;
        private BackgroundSubtractionTracker _tracker;
        private string _error;
        private int _backgroundFramesLeft;
        private int _lastScanCount;
        private int _rateScanCount;
        private float _rateTime;
        private float _hz;

        private void Awake()
        {
            _sensor = new HokuyoEthernetSensor(_settings);
            _tracker = new BackgroundSubtractionTracker(
                new CircleFitTracker(_settings.ClusterRadiusM, _settings.MinClusterPoints, _settings.HeartRadiusM, _settings.FitIterations),
                _settings.AngularResolution, _settings.BackgroundMarginM);
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

                if (_backgroundFramesLeft > 0)
                {
                    _tracker.LearnBackground(scan);
                    _backgroundFramesLeft--;
                    _marker.enabled = false;
                }
                else if (_tracker.TryTrack(scan, out Vector2 positionM))
                {
                    _marker.enabled = true;
                    _marker.transform.position = transform.TransformPoint(positionM);
                    UpdateLabel(scan.Count, positionM, true);
                    return;
                }
                else
                {
                    _marker.enabled = false;
                }
                UpdateLabel(scan.Count, Vector2.zero, false);
            }
            else if (_error != null || !_sensor.IsConnected)
            {
                UpdateLabel(0, Vector2.zero, false);
            }
        }

        private void HandleInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.bKey.wasPressedThisFrame)
                {
                    _tracker.ClearBackground();
                    _backgroundFramesLeft = _settings.BackgroundFrames;
                }
                if (keyboard.cKey.wasPressedThisFrame) _tracker.ClearBackground();
            }

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
            string background = _backgroundFramesLeft > 0 ? "学習中..." : _tracker.HasBackground ? "学習済み" : "なし (最も近い物体を検出)";
            string heart = detected ? $"x {positionM.x * 1000f:F0} mm, y {positionM.y * 1000f:F0} mm (距離 {positionM.magnitude * 1000f:F0} mm)" : "未検出";
            _label.text = $"{status}\n背景: {background}\nハート: {heart}\n[B] ハートを外して背景学習  [C] 背景消去  ホイール: ズーム";
        }
    }
}
