using UndertaleLiDAR.LiDAR;
using UnityEngine;

namespace UndertaleLiDAR.Tracking
{
    /// <summary>
    /// 手法 E (デコレータ): 任意の検出器の出力に指数移動平均をかけ、直前位置から大きく飛ぶ結果や
    /// 一瞬の未検出を数フレーム保留する。手や置物への誤検出が混ざっても SOUL が飛ばないようにする。
    /// </summary>
    public sealed class SmoothedTracker : IHeartTracker
    {
        private readonly IHeartTracker _inner;
        private readonly float _alpha;
        private readonly float _maxJumpSqr;
        private readonly int _maxHoldFrames;
        private Vector2 _position;
        private bool _hasPosition;
        private int _heldFrames;

        public SmoothedTracker(IHeartTracker inner, float alpha, float maxJumpM, int maxHoldFrames)
        {
            _inner = inner ?? throw new System.ArgumentNullException(nameof(inner));
            _alpha = Mathf.Clamp01(alpha);
            _maxJumpSqr = maxJumpM * maxJumpM;
            _maxHoldFrames = Mathf.Max(0, maxHoldFrames);
        }

        public bool TryTrack(LidarScan scan, out Vector2 positionM)
        {
            bool detected = _inner.TryTrack(scan, out Vector2 raw);
            bool jump = detected && _hasPosition && (raw - _position).sqrMagnitude > _maxJumpSqr;

            if ((!detected || jump) && _hasPosition && _heldFrames < _maxHoldFrames)
            {
                // 未検出/飛びは一時的なものとみなし、直前位置を保持する。
                _heldFrames++;
                positionM = _position;
                return true;
            }

            if (!detected)
            {
                positionM = _position;
                return false;
            }

            _heldFrames = 0;
            _position = _hasPosition ? Vector2.Lerp(_position, raw, _alpha) : raw;
            _hasPosition = true;
            positionM = _position;
            return true;
        }
    }
}
