using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace LidarBattle.Flow
{
    /// <summary>
    /// 1 プレイの結果を保存する。会場のネットに頼らないよう、ローカルのファイルだけで完結させる。
    /// - plays.jsonl: 1 行 1 プレイの要約。スコアボード（Tools/ScoreBoard）が数秒ごとに読むので小さく保つ。
    /// - replays/&lt;id&gt;.json: 被弾の詳細と SOUL の軌跡（再生用）。
    /// </summary>
    public static class PlayLog
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string SummaryPath => Path.Combine(Application.persistentDataPath, "plays.jsonl");
        public static string ReplayFolder => Path.Combine(Application.persistentDataPath, "replays");

        public static void Save(PlayRecorder r)
        {
            string id = r.StartedAt.ToString("yyyyMMdd-HHmmss", Inv);
            string head = $"\"id\":\"{id}\",\"time\":\"{r.StartedAt.ToString("yyyy-MM-ddTHH:mm:ss", Inv)}\","
                        + $"\"difficulty\":\"{r.Difficulty}\",\"timeline\":\"{r.Timeline}\",\"hits\":{r.Hits.Count},"
                        + $"\"debug\":{(r.DebugMode ? "true" : "false")}";
            try
            {
                // 要約より先に詳細を書く（要約が見えた時点で詳細もある）。
                Directory.CreateDirectory(ReplayFolder);
                File.WriteAllText(Path.Combine(ReplayFolder, id + ".json"), ReplayJson(head, r));
                File.AppendAllText(SummaryPath, "{" + head + "}\n");
            }
            catch (Exception e)
            {
                // 書けなくてもゲームは止めない。
                Debug.LogWarning($"[PlayLog] 記録に失敗: {Application.persistentDataPath}\n{e.Message}");
            }
        }

        private static string ReplayJson(string head, PlayRecorder r)
        {
            var sb = new StringBuilder("{").Append(head)
                .Append(",\"board\":[").Append(F(r.Board.x)).Append(',').Append(F(r.Board.y)).Append(',')
                .Append(F(r.Board.width)).Append(',').Append(F(r.Board.height)).Append(']')
                .Append(",\"hitEvents\":[");
            for (int i = 0; i < r.Hits.Count; i++)
            {
                var h = r.Hits[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"t\":").Append(F(h.Time))
                  .Append(",\"x\":").Append(F(h.Position.x))
                  .Append(",\"y\":").Append(F(h.Position.y))
                  .Append(",\"bullet\":\"").Append(h.BulletType)
                  .Append("\",\"pattern\":\"").Append(h.Pattern).Append("\"}");
            }
            sb.Append("],\"pathInterval\":").Append(PlayRecorder.PathInterval.ToString(Inv)).Append(",\"path\":[");
            for (int i = 0; i < r.Path.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('[').Append(F(r.Path[i].x)).Append(',').Append(F(r.Path[i].y)).Append(']');
            }
            return sb.Append("]}\n").ToString();
        }

        private static string F(float v) => v.ToString("0.00", Inv);
    }
}
