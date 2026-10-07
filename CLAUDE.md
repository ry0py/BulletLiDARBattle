# CLAUDE.md

このファイルは Claude Code がこのリポジトリで作業する際の最上位ガイドです。
詳細は `.claude/docs/` 配下の各ドキュメントを参照してください（このファイルは「索引と原則」に徹し、重複を書きません＝DRY）。
とりあえず最低限動くものを実装するものを考える。完璧は目指さない
## プロジェクト概要

弾幕 RPG 風のバトルシーン（弾幕パート）を **実機の物理ハート** で操作する Unity プロジェクト。

- **入力**: Hokuyo URG 2D LiDAR が、手で動かす 3D プリント製ハートの位置をスキャンする。
- **変換**: 極座標スキャン → 物理 XY 座標 → ゲーム盤面（Bullet Board）正規化座標。
- **出力**: ゲーム画面の SOUL（ハート）が物理ハートに追従し、弾幕を避ける。

つまり「マウス/キーボードの代わりに、現実のハートを動かして弾幕を避ける」体験を作る。

## ゲーム仕様（工大祭展示）

- 流れ: 最初にテキスト表示 → 1 分間弾幕を避け続ける → 終了時に被弾回数を表示。
- 難易度: **Easy / Mid / Hard**。
- **デスなし**。スコアは出さず、結果は被弾回数だけ（グレイズは点にしない）。
- 結果はバトル終了時に `PlayLog` がローカル（`Application.persistentDataPath`）に保存する。
  要約（開始時刻・難易度・被弾回数・デバッグモードか）は `plays.jsonl` に 1 行ずつ追記し、
  `Tools/ScoreBoard/` のスコアボード（難易度別の被弾ランキング）がブラウザで表示する。
  詳細（被弾ごとの時刻・位置・弾の種類・撃ち方、0.1 秒おきの SOUL 座標）は再生用に `replays/<id>.json` へ。
  デバッグモード（`GameSession.DebugMode`、既定 true）は記録に残すだけで、切り替え手段・再生機能はまだ無い。
  スコアボードは直前 2 プレイの「記録カード」の QR を出す。QR の URL の `#` 以降にプレイのデータを圧縮して入れ
  （形式は `Tools/ScoreBoard/card/cardcode.js`）、来場者のスマホが自分の回線で公開ページ（`card/` を GitHub Pages に
  置いたもの。URL はスコアボードの `CARD_URL`）を開き、その場で画像を描いて保存する。展示 PC はオフラインのまま。
  `cardcode.js` を変えたら公開ページも置き直す。会場のネットが悪いので
  インターネットには頼らない（GAS・DB は使わない）。スプレッドシートへは必要なら後で取り込む。

## 技術スタック

- Unity **6000.3.4f1**（Unity 6）
- Render Pipeline: **URP 17.3**
- Input: **Input System 1.17**（旧 Input Manager は使わない）
- UI: **uGUI (com.unity.ugui)** ＋ TextMeshPro
- LiDAR 連携: **URG-Unity (MediaFrontJapan)** … `com.mediafrontjapan.urg-unity`
  （UST-10LX を SCIP で接続・物体検出。実機の主経路）

## 設計原則（厳守）

このプロジェクトの全コードは以下に従う。詳細・具体例は
[.claude/docs/coding-standards.md](.claude/docs/coding-standards.md) を参照。

- **SOLID** — ハードウェア層・検出層・変換層・ゲーム層をインターフェースで分離する。
  特に LiDAR 実機への依存は `ILidarSensor` の背後に隔離し、実機が無くても開発できること。
- **DRY** — 座標変換・キャリブレーション・定数は 1 箇所に集約。重複ロジック禁止。
- **KISS** — 標準的な弾幕/スコアロジックを過度なパターンで飾らない。素直に書く。
- **YAGNI** — 「将来必要かも」で作らない。今の体験に必要な最小限のみ実装する。

## アーキテクチャ（4 層 + 設定）

データの流れは一方向： **Hardware → Tracking → Mapping → Input → Battle**

| 層 | 名前空間 | 役割 | 主要な型 |
|----|----------|------|----------|
| Hardware | `LidarBattle.LiDAR` | センサーから生スキャン取得 | `ILidarSensor`, `LidarScan`, `HokuyoEthernetSensor`, `HokuyoUrgSensor`, `MockLidarSensor`, `SimulatedLidarSensor`（Physics2D レイキャスト） |
| Tracking | `LidarBattle.Tracking` | スキャンからハート位置を検出 | `IHeartTracker`, `NearestClusterFinder`, `ScanSegmenter`, `NearestClusterTracker`, `SegmentCentroidTracker`, `BackgroundSubtractionTracker`（デコレータ）, `CircleFitTracker`, `SmoothedTracker`（デコレータ） |
| Mapping | `LidarBattle.Mapping` | 物理座標 → 正規化盤面座標 | `ICoordinateMapper`, `RectCoordinateMapper` |
| Input | `LidarBattle.Input` | SOUL の入力源を抽象化 | `IHeartInputSource`, `HeartInputSelector`（キーボード → LiDAR → カメラの順に選ぶ）, `KeyboardInputSource`, `LidarInputSource`, `CameraInputSource` |
| Battle | `LidarBattle.Battle` | 弾幕ゲーム本体 | `SoulController`, `BulletBoard`, `BattleClock`, `BulletSystem`, `Bullet`, `BulletType`（弾の種類）, `FirePattern`（飛ばし方）, `ShotTrack`/`ShotClip`（Timeline 発射）, `ScoreKeeper`, `BattleDebug`, 見た目だけの `HitFeedback`/`SoulView`/`ScannerEye`/`ScanSweep` |
| Flow | `LidarBattle.Flow` | ゲームの進行（会話→難易度選択→バトル→結果） | `SelectFlow`, `DifficultyOption`, `BattleFlow`, `GameSession` |
| UI | `LidarBattle.UI` | 会話表示・立ち絵・日本語フォント | `DialogueBox`, `DialogueTrack`/`DialogueClip`（Timeline セリフ）, `PortraitView`/`PortraitSet`（立ち絵）, `PortraitTrack`/`PortraitClip`（Timeline 表情切り替え）, `PortraitMotionTrack`/`PortraitMotionClip`（Timeline 立ち絵の動き）, `JapaneseFontApplier` |
| Audio | `LidarBattle.Audio` | BGM・SE・セリフ音の再生（常駐・自動生成） | `GameAudio` |
| Config | `LidarBattle.Config` | 接続/キャリブレーションの設定 | `LidarSettings` |
| Sim | `LidarBattle.Sim` | LiDAR シミュレーションと検出手法の評価（開発用） | `LidarSimulator`, `HeartTarget`, `PointCloudView`, `TrackerEvaluator` |

詳細は [.claude/docs/architecture.md](.claude/docs/architecture.md)。
弾幕システム（Battle 層の弾まわり）の設計は [.claude/docs/bullet-system.md](.claude/docs/bullet-system.md) に従う。
ゲーム全体の流れ（開始〜難易度選択〜バトル〜結果）は [.claude/docs/game-flow.md](.claude/docs/game-flow.md)。
LiDAR シミュレーション（実機なしで検出手法を真値と比較）は [.claude/docs/lidar-simulation.md](.claude/docs/lidar-simulation.md)。

## ディレクトリ規約

- C# スクリプト: `Assets/Scripts/<層名>/` （上表の名前空間と一致させる）
- Prefab: `Assets/Prefabs/`
- シーン: `Assets/Scenes/`（メインは `BattleScene`）
- 設定アセット: `Assets/Settings/`（`LidarSettings` や `BulletType`/`FirePattern` の `.asset` 等）
- ScriptableObject 定義クラス: `Assets/Scripts/Config/`（弾幕用の `BulletType`/`FirePattern` は例外で
  `Assets/Scripts/Battle/`。理由は bullet-system.md。立ち絵の `PortraitSet` も表情の enum ごと UI 層に置く）
- Timeline: 弾幕の発射タイミングは Timeline アセットで作る（`ShotTrack` に `ShotClip` を並べる）
- 画像素材: `Assets/Art/`（弾は `Assets/Art/Bullets/`、立ち絵は `Assets/Art/Portraits/<難易度>/`。外部のフリー素材はライセンス表記のファイルを同じフォルダに置く）
- 音源: `Assets/Resources/Audio/`（`GameAudio` が名前で読む）。生成スクリプトは `Tools/AudioGen/generate_audio.py`
- カメラ検出スクリプト: `Tools/CameraTracker/`（Unity の外で動かす Python。`Assets/` には置かない）
- スコアボード: `Tools/ScoreBoard/`（`python Tools/ScoreBoard/serve.py` で LAN に配信。同じ PC は localhost:8000）

## Unity Editor 操作のルール

MCP は使わない。シーン/Prefab/アセットの編集は、直接ファイル編集（YAML を含む）で行ってよい。
YAML を直接編集する場合は、Unity Editor で該当シーンを開いたまま上書きしないよう注意し、
削除した GameObject/Component の `fileID` への参照が残らないようにする。

## コード作業の鉄則

1. スクリプトは直接ファイル書き込みで作成する。起動中の Unity が自動コンパイルする。
2. 編集後は Unity のコンソールでコンパイルエラーが無いことを確認してから次へ進む。
3. MonoBehaviour は「層をまたぐ依存」を直接 `new` せず、インターフェース型のフィールド＋
   Inspector 注入で受け取る（DIP）。
4. `Update()` で重い処理・GC アロケーションを避ける（スキャンは別スレッド/バッファ再利用）。
5. 弾は MonoBehaviour にしない。弾の更新は `BulletSystem` に集約する（bullet-system.md）。

## 現状（2026-09-30 時点）

- Unity 6 プロジェクト。URP/2D/Input System/Timeline/URG-Unity 導入済み。
- Battle/Input 層を bullet-system.md の方針で作り直した（Timeline 発射・BulletSystem 集約・キーボード入力）。
- LiDAR 入力は `LidarInputSource`（UST-20LX, Ethernet）で接続済み。本番ではセンサーを盤面の左端に置き、
  右（盤面側）を向ける（`RotationDeg` 0）。
- LiDAR で検出する物体は 2 種類。どちらも既知の半径の円を当てはめる（`CircleFitTracker`）。
  - 円柱（今の主）: `SelectScene`/`BattleScene`、設定 `LidarSettings.asset`（`HeartRadiusM` 0.02）。
  - 以前の 3D プリントのハート: `HeartSelectScene`/`HeartBattleScene`、設定 `LidarSettings_Heart.asset`（`HeartRadiusM` 0.04）。
    円柱用シーンのコピーで、違いは設定アセットとシーン遷移先だけ。円柱用シーンを直したら
    `Tools > LiDAR Battle > Copy Scenes for Heart`（`HeartSceneCopier`）でコピーし直す（ハート用設定は上書きしない）。
  実機の点群確認は `Tools > LiDAR Battle > Build LiDAR Live Scene`。
- カメラ入力を試作中（LiDAR が実機で難しかったため）。ハートに ArUco マーカー（DICT_4X4_50）を貼り、
  `python Tools/CameraTracker/aruco_tracker.py` が検出して UDP で `CameraInputSource` に送る。
  画像全体を盤面に対応させているだけで、四隅マーカーによる盤面合わせは未実装。
- SOUL の入力は `HeartInputSelector` が **キーボード → LiDAR → カメラ (ArUco)** の順に聞き、最初に位置を出せたものを使う
  （キーボードは移動キーを押している間だけ。LiDAR/カメラは接続中かつハート検出中だけ）。どれも出せなければ SOUL は止まる。
  入力源どうしは Inspector でつながず、優先順は `HeartInputSelector` のコードで固定。
- LiDAR シミュレーション（`LidarSimScene`）を追加。UST-20LX 相当のレイキャスト点群＋白色ノイズで、検出手法 A〜E を真値と比較できる。シーンは `Tools > LiDAR Battle > Build LiDAR Sim Scene`（`Assets/Editor/LidarSimSceneBuilder.cs`）で生成する。
- ゲームの流れ（game-flow.md）を最低限実装。シーン・弾アセット・難易度別 Timeline は
  `Tools > LiDAR Battle > Rebuild Game Setup`（`Assets/Editor/GameSetupBuilder.cs`）で生成する（上書き注意）。
- 見た目は 2 種類あり、シーンもアセットも分けている。弾幕の中身（Timeline の配置・FirePattern）は共通。
  - 元の版（黒背景・白枠。自機は 2026-10-07 に赤ハートから白ふちの灰色の丸に変更）: `SelectScene`/`BattleScene`。`Tools > LiDAR Battle > Rebuild Game Setup`（`GameSetupBuilder`）。
  - 独自の「スキャナー」版（**2026-10-05 から開発停止中**。機能追加・修正は元の版だけに行い、スキャナー版の
    ビルダー・シーン・アセットは触らない。共用コードを変えるときも、スキャナー版のコンパイルが通る範囲にとどめる）:
    `ScannerSelectScene`/`ScannerBattleScene`。`Tools > LiDAR Battle > Rebuild Scanner Setup`
    （`Assets/Editor/ScannerSetupBuilder.cs`）。紺色の背景にシアンの方眼、盤面の上の敵「スキャナー」が扇ビームで盤面を掃く。
    SOUL は琥珀色のハートで、輪がグレイズ範囲。アセットは `Assets/Art/Scanner/`・`Assets/Settings/Scanner/`・`Assets/Timelines/Scanner/`。
    弾は `Assets/Art/Bullets/` の素材（Kenney, CC0）を着色し、Bloom/Vignette（`Assets/Settings/Scanner/ScannerVolume.asset`）で発光させる。
- 結果（2026-10-08）: スコアはやめて被弾回数だけ（`ScoreKeeper`）。被弾回数はドキドキ感のためプレイ中は出さず、終了時の会話で「被弾は ○ 回でした」とだけ出す。バトル中の左下は今の難易度。
- 運用ショートカット（`Flow/OperatorShortcuts`、キー 5 秒長押し・表示なし。円柱用・ハート用それぞれの組の中で移る）: バトルは R でやり直し・P で選択へ、
  選択は E/M/H で難易度を選んでバトルへ・L で `LidarLiveScene` へ、`LidarLiveScene` は P で直前の選択へ。
- 音（2026-10-08）: `GameAudio` が鳴らす。BGM はセレクト用（ポップ）とバトル用（ポップ＋緊迫感、全難易度共通）。
  SE は被弾・選択中（ゲージが溜まるほど高く）・難易度決定・バトル開始・敵のセリフ音（アンダーテール風の「ポポポ」。
  ピッチは Easy 1.0 / Medium 0.85 / Hard 0.7）。音源はすべて `python Tools/AudioGen/generate_audio.py` で合成した自作。
- 立ち絵（2026-10-08、素材は依頼中。今は仮の画像を割り当て済み）: 会話ボックスの左に `PortraitView`。表情はベース・口あけ・技・笑顔・負け顔の 5 種で、
  難易度ごとに `Assets/Settings/Portraits/{Easy,Medium,Hard}.asset`（`PortraitSet`）に画像を割り当てる。詳細は game-flow.md。
- 弾幕の「技」を 8 秒の Timeline として 23 個用意した（`Assets/Timelines/Attacks/`、`Tools > LiDAR Battle > Build Attack Library`）。
  難易度別 Timeline にはまだ組み込んでいない（将来ランダムに再生するかも）。詳細は bullet-system.md。
- 未実装: 弾の寿命。
