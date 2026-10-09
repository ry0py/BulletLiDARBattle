using System;
using System.IO;
using System.Text;
using LidarBattle.Battle;
using LidarBattle.Input;
using UnityEngine;

namespace LidarBattle.Flow
{
    /// <summary>
    /// プレイ中の様子（LiDAR の点群・検出したハート・SOUL・残り時間）を live.json に書く。
    /// スコアボードのサーバーが配り、別の PC の live.html（「LiDAR の視界」）がこれが新しい間だけ点群を出す。
    /// 座標は盤面の正規化座標を 1000 倍した整数（文字列を作らずに書くため）。
    /// </summary>
    public sealed class LiveFeed
    {
        private const float Interval = 0.1f; // 書く間隔（秒、実時間）

        private static string FilePath => Path.Combine(Application.persistentDataPath, "live.json");

        private readonly LidarInputSource _lidar; // 無ければ点群なし
        private readonly SoulController _soul;
        private readonly Difficulty _difficulty;
        private readonly float _boardAspect; // LiDAR が無いときの盤面の横 / 縦
        private readonly StringBuilder _sb = new(16 * 1024);
        private float _nextTime;

        public LiveFeed(LidarInputSource lidar, SoulController soul, Difficulty difficulty, float boardAspect)
        {
            _lidar = lidar;
            _soul = soul;
            _difficulty = difficulty;
            _boardAspect = boardAspect;
        }

        /// <summary>毎フレーム呼ぶ。Interval ごとに書く。</summary>
        public void Tick(float remainingSeconds)
        {
            if (Time.unscaledTime < _nextTime) return;
            _nextTime = Time.unscaledTime + Interval;

            bool lidar = _lidar != null && _lidar.IsActive;
            _sb.Clear().Append("{\"state\":\"playing\",\"difficulty\":\"").Append(DifficultyName(_difficulty))
               .Append("\",\"remaining\":");
            AppendInt(_sb, Mathf.CeilToInt(Mathf.Max(0f, remainingSeconds)));
            _sb.Append(",\"aspect\":");
            AppendInt(_sb, Mathf.RoundToInt((lidar ? _lidar.BoardAspect : _boardAspect) * 1000f));
            _sb.Append(",\"lidar\":").Append(lidar ? "true" : "false");
            _sb.Append(",\"soul\":");
            AppendPoint(_sb, _soul.Normalized);
            if (lidar)
            {
                _sb.Append(",\"sensor\":");
                AppendPoint(_sb, _lidar.SensorNormalized);
                if (_lidar.HasHeart)
                {
                    _sb.Append(",\"heart\":");
                    AppendPoint(_sb, _lidar.HeartNormalized);
                }
                _sb.Append(",\"points\":[");
                var points = _lidar.ViewPoints;
                for (int i = 0; i < points.Count; i++)
                {
                    if (i > 0) _sb.Append(',');
                    AppendInt(_sb, Mathf.RoundToInt(points[i].x * 1000f));
                    _sb.Append(',');
                    AppendInt(_sb, Mathf.RoundToInt(points[i].y * 1000f));
                }
                _sb.Append(']');
            }
            Write(_sb.Append('}').ToString());
        }

        /// <summary>プレイが終わったら呼ぶ。live.html がすぐ待機の表示に戻る（呼ばれなくても数秒で戻る）。</summary>
        public static void WriteIdle() => Write("{\"state\":\"idle\"}");

        private static void Write(string json)
        {
            // 書きかけを読まれないよう、別のファイルに書いてから置き換える。
            // スコアボードのサーバーが読んでいる最中だと置き換えに失敗するが、次の回に書けばよい。
            string tmp = FilePath + ".tmp";
            try
            {
                File.WriteAllText(tmp, json);
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static string DifficultyName(Difficulty d) => d switch
        {
            Difficulty.Easy => "Easy",
            Difficulty.Medium => "Medium",
            _ => "Hard",
        };

        private static void AppendPoint(StringBuilder sb, Vector2 p)
        {
            sb.Append('[');
            AppendInt(sb, Mathf.RoundToInt(p.x * 1000f));
            sb.Append(',');
            AppendInt(sb, Mathf.RoundToInt(p.y * 1000f));
            sb.Append(']');
        }

        /// <summary>int.ToString を使わずに書く（毎回の文字列のアロケーションを避ける）。</summary>
        private static void AppendInt(StringBuilder sb, int v)
        {
            if (v < 0) { sb.Append('-'); v = -v; }
            int div = 1;
            while (v / div >= 10) div *= 10;
            for (; div > 0; div /= 10) sb.Append((char)('0' + v / div % 10));
        }
    }
}
