using System.Text;
using TMPro;
using LidarBattle.LiDAR;
using LidarBattle.Tracking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LidarBattle.Sim
{
    /// <summary>
    /// 複数の検出手法を同じスキャンに同時に適用し、真値 (HeartTarget) との誤差を集計・表示する。
    /// 検出器の生成はここ (合成点) に閉じ込め、パラメータは Inspector から調整できるようにする。
    /// </summary>
    public sealed class TrackerEvaluator : MonoBehaviour
    {
        [SerializeField] private LidarSimulator _simulator;
        [SerializeField] private HeartTarget _heart;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Sprite _markerSprite;

        [Header("共通")]
        [Tooltip("ハートと判定する最小点数")] [SerializeField] private int _minPoints = 3;

        [Header("最近点クラスタ (A, C)")]
        [Tooltip("最近点からこの半径内を 1 クラスタとみなす [m]")] [SerializeField] private float _clusterRadiusM = 0.08f;

        [Header("区間分割 (B)")]
        [Tooltip("隣接点がこれ以上離れたら別の物体とみなす [m]")] [SerializeField] private float _breakDistM = 0.04f;
        [Tooltip("これより幅広い区間 (壁など) は候補から外す [m]")] [SerializeField] private float _maxExtentM = 0.25f;

        [Header("背景差分 (C, D, E)")]
        [Tooltip("背景よりこれ以上手前なら前景 [m]")] [SerializeField] private float _backgroundMarginM = 0.05f;
        [Tooltip("背景学習に使うスキャン枚数")] [SerializeField] private int _backgroundFrames = 20;

        [Header("円当てはめ (D, E)")]
        [Tooltip("円周からこの距離以内の点を表面に当たったとみなす [m]")] [SerializeField] private float _circleToleranceM = 0.01f;
        [Tooltip("円らしさの点数がこれより低ければ未検出")] [SerializeField] private float _minCircleScore = 0.3f;
        [Tooltip("ハートを円とみなしたときの半径 [m]。幅 9 cm のハートでは 0.040 で偏りがほぼ 0 になった")]
        [SerializeField] private float _heartRadiusM = 0.040f;
        [SerializeField] private int _fitIterations = 2;

        [Header("平滑化 (E)")]
        [SerializeField] private float _filterMinCutoffHz = 0.5f;
        [SerializeField] private float _filterBeta = 10f;
        [SerializeField] private float _filterDerivCutoffHz = 0.5f;
        [SerializeField] private float _deadbandM = 0.002f;
        [Tooltip("これ以上の飛びは一時的に無視 [m]")] [SerializeField] private float _maxJumpM = 0.15f;
        [SerializeField] private int _maxHoldFrames = 5;

        private sealed class Method
        {
            public string Name;
            public IHeartTracker Tracker;
            public SpriteRenderer Marker;
            public int Frames;
            public int Detected;
            public float SumErr, SumSqErr, MaxErr, LastErr, SumRadial;

            public void Reset()
            {
                Frames = Detected = 0;
                SumErr = SumSqErr = MaxErr = LastErr = SumRadial = 0f;
            }
        }

        private Method[] _methods;
        private BackgroundSubtractionTracker[] _backgrounds;
        private readonly LidarScan _backgroundScan = new LidarScan();
        private readonly StringBuilder _text = new StringBuilder(1024);
        private int _lastSequence = -1;
        private float _nextLabelTime;

        private void Awake()
        {
            int steps = _simulator.Spec.StepsPerRevolution;
            var bgNearest = new BackgroundSubtractionTracker(
                new NearestClusterTracker(_clusterRadiusM, _minPoints), steps, _backgroundMarginM);
            var bgCircle = new BackgroundSubtractionTracker(
                new CircleFitTracker(_heartRadiusM, _circleToleranceM, _minPoints, _minCircleScore, _fitIterations,
                    _simulator.Spec.StepToAngleRad(0), _simulator.Spec.StepToAngleRad(_simulator.Spec.Steps - 1)), steps, _backgroundMarginM);
            _backgrounds = new[] { bgNearest, bgCircle };

            _methods = new[]
            {
                Make("A Nearest", new NearestClusterTracker(_clusterRadiusM, _minPoints), Color.yellow),
                Make("B Segment", new SegmentCentroidTracker(_breakDistM, _minPoints, _maxExtentM), Color.green),
                Make("C Bg+Nearest", bgNearest, Color.magenta),
                Make("D Bg+CircleFit", bgCircle, new Color(1f, 0.5f, 0f)),
                Make("E D+Smooth", new SmoothedTracker(bgCircle, _filterMinCutoffHz, _filterBeta, _filterDerivCutoffHz, _deadbandM, _maxJumpM, _maxHoldFrames), Color.white),
            };
        }

        private Method Make(string name, IHeartTracker tracker, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * 0.03f;
            var marker = go.AddComponent<SpriteRenderer>();
            marker.sprite = _markerSprite;
            marker.color = color;
            marker.sortingOrder = 30;
            marker.enabled = false;
            return new Method { Name = name, Tracker = tracker, Marker = marker };
        }

        private void Start() => LearnBackground();

        private void OnDestroy() => LogSummary();

        private void LateUpdate()
        {
            HandleKeys();

            LidarScan scan = _simulator.LatestScan;
            if (scan == null || _simulator.ScanSequence == _lastSequence) return;
            _lastSequence = _simulator.ScanSequence;

            Vector2 truth = _simulator.ToSensorFrame(_heart.TruePosition);
            Vector2 radialDir = truth.normalized;
            foreach (Method m in _methods)
            {
                m.Frames++;
                if (m.Tracker.TryTrack(scan, out Vector2 estimate))
                {
                    Vector2 error = estimate - truth;
                    float err = error.magnitude;
                    m.Detected++;
                    m.SumErr += err;
                    m.SumSqErr += err * err;
                    m.SumRadial += Vector2.Dot(error, radialDir); // 負ならセンサー側に偏っている
                    m.LastErr = err;
                    if (err > m.MaxErr) m.MaxErr = err;
                    m.Marker.enabled = true;
                    m.Marker.transform.position = _simulator.ToWorld(estimate);
                }
                else
                {
                    m.Marker.enabled = false;
                }
            }

            if (_label != null && Time.time >= _nextLabelTime)
            {
                _nextLabelTime = Time.time + 0.2f;
                _label.text = BuildReport(false);
            }
        }

        private void HandleKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.rKey.wasPressedThisFrame)
            {
                LogSummary();
                foreach (Method m in _methods) m.Reset();
            }
            if (keyboard.bKey.wasPressedThisFrame) LearnBackground();
            if (keyboard.mKey.wasPressedThisFrame)
                _heart.Mode = _heart.Mode == HeartTarget.MoveMode.Mouse ? HeartTarget.MoveMode.Auto : HeartTarget.MoveMode.Mouse;
        }

        /// <summary>
        /// ハートをセンサーの真後ろ (視野外) に退避させて背景を学習する (実機で「ハートを外して校正」に相当)。
        /// SetActive ではなく移動にするのは、Transform の変更なら ScanInto 内の SyncTransforms で確実に反映されるため。
        /// </summary>
        private void LearnBackground()
        {
            foreach (BackgroundSubtractionTracker bg in _backgrounds) bg.ClearBackground();
            Vector3 original = _heart.transform.position;
            _heart.transform.position = _simulator.ToWorld(new Vector2(-3f, 0f));
            for (int i = 0; i < _backgroundFrames; i++)
            {
                _simulator.ScanInto(_backgroundScan, true);
                foreach (BackgroundSubtractionTracker bg in _backgrounds) bg.LearnBackground(_backgroundScan);
            }
            _heart.transform.position = original;
        }

        private void LogSummary()
        {
            if (_methods == null) return;
            Debug.Log("[TrackerEvaluator]\n" + BuildReport(true));
        }

        private string BuildReport(bool plain)
        {
            SimulatedLidarSpec spec = _simulator.Spec;
            _text.Clear();
            _text.AppendFormat("UST-20LX sim | sigma {0:F0} mm | dropout {1:P1} | scans {2} | heart {3}\n",
                spec.NoiseSigmaM * 1000f, spec.DropoutRate, _simulator.ScanSequence, _heart.Mode);
            _text.Append("[R] reset  [B] relearn background  [M] mouse/auto\n");
            if (!plain) _text.Append("<mspace=0.6em>");
            _text.Append("Method             det%   mean    rms    max   last  radial [mm]\n");
            foreach (Method m in _methods)
            {
                float n = Mathf.Max(1, m.Detected);
                _text.AppendFormat("{0,-18} {1,5:F1} {2,6:F1} {3,6:F1} {4,6:F1} {5,6:F1} {6,7:F1}\n",
                    m.Name,
                    m.Frames > 0 ? 100f * m.Detected / m.Frames : 0f,
                    1000f * m.SumErr / n,
                    1000f * Mathf.Sqrt(m.SumSqErr / n),
                    1000f * m.MaxErr,
                    1000f * m.LastErr,
                    1000f * m.SumRadial / n);
            }
            if (!plain) _text.Append("</mspace>");
            return _text.ToString();
        }
    }
}
