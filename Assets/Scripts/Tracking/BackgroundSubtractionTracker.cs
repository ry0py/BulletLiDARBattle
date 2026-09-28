using UndertaleLiDAR.LiDAR;
using UnityEngine;

namespace UndertaleLiDAR.Tracking
{
    /// <summary>
    /// 背景差分 (デコレータ): ハート無しの状態で step ごとの背景距離を学習し、背景より手前の点だけを
    /// 前景スキャンとして内側の検出器に渡す。URG-Unity の ClampDistances と同じ考え方で、壁や置物を除外する。
    /// 学習は合成点が <see cref="LearnBackground"/> で行う (実機では「ハートを外して校正」の操作に相当)。
    /// </summary>
    public sealed class BackgroundSubtractionTracker : IHeartTracker
    {
        private readonly IHeartTracker _inner;
        private readonly float[] _background;
        private readonly float _angularStepRad;
        private readonly float _marginM;
        private readonly LidarScan _foreground = new LidarScan();

        public bool HasBackground { get; private set; }

        public BackgroundSubtractionTracker(IHeartTracker inner, int stepsPerRevolution, float marginM)
        {
            _inner = inner ?? throw new System.ArgumentNullException(nameof(inner));
            int steps = Mathf.Max(1, stepsPerRevolution);
            _background = new float[steps];
            _angularStepRad = Mathf.PI * 2f / steps;
            _marginM = Mathf.Max(0f, marginM);
            ClearBackground();
        }

        public void ClearBackground()
        {
            for (int i = 0; i < _background.Length; i++) _background[i] = float.PositiveInfinity;
            HasBackground = false;
        }

        /// <summary>背景スキャンを 1 枚学習する。複数枚呼ぶとノイズに対して保守的 (最小距離) になる。</summary>
        public void LearnBackground(LidarScan scan)
        {
            if (scan == null) return;
            for (int i = 0; i < scan.Count; i++)
            {
                int index = StepIndex(scan[i].AngleRad);
                if (scan[i].DistanceM < _background[index]) _background[index] = scan[i].DistanceM;
            }
            HasBackground = true;
        }

        public bool TryTrack(LidarScan scan, out Vector2 positionM)
        {
            positionM = Vector2.zero;
            if (scan == null) return false;

            _foreground.Clear();
            for (int i = 0; i < scan.Count; i++)
            {
                LidarMeasurement m = scan[i];
                if (m.DistanceM < _background[StepIndex(m.AngleRad)] - _marginM) _foreground.Add(m);
            }
            return _inner.TryTrack(_foreground, out positionM);
        }

        private int StepIndex(float angleRad)
        {
            int index = Mathf.RoundToInt(angleRad / _angularStepRad) % _background.Length;
            return index < 0 ? index + _background.Length : index;
        }
    }
}
