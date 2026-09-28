using LidarBattle.LiDAR;
using UnityEngine;

namespace LidarBattle.Tracking
{
    /// <summary>
    /// スキャンからハート (物体) の物理位置を 1 点に特定する抽象。
    /// 盤面やゲームを一切知らない。出力は物理直交座標 [m]。
    /// </summary>
    public interface IHeartTracker
    {
        /// <summary>ハートを検出できたら true と物理座標[m]を返す。</summary>
        bool TryTrack(LidarScan scan, out Vector2 positionM);
    }
}
