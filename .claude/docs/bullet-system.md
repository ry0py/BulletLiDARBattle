# 弾幕システム設計

Battle 層のうち、弾の発射・移動・判定に関する設計方針。CLAUDE.md の設計原則（特に KISS / YAGNI）を前提とする。

## 全体像：Timeline で撃ち、BulletSystem が管理する

```text
Timeline（いつ・何を・どう撃つか）
  └ ShotTrack（バインド先 = BulletSystem）
      └ ShotClip … BulletType + FirePattern + 発射位置 + 発射間隔 + シード
            │ BulletSystem.Fire(...)
            ▼
BulletSystem（撃った後のすべて）
  ├ 弾ごとの FirePattern を読んで動かす
  ├ 弾ごとの BulletType を読んで当たり判定の大きさを知る
  ├ 被弾・グレイズの判定とイベント通知
  └ 盤面外の消去・全弾消去

Bullet … 状態を持つだけ（位置・角度・速さ・経過時間・BulletType/FirePattern への参照）
```

- **弾 ＝ 弾の種類（`BulletType`）× 飛ばし方（`FirePattern`）**。この 2 つを `ShotClip` が組み合わせて撃つ。
- 弾幕を作る作業は **Timeline にクリップを並べるだけ**。コードの追加は不要。
  - クリップを横に並べる → 途中で弾の種類・飛ばし方を切り替える。
  - トラックを縦に並べる → 複数の弾を同時に撃つ。
  - クリップの長さ ＝ 撃ち続ける時間。発射間隔 0 ならクリップの頭で 1 回だけ撃つ。
- Timeline 画面でシークしただけでは撃たない（Play 中のみ発射）。

## クラスと役割

| クラス | 種別 | 役割 |
| --- | --- | --- |
| `BulletType` | ScriptableObject | 弾の種類。スプライト・色・表示サイズ・当たり判定半径 |
| `FirePattern` | ScriptableObject | 飛ばし方。撃ち方（`ShotShape`）と飛び方（`MoveType`）を enum で選び、パラメータを持つ |
| `Bullet` | C# クラス | 弾 1 発の状態。自分では何もしない（MonoBehaviour にしない） |
| `BulletSystem` | MonoBehaviour | 弾の更新を行う唯一のクラス。プール・移動・判定・消去・イベント |
| `ShotTrack` / `ShotClip` / `ShotBehaviour` | Timeline | 発射のタイミングと組み合わせを決める。`ClearClip` はクリップの頭で盤面の弾を全部消す |
| `BattleClock` | MonoBehaviour | 一時停止・スロー。Timeline の再生速度も合わせる |
| `SoulController` | MonoBehaviour | SOUL の移動・判定半径・被弾後の無敵時間 |
| `ScoreKeeper` / `ScoreView` | MonoBehaviour | 被弾回数を数える / 被弾回数を画面に出す（元の版では使わない。プレイ中は出さず終了時の会話でだけ出す。グレイズは SOUL の見た目とログだけ） |
| `HitFeedback` | MonoBehaviour | 被弾時のカメラ振動と画面の赤フラッシュ（見た目だけ） |
| `BattleDebug` | MonoBehaviour | Gizmo・イベントログ・デバッグキー |

## 撃ち方と飛び方（FirePattern）

種類ごとにクラスを分けず、**enum ＋ switch** で書く（KISS）。新しい種類は enum に値を 1 つ足し、
switch に分岐を 1 つ足す。Inspector には全パラメータが並ぶが、使うものだけ設定すればよい。

- 角度は度数法（0° = 右、反時計回り）。弾の向きと速度は「角度＋速さ」で持つ。
- **撃ち方（`ShotShape`）**
  - `Ring`: 全方位。`Count` 発を等間隔に。
  - `Aimed`: 自機狙いの扇形。発射した瞬間の SOUL 位置を 1 回だけ参照する（追いかけるのは `Homing`）。
    **発射数そのもので奇数・偶数が決まる**（奇数 = 中央の 1 発が SOUL を向く、偶数 = 両脇を通る）。
  - `Spiral`: 渦巻き。「何回目の発射か」から角度を計算するので、パターン自体は状態を持たない。
  - `Random`: ばらまき。乱数は `ShotClip` が持つ `System.Random` を使う。
  - `Arc`: 向き固定の扇形（`Aimed` の中心角を `Angle` に固定したもの）。
  - `Line`: 進む向きと直交する長さ `Width` の線上に `Count` 発を等間隔に並べ、全弾同じ向きに撃つ（壁）。
  - `RandomLine`: `Line` の線上のランダムな位置から撃つ（盤面全体に降る雨など）。
  - `GapWall`: `Line` から、線上のランダムな位置の幅 `Gap` にかかる弾を抜いた壁。すき間を探して大きく動かせる。
    すき間の位置は `GapMode` で選ぶ: `Random`（毎回ランダム）/ `Alternate`（線の両端寄りに交互。大きく動かす）/
    `Wave`（1 回ごとに `SpiralStep` 度ずつ正弦波でずらす。細かい間隔で撃つと、すき間がつながってうねる一本道になる）。
  - `Converge`: 発射位置は使わず、SOUL を中心とする半径 `Radius` の円上に `Count` 発並べて内向きに撃つ（縮む輪）。
    1 回ごとに `SpiralStep` だけ回す。同じ場所に居続けると当たる。
  - `Fill`: 盤面を間隔 `Width` の格子で埋める。発射位置を中心とする `HoleSize` の長方形（安置）には置かない。
  - 撃ち方は「発射位置からのずれ＋発射角」の組を返す（`GetShots`）。ずれを使うのは `Line`/`RandomLine`/`GapWall`/`Converge`/`Fill` だけ。
- **飛び方（`MoveType`）**
  - `Linear`: 直線。
  - `Accelerate`: 加減速。速さは最小〜最大に収める。
  - `Curve`: 角速度で向きを回す。
  - `SineWave`: 毎ステップ加算せず、発射位置と経過時間から現在位置を直接計算する（誤差の蓄積を防ぐ）。
  - `Homing`: 1 秒あたりの旋回量に上限を付けて SOUL の方へ向きを変える。
  - `StopAndAim`: `StopTime` 秒かけて止まり、`WaitTime` 秒待ってから 1 回だけ SOUL へ向け直して `MaxSpeed` で飛ぶ。
    状態は持たず、経過時間が再発射の時刻をまたいだステップで向け直す。
  - `Gravity`: 下向きに `Gravity` で加速する（放物線）。
- **寿命（`Lifetime`）**: 飛び方に関係なく、撃ってから `Lifetime` 秒で消える（0 = 盤面の外に出るまで消えない）。
  消える前の `BulletSystem._fadeSeconds` 秒で薄くなる（薄くなっている間も当たる）。一定秒で消えるホーミングなどに使う。
  寿命のある弾は盤面の外に出ても消さない（盤面の外から入ってくる `Converge` の大きな輪を途中で消さないため）。
- **切り替え（`Next`/`NextTime`）**: 撃ってから `NextTime` 秒で飛び方を `Next` に切り替える（位置と向きはそのまま、速さ・寿命は切り替え先のもの）。
  花火（`Fireworks`）の破片が放物線のあとホーミングになるのに使う。

## 技のライブラリ（Timeline）

- 1 つの「技」は `AttackLibraryBuilder` の関数で、「開始時刻と難易度を受け取って 8 秒分のクリップを並べる」。難易度で発射間隔
  （Easy ×1.7・Hard ×0.75）や、技によっては弾の数・速さ・すき間の幅が変わる（難易度で変わる FirePattern は名前の末尾に難易度が付く）。
  `Tools > LiDAR Battle > Build Attack Library` は技を 1 つずつ 8 秒の Timeline（Medium）にする（`Assets/Timelines/Attacks/`、確認用）。
  技用の FirePattern は `Assets/Settings/Bullets/Attacks/`。弾の種類は White/Yellow に Big（大きい赤）を足した。
- 上からだけ撃つと盤面の下に居座れてしまうので、本番では盤面の全体を動き回らせる技だけを使う（`AttackLibraryBuilder.BattleAttacks`）:
  `SafeZone`（安置。半透明の緑のシートに「あんぜん」を先に出し、少したつとシートの外を `Fill` の弾で埋める（1 秒で消える）。
  安置は毎回離れた場所へ移る。シートは盤面の子の `SafeZoneView` で、Timeline の `SafeZoneTrack`/`SafeZoneClip` がクリップの間だけ出す。見た目だけで当たり判定は無い）・
  `Corridor`/`CorridorSide`（すき間がうねる壁を細かく流して一本道にする。上から／右から）・
  `HomingFade`（四隅から一定秒で消えるホーミング）・`Converge`（大きな輪がゆっくり縮む）・`GapWalls`/`GapWallsSide`（すき間のある壁。
  Easy はすき間がランダム、Medium/Hard は端寄りに交互に空いて大きく動かされる）・
  `Fireworks`（花火の破片が少したつとホーミングになり、一定秒で消える）・`FreezeAim`（上下から交互に、止まってから自機へ撃ち直す扇）・
  `HomingSwarm`（ゆっくりのホーミング。Easy は一定秒で消える）・`SideWalls`（左右から弾の列）・`CurveLattice`（曲がる輪の格子）。
  残りの `AccelBloom`/`AimedFan`/`AimedStream`/`ArcCurtain`/`BigBalls`/`WaveCurtain` は本番では使っていない。
- 本番の Timeline（`BattleTimelineBuilder`、`Tools > LiDAR Battle > Build Battle Timelines`）: 難易度ごとに 4 本
  （`Assets/Timelines/Battle/Easy1〜4`・`Medium1〜4`・`Hard1〜4`）。1 本は 60 秒で、3 秒から技を 8 秒ずつ 7 個並べ、セリフと立ち絵の動きを足す。
  並びは `BattleTimelineBuilder.Plans` の表（4 本分。全難易度で共通で、難易度で変わるのは技の中身だけ）。
  4 本どれにも安置（`SafeZone`）と道が入る。道は 1 本の中で縦（`Corridor`）か横（`CorridorSide`）の 1 種類だけ。
  残りの 5 枠は 1 本ごとに使う技を変える（並べ替えだけにしない）。技は重ねない（道・安置・壁・渦はほかと重ねると確実によけられなくなり、
  縮む輪とホーミングを一緒にしてもよけられない）。
  技が切り替わる 0.2 秒前に盤面の弾を全部消す（ShotTrack「Clear」に置いた `ClearClip`）。前の技の弾やホーミングが次の技に残らない。
  ShotTrack は時間の重ならない技どうしで使い回す（`AttackLibraryBuilder.Lanes`）。作り直しても GUID は変わらないので、
  本数を変えなければシーンは作り直さなくてよい（本数を変えたら `Rebuild Game Setup`）。`BattleFlow` が毎回ランダムに 1 本選ぶ（game-flow.md）。
- 技の確認は弾幕テストシーン（`Assets/Scenes/Test/BattleTestScene`）で行う。選択シーンでデバッグモードのときに T を 2 秒長押しすると移る。
  左上のドロップダウン（マウスで操作）で技を選ぶとすぐ再生し、終わったら同じ技を繰り返す（一覧は本番の 12 本 → 本番で使う技 → 残りの技の順）。
  左下に技名と 1 回ごとの被弾回数。P 5 秒長押しで選択へ戻る。記録は残さない。技ごとの被弾回数は Console にも `[BattleTest]` で出る。シーンは `Tools > LiDAR Battle > Build Battle Test Scene`
  （`BattleTestSceneBuilder`）が BattleScene をコピーして BattleFlow を `BattleTestFlow` に差し替えて作る。BattleScene や技を作り直したら作り直す。

## 当たり判定・グレイズ・被弾

- Physics2D は使わない。円同士の中心距離の 2 乗と、半径の和の 2 乗を比較する。
- 半径: 弾は `BulletType.Radius`、SOUL は `SoulController` の `HitRadius` / `GrazeRadius`。
  当たり判定は見た目より小さく、グレイズは当たり判定より大きくする。
- グレイズは弾 1 発につき 1 回だけ。同じステップで被弾とグレイズが両方起きたら被弾を優先。
- 被弾すると SOUL が一定時間無敵になり、その間は被弾判定をしない。弾は消えずに残る。
- 被弾・グレイズは `BulletSystem.Hit` / `Grazed` イベントで通知する。受け手（スコア等）の処理は
  BulletSystem は知らない。

## 時間の扱い

- 時間倍率は `BattleClock` が一元管理する。`Time.timeScale` は使わない（UI・デバッグ表示を止めないため）。
  - 一時停止: SOUL・弾・Timeline をすべて止める。
  - スロー: 弾と Timeline だけ遅くする。SOUL は通常速度で動ける。
- 弾は固定ステップ（既定 1/60 秒）で進め、フレームレートに依存させない。

## データの置き場所

- `BulletType` / `FirePattern` の定義クラスは `Assets/Scripts/Battle/` に置く
  （`FirePattern` が `Bullet` を直接動かすため。Config 層に置くと Config → Battle の逆依存になる）。
- `.asset` は `Assets/Settings/` に置く。
- 乱数はクリップごとにシードを固定できる。本番の弾幕はシード固定にする（同じ Timeline なら毎回同じ。Timeline は難易度ごとに 4 本からランダム）。

## 見た目

- 弾の GameObject は `BulletSystem` が実行時に作る（SpriteRenderer のみ）。Prefab は使わない。
  見た目は `BulletType` のスプライト・色・サイズで決まる。
- 元の版は単色の円スプライト。スキャナー版（`ScannerSetupBuilder`）だけ、`Assets/Art/Bullets/` の白い素材
  （Kenney Particle Pack, CC0。同フォルダの LICENSE 参照）を `BulletType.Color` で着色し、URP の Bloom で発光させる。
  当たり判定の半径は見た目より小さいままにする（光の縁は判定に入れない）。
- スキャナー版の SOUL の輪の脈動・無敵中の点滅・グレイズ時のフラッシュは `SoulView`、敵の瞳と扇ビームは
  `ScannerEye`/`ScanSweep`。いずれも見た目だけで、ゲームの状態は持たない（`HitFeedback` と同じ扱い）。
- パーティクルや専用シェーダーは使わない（必要になったら検討）。

## デバッグ（BattleDebug）

- Gizmo: SOUL の当たり判定（赤。無敵中はマゼンタ）、グレイズ範囲（黄）、各弾の判定半径（緑）。
- 被弾・グレイズを Console にログ出力する。
- キー: P = 一時停止、Tab = スロー、X = 全弾消去。

## 未実装（必要になったら追加）

- LiDAR 入力（`IHeartInputSource` を実装した入力源を足す）。
