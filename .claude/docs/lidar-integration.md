# Hokuyo LiDAR 実機連携

## 結論（2026-09-28 時点）

- 対象機は **HOKUYO UST-20LX**（Ethernet, TCP ポート 10940, 270° / 0.25° = 1081 step, 40 Hz）。
- 経路は **自作の Ethernet SCIP センサー → Tracking 層 → `RectCoordinateMapper`**。
  検出ロジックは [lidar-simulation.md](lidar-simulation.md) で検証済みの
  `CircleFitTracker` を使う（2026-10-09 に背景差分をやめ、円らしさの採点で円柱を選ぶ方式に変更。下の「検出の方式」）。
- **URG-Unity パッケージ（`com.mediafrontjapan.urg-unity`）は使わない。** 生スキャン
  （`SCIPClient.Capture.Distances`）が `internal` で外から読めず、公開されるのはパッケージ独自の物体検出結果
  （`SCIPScanPlane.ObjectLocalPositions`, 表面重心）だけなので、検証した検出器チェーンを通せない。
- `HokuyoUrgSensor`（シリアル / URG-04LX 用, `URG_SERIAL_ENABLED` 時のみ有効）は USB 機を使う場合の
  フォールバックとして残す。UST-20LX には使えない（シリアル専用）。

手順 1（センサー実装）は実装済み（`HokuyoEthernetSensor` / `ScipScanParser`）。実機確認は
`Tools > LiDAR Battle > Build LiDAR Live Scene` で `LidarLiveScene` を生成して再生する（`LidarLiveView`: 点群＋検出マーカー）。
手順 2・3 も実装済み: `LidarInputSource`（Select/Battle 両シーンの SOUL 入力）。接続できない / `UseLidar=false` / 見失い中は
位置を出さず、`HeartInputSelector` が次の入力源（カメラ）に回す（キーボードは押している間だけ最優先）。接続はシーンをまたいで static に保持する。
キー: `1` 今のハート位置を画面左下 (`PhysicalMin`) / `2` 右上 (`PhysicalMax`) / `F1` 状態表示。
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
  2. 検出器: `LidarTrackerChain.Create(settings, out region)`（`LidarLiveView` と共通）=
     `SmoothedTracker(RegionFilterTracker(CircleFitTracker(...)))`。
     `RegionFilterTracker` は検出した中心が盤面（半径ぶん広げる）の外なら未検出にする（点は削らない）。
     `SmoothedTracker` は One Euro フィルタ＋遊び（`DeadbandM`）。実機で止まっている円柱の震えが幅 約 20 mm → 約 1 mm になった（2026-10-09）。
     遊びを大きくすると、ゆっくり動かしたときに止まる・進むを繰り返してカクつく（4 mm では 1 cm/s で 6 割のスキャンが止まった）ので 2 mm。
  - 位置は 1 秒に約 34 回しか来ず、画面（60〜100 fps 以上）とは間隔がそろわない。`LidarInputSource` は届いた位置の間を
    毎フレーム補間して SOUL に渡す（1 スキャンぶん 約 30 ms 遅れる代わりにカクつかない）。
  3. マッパー: `new RectCoordinateMapper(PhysicalMin, PhysicalMax, RotationDeg, InvertX, InvertY)`。
- `ReadTarget(current, dt)`: `TryGetLatestScan` → `TryTrack` → `ToNormalized` の順に呼び、未検出なら `current` を返す
  （SOUL はその場で止まる）。
- `OnDestroy` で `Dispose`。
- シーンへの組み込み: `GameSetupBuilder.BuildCommon` で `KeyboardInputSource` と並べて生成し、
  `SoulController._inputSource` にどちらを挿すかを選べるようにする（切替はビルダー引数か Inspector で十分）。
- 完了条件: BattleScene で SOUL が実機ハートに追従し、盤面の四隅まで届く。

### 検出の方式（2026-10-09〜）

フィールドを壁で囲み、`MaxRangeM` 0.4 m より遠い点はセンサーの段階で捨てる。背景差分は使わない。
壁の点はノイズ（実機で σ 6 mm 程度、センサーのすぐ近くでは厚さ 2 cm ほどの帯）が半径 2 cm に比べて大きく、
1 枚のスキャンの曲がり具合だけでは円柱と平らな壁を見分けられない。そこで `CircleFitTracker` は:

1. 各点（1 点おき）を円柱の手前の表面とみなし、近くの 5 点の距離の中央値 + 半径の所に円を仮置きする。
2. 点数 = 円の幅に入るビームのうち、円の手前の表面から `CircleToleranceM` 以内に当たった割合
   − 円の左右の脇（見かけの幅の 1.5 倍まで）がふさがっている割合の悪い側。
   独立した円柱の脇は奥まで抜けるが、壁は脇にも同じくらいの距離で続く。スキャン範囲の外にはみ出す脇はふさがり扱い。
3. 点数が最高の円を採り（`MinCircleScore` 未満なら未検出）、表面に当たった点だけから中心を求め直す（`FitIterations` 回）。
   向きは点の角度の平均、距離は各点が円周上にあるとしたときの中心までの距離の平均。
   円の当てはめ（Gauss-Newton）より止まっているときのぶれが小さい（実機で 左右 σ 4.4 → 0.9 mm、奥行き σ 2.9 → 2.5 mm）。
   奥行きのぶれ（σ 2.5 mm）はスキャンごとに独立に出るセンサーのノイズで、1 枚の中ではこれ以上減らせなかった。

実機（円柱あり 40 枚）・円柱なし・合成した円柱 80 か所で確かめ、外れは 0。未検出になるのは円柱が横の壁に触れているときだけ。
数を数えるだけの RANSAC（円に乗った点の数で選ぶ）は、センサー近くの壁の帯の方が点が密なので壁を選んでしまった。

### 現場手順（完了後の運用）

1. センサーを盤面の手前側に、プレイヤーが奥側に立つ向きで固定する（手はハートより奥で持つ）。
2. ハートを画面の左下に当たる位置で `1`、右上で `2` を押す（エディタなら Ctrl+S でアセットに保存）。
3. 画面の SOUL がハートに追従することを確認して展示開始。

### 実機で調整する項目

- **円の半径 `HeartRadiusM`**: LiDAR は 1 平面を切るので、スキャン面の高さでのハート断面幅に合わせる
  （シミュレーションでは幅 9 cm に対して 0.040 m で偏りがほぼ 0）。誤差の `radial` が 0 になる値を探す。
- **フィールドの外の人**: `MaxRangeM` 0.4 m より遠い点は捨てる。盤面の外で円柱と判定されたものは `RegionFilterTracker` が捨てる。
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
| `MinRangeM` / `MaxRangeM` | `0.06` / `0.4` | 有効距離（フィールドの外は捨てる） | 既存（値を更新） |
| `PollIntervalMs` | `25` | GD ポーリング周期 | 既存 |
| `CircleToleranceM` / `MinCirclePoints` / `MinCircleScore` | `0.01` / `5` / `0.3` | 円らしさの採点（上の「検出の方式」） | 2026-10-09 に最近点クラスタ・背景差分から変更 |
| `FilterMinCutoffHz` / `FilterBeta` / `FilterDerivCutoffHz` / `DeadbandM` | `0.5` / `10` / `0.5` / `0.002` | 平滑化（One Euro＋遊び） | 2026-10-09 に移動平均から変更 |
| `HeartRadiusM` / `FitIterations` | `0.020`（円柱）/ `2` | 円の半径・中心を求め直す回数 | 既存 |
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
