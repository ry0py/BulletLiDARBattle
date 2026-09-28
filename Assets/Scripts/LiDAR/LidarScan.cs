using System.Collections.Generic;
using UnityEngine;

namespace LidarBattle.LiDAR
{
    /// <summary>1 点の計測値 (極座標)。センサー固有単位を排し、角度[rad]・距離[m]で保持する。</summary>
    public readonly struct LidarMeasurement
    {
        public readonly float AngleRad;
        public readonly float DistanceM;

        public LidarMeasurement(float angleRad, float distanceM)
        {
            AngleRad = angleRad;
            DistanceM = distanceM;
        }

        /// <summary>極座標 → 物理直交座標 [m]。</summary>
        public Vector2 ToCartesian()
            => new Vector2(DistanceM * Mathf.Cos(AngleRad), DistanceM * Mathf.Sin(AngleRad));
    }

    /// <summary>
    /// 1 回ぶんのスキャン結果。GC を避けるため内部バッファを再利用する可変コンテナ。
    /// 返却された参照は次回の取得まで有効 (呼び出し側は即座に読むこと)。
    /// </summary>
    public sealed class LidarScan
    {
        private readonly List<LidarMeasurement> _measurements = new List<LidarMeasurement>(1024);

        public int Count => _measurements.Count;
        public LidarMeasurement this[int index] => _measurements[index];
        public IReadOnlyList<LidarMeasurement> Measurements => _measurements;

        public void Clear() => _measurements.Clear();
        public void Add(in LidarMeasurement m) => _measurements.Add(m);
    }
}
