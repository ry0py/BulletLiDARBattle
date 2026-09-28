using UnityEngine;

namespace UndertaleLiDAR.Input
{
    /// <summary>
    /// SOUL の入力源。キーボードでも LiDAR でも「盤面の正規化座標 (0〜1) での目標位置」を返す形に揃え、
    /// SoulController が入力の種類を区別しなくて済むようにする。
    /// </summary>
    public interface IHeartInputSource
    {
        Vector2 ReadTarget(Vector2 currentNormalized, float deltaTime);
    }
}
