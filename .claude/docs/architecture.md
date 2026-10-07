# アーキテクチャ詳細

データは一方向に流れる。各層は **下位層のインターフェースだけ**に依存し、具象実装には依存しない（DIP）。

```
[物理ハート]
    │  手で動かす
    ▼
┌──────────────────────────────────────────────────────────────┐
│ Hardware 層 (LidarBattle.LiDAR)                            │
│   ILidarSensor ── HokuyoUrgSensor (実機 / SCIP 2.0)          │
│              └─── MockLidarSensor (マウス駆動 / 開発用)       │
│   出力: LidarScan（極座標 measurement の配列）               │
└──────────────────────────────────────────────────────────────┘
    │  LidarScan
    ▼
┌──────────────────────────────────────────────────────────────┐
│ Tracking 層 (LidarBattle.Tracking)                        │
│   IHeartTracker ── NearestClusterTracker                     │
│   出力: 物理 XY 座標 (メートル, Vector2) ＋ 検出成否         │
└──────────────────────────────────────────────────────────────┘
    │  物理 Vector2
    ▼
┌──────────────────────────────────────────────────────────────┐
│ Mapping 層 (LidarBattle.Mapping)                          │
│   ICoordinateMapper ── RectCoordinateMapper                  │
│   出力: 正規化座標 (0..1, Vector2)  ※盤面非依存             │
└──────────────────────────────────────────────────────────────┘
    │  正規化 Vector2
    ▼
┌──────────────────────────────────────────────────────────────┐
│ Input 層 (LidarBattle.Input)                              │
│   IHeartInputSource ── KeyboardInputSource                   │
│                   ├─── LidarInputSource (上3層を合成)        │
│                   └─── CameraInputSource (UDP 受信)          │
│   出力: 目標位置の正規化座標 (0..1)                          │
└──────────────────────────────────────────────────────────────┘
    │  正規化 Vector2
    ▼
┌──────────────────────────────────────────────────────────────┐
│ Battle 層 (LidarBattle.Battle)                            │
│   SoulController ── 正規化座標を BulletBoard 内の実座標へ     │
│   BulletBoard ──── 盤面の矩形境界（唯一の座標基準）         │
│   BattleClock ──── 一時停止・スロー                          │
│   Timeline(ShotClip) ─ いつ・何を・どう撃つか               │
│   BulletSystem ─── 撃った後の弾の移動・判定・イベント        │
└──────────────────────────────────────────────────────────────┘
```

## 層の責務（単一責任 / SRP）

### Hardware 層
- 唯一の責務: センサーから生スキャンを取得し `LidarScan` として供給する。
- **座標変換やゲームロジックを持たない。** 単位はセンサー固有（mm 距離・step 角度）。
- `HokuyoUrgSensor` は SCIP 2.0 をバックグラウンドスレッドで通信（[lidar-integration.md](lidar-integration.md)）。
- `MockLidarSensor` は実機なしで上位層を開発・デモするための差し替え。**LSP**: どちらも
  `ILidarSensor` 契約（接続→`TryGetLatestScan` で最新スキャンをプル→切断）を完全に満たす。
- スキャン取得は **プル型**（メインスレッドが `TryGetLatestScan` を呼ぶ）。Hokuyo は別スレッドで
  受信した最新スキャンをロック越しに渡すため、Unity API をスレッド外から触らずに済む。

### Tracking 層
- 唯一の責務: スキャンの中から「ハート（最も近い物体クラスタ）」を 1 点に特定する。
- ノイズ点の除去・クラスタリング・重心計算のみ。盤面やゲームを知らない。
- 実装は「背景差分（デコレータ）→ 最近点クラスタ → 円当てはめ」の合成が本命。比較と根拠は [lidar-simulation.md](lidar-simulation.md)。

### Mapping 層
- 唯一の責務: 物理座標 ↔ 正規化座標 (0..1) の相互変換。
- キャリブレーション（物理スキャン範囲の min/max）はここ **だけ** が保持（DRY）。

### Input 層
- 唯一の責務: 「SOUL の目標位置（正規化座標）を毎フレーム供給する」抽象 `IHeartInputSource`。
  キーボードのような相対入力も、今の位置に移動量を足して目標位置として返す。
- `KeyboardInputSource` は Input System で矢印キー / WASD を読む。
- `LidarInputSource` は Hardware+Tracking+Mapping を **合成するだけ**のクラス。
- `CameraInputSource` は、Unity の外で動く `Tools/CameraTracker/aruco_tracker.py` が検出した
  ArUco マーカーの画像内位置 (0..1) を UDP で受け取り、`RectCoordinateMapper` で盤面座標にする。
  検出を Python (OpenCV) に任せるのは、Unity に ArUco 検出の手段が無いため。
  トラッカーが止まっていれば次の入力源 (LiDAR → キーボード) に任せる。
- これにより Battle 層は **入力源を一切知らずに**動く（OCP: 入力源追加は新クラスのみ）。

### Battle 層
- 弾幕ゲームの本体。`BulletBoard` が座標の唯一の基準（SOUL も弾もこの矩形内）。
- 弾の発射は Timeline、発射後の管理は `BulletSystem`。詳細は [bullet-system.md](bullet-system.md)。
- 全体をまとめる司令役クラスは置かず、参照は Inspector で直接つなぐ。

### Audio 層
- 唯一の責務: BGM・SE・セリフ音を鳴らす。`GameAudio` 1 つだけ。
- 起動時に自動生成されてシーンをまたいで残り（BGM を途切れさせない）、`Resources/Audio/` の WAV を読む。
  呼び出し側は静的メソッド（`PlayBgm`/`PlaySe`/`PlaySelectTick`/`PlayVoice`）を呼ぶだけで、シーン側の配線は不要。
  例外的に Inspector 注入にしていないのは、音を足すたびに全シーン（円柱用・ハート用）を組み直さずに済ませるため（KISS）。
- 何をいつ鳴らすかは呼ぶ側が決める（Flow が BGM・決定音・開始音・被弾音、`DialogueBox` がセリフ音、
  `DifficultyOption` が選択中の音）。セリフ音のピッチは難易度ごとに `BattleFlow._voicePitches` で決める。
- 音源は `Tools/AudioGen/generate_audio.py` で合成した自作チップチューン（外部素材なし）。差し替えは同名の WAV を置くだけ。

## 依存性注入の方針

- MonoBehaviour 同士の参照は Inspector 注入（`[SerializeField]`）を基本とする。
- 非 MonoBehaviour（センサー実装・トラッカー・マッパー）は、それを使う
  InputSource の `Awake` で生成・結線する。具象の `new` は合成点に閉じ込める。
- `LidarSettings`（ScriptableObject）で接続情報とキャリブレーションを外部化し、
  コード再ビルド無しで現場調整できるようにする。

## なぜこの構成か（KISS/YAGNI の判断記録）

- 層は **4 つだけ**。これ以上分けても今の体験に価値が無い（YAGNI）。
- 抽象（interface）は「差し替えが現実に起きる所」にだけ置いた:
  - センサー（実機⇔モック）… 必須
  - 入力源（LiDAR⇔キーボード）… 必須（デモ・デバッグ）
- 弾幕の撃ち方・飛び方は interface にせず enum ＋ switch にした（KISS。種類は 1 ファイルで見渡せる）。
- トラッカー/マッパーも interface 化したが、実装は各 1 つ。差し替え予定が無ければ
  将来 interface を畳んでも良い（過剰な抽象を増やさない方針）。
