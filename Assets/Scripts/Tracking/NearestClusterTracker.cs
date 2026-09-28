using UndertaleLiDAR.LiDAR;
using UnityEngine;

namespace UndertaleLiDAR.Tracking
{
    /// <summary>
    /// 「最も近い点」を核に、半径内の点を 1 クラスタとみなし重心を返すシンプルな検出器。
    /// 手で持つハートはセンサーに最も近い物体である、という前提に基づく (KISS)。
    /// 壁や置物が手前にあると誤検出するので、実際には背景差分の内側で使う。
    /// </summary>
    public sealed class NearestClusterTracker : IHeartTracker
    {
        private readonly float _clusterRadiusM;
        private readonly int _minPoints;

        public NearestClusterTracker(float clusterRadiusM, int minPoints)
        {
            _clusterRadiusM = Mathf.Max(0.001f, clusterRadiusM);
            _minPoints = Mathf.Max(1, minPoints);
        }

        public bool TryTrack(LidarScan scan, out Vector2 positionM)
            => NearestClusterFinder.TryFind(scan, _clusterRadiusM, _minPoints, null, out positionM);
    }
}
