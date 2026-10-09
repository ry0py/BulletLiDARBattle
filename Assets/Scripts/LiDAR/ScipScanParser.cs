using System;
using LidarBattle.Config;
using UnityEngine;

namespace LidarBattle.LiDAR
{
    /// <summary>
    /// SCIP 2.0 の応答パース (シリアル版と Ethernet 版で共有する)。
    /// 行の読み取り手段は呼び出し側が <c>readLine</c> で渡す。Unity API は使わないので受信スレッドから呼べる。
    /// </summary>
    public static class ScipScanParser
    {
        /// <summary>
        /// GD 応答のエコー以降 (ステータス → タイムスタンプ → データ行 → 空行) を読み、有効距離の点を <paramref name="into"/> に詰める。
        /// ステータス異常なら空行まで読み捨てて false。
        /// </summary>
        public static bool ReadDistanceResponse(Func<string> readLine, LidarSettings settings, LidarScan into)
        {
            string status = readLine();
            if (status.Length < 2 || status[0] != '0' || status[1] != '0')
            {
                ReadUntilBlank(readLine);
                return false;
            }
            readLine(); // タイムスタンプ(+sum)

            into.Clear();
            int step = settings.StartStep;

            // データ行: 各行末はチェックサム 1 文字。空行で終端。
            string line;
            string carry = string.Empty; // 3 文字境界が行をまたぐ場合の繰り越し
            while (!string.IsNullOrEmpty(line = readLine()))
            {
                string data = carry + line.Substring(0, line.Length - 1); // 末尾 sum を除去
                int usable = data.Length - (data.Length % 3);
                carry = data.Substring(usable);
                for (int i = 0; i < usable; i += 3)
                {
                    float distM = Decode3(data[i], data[i + 1], data[i + 2]) * 0.001f;
                    if (distM >= settings.MinRangeM && distM <= settings.MaxRangeM)
                        into.Add(new LidarMeasurement(settings.StepToAngleRad(step), distM));
                    step++;
                }
            }
            return true;
        }

        public static void ReadUntilBlank(Func<string> readLine)
        {
            while (!string.IsNullOrEmpty(readLine())) { }
        }

        /// <summary>SCIP 2.0 の 3 文字エンコードを 18bit 距離[mm]へデコードする。</summary>
        public static int Decode3(char a, char b, char c)
            => ((a - 0x30) << 12) | ((b - 0x30) << 6) | (c - 0x30);
    }
}
