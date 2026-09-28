using UnityEngine;
using UnityEngine.InputSystem;

namespace LidarBattle.LiDAR
{
    /// <summary>
    /// 実機なしで上位層 (Tracking/Mapping/Battle) を開発・デモするためのモック。
    /// マウス位置を「物理範囲内のハート位置」に見立て、実機と同じ極座標スキャン
    /// (小さな点群クラスタ) を生成する。これにより Tracking/Mapping を本物同様に検証できる (LSP)。
    /// </summary>
    public sealed class MockLidarSensor : ILidarSensor
    {
        private readonly Vector2 _physicalMin;
        private readonly Vector2 _physicalMax;
        private readonly float _blobRadiusM;
        private readonly int _blobPoints;
        private readonly LidarScan _scan = new LidarScan();

        public bool IsConnected { get; private set; }

        public MockLidarSensor(Vector2 physicalMin, Vector2 physicalMax, float blobRadiusM, int blobPoints)
        {
            _physicalMin = physicalMin;
            _physicalMax = physicalMax;
            _blobRadiusM = Mathf.Max(0.005f, blobRadiusM);
            _blobPoints = Mathf.Max(3, blobPoints);
        }

        public void Connect() => IsConnected = true;
        public void Disconnect() => IsConnected = false;
        public void Dispose() => Disconnect();

        public bool TryGetLatestScan(out LidarScan scan)
        {
            scan = _scan;
            if (!IsConnected || Mouse.current == null)
            {
                return false;
            }

            // マウス(画面 0..1) → 物理範囲[m] のハート中心。
            Vector2 mousePx = Mouse.current.position.ReadValue();
            float u = Mathf.Clamp01(mousePx.x / Mathf.Max(1, Screen.width));
            float v = Mathf.Clamp01(mousePx.y / Mathf.Max(1, Screen.height));
            Vector2 center = new Vector2(
                Mathf.Lerp(_physicalMin.x, _physicalMax.x, u),
                Mathf.Lerp(_physicalMin.y, _physicalMax.y, v));

            // ハート表面を模した小クラスタを極座標で生成する。
            _scan.Clear();
            for (int i = 0; i < _blobPoints; i++)
            {
                float t = (i / (float)_blobPoints) * Mathf.PI * 2f;
                Vector2 p = center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * (_blobRadiusM * 0.4f);
                float dist = p.magnitude;
                float angle = Mathf.Atan2(p.y, p.x);
                _scan.Add(new LidarMeasurement(angle, dist));
            }
            return true;
        }
    }
}
