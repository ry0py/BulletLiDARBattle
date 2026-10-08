// 1 プレイの再生（軌跡を伸ばしながら SOUL を動かし、被弾で ✕）を canvas に繰り返し流す。
// スコアボード（展示 PC）と記録カードのページ（来場者のスマホ）で共用する。
//
//   const player = ReplayPlayer.create(canvas, { fontPx: () => 16 });
//   player.show({ aspect, interval, path: [[x, y]], hits: [{ t, x, y }] }); // 座標は盤面の 0〜1、y は上向き。null で消す
const ReplayPlayer = (() => {
  const SPEED = 2; // 何倍速で流すか
  const HOLD = 3;  // 最後まで流したあと止めておく秒数（実時間）
  const FONT = '"BIZ UDGothic", "Hiragino Sans", "Noto Sans JP", "Yu Gothic UI", sans-serif';

  function cross(g, x, y, r) {
    g.beginPath(); g.moveTo(x - r, y - r); g.lineTo(x + r, y + r); g.moveTo(x + r, y - r); g.lineTo(x - r, y + r); g.stroke();
  }

  /** options.fontPx: 下の文字の大きさ（CSS px）を返す関数 */
  function create(canvas, { fontPx = () => 16 } = {}) {
    let rec = null, start = 0;

    function draw(now) {
      requestAnimationFrame(draw);
      const dpr = window.devicePixelRatio || 1;
      const w = Math.round(canvas.clientWidth * dpr), h = Math.round(canvas.clientHeight * dpr);
      if (canvas.width !== w || canvas.height !== h) { canvas.width = w; canvas.height = h; }
      const g = canvas.getContext("2d");
      g.clearRect(0, 0, w, h);
      if (!rec || w === 0 || h === 0) return;

      const duration = rec.path.length * rec.interval;
      const t = Math.min(duration, ((now - start) / 1000 * SPEED) % (duration + HOLD * SPEED));
      const font = fontPx() * dpr, textH = font * 1.6; // 下の文字の段
      const bh = Math.min(w / rec.aspect, h - textH) - 2 * dpr, bw = bh * rec.aspect;
      const bx = (w - bw) / 2, by = (h - textH - bh) / 2; // 盤面と下の文字をまとめて上下の真ん中に
      const X = u => bx + u * bw, Y = v => by + (1 - v) * bh;

      g.strokeStyle = "#fff"; g.lineWidth = 2 * dpr;
      g.strokeRect(bx, by, bw, bh);

      // ここまでの軌跡
      const n = Math.min(rec.path.length, Math.floor(t / rec.interval) + 1);
      g.strokeStyle = "#4dc3ff"; g.lineWidth = 2 * dpr; g.lineJoin = "round";
      g.beginPath();
      for (let i = 0; i < n; i++) i === 0 ? g.moveTo(X(rec.path[i][0]), Y(rec.path[i][1])) : g.lineTo(X(rec.path[i][0]), Y(rec.path[i][1]));
      g.stroke();

      // ここまでの被弾。当たった直後は赤い輪が広がる
      let hitCount = 0;
      g.strokeStyle = "#ff3b3b"; g.lineWidth = 2.5 * dpr;
      for (const hit of rec.hits) {
        if (hit.t > t) continue;
        hitCount++;
        const x = X(hit.x), y = Y(hit.y), age = (t - hit.t) / SPEED;
        cross(g, x, y, 6 * dpr);
        if (age < 0.6) {
          g.globalAlpha = 1 - age / 0.6;
          g.beginPath(); g.arc(x, y, (8 + age * 50) * dpr, 0, Math.PI * 2); g.stroke();
          g.globalAlpha = 1;
        }
      }

      // 今の SOUL
      const [u, v] = rec.path[n - 1] ?? [0.5, 0.5];
      g.fillStyle = "#fff";
      g.beginPath(); g.arc(X(u), Y(v), 5 * dpr, 0, Math.PI * 2); g.fill();

      g.font = `${font}px ${FONT}`;
      g.textBaseline = "bottom";
      g.textAlign = "left";
      g.fillStyle = "#ddd";
      g.fillText(`${Math.floor(t)} / ${Math.round(duration)} 秒`, bx, by + bh + textH);
      g.textAlign = "right";
      g.fillStyle = hitCount ? "#ff5a5a" : "#ddd";
      g.fillText(`被弾 ${hitCount} 回`, bx + bw, by + bh + textH);
    }
    requestAnimationFrame(draw);

    return {
      show(next) { rec = next; start = performance.now(); },
    };
  }

  return { create, SPEED };
})();
