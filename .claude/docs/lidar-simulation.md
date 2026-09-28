# LiDAR シミュレーション（実機なしでハート検出を検証する）

実機 UST-20LX が無くても「点群からハート位置を取り出せるか」を検証するためのシーンと仕組み。
Hardware 層の差し替え（`ILidarSensor`）だけで成立させ、Tracking 層はそのまま実機に持ち込む。

## シーンの作り方

`Tools > Undertale LiDAR > Build LiDAR Sim Scene`（`Assets/Editor/LidarSimSceneBuilder.cs`）で
`Assets/Scenes/LidarSimScene.unity` を生成する（上書き注意）。スプライトは Rebuild Game Setup が作る
`Assets/Art` を使うので、無ければ先にそちらを実行する。

シーン内容（1 unit = 1 m）:

| GameObject | 役割 |
|------------|------|
| `LidarSimulator` | 原点・上向き（+X 正面を z=90° 回転）。`SimulatedLidarSensor` の合成点 |
| `PointCloud` | 点群をメッシュで描画（`PointCloudView`）。原点・無回転で置く |
| `HeartTarget` | 真値。ハート形 `PolygonCollider2D`（幅 9 cm）＋奥に伸びる手 `BoxCollider2D`（6×30 cm） |
| `Environment` | 壁 3 辺（`EdgeCollider2D`, x=±0.6 / y=1.1）と置物 `Clutter`（(0.45, 0.35) の 6 cm 角） |
| `TrackerEvaluator` | 全手法を同時実行し、誤差を Canvas に表示 |

## センサーモデル（`SimulatedLidarSensor` / `SimulatedLidarSpec`）

UST-20LX の代表値を既定にした Physics2D レイキャスト。数値は Inspector で変更できる。

| 項目 | 既定値 |
|------|--------|
| 視野 / 分解能 | 270° / 0.25°（1081 step, 正面 540, 1 周 1440） |
| 距離 | 0.06〜20 m |
| 周期 | 40 Hz |
| ノイズ | 距離に白色ガウス σ=20 mm、欠落 0.5 %、1 mm 量子化 |

出力は実機と同じ `LidarScan`（角度[rad]・距離[m] の極座標、角度順）。
`Physics2D.SyncTransforms()` を呼んでから撃つので、同フレームで動かしたハートが反映される。

## 検出手法（Tracking 層、すべて `IHeartTracker`）

| 記号 | 構成 | 考え方 | 結果 |
|------|------|--------|------|
| A | `NearestClusterTracker` | 最近点を核に半径内の重心 | 壁の端や置物を拾って全滅（誤差 ~400 mm） |
| B | `SegmentCentroidTracker` | 隣接点距離で区間分割 → 幅 ≤ 上限の最近区間の重心 | ノイズで壁が細切れになり同じく全滅。斜めから見ると手がつながり幅上限で弾かれる |
| C | `BackgroundSubtractionTracker(A)` | 背景距離（ハート無し）を学習し、手前の点だけを A に渡す | 安定。ただし表面重心なので中心よりセンサー側に ~25 mm 偏る |
| D | `BackgroundSubtractionTracker(CircleFitTracker)` | 前景の最近点クラスタに既知半径の円を当てはめ中心を推定 | **採用候補**。偏りが消え平均誤差 ~10 mm |
| E | `SmoothedTracker(D)` | EMA ＋ 飛び・未検出を数フレーム保留 | 最大誤差を半減。遅延と引き換え |

- 背景差分と平滑化はデコレータ（内側の `IHeartTracker` を差し替え可能）。
- 最近点クラスタは `NearestClusterFinder` に一本化。核のクラスタが最小点数に満たなければ
  （ノイズの孤立点）次に近い核を試す。これが無いと σ=30 mm で検出率が 88 % に落ちる。
- 区間分割は `ScanSegmenter`（B 専用）。

### 解析ハーネスでの数値（同じ幾何・Lissajous 移動・2400 スキャン、単位 mm）

Unity 外で Tracking 層の実ソースを回した結果（`TrackerEvaluator` と同じ既定値、円半径 0.040）。

| 条件 | C mean | D mean | D radial | E mean | E max |
|------|--------|--------|----------|--------|-------|
| σ=20, 手＋置物あり | 27.5 | 9.9 | 0.3 | 12.4 | 37 |
| σ=0（幾何偏りのみ） | 21.3 | ~18 | +16（半径 0.045 時） | — | — |
| σ=30, 欠落 1 % | 42.0 | 21.6 | −11 | 19.3 | 78 |

A/B はどの条件でも平均 375 mm 以上（別物体を拾う）。円半径は 0.045 → +8 mm、0.035 → −9 mm の偏りで、
幅 9 cm のハートには 0.040 が最も偏りが小さい。実機ではハートの実寸に合わせて調整する。

## 評価（`TrackerEvaluator`）

真値はハートの `transform.position` をセンサー座標系に変換したもの（`LidarSimulator.ToSensorFrame`）。
新しいスキャンごとに各手法へ同じスキャンを渡し、以下を集計して画面に出す（単位 mm）。

- `det%` 検出率、`mean` / `rms` / `max` / `last` 誤差
- `radial` センサー→真値方向の符号付き平均誤差。**負ならセンサー側に偏っている**（表面重心の偏り）

背景学習はハートをセンサー真後ろ（視野外）へ退避させて 20 枚スキャンする（実機の「ハートを外して校正」に相当）。
キー: `R` 集計リセット（直前の集計は Console に出力）、`B` 背景再学習、`M` マウス追従 / 自動移動切替。
終了時にも Console に集計を出す。

## 実機へつなぐとき

`IHeartInputSource` 実装で `HokuyoUrgSensor`（または URG-Unity）→ `BackgroundSubtractionTracker(CircleFitTracker)`
（必要なら `SmoothedTracker` で包む）→ `RectCoordinateMapper` を合成する。Tracking 層のクラスはシミュレーションと
共通なので変更不要。背景学習の操作（ハートを外してキー押下）を現場手順に入れること。
