using System;
using LidarBattle.Config;
using UnityEngine;

// 実機シリアル通信は System.IO.Ports に依存する。既定ビルドを壊さないため、
// ハードウェア利用時のみ Scripting Define Symbol "URG_SERIAL_ENABLED" を有効化する。
// (Project Settings > Player > Scripting Define Symbols。API 互換性は .NET Framework 推奨)
#if URG_SERIAL_ENABLED
using System.IO.Ports;
using System.Threading;
#endif

namespace LidarBattle.LiDAR
{
    /// <summary>
    /// Hokuyo URG 系 2D LiDAR を SCIP 2.0 (Serial/USB 仮想COM) で駆動する実機実装。
    /// 受信は専用スレッドで行い、最新スキャンのみロック越しにメインスレッドへ渡す
    /// (Unity API をスレッド外で触らない)。SCIP/デコードの詳細は docs/lidar-integration.md。
    /// </summary>
    public sealed class HokuyoUrgSensor : ILidarSensor
    {
        private readonly LidarSettings _settings;

        public bool IsConnected { get; private set; }

        public HokuyoUrgSensor(LidarSettings settings)
        {
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
        }

#if URG_SERIAL_ENABLED
        private SerialPort _port;
        private Thread _thread;
        private volatile bool _running;
        private readonly object _swapLock = new object();
        private LidarScan _frontScan = new LidarScan(); // メインスレッドが読む
        private LidarScan _backScan = new LidarScan();   // 受信スレッドが書く
        private bool _hasScan;

        public void Connect()
        {
            if (IsConnected) return;

            _port = new SerialPort(_settings.PortName, _settings.BaudRate)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000,
                NewLine = "\n"
            };
            _port.Open();

            SendCommand("SCIP2.0"); // SCIP2 モードへ
            SendCommand("BM");      // レーザ ON

            _running = true;
            _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "HokuyoUrg" };
            _thread.Start();
            IsConnected = true;
        }

        public void Disconnect()
        {
            _running = false;
            if (_thread != null && _thread.IsAlive) _thread.Join(500);
            _thread = null;

            if (_port != null)
            {
                try
                {
                    if (_port.IsOpen)
                    {
                        _port.Write("QT\n"); // レーザ OFF
                        _port.Close();
                    }
                }
                catch (Exception e) { Debug.LogException(e); }
                finally { _port.Dispose(); _port = null; }
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

        /// <summary>受信スレッド本体。GD ポーリングでスキャンを取得し続ける。</summary>
        private void ReceiveLoop()
        {
            string gd = $"GD{_settings.StartStep:D4}{_settings.EndStep:D4}00";
            while (_running)
            {
                try
                {
                    RequestAndParse(gd);
                    Thread.Sleep(Mathf.Max(1, _settings.PollIntervalMs));
                }
                catch (TimeoutException) { /* 一時的な無応答は無視して継続 */ }
                catch (Exception e) { Debug.LogException(e); Thread.Sleep(100); }
            }
        }

        private void RequestAndParse(string gd)
        {
            _port.Write(gd + "\n");
            _port.ReadLine(); // コマンドエコー
            if (!ScipScanParser.ReadDistanceResponse(_port.ReadLine, _settings, _backScan)) return;

            lock (_swapLock)
            {
                (_frontScan, _backScan) = (_backScan, _frontScan);
                _hasScan = true;
            }
        }

        private void SendCommand(string cmd)
        {
            _port.Write(cmd + "\n");
            ScipScanParser.ReadUntilBlank(_port.ReadLine); // エコー + ステータス + 空行
        }
#else
        // URG_SERIAL_ENABLED 未定義時のスタブ。既定ビルドのコンパイルを保証する。
        // 開発時は MockLidarSensor を使用すること。
        public void Connect()
            => throw new NotSupportedException(
                "実機シリアルは無効です。Scripting Define Symbol 'URG_SERIAL_ENABLED' を有効化し、" +
                "API 互換性を .NET Framework に設定してください (docs/lidar-integration.md)。");

        public void Disconnect() { }
        public void Dispose() { }

        public bool TryGetLatestScan(out LidarScan scan)
        {
            scan = null;
            return false;
        }
#endif
    }
}
