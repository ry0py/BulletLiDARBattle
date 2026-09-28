# コーディング規約（SOLID / DRY / KISS / YAGNI）

このプロジェクト固有の適用方針。一般論ではなく「ここではこうする」を書く。

## SOLID

### S — 単一責任
- 1 クラス = 1 責務。Hardware 層クラスにゲームロジックを書かない、等（層境界＝責任境界）。
- 「〜Manager」「〜System」が肥大化したら分割の合図。

### O — 開放/閉鎖
- 拡張は **新クラス追加**で行い、既存クラスは編集しない。
  - 新しい弾幕 → Timeline にクリップを並べる（コード変更なし）。撃ち方・飛び方の種類追加は
    `FirePattern` の enum と switch に 1 つずつ足す（KISS を優先した例外。bullet-system.md）。
  - 新しい入力 → `IHeartInputSource` 実装を追加。`SoulController` は触らない。

### L — リスコフ置換
- `MockLidarSensor` は `HokuyoUrgSensor` と完全に交換可能であること。
  接続状態・イベント発火順・例外契約を同じにする。テストはモックで成立させる。

### I — インターフェース分離
- インターフェースは小さく保つ。`ILidarSensor` は「接続/切断/スキャン通知」だけ。
  キャリブレーションやゲーム用 API を混ぜない。

### D — 依存性逆転
- 上位層は下位層の **interface** に依存する。具象生成は合成点（各 InputSource の `Awake`）に閉じ込める。
  MonoBehaviour 参照は `[SerializeField]` で注入。

## DRY
- 座標変換は `Mapping` 層に一本化。Battle 側で物理単位の再計算を書かない。
- マジックナンバー禁止。盤面サイズ・HP・速度は `[SerializeField]` か ScriptableObject へ。
- SCIP のエンコード/デコードは 1 箇所（`HokuyoUrgSensor`）に閉じる。

## KISS
- まず素直な実装。`Update()` の単純なループで足りるなら状態機械を持ち込まない。
- 既製機能を使う（uGUI, Input System, Coroutine/Time）。車輪の再発明をしない。

## YAGNI
- 「将来の汎用化」のための引数・設定・抽象を先回りで作らない。
- 必要になった時に追加する。未使用の public API を残さない。

## Unity 固有

- **Input System のみ**使用（旧 `Input.GetAxis` 等は使わない）。
- `Update()` 内で `GetComponent`/`Find`/`new` を毎フレーム呼ばない（キャッシュする）。
- LiDAR 通信は **メインスレッドを止めない**（別スレッド + メインスレッドへ受け渡し）。
  Unity API はメインスレッド外から呼べないため、スキャンデータのみ受け渡す。
- 例外は握りつぶさず `Debug.LogError`／`Debug.LogException` で可視化する。
- 破棄が必要な資源（SerialPort/Thread）は `OnDestroy`/`OnDisable` で確実に解放する。

## 命名・スタイル
- 名前空間は `LidarBattle.<層>`。1 ファイル 1 主要型、ファイル名＝型名。
- private フィールドは `_camelCase`、`[SerializeField] private` で Inspector 公開。
- public はパスカルケース。インターフェースは `I` 接頭辞。
- XML doc コメントは「なぜ」を中心に簡潔に。自明な「何を」は書かない。

## レビュー観点（PR 前チェック）
1. 層をまたぐ具象依存が合成点の外に漏れていないか。
2. 同じ計算・定数が 2 箇所以上に無いか。
3. 使われていない抽象・設定・public が無いか（YAGNI 違反）。
4. コンパイル警告ゼロ・`read_console` でエラー/警告ゼロ。
