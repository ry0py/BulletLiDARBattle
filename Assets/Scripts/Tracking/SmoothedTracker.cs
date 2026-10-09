using LidarBattle.LiDAR;
using UnityEngine;

namespace LidarBattle.Tracking
{
    /// <summary>
    /// 平滑化 (デコレータ): 検出位置の震えを抑える。
    /// 1) One Euro フィルタ: 遅く動くときは強くならし、速く動くときは弱めて遅れを減らす。
    /// 2) 遊び: 出力は、フィルタの値が出力から遊びより離れたときだけ、その差のぶん付いていく (止まっているときは出力が動かない)。
    /// 大きく飛んだ結果や一瞬の未検出は数フレームだけ直前位置を保ち、それより長く続けば新しい位置から始め直す。
    /// </summary>
    public sealed class SmoothedTracker : IHeartTracker
    {
        private readonly IHeartTracker _inner;
        private readonly float _minCutoffHz;
        private readonly float _beta;
        private readonly float _derivCutoffHz;
        private readonly float _deadbandM;
        private readonly float _maxJumpSqr;
        private readonly int _maxHoldFrames;

        private Vector2 _filtered;  // One Euro の値
        private Vector2 _velocity;  // ならした速さ [m/s]
        private Vector2 _output;    // 遊びを通した値
        private bool _hasPosition;
        private int _heldFrames;
        private float _lastTime;

        /// <param name="minCutoffHz">止まっているときのならし具合。小さいほど震えが減るが遅れる</param>
        /// <param name="beta">速さに応じてならしを弱める度合い。大きいほど速い動きの遅れが減るが震えが残る</param>
        /// <param name="derivCutoffHz">速さを求めるときのならし具合</param>
        /// <param name="deadbandM">遊びの半径 [m]</param>
        public SmoothedTracker(IHeartTracker inner, float minCutoffHz, float beta, float derivCutoffHz, float deadbandM,
            float maxJumpM, int maxHoldFrames)
        {
            _inner = inner ?? throw new System.ArgumentNullException(nameof(inner));
            _minCutoffHz = Mathf.Max(0.01f, minCutoffHz);
            _beta = Mathf.Max(0f, beta);
            _derivCutoffHz = Mathf.Max(0.01f, derivCutoffHz);
            _deadbandM = Mathf.Max(0f, deadbandM);
            _maxJumpSqr = maxJumpM * maxJumpM;
            _maxHoldFrames = Mathf.Max(0, maxHoldFrames);
        }

        public bool TryTrack(LidarScan scan, out Vector2 positionM)
        {
            bool detected = _inner.TryTrack(scan, out Vector2 raw);
            bool jump = detected && _hasPosition && (raw - _filtered).sqrMagnitude > _maxJumpSqr;

            if ((!detected || jump) && _hasPosition && _heldFrames < _maxHoldFrames)
            {
                // 未検出/飛びは一時的なものとみなし、直前位置を保持する。
                _heldFrames++;
                positionM = _output;
                return true;
            }

            float now = Time.unscaledTime;
            _heldFrames = 0;
            if (!detected)
            {
                // 見失いが続いた。次に見つけた位置から始め直す。
                _hasPosition = false;
                positionM = _output;
                return false;
            }
            if (!_hasPosition || jump)
            {
                _filtered = _output = raw;
                _velocity = Vector2.zero;
                _hasPosition = true;
                _lastTime = now;
                positionM = _output;
                return true;
            }

            float dt = Mathf.Clamp(now - _lastTime, 0.005f, 0.2f);
            _lastTime = now;
            _velocity = Vector2.Lerp(_velocity, (raw - _filtered) / dt, Alpha(_derivCutoffHz, dt));
            float cutoff = _minCutoffHz + _beta * _velocity.magnitude;
            _filtered = Vector2.Lerp(_filtered, raw, Alpha(cutoff, dt));

            Vector2 d = _filtered - _output;
            float len = d.magnitude;
            if (len > _deadbandM) _output += d * ((len - _deadbandM) / len);

            positionM = _output;
            return true;
        }

        /// <summary>遮断周波数 cutoffHz の 1 次ローパスを dt 秒ぶん進める係数。</summary>
        private static float Alpha(float cutoffHz, float dt)
        {
            float tau = 1f / (2f * Mathf.PI * cutoffHz);
            return 1f / (1f + tau / dt);
        }
    }
}
