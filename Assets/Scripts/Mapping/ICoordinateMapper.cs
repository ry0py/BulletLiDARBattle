using UnityEngine;

namespace LidarBattle.Mapping
{
    /// <summary>
    /// 物理座標 [m] を正規化座標 (0..1) へ変換する抽象。
    /// 盤面サイズに依存しない中立な座標を出力し、Battle 層から物理単位を隠蔽する。
    /// </summary>
    public interface ICoordinateMapper
    {
        /// <summary>物理座標[m] → 0..1 (範囲外はクランプ)。</summary>
        Vector2 ToNormalized(Vector2 physicalM);
    }
}
