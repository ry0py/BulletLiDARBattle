// 記録カードのデータを URL の # 以降に詰める／戻す。
// スコアボード（encode, 展示 PC）とカードページ（decode, 来場者のスマホ）で共用する。
// QR を小さく保つため、replays/<id>.json を粗くしてから圧縮する（元のファイルはそのまま）。
//
// 形式 v3（圧縮前のバイト列、数値はリトルエンディアン）
//   [0] 版 (3)  [1] 難易度 0-2 | デバッグなら 0x80
//   [2..6] 開始時刻 年-2000, 月, 日, 時, 分
//   [7] 盤面の横/縦 × 50
//   [8] 軌跡の間隔（0.1 秒単位）
//   [9..10] カードに出す被弾回数（管理画面で直した値。被弾の記録の数と違うことがある）
//   [11..12] その日の順位, [13..14] その日の人数（同じ難易度。0 なら出さない）
//   被弾の記録の数 n (u16), 続けて n 個 × (時刻 0.1 秒単位 u16, x u8, y u8)
//   軌跡の点数 m (u16), 続けて m 個 × (x, y) の前の点との差 (mod 256)
//   座標は盤面の左下 0, 右上 255。
// v2 は [11..14] が無い。v1 はさらに [9..10] も無く、被弾回数 = 被弾の記録の数。読むときは v1・v2 も受け付ける。
const CardCode = (() => {
  // カードページ（card/）の公開先。来場者のスマホが自分の回線で開く（.github/workflows/card-pages.yml）。
  const PAGE_URL = "https://ry0py.com/BulletLiDARBattle/";
  const VERSION = 3;
  const PATH_STEP = 2; // 0.1 秒おきの軌跡を 2 つに 1 つへ間引く（0.2 秒おき）
  const DIFFICULTIES = ["Easy", "Medium", "Hard"];

  const quantize = v => Math.round(Math.min(1, Math.max(0, v)) * 255);

  async function pipe(bytes, stream) {
    const res = new Response(new Blob([bytes]).stream().pipeThrough(stream));
    return new Uint8Array(await res.arrayBuffer());
  }

  function toBase64Url(bytes) {
    let s = "";
    for (const b of bytes) s += String.fromCharCode(b);
    return btoa(s).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
  }

  function fromBase64Url(text) {
    const s = atob(text.replace(/-/g, "+").replace(/_/g, "/"));
    return Uint8Array.from(s, c => c.charCodeAt(0));
  }

  /** replays/<id>.json の中身と、その日の順位 { rank, total }（無ければ出さない）→ URL に入れる文字列 */
  async function encode(replay, standing) {
    const [bx, by, bw, bh] = replay.board;
    const nx = x => quantize((x - bx) / bw);
    const ny = y => quantize((y - by) / bh);
    const [, yy, mo, dd, hh, mi] = replay.time.match(/^(\d{4})-(\d\d)-(\d\d)T(\d\d):(\d\d)/).map(Number);

    const out = [];
    const u16 = v => out.push(v & 255, (v >> 8) & 255);

    out.push(VERSION, DIFFICULTIES.indexOf(replay.difficulty) | (replay.debug ? 0x80 : 0));
    out.push(yy - 2000, mo, dd, hh, mi);
    out.push(Math.min(255, Math.round(bw / bh * 50)));
    out.push(Math.round(replay.pathInterval * PATH_STEP * 10));

    // 管理画面で被弾回数を減らしたら、× 印も先頭からその数だけにする（元の記録は消さない）。
    const count = replay.hits ?? replay.hitEvents.length;
    const hitEvents = replay.hitEvents.slice(0, count);
    u16(count);
    u16(standing?.rank ?? 0);
    u16(standing?.total ?? 0);
    u16(hitEvents.length);
    for (const h of hitEvents) {
      u16(Math.round(h.t * 10));
      out.push(nx(h.x), ny(h.y));
    }

    const path = replay.path.filter((_, i) => i % PATH_STEP === 0);
    u16(path.length);
    let px = 0, py = 0;
    for (const [x, y] of path) {
      const qx = nx(x), qy = ny(y);
      out.push((qx - px) & 255, (qy - py) & 255);
      px = qx; py = qy;
    }

    return toBase64Url(await pipe(new Uint8Array(out), new CompressionStream("deflate-raw")));
  }

  /** URL の文字列 → { time, difficulty, debug, aspect, count, rank, total, hits:[{t,x,y}], path:[[x,y]], interval }
   *  （座標は 0〜1。順位が無ければ rank・total は 0） */
  async function decode(text) {
    const b = await pipe(fromBase64Url(text), new DecompressionStream("deflate-raw"));
    let i = 0;
    const u8 = () => b[i++];
    const u16 = () => b[i++] | (b[i++] << 8);

    const version = u8();
    if (version < 1 || version > 3) throw new Error("unknown version");
    const flags = u8();
    const [yy, mo, dd, hh, mi] = [u8() + 2000, u8(), u8(), u8(), u8()];
    const aspect = u8() / 50;
    const interval = u8() / 10;
    const shownCount = version >= 2 ? u16() : null;
    const [rank, total] = version >= 3 ? [u16(), u16()] : [0, 0];

    const hits = [];
    for (let n = u16(); n > 0; n--) hits.push({ t: u16() / 10, x: u8() / 255, y: u8() / 255 });
    const count = shownCount ?? hits.length;

    const path = [];
    let px = 0, py = 0;
    for (let n = u16(); n > 0; n--) {
      px = (px + u8()) & 255;
      py = (py + u8()) & 255;
      path.push([px / 255, py / 255]);
    }

    return {
      time: new Date(yy, mo - 1, dd, hh, mi),
      difficulty: DIFFICULTIES[flags & 0x7f] ?? "?",
      debug: (flags & 0x80) !== 0,
      aspect, interval, count, rank, total, hits, path,
    };
  }

  /** replays/<id>.json の中身（と順位）→ そのプレイの記録カードを開く公開ページの URL（QR に入れるもの） */
  async function url(replay, standing) {
    return PAGE_URL + "#" + await encode(replay, standing);
  }

  return { encode, decode, url };
})();
