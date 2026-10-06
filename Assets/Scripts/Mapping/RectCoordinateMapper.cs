using UnityEngine;

namespace LidarBattle.Mapping
{
    /// <summary>
    /// 矩形キャリブレーションによる物理[m] → 正規化(0..1) 変換。
    /// 物理範囲・取付回転・反転という「現場で変わる値」を唯一ここで保持する (DRY)。
    /// </summary>
    public sealed class RectCoordinateMapper : ICoordinateMapper
    {
        private readonly Vector2 _min;
        private readonly Vector2 _size;
        private readonly float _cos;
        private readonly float _sin;
        private readonly bool _invertX;
        private readonly bool _invertY;

        public RectCoordinateMapper(Vector2 physicalMin, Vector2 physicalMax,
            float rotationDeg, bool invertX, bool invertY)
        {
            _min = physicalMin;
            Vector2 size = physicalMax - physicalMin;
            // ゼロ割回避: 退化した範囲は 1 とみなす。
            _size = new Vector2(
                Mathf.Approximately(size.x, 0f) ? 1f : size.x,
                Mathf.Approximately(size.y, 0f) ? 1f : size.y);
            float rad = rotationDeg * Mathf.Deg2Rad;
            _cos = Mathf.Cos(rad);
            _sin = Mathf.Sin(rad);
            _invertX = invertX;
            _invertY = invertY;
        }

        /// <summary>取付回転だけを補正した物理座標[m]。PhysicalMin/Max はこの座標系で測る。</summary>
        public Vector2 ToAligned(Vector2 physicalM)
            => new Vector2(
                physicalM.x * _cos + physicalM.y * _sin,
                -physicalM.x * _sin + physicalM.y * _cos);

        /// <summary>物理座標[m] がキャリブレーション矩形を marginM だけ広げた範囲に入るか。</summary>
        public bool Contains(Vector2 physicalM, float marginM)
        {
            Vector2 r = ToAligned(physicalM);
            Vector2 max = _min + _size;
            return r.x >= Mathf.Min(_min.x, max.x) - marginM && r.x <= Mathf.Max(_min.x, max.x) + marginM
                && r.y >= Mathf.Min(_min.y, max.y) - marginM && r.y <= Mathf.Max(_min.y, max.y) + marginM;
        }

        public Vector2 ToNormalized(Vector2 physicalM)
        {
            Vector2 r = ToAligned(physicalM);
            // min > max (負のサイズ) なら自然に反転する。
            float nx = (r.x - _min.x) / _size.x;
            float ny = (r.y - _min.y) / _size.y;
            if (_invertX) nx = 1f - nx;
            if (_invertY) ny = 1f - ny;
            return new Vector2(Mathf.Clamp01(nx), Mathf.Clamp01(ny));
        }
    }
}
