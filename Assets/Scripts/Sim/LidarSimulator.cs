using UndertaleLiDAR.LiDAR;
using UnityEngine;

namespace UndertaleLiDAR.Sim
{
    /// <summary>
    /// シミュレーション用センサーの合成点。Transform の +X を正面として毎フレーム
    /// <see cref="SimulatedLidarSensor"/> をプルし、最新スキャンを公開・描画する。
    /// HeartTarget が Update で動いた後にスキャンするよう実行順を後ろにずらす (真値との時間差ゼロ)。
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class LidarSimulator : MonoBehaviour
    {
        [SerializeField] private SimulatedLidarSpec _spec = new SimulatedLidarSpec();
        [Tooltip("レイキャスト対象レイヤー")] [SerializeField] private LayerMask _layerMask = -1;
        [Tooltip("ノイズ乱数のシード (再現性のため固定)")] [SerializeField] private int _seed = 12345;
        [SerializeField] private PointCloudView _view;

        private SimulatedLidarSensor _sensor;
        private int _shownSequence = -1;

        public SimulatedLidarSpec Spec => _spec;
        public LidarScan LatestScan { get; private set; }

        /// <summary>スキャンの通し番号。前回と同じなら新しいスキャンは無い。</summary>
        public int ScanSequence => _sensor != null ? _sensor.ScanCount : 0;

        private void Awake()
        {
            _sensor = new SimulatedLidarSensor(transform, _spec, _layerMask, _seed);
            _sensor.Connect();
        }

        private void OnDestroy() => _sensor?.Dispose();

        private void Update()
        {
            if (!_sensor.TryGetLatestScan(out LidarScan scan)) return;
            LatestScan = scan;
            if (_view != null && _shownSequence != ScanSequence)
            {
                _view.Show(scan, transform);
                _shownSequence = ScanSequence;
            }
        }

        /// <summary>タイマーを無視して即時にスキャンする (背景学習用)。</summary>
        public void ScanInto(LidarScan scan, bool withNoise) => _sensor.ScanInto(scan, withNoise);

        /// <summary>ワールド座標 → センサー座標系 [m] (真値をスキャンと同じ座標系で比較するため)。</summary>
        public Vector2 ToSensorFrame(Vector3 world) => transform.InverseTransformPoint(world);

        public Vector3 ToWorld(Vector2 sensorM) => transform.TransformPoint(sensorM);

        private void OnDrawGizmos()
        {
            // 視野の両端ビームと最小検出距離を描く。
            float half = _spec.StepToAngleRad(_spec.Steps - 1);
            Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
            Vector3 origin = transform.position;
            Gizmos.DrawLine(origin, transform.TransformPoint(new Vector3(Mathf.Cos(half), Mathf.Sin(half), 0f) * 2f));
            Gizmos.DrawLine(origin, transform.TransformPoint(new Vector3(Mathf.Cos(-half), Mathf.Sin(-half), 0f) * 2f));
            Gizmos.DrawWireSphere(origin, _spec.MinRangeM);
        }
    }
}
