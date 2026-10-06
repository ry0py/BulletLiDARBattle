using System;
using LidarBattle.LiDAR;
using UnityEngine;

namespace LidarBattle.Tracking
{
    /// <summary>
    /// 範囲フィルタ (デコレータ): <see cref="Region"/> に入る点だけを内側の検出器に渡す。
    /// 盤面の外 (センサーの後ろの台や人) を最近点として拾わないようにする。範囲の判定は呼び出し側が与える。
    /// </summary>
    public sealed class RegionFilterTracker : IHeartTracker
    {
        private readonly IHeartTracker _inner;
        private readonly LidarScan _inside = new LidarScan();

        /// <summary>物理座標 [m] が範囲内なら true。null なら全点を通す。</summary>
        public Func<Vector2, bool> Region { get; set; }

        public RegionFilterTracker(IHeartTracker inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public bool TryTrack(LidarScan scan, out Vector2 positionM)
        {
            positionM = Vector2.zero;
            if (scan == null) return false;
            if (Region == null) return _inner.TryTrack(scan, out positionM);

            _inside.Clear();
            for (int i = 0; i < scan.Count; i++)
            {
                LidarMeasurement m = scan[i];
                if (Region(m.ToCartesian())) _inside.Add(m);
            }
            return _inner.TryTrack(_inside, out positionM);
        }
    }
}
