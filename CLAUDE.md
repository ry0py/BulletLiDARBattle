# CLAUDE.md

このファイルは Claude Code がこのリポジトリで作業する際の最上位ガイドです。
詳細は `.claude/docs/` 配下の各ドキュメントを参照してください（このファイルは「索引と原則」に徹し、重複を書きません＝DRY）。

## プロジェクト概要

Undertale のバトルシーン（弾幕パート）を **実機の物理ハート** で操作する Unity プロジェクト。

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
| Hardware | `UndertaleLiDAR.LiDAR` | センサーから生スキャン取得 | `ILidarSensor`, `LidarScan`, `HokuyoUrgSensor`, `MockLidarSensor` |
| Tracking | `UndertaleLiDAR.Tracking` | スキャンからハート位置を検出 | `IHeartTracker`, `NearestClusterTracker` |
| Mapping | `UndertaleLiDAR.Mapping` | 物理座標 → 正規化盤面座標 | `ICoordinateMapper`, `RectCoordinateMapper` |
| Input | `UndertaleLiDAR.Input` | SOUL の入力源を抽象化 | `IHeartInputSource`, `KeyboardInputSource` |
| Battle | `UndertaleLiDAR.Battle` | 弾幕ゲーム本体 | `SoulController`, `BulletBoard`, `BattleClock`, `BulletSystem`, `Bullet`, `BulletType`（弾の種類）, `FirePattern`（飛ばし方）, `ShotTrack`/`ShotClip`（Timeline 発射）, `BattleDebug` |
| Config | `UndertaleLiDAR.Config` | 接続/キャリブレーションの設定 | `LidarSettings` |

詳細は [.claude/docs/architecture.md](.claude/docs/architecture.md)。
弾幕システム（Battle 層の弾まわり）の設計は [.claude/docs/bullet-system.md](.claude/docs/bullet-system.md) に従う。
ゲーム全体の流れ（開始〜難易度選択〜バトル〜結果）は [.claude/docs/game-flow.md](.claude/docs/game-flow.md)。

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
- LiDAR 入力は未接続。Hardware/Tracking/Mapping 層は残っており、`IHeartInputSource` の実装を足して接続する予定。
- `BattleScene` は旧スクリプトの Missing Script が残っており、新クラスでの組み直しが必要。
- 未実装: スコア（`ScoreKeeper`、`BulletSystem.Hit`/`Grazed` を購読）、弾の寿命。
