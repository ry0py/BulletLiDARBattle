using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LidarBattle.Flow
{
    /// <summary>
    /// Unity の外で動かす Python のツール（リポジトリの Tools/ 以下）を起動・停止する。展示の運用で使う。
    /// 起動したものはゲームの終了と一緒に止める。コンソール窓が出るので、窓を閉じても止まる。
    /// </summary>
    public sealed class ToolProcess
    {
        public static readonly ToolProcess ScoreBoard = new("ScoreBoard/serve.py", port: 8000);
        public static readonly ToolProcess CameraTracker = new("CameraTracker/aruco_tracker.py", port: null);

        private readonly string _script;
        private readonly int? _port;
        private Process _process;

        static ToolProcess() => Application.quitting += () =>
        {
            ScoreBoard.Stop();
            CameraTracker.Stop();
        };

        private ToolProcess(string script, int? port)
        {
            _script = script;
            _port = port;
        }

        /// <summary>このゲームが起動して、まだ動いているか。</summary>
        public bool IsRunning => _process != null && !_process.HasExited;

        /// <summary>ポートで受け付けているか（サーバーが使えるようになったか）。ポートを持たないツールは false。</summary>
        public bool IsListening => _port is int port && PortInUse(port);

        /// <summary>このゲームとは別に起動済みか（ポートがもう使われている）。</summary>
        public bool IsRunningElsewhere => !IsRunning && IsListening;

        /// <summary>最後に起動できなかった理由。無ければ null。</summary>
        public string Error { get; private set; }

        /// <summary>起動する。もう動いている（このゲームか別で起動済み）なら何もしない。</summary>
        public void Start()
        {
            if (IsRunning || IsRunningElsewhere) return;
            Error = null;
            string path = FindScript();
            if (path == null)
            {
                Error = $"Tools/{_script} が見つからない";
                return;
            }
            try
            {
                // UseShellExecute=false でもコンソール窓は出る（CreateNoWindow=false）。ログが見えて、閉じれば止まる。
                _process = Process.Start(new ProcessStartInfo("python", $"\"{path}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    WorkingDirectory = Path.GetDirectoryName(path),
                });
            }
            catch (Exception e)
            {
                Error = "python を起動できない";
                Debug.LogWarning($"[ToolProcess] {_script}: {e.Message}");
            }
        }

        /// <summary>このゲームが起動したものを止める（別に起動したものは止めない）。</summary>
        public void Stop()
        {
            if (!IsRunning) return;
            try { _process.Kill(); } catch (InvalidOperationException) { /* もう止まっている */ }
            _process = null;
        }

        /// <summary>エディタは Assets の 1 つ上、ビルド（Build/〇〇_Data）は 2 つ上がリポジトリ。上へ順に探す。</summary>
        private string FindScript()
        {
            var dir = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 4 && dir != null; i++, dir = dir.Parent)
            {
                string path = Path.Combine(dir.FullName, "Tools", _script);
                if (File.Exists(path)) return path;
            }
            return null;
        }

        private static bool PortInUse(int port)
        {
            try
            {
                using var client = new TcpClient();
                return client.ConnectAsync("127.0.0.1", port).Wait(100) && client.Connected;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
