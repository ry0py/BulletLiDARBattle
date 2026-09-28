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

        [Header("有効距離 [m]")]
        public float MinRangeM = 0.02f;
        public float MaxRangeM = 4.0f;

        [Header("ポーリング")]
        [Tooltip("GD コマンドのポーリング周期 [ms]")] public int PollIntervalMs = 25;

        [Header("検出 (Tracking)")]
        [Tooltip("同一クラスタとみなす半径 [m]")] public float ClusterRadiusM = 0.10f;
        [Tooltip("ハートと判定する最小点数")] public int MinClusterPoints = 3;
        [Tooltip("背景よりこれ以上手前なら前景 [m]")] public float BackgroundMarginM = 0.05f;
        [Tooltip("背景学習に使うスキャン枚数")] public int BackgroundFrames = 20;
        [Tooltip("ハートを円とみなしたときの半径 [m] (スキャン面の高さでの断面幅の半分程度)")] public float HeartRadiusM = 0.040f;
        public int FitIterations = 5;

        [Header("キャリブレーション (Mapping: 物理[m] → 正規化0..1)")]
        [Tooltip("画面の左下に対応する物理座標 [m] (回転補正後)。実行中に [1] で記録できる")] public Vector2 PhysicalMin = new Vector2(-0.3f, 0.3f);
        [Tooltip("画面の右上に対応する物理座標 [m] (回転補正後)。実行中に [2] で記録できる")] public Vector2 PhysicalMax = new Vector2(0.3f, 0.9f);
        [Tooltip("センサー取付の回転補正 [deg]。センサーが盤面の下辺から上を向くなら -90")] public float RotationDeg = 0f;
        public bool InvertX = false;
        public bool InvertY = false;
    }
}
