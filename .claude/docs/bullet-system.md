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
  └ 盤面外の消去・全弾消去・弾数

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
| `ShotTrack` / `ShotClip` / `ShotBehaviour` | Timeline | 発射のタイミングと組み合わせを決める |
| `BattleClock` | MonoBehaviour | 一時停止・スロー。Timeline の再生速度も合わせる |
| `SoulController` | MonoBehaviour | SOUL の移動・判定半径・被弾後の無敵時間 |
| `BattleDebug` | MonoBehaviour | Gizmo・弾数表示・イベントログ・デバッグキー |

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
- **飛び方（`MoveType`）**
  - `Linear`: 直線。
  - `Accelerate`: 加減速。速さは最小〜最大に収める。
  - `Curve`: 角速度で向きを回す。
  - `SineWave`: 毎ステップ加算せず、発射位置と経過時間から現在位置を直接計算する（誤差の蓄積を防ぐ）。
  - `Homing`: 1 秒あたりの旋回量に上限を付けて SOUL の方へ向きを変える。

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
- 乱数はクリップごとにシードを固定できる。来場者どうしでスコアを比べるため、本番の弾幕はシード固定にする。

## 見た目

- 弾の GameObject は `BulletSystem` が実行時に作る（SpriteRenderer のみ）。Prefab は使わない。
  見た目は `BulletType` のスプライト・色・サイズで決まる。
- シェーダー・Bloom・パーティクルなどの演出は、弾幕ロジックが完成してから着手する。

## デバッグ（BattleDebug）

- Gizmo: SOUL の当たり判定（赤。無敵中はマゼンタ）、グレイズ範囲（黄）、各弾の判定半径（緑）。
- 画面に現在の弾数を表示する（TextMeshPro）。
- 被弾・グレイズを Console にログ出力する。
- キー: P = 一時停止、Tab = スロー、X = 全弾消去。

## 未実装（必要になったら追加）

- 弾の寿命（今は盤面外に出たら消える）。
- スコア計算（`ScoreKeeper`）。`Hit` / `Grazed` イベントを購読する形で作る。
- LiDAR 入力（`IHeartInputSource` を実装した入力源を足す）。
