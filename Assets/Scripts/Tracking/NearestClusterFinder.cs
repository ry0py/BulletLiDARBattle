using System;
using System.Collections.Generic;
using LidarBattle.LiDAR;
using UnityEngine;

namespace LidarBattle.Tracking
{
    /// <summary>
    /// 「最も近い点を核に、半径内の点を 1 クラスタとみなす」処理の唯一の実装 (DRY)。
    /// 核のクラスタが最小点数に満たなければ (ノイズの孤立点)、その点群を除いて次に近い核を試す。
    /// </summary>
    public static class NearestClusterFinder
    {
        /// <summary>孤立ノイズ点を核として掴んだときに、次の核を試す回数。</summary>
        private const int MaxSeedRetries = 4;

        /// <param name="points">クラスタの点を受け取るバッファ (不要なら null)。呼び出し側が再利用する。</param>
        public static bool TryFind(LidarScan scan, float radiusM, int minPoints, List<Vector2> points, out Vector2 centroid)
        {
            centroid = Vector2.zero;
            points?.Clear();
            if (scan == null || scan.Count == 0) return false;

            float radiusSqr = radiusM * radiusM;
            Span<Vector2> rejectedCores = stackalloc Vector2[MaxSeedRetries];
            int rejectedCount = 0;

            for (int attempt = 0; attempt <= MaxSeedRetries; attempt++)
            {
                // 1) 却下済みクラスタに属さない点のうち最も近い点を核にする。
                int nearestIndex = -1;
                float nearestDist = float.MaxValue;
                for (int i = 0; i < scan.Count; i++)
                {
                    if (scan[i].DistanceM >= nearestDist) continue;
                    if (rejectedCount > 0 && IsNearAny(scan[i].ToCartesian(), rejectedCores, rejectedCount, radiusSqr)) continue;
                    nearestDist = scan[i].DistanceM;
                    nearestIndex = i;
                }
                if (nearestIndex < 0) return false;

                // 2) 核の半径内を集めて重心を取る。
                Vector2 core = scan[nearestIndex].ToCartesian();
                Vector2 sum = Vector2.zero;
                int count = 0;
                points?.Clear();
                for (int i = 0; i < scan.Count; i++)
                {
                    Vector2 p = scan[i].ToCartesian();
                    if ((p - core).sqrMagnitude > radiusSqr) continue;
                    sum += p;
                    count++;
                    points?.Add(p);
                }

                if (count >= minPoints)
                {
                    centroid = sum / count;
                    return true;
                }
                if (rejectedCount >= MaxSeedRetries) return false;
                rejectedCores[rejectedCount++] = core;
            }
            return false;
        }

        private static bool IsNearAny(Vector2 p, Span<Vector2> cores, int count, float radiusSqr)
        {
            for (int k = 0; k < count; k++)
            {
                if ((p - cores[k]).sqrMagnitude <= radiusSqr) return true;
            }
            return false;
        }
    }
}
