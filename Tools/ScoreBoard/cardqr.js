// 記録カードの QR を作る。スコアボード（index.html）と管理画面（admin.html）で共用する。
// qrcode.js と card/cardcode.js の後に読み込む。
// replays/<id>.json を読み、データごと公開ページの URL に入れて QR の画像（data URL）にする。

async function fetchReplay(id) {
  const res = await fetch(`replays/${id}.json`, { cache: "no-store" });
  if (!res.ok) throw new Error(res.status);
  return res.json();
}

// その日・同じ難易度・同じモード（本番／デバッグ）で、このプレイまでに遊んだ人の中の順位。
// 被弾が少ないほど上で、同じ回数なら同じ順位。後のプレイは数えないので、あとで作り直しても変わらない。
async function todayStanding(replay) {
  const res = await fetch("plays.jsonl", { cache: "no-store" });
  const text = res.ok ? await res.text() : "";
  const day = replay.time.slice(0, 10);
  const hits = replay.hits ?? replay.hitEvents.length;
  let rank = 1, total = 1; // 自分のぶん
  for (const line of text.split("\n")) {
    let p;
    try { p = JSON.parse(line); } catch { continue; }
    if (p.id === replay.id || typeof p.hits !== "number" || typeof p.time !== "string") continue;
    if (p.difficulty !== replay.difficulty || !!p.debug !== !!replay.debug) continue;
    if (p.time.slice(0, 10) !== day || p.time > replay.time) continue;
    total++;
    if (p.hits < hits) rank++;
  }
  return { rank, total };
}

/** そのプレイの記録カードを開く公開ページの URL */
async function cardUrl(id) {
  const replay = await fetchReplay(id);
  return CardCode.url(replay, await todayStanding(replay));
}

async function cardQrDataUrl(id, cellSize = 1) {
  const qr = qrcode(0, "L");
  qr.addData(await cardUrl(id), "Byte");
  qr.make();
  return qr.createDataURL(cellSize, 3);
}
