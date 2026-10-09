using System;
using LidarBattle.LiDAR;
using UnityEngine;

namespace LidarBattle.Tracking
{
    /// <summary>
    /// 範囲フィルタ (デコレータ): 内側の検出器が出した位置が <see cref="Region"/> の外なら未検出にする。
    /// 点は削らずに渡す (円柱の脇の壁の点も円柱かどうかの判定に使うため)。範囲の判定は呼び出し側が与える。
    /// </summary>
    public sealed class RegionFilterTracker : IHeartTracker
    {
        private readonly IHeartTracker _inner;

        /// <summary>物理座標 [m] が範囲内なら true。null なら範囲を見ない。</summary>
        public Func<Vector2, bool> Region { get; set; }

        public RegionFilterTracker(IHeartTracker inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public bool TryTrack(LidarScan scan, out Vector2 positionM)
            => _inner.TryTrack(scan, out positionM) && (Region == null || Region(positionM));
    }
}
