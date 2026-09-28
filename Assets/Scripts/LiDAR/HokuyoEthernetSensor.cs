using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using LidarBattle.Config;
using UnityEngine;

namespace LidarBattle.LiDAR
{
    /// <summary>
    /// Hokuyo UST 系 (UST-20LX 等) を SCIP 2.0 over TCP で駆動する実機実装。
    /// 受信は専用スレッドで GD をポーリングし、最新スキャンのみロック越しにメインスレッドへ渡す。
    /// センサーは同時に 1 接続しか受け付けない (UrgBenriPlus 等を閉じてから接続する)。
    /// </summary>
    public sealed class HokuyoEthernetSensor : ILidarSensor
    {
        private const int ConnectTimeoutMs = 2000;
        private const int IoTimeoutMs = 1000;

        private readonly LidarSettings _settings;
        private readonly object _swapLock = new object();
        private TcpClient _client;
        private StreamReader _reader;
        private NetworkStream _stream;
        private Func<string> _readLine;
        private Thread _thread;
        private volatile bool _running;
        private LidarScan _frontScan = new LidarScan(); // メインスレッドが読む
        private LidarScan _backScan = new LidarScan();   // 受信スレッドが書く
        private bool _hasScan;

        public bool IsConnected { get; private set; }

        /// <summary>受信したスキャンの通算枚数 (受信レート表示用)。</summary>
        public int ScanCount { get; private set; }

        public HokuyoEthernetSensor(LidarSettings settings)
        {
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
        }

        public void Connect()
        {
            if (IsConnected) return;

            _client = new TcpClient { NoDelay = true, ReceiveTimeout = IoTimeoutMs, SendTimeout = IoTimeoutMs };
            if (!_client.ConnectAsync(_settings.HostName, _settings.TcpPort).Wait(ConnectTimeoutMs))
            {
                _client.Close();
                _client = null;
                throw new TimeoutException($"LiDAR {_settings.HostName}:{_settings.TcpPort} に接続できません。");
            }
            _stream = _client.GetStream();
            _reader = new StreamReader(_stream, Encoding.ASCII);
            _readLine = ReadLine;

            Debug.Log("[HokuyoEthernetSensor] PP\n" + Query("PP"));
            Send("BM"); // レーザ ON
            ScipScanParser.ReadUntilBlank(_readLine);

            _running = true;
            _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "HokuyoEthernet" };
            _thread.Start();
            IsConnected = true;
        }

        public void Disconnect()
        {
            _running = false;
            if (_thread != null && _thread.IsAlive) _thread.Join(IoTimeoutMs + 500);
            _thread = null;

            if (_client != null)
            {
                try
                {
                    if (_client.Connected) Send("QT"); // レーザ OFF
                }
                catch (Exception e) { Debug.LogException(e); }
                finally
                {
                    _reader?.Dispose();
                    _client.Close();
                    _reader = null;
                    _stream = null;
                    _client = null;
                }
            }
            IsConnected = false;
        }

        public void Dispose() => Disconnect();

        public bool TryGetLatestScan(out LidarScan scan)
        {
            lock (_swapLock)
            {
                scan = _frontScan;
                return _hasScan && _frontScan.Count > 0;
            }
        }

        private void ReceiveLoop()
        {
            string gd = $"GD{_settings.StartStep:D4}{_settings.EndStep:D4}00";
            while (_running)
            {
                try
                {
                    Send(gd);
                    ReadLine(); // コマンドエコー
                    if (ScipScanParser.ReadDistanceResponse(_readLine, _settings, _backScan))
                    {
                        lock (_swapLock)
                        {
                            (_frontScan, _backScan) = (_backScan, _frontScan);
                            _hasScan = true;
                            ScanCount++;
                        }
                    }
                    Thread.Sleep(Mathf.Max(1, _settings.PollIntervalMs));
                }
                catch (Exception e)
                {
                    if (!_running) break;
                    Debug.LogWarning($"[HokuyoEthernetSensor] 受信エラー: {e.Message}");
                    Thread.Sleep(100);
                }
            }
        }

        /// <summary>コマンドを送り、応答ブロック (空行まで) を文字列で返す。接続時の情報取得用。</summary>
        private string Query(string cmd)
        {
            Send(cmd);
            var sb = new StringBuilder();
            string line;
            while (!string.IsNullOrEmpty(line = ReadLine())) sb.AppendLine(line);
            return sb.ToString();
        }

        private void Send(string cmd)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(cmd + "\n");
            _stream.Write(bytes, 0, bytes.Length);
        }

        private string ReadLine()
            => _reader.ReadLine() ?? throw new IOException("LiDAR との接続が切れました。");
    }
}
