using System;

namespace LidarBattle.LiDAR
{
    /// <summary>
    /// 2D LiDAR センサーの抽象 (ISP: 接続/切断/最新スキャン取得のみ)。
    /// 上位層はこのインターフェースだけに依存し、実機/モックを差し替え可能にする (DIP/LSP)。
    /// 取得は「プル型」: メインスレッドが <see cref="TryGetLatestScan"/> を呼ぶ。
    /// </summary>
    public interface ILidarSensor : IDisposable
    {
        bool IsConnected { get; }

        /// <summary>通信を開始する。失敗時は例外を投げる (握りつぶさない)。</summary>
        void Connect();

        void Disconnect();

        /// <summary>
        /// 最新スキャンを取得する。新しいスキャンが無い/未検出なら false。
        /// 返す <see cref="LidarScan"/> は次回呼び出しまでのみ有効 (即読み取り前提)。
        /// </summary>
        bool TryGetLatestScan(out LidarScan scan);
    }
}
