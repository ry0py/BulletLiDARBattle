using System.Collections.Generic;
using LidarBattle.LiDAR;
using UnityEngine;

namespace LidarBattle.Tracking
{
    /// <summary>
    /// 円当てはめ: LiDAR はハートの「手前の表面」しか見えないため、表面点の重心は真の中心よりセンサー側に
    /// 半径ぶん偏る。最近点クラスタの点に既知半径の円を当てはめ (Gauss-Newton)、中心を推定して偏りを補正する。
    /// </summary>
    public sealed class CircleFitTracker : IHeartTracker
    {
        private readonly float _clusterRadiusM;
        private readonly int _minPoints;
        private readonly float _radiusM;
        private readonly int _iterations;
        private readonly List<Vector2> _points = new List<Vector2>(256);

        public CircleFitTracker(float clusterRadiusM, int minPoints, float radiusM, int iterations)
        {
            _clusterRadiusM = Mathf.Max(0.001f, clusterRadiusM);
            _minPoints = Mathf.Max(1, minPoints);
            _radiusM = Mathf.Max(0.001f, radiusM);
            _iterations = Mathf.Max(1, iterations);
        }

        public bool TryTrack(LidarScan scan, out Vector2 positionM)
        {
            if (!NearestClusterFinder.TryFind(scan, _clusterRadiusM, _minPoints, _points, out Vector2 centroid))
            {
                positionM = Vector2.zero;
                return false;
            }

            // 初期値: 表面点の重心を、センサーから遠ざかる向きに半径ぶん押し出す。
            Vector2 center = centroid + centroid.normalized * _radiusM;
            for (int it = 0; it < _iterations; it++)
            {
                float a11 = 0f, a12 = 0f, a22 = 0f, b1 = 0f, b2 = 0f;
                for (int i = 0; i < _points.Count; i++)
                {
                    Vector2 d = _points[i] - center;
                    float len = d.magnitude;
                    if (len < 1e-6f) continue;
                    float r = len - _radiusM;   // 残差
                    Vector2 j = -d / len;       // 残差の center に関する勾配
                    a11 += j.x * j.x;
                    a12 += j.x * j.y;
                    a22 += j.y * j.y;
                    b1 += j.x * r;
                    b2 += j.y * r;
                }

                // (J^T J) delta = -J^T r を Cramer の公式で解く。
                float det = a11 * a22 - a12 * a12;
                if (Mathf.Abs(det) < 1e-9f) break;
                float dx = (-b1 * a22 + a12 * b2) / det;
                float dy = (-a11 * b2 + a12 * b1) / det;
                center += new Vector2(dx, dy);
                if (dx * dx + dy * dy < 1e-10f) break;
            }

            positionM = center;
            return true;
        }
    }
}
