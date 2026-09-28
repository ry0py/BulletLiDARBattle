using System.Collections.Generic;
using LidarBattle.LiDAR;
using UnityEngine;

namespace LidarBattle.Tracking
{
    /// <summary>
    /// 手法 B: 隣接点距離で区間分割し、幅がハート相当の区間のうち最も近いものの重心を返す。
    /// 最近点だけを核にする <see cref="NearestClusterTracker"/> と違い、壁など幅広い物体を除外できる。
    /// </summary>
    public sealed class SegmentCentroidTracker : IHeartTracker
    {
        private readonly float _breakDistM;
        private readonly int _minPoints;
        private readonly float _maxExtentM;
        private readonly List<ScanSegment> _segments = new List<ScanSegment>(64);

        public SegmentCentroidTracker(float breakDistM, int minPoints, float maxExtentM)
        {
            _breakDistM = Mathf.Max(0.001f, breakDistM);
            _minPoints = Mathf.Max(1, minPoints);
            _maxExtentM = Mathf.Max(0.001f, maxExtentM);
        }

        public bool TryTrack(LidarScan scan, out Vector2 positionM)
        {
            ScanSegmenter.Segment(scan, _breakDistM, _minPoints, _segments);
            if (!ScanSegmenter.TryPickNearest(_segments, _maxExtentM, out ScanSegment segment))
            {
                positionM = Vector2.zero;
                return false;
            }
            positionM = segment.Centroid;
            return true;
        }
    }
}
