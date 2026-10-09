using System;
using System.Collections.Generic;
using LidarBattle.Config;
using LidarBattle.LiDAR;
using LidarBattle.Mapping;
using LidarBattle.Tracking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LidarBattle.Input
{
    /// <summary>
    /// 実機 LiDAR で検出したハート位置を SOUL の目標にする入力源 (Hardware → Tracking → Mapping の合成点)。
    /// 接続できない / UseLidar=false / 見失い中は位置を出さない（どの入力源を使うかは HeartInputSelector が決める）。
    /// センサー接続は static に持ち、シーンをまたいでも再接続しない。
    /// キー: [1] 今の位置を画面左下に / [2] 今の位置を画面右上に / [F1] 状態表示。
    /// </summary>
    [DefaultExecutionOrder(-10)] // SoulController より先にスキャンを読む
    public sealed class LidarInputSource : MonoBehaviour, IHeartInputSource
    {
        [SerializeField] private LidarSettings _settings;

        private static HokuyoEthernetSensor s_sensor;
        private static LidarSettings s_connectedSettings; // 接続と検出器を作ったときの設定
        private static RegionFilterTracker s_region;
        private static SmoothedTracker s_smoothed;
        private static string s_error;
        private static bool s_showStatus; // 展示中は出さない。[F1] で表示（シーンをまたいで保持）

        /// <summary>ViewPoints に入れる範囲。盤面（正規化座標 0..1）をこれだけ広げる。</summary>
        private const float ViewMargin = 0.3f;

        private RectCoordinateMapper _mapper;
        private int _lastScanCount = -1;
        private bool _detected;
        private Vector2 _positionM; // 画面に出す位置 (スキャンの間を補間したもの)
        // 位置は 1 秒に約 34 回しか来ないので、届いた位置の間を毎フレームつなぐ (1 スキャンぶん遅れる代わりにカクつかない)。
        private Vector2 _fromM;
        private Vector2 _toM;
        private float _toTime;
        private float _scanIntervalS = 1f / 34f;
        private readonly List<Vector2> _viewPoints = new(1100);

        private static bool LidarActive => s_sensor != null && s_sensor.IsConnected;

        // 別の PC の「LiDAR の視界」（live.html）用（Flow/LiveFeed）。座標はどれも盤面の正規化座標（盤面の外は 0..1 の外）。
        public bool IsActive => LidarActive;
        /// <summary>最新のスキャンの点のうち盤面の付近のもの（背景も含む生の点）。</summary>
        public IReadOnlyList<Vector2> ViewPoints => _viewPoints;
        public bool HasHeart => LidarActive && _detected;
        public Vector2 HeartNormalized => _mapper.ToNormalized(_positionM);
        public Vector2 SensorNormalized => _mapper.ToNormalizedUnclamped(Vector2.zero);
        public float BoardAspect => _mapper.Aspect;

        private void Awake()
        {
            if (_settings.UseLidar) EnsureConnected(_settings);
            RebuildMapper();
        }

        private static void EnsureConnected(LidarSettings settings)
        {
            // 円柱用とハート用のシーンは設定アセットが違う（円の半径など）。違う設定のシーンに来たら作り直す。
            if (s_sensor != null && s_connectedSettings == settings) return;
            if (s_sensor != null) Shutdown();

            s_connectedSettings = settings;

            s_sensor = new HokuyoEthernetSensor(settings);
            s_smoothed = LidarTrackerChain.Create(settings, out s_region);
            Application.quitting += Shutdown;
            try { s_sensor.Connect(); }
            catch (Exception e)
            {
                s_error = e.Message;
                Debug.LogWarning($"[LidarInputSource] LiDAR に接続できないので LiDAR 入力は使いません: {e.Message}");
            }
        }

        /// <summary>LiDAR との接続を閉じる。次に LidarInputSource が起きたときに接続し直す（LiDAR 確認シーンへ移る前に呼ぶ）。</summary>
        public static void Shutdown()
        {
            Application.quitting -= Shutdown;
            s_sensor?.Dispose();
            s_sensor = null;
            s_connectedSettings = null;
            s_region = null;
            s_smoothed = null;
            s_error = null;
        }

        private void RebuildMapper()
        {
            var mapper = new RectCoordinateMapper(_settings.PhysicalMin, _settings.PhysicalMax,
                _settings.RotationDeg, _settings.InvertX, _settings.InvertY);
            _mapper = mapper;
            // PhysicalMin/Max はハート中心の可動範囲。少しはみ出しても拾えるよう半径ぶん広げる。
            float margin = _settings.HeartRadiusM;
            if (s_region != null) s_region.Region = p => mapper.Contains(p, margin);
        }

        private void Update()
        {
            HandleKeys();
            if (LidarActive && s_sensor.ScanCount != _lastScanCount && s_sensor.TryGetLatestScan(out LidarScan scan))
            {
                _lastScanCount = s_sensor.ScanCount;
                CollectViewPoints(scan);
                bool wasDetected = _detected;
                _detected = s_smoothed.TryTrack(scan, out Vector2 latest);
                if (_detected)
                {
                    float now = Time.unscaledTime;
                    _scanIntervalS = Mathf.Lerp(_scanIntervalS, Mathf.Clamp(now - _toTime, 0.01f, 0.1f), 0.1f);
                    _fromM = wasDetected ? _positionM : latest; // 今出している位置から次の位置へ
                    _toM = latest;
                    _toTime = now;
                }
            }
            if (_detected) _positionM = Vector2.Lerp(_fromM, _toM, (Time.unscaledTime - _toTime) / _scanIntervalS);
        }

        private void CollectViewPoints(LidarScan scan)
        {
            _viewPoints.Clear();
            for (int i = 0; i < scan.Count; i++)
            {
                Vector2 n = _mapper.ToNormalizedUnclamped(scan[i].ToCartesian());
                if (n.x >= -ViewMargin && n.x <= 1f + ViewMargin && n.y >= -ViewMargin && n.y <= 1f + ViewMargin)
                    _viewPoints.Add(n);
            }
        }

        public bool TryReadTarget(Vector2 currentNormalized, float deltaTime, out Vector2 target)
        {
            bool ok = LidarActive && _detected;
            target = ok ? _mapper.ToNormalized(_positionM) : currentNormalized;
            return ok;
        }

        private void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f1Key.wasPressedThisFrame) s_showStatus = !s_showStatus;
            if (!LidarActive) return;

            if (_detected && keyboard.digit1Key.wasPressedThisFrame) SetCorner(ref _settings.PhysicalMin, "PhysicalMin (左下)");
            if (_detected && keyboard.digit2Key.wasPressedThisFrame) SetCorner(ref _settings.PhysicalMax, "PhysicalMax (右上)");
        }

        private void SetCorner(ref Vector2 corner, string label)
        {
            corner = _mapper.ToAligned(_positionM);
            RebuildMapper();
            Debug.Log($"[LidarInputSource] {label} = ({corner.x:F3}, {corner.y:F3})");
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(_settings); // Ctrl+S (Save Project) でアセットに保存される
#endif
        }

        private void OnGUI()
        {
            if (!s_showStatus) return;

            string lidar = !_settings.UseLidar ? "OFF"
                : LidarActive ? $"connected {_settings.HostName}"
                : $"FAILED {s_error}";
            string heart = !LidarActive ? "-"
                : _detected ? $"aligned ({_mapper.ToAligned(_positionM).x:F3}, {_mapper.ToAligned(_positionM).y:F3}) m -> {_mapper.ToNormalized(_positionM):F2}"
                : "lost";
            GUI.Box(new Rect(10, 10, 620, 90), GUIContent.none);
            GUI.Label(new Rect(20, 15, 600, 80),
                $"LiDAR: {lidar}\nHeart: {heart}\n[1] bottom-left  [2] top-right  [F1] hide");
        }
    }
}
