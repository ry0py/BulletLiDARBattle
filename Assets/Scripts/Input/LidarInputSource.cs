using System;
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
    /// 接続できない / UseLidar=false のときは <see cref="_fallback"/> (キーボード) に任せる。
    /// センサー接続と背景は static に持ち、シーンをまたいでも再接続・再校正しない。
    /// キー: [B] ハートを外して背景学習 / [1] 今の位置を画面左下に / [2] 今の位置を画面右上に / [F1] 状態表示。
    /// </summary>
    [DefaultExecutionOrder(-10)] // SoulController より先にスキャンを読む
    public sealed class LidarInputSource : MonoBehaviour, IHeartInputSource
    {
        [SerializeField] private LidarSettings _settings;
        [Tooltip("LiDAR が使えないときの入力源 (IHeartInputSource)")]
        [SerializeField] private MonoBehaviour _fallback;

        private static HokuyoEthernetSensor s_sensor;
        private static BackgroundSubtractionTracker s_tracker;
        private static RegionFilterTracker s_region;
        private static string s_error;
        private static int s_backgroundFramesLeft;
        private static bool s_showStatus = true;

        private IHeartInputSource _fallbackInput;
        private RectCoordinateMapper _mapper;
        private int _lastScanCount = -1;
        private bool _detected;
        private Vector2 _positionM;

        private static bool LidarActive => s_sensor != null && s_sensor.IsConnected;

        private void Awake()
        {
            _fallbackInput = _fallback as IHeartInputSource;
            if (_settings.UseLidar) EnsureConnected(_settings);
            RebuildMapper();
        }

        private static void EnsureConnected(LidarSettings settings)
        {
            if (s_sensor != null) return;

            s_sensor = new HokuyoEthernetSensor(settings);
            s_region = new RegionFilterTracker(
                new CircleFitTracker(settings.ClusterRadiusM, settings.MinClusterPoints, settings.HeartRadiusM, settings.FitIterations));
            s_tracker = new BackgroundSubtractionTracker(s_region, settings.AngularResolution, settings.BackgroundMarginM);
            Application.quitting += Shutdown;
            try { s_sensor.Connect(); }
            catch (Exception e)
            {
                s_error = e.Message;
                Debug.LogWarning($"[LidarInputSource] LiDAR に接続できないのでキーボードで操作します: {e.Message}");
            }
        }

        private static void Shutdown()
        {
            Application.quitting -= Shutdown;
            s_sensor?.Dispose();
            s_sensor = null;
            s_tracker = null;
            s_region = null;
            s_error = null;
            s_backgroundFramesLeft = 0;
        }

        private void RebuildMapper()
        {
            var mapper = new RectCoordinateMapper(_settings.PhysicalMin, _settings.PhysicalMax,
                _settings.RotationDeg, _settings.InvertX, _settings.InvertY);
            _mapper = mapper;
            // PhysicalMin/Max はハート中心の可動範囲なので、表面点が入るよう半径ぶん広げた盤面だけを見る。
            float margin = _settings.HeartRadiusM;
            if (s_region != null) s_region.Region = p => mapper.Contains(p, margin);
        }

        private void Update()
        {
            HandleKeys();
            if (!LidarActive || s_sensor.ScanCount == _lastScanCount || !s_sensor.TryGetLatestScan(out LidarScan scan)) return;
            _lastScanCount = s_sensor.ScanCount;

            if (s_backgroundFramesLeft > 0)
            {
                s_tracker.LearnBackground(scan);
                s_backgroundFramesLeft--;
                _detected = false;
                return;
            }
            _detected = s_tracker.TryTrack(scan, out _positionM);
        }

        public Vector2 ReadTarget(Vector2 currentNormalized, float deltaTime)
        {
            if (!LidarActive)
                return _fallbackInput != null ? _fallbackInput.ReadTarget(currentNormalized, deltaTime) : currentNormalized;
            return _detected ? _mapper.ToNormalized(_positionM) : currentNormalized; // 見失ったらその場で止める
        }

        private void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f1Key.wasPressedThisFrame) s_showStatus = !s_showStatus;
            if (!LidarActive) return;

            if (keyboard.bKey.wasPressedThisFrame)
            {
                s_tracker.ClearBackground();
                s_backgroundFramesLeft = _settings.BackgroundFrames;
            }
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

            string lidar = !_settings.UseLidar ? "OFF (keyboard)"
                : LidarActive ? $"connected {_settings.HostName}"
                : $"FAILED (keyboard) {s_error}";
            string background = s_backgroundFramesLeft > 0 ? "learning..."
                : s_tracker != null && s_tracker.HasBackground ? "learned" : "none";
            string heart = !LidarActive ? "-"
                : _detected ? $"aligned ({_mapper.ToAligned(_positionM).x:F3}, {_mapper.ToAligned(_positionM).y:F3}) m -> {_mapper.ToNormalized(_positionM):F2}"
                : "lost";
            GUI.Box(new Rect(10, 10, 620, 110), GUIContent.none);
            GUI.Label(new Rect(20, 15, 600, 100),
                $"LiDAR: {lidar}\nBackground: {background}\nHeart: {heart}\n[B] learn background (remove heart)  [1] bottom-left  [2] top-right  [F1] hide");
        }
    }
}
