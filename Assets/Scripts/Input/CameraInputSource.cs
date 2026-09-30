using System;
using System.Net;
using System.Net.Sockets;
using LidarBattle.Mapping;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LidarBattle.Input
{
    /// <summary>
    /// カメラで検出した ArUco マーカー (ハートに貼る) の位置を SOUL の目標にする入力源。
    /// 検出は Unity の外 (Tools/CameraTracker/aruco_tracker.py) で行い、ここは結果を UDP で受け取るだけ。
    /// パケットは float32 x2 = 画像内の位置 (0..1、x 右向き・y 下向き)。見失っている間は NaN が届く。
    /// トラッカーが動いていない (パケットが来ない) ときは <see cref="_fallback"/> に任せる。[F1] 状態表示。
    /// ソケットは static に持つ。シーン切替では新シーンの Awake が旧シーンの破棄より先に走り、同じポートを開き直せないため。
    /// </summary>
    [DefaultExecutionOrder(-10)] // SoulController より先に受信する
    public sealed class CameraInputSource : MonoBehaviour, IHeartInputSource
    {
        [Tooltip("aruco_tracker.py の --port と合わせる")]
        [SerializeField] private int _port = 5005;
        [Tooltip("盤面の左下に対応する画像内の位置 (0..1、x 右向き・y 下向き)")]
        [SerializeField] private Vector2 _imageBottomLeft = new Vector2(0f, 1f);
        [Tooltip("盤面の右上に対応する画像内の位置 (0..1、x 右向き・y 下向き)")]
        [SerializeField] private Vector2 _imageTopRight = new Vector2(1f, 0f);
        [Tooltip("この秒数パケットが来なければトラッカー停止とみなす")]
        [SerializeField] private float _timeoutSec = 1f;
        [Tooltip("トラッカーが動いていないときの入力源 (IHeartInputSource)")]
        [SerializeField] private MonoBehaviour _fallback;

        private static Socket s_socket;

        private readonly byte[] _buffer = new byte[8];
        private IHeartInputSource _fallbackInput;
        private RectCoordinateMapper _mapper;
        private float _lastPacketTime = float.NegativeInfinity;
        private bool _detected;
        private Vector2 _imagePosition;
        private bool _showStatus = true;

        private bool TrackerActive => Time.unscaledTime - _lastPacketTime < _timeoutSec;

        private void Awake()
        {
            _fallbackInput = _fallback as IHeartInputSource;
            RebuildMapper();
            EnsureOpen(_port);
        }

        private static void EnsureOpen(int port)
        {
            if (s_socket != null) return;

            try
            {
                s_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { Blocking = false };
                s_socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
                Application.quitting += Close;
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"[CameraInputSource] UDP ポート {port} を開けないのでカメラ入力は使いません: {e.Message}");
                s_socket?.Dispose();
                s_socket = null;
            }
        }

        private static void Close()
        {
            Application.quitting -= Close;
            s_socket?.Dispose();
            s_socket = null;
        }

        private void OnValidate() => RebuildMapper(); // 実行中に Inspector で範囲を調整できるようにする

        // 画像の y は下向きなので、左下 > 右上 (負のサイズ) を渡してマッパーに反転させる。
        private void RebuildMapper()
            => _mapper = new RectCoordinateMapper(_imageBottomLeft, _imageTopRight, 0f, false, false);

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) _showStatus = !_showStatus;
            if (s_socket == null) return;

            while (s_socket.Available > 0) // 溜まった分を読み切って最新だけ使う
            {
                if (s_socket.Receive(_buffer) != _buffer.Length) continue;
                float x = BitConverter.ToSingle(_buffer, 0);
                float y = BitConverter.ToSingle(_buffer, 4);
                _lastPacketTime = Time.unscaledTime;
                _detected = !float.IsNaN(x) && !float.IsNaN(y);
                if (_detected) _imagePosition = new Vector2(x, y);
            }
        }

        public Vector2 ReadTarget(Vector2 currentNormalized, float deltaTime)
        {
            if (!TrackerActive)
                return _fallbackInput != null ? _fallbackInput.ReadTarget(currentNormalized, deltaTime) : currentNormalized;
            return _detected ? _mapper.ToNormalized(_imagePosition) : currentNormalized; // 見失ったらその場で止める
        }

        private void OnGUI()
        {
            if (!_showStatus) return;

            string state = s_socket == null ? $"port {_port} unavailable (fallback)"
                : !TrackerActive ? "tracker not running (fallback)"
                : _detected ? $"marker ({_imagePosition.x:F2}, {_imagePosition.y:F2}) -> {_mapper.ToNormalized(_imagePosition):F2}"
                : "marker lost";
            GUI.Box(new Rect(10, 125, 620, 30), GUIContent.none);
            GUI.Label(new Rect(20, 130, 600, 20), $"Camera: {state}");
        }
    }
}
