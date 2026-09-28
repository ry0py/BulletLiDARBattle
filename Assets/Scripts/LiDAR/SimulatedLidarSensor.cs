using System;
using UnityEngine;

namespace UndertaleLiDAR.LiDAR
{
    /// <summary>
    /// シミュレータの機種仕様とノイズ。既定値は Hokuyo UST-20LX
    /// (270° / 0.25° = 1081 step, 40 Hz, 0.06〜20 m, 繰返し精度 σ ≈ 30 mm 以下)。
    /// </summary>
    [Serializable]
    public sealed class SimulatedLidarSpec
    {
        [Header("機種 (既定: UST-20LX)")]
        [Tooltip("1 スキャンの step 数 (270° / 0.25° + 1)")] public int Steps = 1081;
        [Tooltip("正面に対応する step")] public int FrontStep = 540;
        [Tooltip("1 周あたりの step 数 (360° / 0.25°)")] public int StepsPerRevolution = 1440;
        [Tooltip("最小検出距離 [m]")] public float MinRangeM = 0.06f;
        [Tooltip("最大検出距離 [m]")] public float MaxRangeM = 20f;
        [Tooltip("スキャン周波数 [Hz]")] public float ScanRateHz = 40f;

        [Header("ノイズ")]
        [Tooltip("距離に加える白色ガウスノイズの σ [m]")] public float NoiseSigmaM = 0.02f;
        [Tooltip("1 step の計測が欠落する確率")] [Range(0f, 1f)] public float DropoutRate = 0.005f;
        [Tooltip("実機同様に距離を 1 mm 単位へ量子化する")] public bool QuantizeToMm = true;

        public float AngularStepRad => Mathf.PI * 2f / Mathf.Max(1, StepsPerRevolution);
        public float StepToAngleRad(int step) => (step - FrontStep) * AngularStepRad;
    }

    /// <summary>
    /// Physics2D レイキャストで 2D LiDAR を模擬する <see cref="ILidarSensor"/>。
    /// センサー Transform の +X を正面 (角度 0) とし、実機と同じ極座標スキャンを返す (LSP)。
    /// 距離には白色ガウスノイズ・欠落・mm 量子化を加え、実機の点群の粗さを再現する。
    /// Physics2D を使うためメインスレッド専用 (プル型なので契約上問題ない)。
    /// </summary>
    public sealed class SimulatedLidarSensor : ILidarSensor
    {
        private readonly Transform _pose;
        private readonly SimulatedLidarSpec _spec;
        private readonly int _layerMask;
        private readonly System.Random _random;
        private readonly LidarScan _scan = new LidarScan();
        private float _nextScanTime;
        private bool _hasScan;

        public bool IsConnected { get; private set; }

        /// <summary>生成したスキャンの通し番号。新旧判定に使う。</summary>
        public int ScanCount { get; private set; }

        public SimulatedLidarSensor(Transform pose, SimulatedLidarSpec spec, int layerMask, int seed)
        {
            _pose = pose != null ? pose : throw new ArgumentNullException(nameof(pose));
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _layerMask = layerMask;
            _random = new System.Random(seed);
        }

        public void Connect()
        {
            IsConnected = true;
            _nextScanTime = 0f;
        }

        public void Disconnect() => IsConnected = false;
        public void Dispose() => Disconnect();

        public bool TryGetLatestScan(out LidarScan scan)
        {
            scan = _scan;
            if (!IsConnected) return false;

            if (Time.time >= _nextScanTime)
            {
                ScanInto(_scan, true);
                _nextScanTime = Time.time + 1f / Mathf.Max(1f, _spec.ScanRateHz);
                ScanCount++;
                _hasScan = true;
            }
            return _hasScan && _scan.Count > 0;
        }

        /// <summary>今すぐ 1 スキャンぶんレイキャストして書き込む (背景学習などの即時取得用)。</summary>
        public void ScanInto(LidarScan scan, bool withNoise)
        {
            scan.Clear();
            Physics2D.SyncTransforms(); // 同フレームで動かしたコライダを反映する。
            Vector2 origin = _pose.position;
            for (int step = 0; step < _spec.Steps; step++)
            {
                if (withNoise && _random.NextDouble() < _spec.DropoutRate) continue;

                float angle = _spec.StepToAngleRad(step);
                Vector2 dir = _pose.TransformDirection(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
                RaycastHit2D hit = Physics2D.Raycast(origin, dir, _spec.MaxRangeM, _layerMask);
                if (hit.collider == null) continue;

                float dist = hit.distance;
                if (withNoise) dist += NextGaussian() * _spec.NoiseSigmaM;
                if (_spec.QuantizeToMm) dist = Mathf.Round(dist * 1000f) * 0.001f;
                if (dist < _spec.MinRangeM || dist > _spec.MaxRangeM) continue;

                scan.Add(new LidarMeasurement(angle, dist));
            }
        }

        /// <summary>Box-Muller 法による標準正規乱数。</summary>
        private float NextGaussian()
        {
            double u1 = 1.0 - _random.NextDouble();
            double u2 = _random.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }
    }
}
