using UnityEngine;

namespace LidarBattle.Config
{
    /// <summary>
    /// LiDAR の接続・検出・キャリブレーションを 1 アセットに集約する設定 (DRY の集約点)。
    /// コードを再ビルドせずに現場で調整できるよう、すべて外部化する。
    /// 機種/設置に合わせて Inspector で調整する (UST-20LX の値は docs/lidar-integration.md)。
    /// </summary>
    [CreateAssetMenu(fileName = "LidarSettings", menuName = "LiDAR Battle/Lidar Settings")]
    public sealed class LidarSettings : ScriptableObject
    {
        [Tooltip("false ならゲームは LiDAR に接続せずキーボードで動かす")] public bool UseLidar = true;

        [Header("Ethernet 接続 (UST 系)")]
        [Tooltip("センサーの IP アドレス")] public string HostName = "192.168.0.10";
        public int TcpPort = 10940;

        [Header("Serial 接続 (URG-04LX 等)")]
        [Tooltip("シリアルポート名。例: Windows=COM3, macOS=/dev/tty.usbmodem*")]
        public string PortName = "COM3";
        public int BaudRate = 115200;

        [Header("スキャン範囲 (step)")]
        [Tooltip("取得を開始する step")] public int StartStep = 44;
        [Tooltip("取得を終了する step")] public int EndStep = 725;
        [Tooltip("センサー正面に対応する step")] public int FrontStep = 384;
        [Tooltip("1 周あたりの step 数 (角度分解能)")] public int AngularResolution = 1024;

        /// <summary>step → センサー座標の角度 [rad] (正面 0、反時計回りが正)。</summary>
        public float StepToAngleRad(int step) => (step - FrontStep) * (Mathf.PI * 2f / AngularResolution);

        [Header("有効距離 [m]")]
        public float MinRangeM = 0.02f;
        public float MaxRangeM = 4.0f;

        [Header("ポーリング")]
        [Tooltip("GD コマンドのポーリング周期 [ms]")] public int PollIntervalMs = 25;

        [Header("検出 (Tracking)")]
        [Tooltip("ハートを円とみなしたときの半径 [m] (スキャン面の高さでの断面幅の半分程度)")] public float HeartRadiusM = 0.040f;
        [Tooltip("円周からこの距離以内の点を表面に当たったとみなす [m]。表面のノイズ (実機で σ 6 mm 程度) より少し大きく")] public float CircleToleranceM = 0.01f;
        [Tooltip("表面に当たった点がこれより少ない円は採らない")] public int MinCirclePoints = 5;
        [Tooltip("円らしさの点数 (-1〜1) がこれより低ければ未検出。壁だけのときは 0 付近、円柱は 0.5 以上")] public float MinCircleScore = 0.3f;
        [Tooltip("見つけた円の中心を、表面に当たった点から求め直す回数")] public int FitIterations = 2;

        [Header("平滑化 (震え対策)")]
        [Tooltip("One Euro: 止まっているときのならし具合 [Hz]。小さいほど震えが減るが遅れる")] public float FilterMinCutoffHz = 0.5f;
        [Tooltip("One Euro: 速く動くほどならしを弱める度合い。大きいほど速い動きの遅れが減るが震えが残る")] public float FilterBeta = 10f;
        [Tooltip("One Euro: 速さを求めるときのならし具合 [Hz]")] public float FilterDerivCutoffHz = 0.5f;
        [Tooltip("遊びの半径 [m]。位置がこれ以上ずれたときだけ出力が動く (止まっているときの震え対策)")] public float DeadbandM = 0.002f;
        [Tooltip("1 スキャンでこれ以上飛んだ結果は誤検出とみなし、少しの間直前位置を保つ [m]")] public float MaxJumpM = 0.1f;
        [Tooltip("見失い・飛びのときに直前位置を保つスキャン数")] public int MaxHoldFrames = 5;

        [Header("キャリブレーション (Mapping: 物理[m] → 正規化0..1)")]
        [Tooltip("画面の左下に対応する物理座標 [m] (回転補正後)。実行中に [1] で記録できる")] public Vector2 PhysicalMin = new Vector2(-0.3f, 0.3f);
        [Tooltip("画面の右上に対応する物理座標 [m] (回転補正後)。実行中に [2] で記録できる")] public Vector2 PhysicalMax = new Vector2(0.3f, 0.9f);
        [Tooltip("センサー取付の回転補正 [deg]。センサーが盤面の下辺から上を向くなら -90")] public float RotationDeg = 0f;
        public bool InvertX = false;
        public bool InvertY = false;
    }
}
