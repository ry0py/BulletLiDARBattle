using UnityEngine;

namespace LidarBattle.Input
{
    /// <summary>
    /// SOUL の入力源。キーボードでも LiDAR でも「盤面の正規化座標 (0〜1) での目標位置」を返す形に揃え、
    /// SoulController が入力の種類を区別しなくて済むようにする。
    /// </summary>
    public interface IHeartInputSource
    {
        /// <summary>今この入力源が位置を出せれば true（未接続・見失い・キーを押していない等は false）。</summary>
        bool TryReadTarget(Vector2 currentNormalized, float deltaTime, out Vector2 target);
    }
}
