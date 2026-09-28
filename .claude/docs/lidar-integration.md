# Hokuyo URG LiDAR 連携メモ

## 実機の主経路: URG-Unity (MediaFrontJapan)

実機接続は自作 SCIP ではなく **URG-Unity パッケージ**（`com.mediafrontjapan.urg-unity`）を使う。
SCIP 2.0 は仕様が凍結されており、接続・背景除去・物体検出を持つ既存実装を使う方が堅牢（DRY/YAGNI）。

- 対象: **HOKUYO UST-10LX**（Ethernet, ポート 10940）。他機種は弾かれる。
- 提供物: `SCIPClient`（内部）/ `SCIPScanPlane`（public）。`SCIPInputModules.prefab` を置くだけで動く。
- セットアップ:
  1. `SCIPInputModules.prefab` をシーンに配置。
  2. Play 中に `C` キーで設定 UI を開き、IP/位置/角度/スケールと背景（ClampDistances）を校正。
  3. 再度 `C` で閉じると PlayerPrefs に保存。`SCIPClient` と `SCIPScanPlane` の `playerPrefsKey` は一致させる。
- 連携点: 当プロジェクトは `SCIPScanPlane.ObjectLocalPositions`（**センサー座標系メートル**の検出物体配列）を
  `ScanPlaneInputSource` で読み、既存の `RectCoordinateMapper`（m→正規化 0..1）に通して SOUL を動かす。
  ライブラリの物体検出をそのまま使い、検出ロジックを再実装しない。
- 物体選択: 直前位置に最も近い検出物体を採用し、手・腕が映り込んでもハートを安定追従させる。

`BattleManager.InputMode` で経路を切替: `ScanPlane`(実機・既定) / `Mock`(マウス) / `HokuyoSerial`(下記自作)。

## 自作シリアル実装（URG-04LX 等のフォールバック）

UST-10LX 以外（USB シリアルの URG-04LX 等）を使う場合のための自作 SCIP 実装。
`URG_SERIAL_ENABLED` 定義時のみ有効（未定義ではスタブ）。以下はその仕様。

## 対象機種と接続

Hokuyo URG 系 2D LiDAR。代表例:
- **URG-04LX-UG01**: USB 接続（仮想 COM ポート）, SCIP 2.0, 240°/682 step。
- **UST-10LX 等**: Ethernet 接続, SCIP 2.0, 270°。

本プロジェクトの `HokuyoUrgSensor` は **SCIP 2.0 over Serial（USB 仮想COM）** を基本実装とする。
Ethernet 機を使う場合は `ILidarSensor` を実装する別クラスを追加する（OCP）。

> **重要**: 実機パラメータ（COM ポート名・step 範囲・エンコード桁・取付向き・距離）は
> 機種と設置により異なる。すべて `LidarSettings`（ScriptableObject）で外部化し、
> コード変更なしで現場調整する。下の数値は URG-04LX のデフォルト例。

## SCIP 2.0 の要点

通信は ASCII コマンド + 改行(`\n`)。応答はエコー → ステータス → データ → 空行。

主に使うコマンド:
- `SCIP2.0` … SCIP2 モードへ。
- `BM` … 計測（レーザ）ON。
- `GD<start4><end4><cluster2>` … 1 回だけ最新距離を取得（要求駆動・実装が単純で安全）。
  例: `GD0044072500` = step 44〜725, cluster 00。
- `MD...` … 連続取得（高頻度向け）。本実装は KISS のため `GD` ポーリングを採用。
- `QT` … 計測 OFF。

### 距離データのデコード（3 文字エンコード）
URG-04LX は距離 1 点を 3 文字でエンコード:
1. 各文字から `0x30` を引く（6bit 値）。
2. 上位から `((c0)<<12) | ((c1)<<6) | (c2)` で 18bit の距離[mm]。
3. 応答は 64 バイトごとに区切られ、各行末にチェックサムが付く（行末 1 文字を除去）。

step（角度インデックス）→ 角度の換算:
```
angle_rad = (step - frontStep) * (2π / angularResolution)
```
- `frontStep`: 正面に当たる step（URG-04LX は 384）。
- `angularResolution`: 1 周あたりの step 数（URG-04LX は 1024）。

極座標 → 物理 XY:
```
x = distance_m * cos(angle_rad)
y = distance_m * sin(angle_rad)
```

## LidarSettings に持たせる項目（DRY の集約点）

| 項目 | 例(URG-04LX) | 意味 |
|------|--------------|------|
| `portName` | `COM3` | シリアルポート |
| `baudRate` | `115200` | ボーレート |
| `startStep` / `endStep` | `44` / `725` | 取得 step 範囲 |
| `frontStep` | `384` | 正面 step |
| `angularResolution` | `1024` | 1 周 step 数 |
| `minRangeM` / `maxRangeM` | `0.02` / `4.0` | 有効距離（範囲外は無視） |
| `pollIntervalMs` | `25` | GD ポーリング周期(約40Hz) |

## キャリブレーション（Mapping 層）

物理スキャン領域の四隅（または min/max XY）を実測し `RectCoordinateMapper` に渡す:
- `physicalMin` (x,y) / `physicalMax` (x,y): ハートが動く実物理範囲[m]。
- センサー取付の回転・反転は `invertX` / `invertY` / `rotationDeg` で吸収。
- 出力は 0..1 正規化。盤面サイズ非依存（盤面は Battle 層 `BulletBoard` が決める）。

手順:
1. ハートを左下に置き、Tracking 出力の物理 XY を記録 → `physicalMin`。
2. 右上に置き記録 → `physicalMax`。
3. 上下/左右が画面と逆なら `invert*` を立てる。

## 開発時の注意

- `System.IO.Ports.SerialPort` を使うため Player Settings の API 互換性レベルを
  **.NET Framework**（または Standard 2.1 で Ports 解決可）にする必要がある場合あり。
  解決できない環境では Ethernet/UDP ブリッジ実装に切り替える。
- 通信は別スレッド。Unity API はそのスレッドから呼ばない。`LidarScan` だけを
  ロック越しにメインスレッドへ受け渡す（`HokuyoUrgSensor` 実装参照）。
- 実機が無い間は `MockLidarSensor`（マウス位置を擬似スキャン化）で全上位層を検証可能。
