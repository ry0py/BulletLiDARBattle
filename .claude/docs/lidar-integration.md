# Hokuyo LiDAR 実機連携

## 結論（2026-09-28 時点）

- 対象機は **HOKUYO UST-20LX**（Ethernet, TCP ポート 10940, 270° / 0.25° = 1081 step, 40 Hz）。
- 経路は **自作の Ethernet SCIP センサー → Tracking 層 → `RectCoordinateMapper`**。
  検出ロジックは [lidar-simulation.md](lidar-simulation.md) で検証済みの
  `BackgroundSubtractionTracker(new CircleFitTracker(...))` をそのまま使い、実機用に書き直さない。
- **URG-Unity パッケージ（`com.mediafrontjapan.urg-unity`）は使わない。** 生スキャン
  （`SCIPClient.Capture.Distances`）が `internal` で外から読めず、公開されるのはパッケージ独自の物体検出結果
  （`SCIPScanPlane.ObjectLocalPositions`, 表面重心）だけなので、検証した検出器チェーンを通せない。
- `HokuyoUrgSensor`（シリアル / URG-04LX 用, `URG_SERIAL_ENABLED` 時のみ有効）は USB 機を使う場合の
  フォールバックとして残す。UST-20LX には使えない（シリアル専用）。

手順 1（センサー実装）は実装済み（`HokuyoEthernetSensor` / `ScipScanParser`）。実機確認は
`Tools > LiDAR Battle > Build LiDAR Live Scene` で `LidarLiveScene` を生成して再生する（`LidarLiveView`: 点群＋検出マーカー、`B` で背景学習）。
手順 2・3 も実装済み: `LidarInputSource`（Select/Battle 両シーンの SOUL 入力）。接続できない / `UseLidar=false` / 見失い中は
位置を出さず、`HeartInputSelector` が次の入力源（カメラ）に回す（キーボードは押している間だけ最優先）。接続と背景はシーンをまたいで static に保持する。
キー: `B` 背景学習 / `1` 今のハート位置を画面左下 (`PhysicalMin`) / `2` 右上 (`PhysicalMax`) / `F1` 状態表示。
`PhysicalMin/Max` は `RotationDeg` 補正後の座標で、min > max なら反転になる（`Invert*` は不要）。
センサーは同時 1 接続のみ。UrgBenriPlus 等で接続中だと Unity から繋がらない。

## 実機接続の作業手順（この順に作る）

### 手順 1: UST-20LX 用センサー `HokuyoEthernetSensor : ILidarSensor`

- 置き場所: `Assets/Scripts/LiDAR/HokuyoEthernetSensor.cs`（名前空間 `LidarBattle.LiDAR`）。
- 通信: `System.Net.Sockets.TcpClient` で `LidarSettings.HostName : 10940` に接続。
  受信は専用スレッド、`TryGetLatestScan` でフロント/バックの `LidarScan` を交換する（`HokuyoUrgSensor` と同じ構造）。
- プロトコル（下記「SCIP 2.0 の要点」）: 接続 → `BM` → `GD0000108000` を `PollIntervalMs`（25 ms）ごとに送り、
  応答をデコードして `LidarScan` に詰める。切断時は `QT`。
- DRY: 3 文字デコード・データ行のパース（末尾チェックサム除去、3 文字境界の行またぎ繰り越し、
  step → 角度換算）を `HokuyoUrgSensor` から static ヘルパー（例: `ScipScanParser`）へ切り出し、
  シリアル版と Ethernet 版で共有する。シリアル版の挙動は変えない。
- 動作確認: `PointCloudView.Show(scan, transform)` を呼ぶだけの小さな MonoBehaviour を作り、
  実機の点群が出ることを見る（`LidarSimScene` の `PointCloudView` を流用）。
- 完了条件: 1 スキャンあたり約 1081 点、約 40 Hz、`MinRangeM`〜`MaxRangeM` 外の点は捨てている、
  受信スレッドから Unity API を呼んでいない、`Dispose` でスレッドとソケットが確実に閉じる。

### 手順 2: 合成点 `LidarInputSource : MonoBehaviour, IHeartInputSource`

- 置き場所: `Assets/Scripts/Input/LidarInputSource.cs`（名前空間 `LidarBattle.Input`）。
- `[SerializeField] LidarSettings _settings` を受け取り、`Awake` で結線する（具象の `new` はここだけ）:
  1. センサー: `HokuyoEthernetSensor(_settings)`。実機が無いときは `MockLidarSensor` に切替できる bool を 1 つ持つ。
  2. 検出器: `new BackgroundSubtractionTracker(new CircleFitTracker(ClusterRadiusM, MinClusterPoints, HeartRadiusM, FitIterations), 1440, BackgroundMarginM)`。
     必要なら `SmoothedTracker` で包む（シミュレーションでは不要だった。実機で欠落や外れ値が目立ったときだけ）。
  3. マッパー: `new RectCoordinateMapper(PhysicalMin, PhysicalMax, RotationDeg, InvertX, InvertY)`。
- `ReadTarget(current, dt)`: `TryGetLatestScan` → `TryTrack` → `ToNormalized` の順に呼び、未検出なら `current` を返す
  （SOUL はその場で止まる）。
- `OnDestroy` で `Dispose`。
- シーンへの組み込み: `GameSetupBuilder.BuildCommon` で `KeyboardInputSource` と並べて生成し、
  `SoulController._inputSource` にどちらを挿すかを選べるようにする（切替はビルダー引数か Inspector で十分）。
- 完了条件: BattleScene で SOUL が実機ハートに追従し、盤面の四隅まで届く。

### 手順 3: 背景校正の操作

- `LidarInputSource` にキー（`B`）を追加し、「ハートを外した状態で `BackgroundFrames` 枚スキャンして
  `LearnBackground`」を実行する（`TrackerEvaluator.LearnBackground` と同じ。ハートを退避させる処理は不要、人が外す）。
- 校正結果は保存しない（YAGNI）。起動ごとに校正する運用にし、下の現場手順に入れる。
- `LidarSettings` に項目を追加する（表は下記）。

### 現場手順（完了後の運用）

1. センサーを盤面の手前側に、プレイヤーが奥側に立つ向きで固定する（手はハートより奥で持つ）。
2. Unity を起動し、ハートを視野から外して `B` を押す（背景校正）。
3. ハートを画面の左下に当たる位置で `1`、右上で `2` を押す（エディタなら Ctrl+S でアセットに保存）。
4. 画面の SOUL がハートに追従することを確認して展示開始。

### 実機で調整する項目

- **円の半径 `HeartRadiusM`**: LiDAR は 1 平面を切るので、スキャン面の高さでのハート断面幅に合わせる
  （シミュレーションでは幅 9 cm に対して 0.040 m で偏りがほぼ 0）。誤差の `radial` が 0 になる値を探す。
- **背景側の人の横切り**: 視野 270° の中で人が動くと前景になる。ハートの方が近ければ問題ないが、
  困ったら「`PhysicalMin/Max` の外の点を捨てる」`IHeartTracker` デコレータを 1 つ足す。
- **手の持ち方**: 手がハートよりセンサー側に来ると手を拾う。配置で回避する。

## SCIP 2.0 の要点（UST-20LX）

通信は ASCII コマンド + 改行(`\n`)。応答はエコー → ステータス(+sum) → タイムスタンプ(+sum) → データ行 → 空行。

- `BM` … 計測（レーザ）ON。
- `GD<start4><end4><cluster2>` … 最新距離を 1 回取得。UST-20LX は `GD0000108000`（step 0〜1080, cluster 00）。
  `MD` の連続取得もあるが KISS のため `GD` ポーリング（25 ms）を採用。
- `PP` … パラメータ取得（AMIN/AMAX/AFRT/ARES/DMIN/DMAX）。接続時に読んで `LidarSettings` と食い違えばログに出す。
- `QT` … 計測 OFF。
- UST 系は SCIP 2.0 ネイティブなので `SCIP2.0` コマンドは不要（URG-04LX では必要）。

距離データ（3 文字エンコード）:
1. 各文字から `0x30` を引く（6 bit）。
2. `(c0 << 12) | (c1 << 6) | c2` で 18 bit の距離[mm]（20 m = 20000 mm は 18 bit に収まる）。
3. データは 64 文字ごとに行分割され、各行末にチェックサム 1 文字。3 文字の境界が行をまたぐので繰り越す。

step → 角度:
```
angle_rad = (step - frontStep) * (2π / angularResolution)   // UST-20LX: frontStep=540, angularResolution=1440
x = d * cos(angle_rad),  y = d * sin(angle_rad)               // 正面 = +X, 反時計回りが正
```
この規約は `SimulatedLidarSensor` と同じなので、Tracking 層の結果をそのまま比較できる。

## LidarSettings の項目

| 項目 | UST-20LX の値 | 用途 | 状態 |
|------|---------------|------|------|
| `HostName` | 例 `192.168.0.10` | Ethernet 接続先 | **追加** |
| `PortName` / `BaudRate` | — | シリアル版（URG-04LX）のみ | 既存 |
| `StartStep` / `EndStep` | `0` / `1080` | 取得 step 範囲 | 既存（値を更新） |
| `FrontStep` / `AngularResolution` | `540` / `1440` | 角度換算 | 既存（値を更新） |
| `MinRangeM` / `MaxRangeM` | `0.06` / `20.0` | 有効距離 | 既存（値を更新） |
| `PollIntervalMs` | `25` | GD ポーリング周期 | 既存 |
| `ClusterRadiusM` / `MinClusterPoints` | `0.08` / `3` | 最近点クラスタ | 既存（値を更新） |
| `BackgroundMarginM` / `BackgroundFrames` | `0.05` / `20` | 背景差分 | **追加** |
| `HeartRadiusM` / `FitIterations` | `0.040` / `5` | 円当てはめ | **追加** |
| `PhysicalMin` / `PhysicalMax` / `RotationDeg` / `InvertX` / `InvertY` | 現場で実測 | Mapping | 既存 |

## キャリブレーション（Mapping 層）

物理スキャン領域の min/max XY を実測し `RectCoordinateMapper` に渡す:
- `PhysicalMin` / `PhysicalMax`: ハートが動く実物理範囲[m]（センサー座標系）。
- センサー取付の回転・反転は `RotationDeg` / `InvertX` / `InvertY` で吸収。
- 出力は 0..1 正規化。盤面サイズ非依存（盤面は Battle 層 `BulletBoard` が決める）。

手順:
1. ハートを左下に置き、Tracking 出力の物理 XY を記録 → `PhysicalMin`。
2. 右上に置き記録 → `PhysicalMax`。
3. 上下/左右が画面と逆なら `Invert*` を立てる。

## 開発時の注意

- 通信は別スレッド。Unity API はそのスレッドから呼ばない。`LidarScan` だけをロック越しにメインスレッドへ渡す。
- 破棄が必要な資源（Socket / Thread）は `Dispose` と `OnDestroy` で確実に解放する。
- 実機が無い間は `LidarSimScene`（[lidar-simulation.md](lidar-simulation.md)）と `MockLidarSensor` で上位層を検証する。
- シリアル版（URG-04LX）だけは `System.IO.Ports` のため `URG_SERIAL_ENABLED` と API 互換性 .NET Framework が必要。
