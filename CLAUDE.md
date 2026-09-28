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

- 流れ: 最初にテキスト表示 → 1 分間弾幕を避け続ける → 終了時にスコアを表示。
- 難易度: **Easy / Mid / Hard**。
- **デスなし**。グレイズで加点し、被弾数に応じてスコア倍率が下がる。
- スコアは会場でスプレッドシートに記録する。ゲーム側での保存・ランキング機能は作らない（YAGNI）。

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
| Input | `LidarBattle.Input` | SOUL の入力源を抽象化 | `IHeartInputSource`, `KeyboardInputSource`, `LidarInputSource` |
| Battle | `LidarBattle.Battle` | 弾幕ゲーム本体 | `SoulController`, `BulletBoard`, `BattleClock`, `BulletSystem`, `Bullet`, `BulletType`（弾の種類）, `FirePattern`（飛ばし方）, `ShotTrack`/`ShotClip`（Timeline 発射）, `ScoreKeeper`, `BattleDebug` |
| Flow | `LidarBattle.Flow` | ゲームの進行（会話→難易度選択→バトル→結果） | `SelectFlow`, `DifficultyOption`, `BattleFlow`, `GameSession` |
| UI | `LidarBattle.UI` | 会話表示・日本語フォント | `DialogueBox`, `DialogueTrack`/`DialogueClip`（Timeline セリフ）, `JapaneseFontApplier` |
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
  `Assets/Scripts/Battle/`。理由は bullet-system.md）
- Timeline: 弾幕の発射タイミングは Timeline アセットで作る（`ShotTrack` に `ShotClip` を並べる）

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

## 現状（2026-09-28 時点）

- Unity 6 プロジェクト。URP/2D/Input System/Timeline/URG-Unity 導入済み。
- Battle/Input 層を bullet-system.md の方針で作り直した（Timeline 発射・BulletSystem 集約・キーボード入力）。
- LiDAR 入力は `LidarInputSource`（UST-20LX, Ethernet）で接続済み。繋がらないときはキーボードにフォールバック。
  実機の点群確認は `Tools > LiDAR Battle > Build LiDAR Live Scene`。
- LiDAR シミュレーション（`LidarSimScene`）を追加。UST-20LX 相当のレイキャスト点群＋白色ノイズで、検出手法 A〜E を真値と比較できる。シーンは `Tools > LiDAR Battle > Build LiDAR Sim Scene`（`Assets/Editor/LidarSimSceneBuilder.cs`）で生成する。
- ゲームの流れ（game-flow.md）を最低限実装。シーン・弾アセット・難易度別 Timeline は
  `Tools > LiDAR Battle > Rebuild Game Setup`（`Assets/Editor/GameSetupBuilder.cs`）で生成する（上書き注意）。
- 未実装: スコア計算式（`ScoreKeeper` は仮の式）、弾の寿命。
