using System.Collections.Generic;
using LidarBattle.LiDAR;
using UnityEngine;

namespace LidarBattle.Tracking
{
    /// <summary>角度順の連続点をまとめた 1 区間 (物体候補)。</summary>
    public readonly struct ScanSegment
    {
        public readonly int Start;
        public readonly int Count;
        public readonly Vector2 Centroid;
        /// <summary>センサーから区間内で最も近い点までの距離 [m]。</summary>
        public readonly float MinDistanceM;
        /// <summary>区間の両端点間の距離 [m] (物体の見かけの幅)。</summary>
        public readonly float ExtentM;

        public ScanSegment(int start, int count, Vector2 centroid, float minDistanceM, float extentM)
        {
            Start = start;
            Count = count;
            Centroid = centroid;
            MinDistanceM = minDistanceM;
            ExtentM = extentM;
        }
    }

    /// <summary>
    /// 角度順に並んだスキャンを、隣接点の距離が閾値を超える所で区切る (ブレークポイント法)。
    /// 複数の検出器で共有する唯一の分割ロジック (DRY)。GC を避けるため結果リストは呼び出し側が再利用する。
    /// </summary>
    public static class ScanSegmenter
    {
        public static void Segment(LidarScan scan, float breakDistM, int minPoints, List<ScanSegment> result)
        {
            result.Clear();
            if (scan == null || scan.Count == 0) return;

            float breakSqr = breakDistM * breakDistM;
            int start = 0;
            Vector2 first = scan[0].ToCartesian();
            Vector2 prev = first;
            Vector2 sum = first;
            float minDist = scan[0].DistanceM;

            for (int i = 1; i <= scan.Count; i++)
            {
                bool continues = false;
                Vector2 p = default;
                if (i < scan.Count)
                {
                    p = scan[i].ToCartesian();
                    continues = (p - prev).sqrMagnitude <= breakSqr;
                }

                if (continues)
                {
                    sum += p;
                    prev = p;
                    if (scan[i].DistanceM < minDist) minDist = scan[i].DistanceM;
                    continue;
                }

                int count = i - start;
                if (count >= minPoints)
                    result.Add(new ScanSegment(start, count, sum / count, minDist, (prev - first).magnitude));

                if (i < scan.Count)
                {
                    start = i;
                    first = p;
                    prev = p;
                    sum = p;
                    minDist = scan[i].DistanceM;
                }
            }
        }

        /// <summary>見かけの幅が上限以下の区間のうち、最もセンサーに近いものを選ぶ (ハートは手前にある前提)。</summary>
        public static bool TryPickNearest(List<ScanSegment> segments, float maxExtentM, out ScanSegment picked)
        {
            picked = default;
            bool found = false;
            for (int i = 0; i < segments.Count; i++)
            {
                ScanSegment s = segments[i];
                if (s.ExtentM > maxExtentM) continue;
                if (!found || s.MinDistanceM < picked.MinDistanceM)
                {
                    picked = s;
                    found = true;
                }
            }
            return found;
        }
    }
}
