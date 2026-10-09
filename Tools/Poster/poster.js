// 掲示物の図で、手で書くと長くなる部分（QR の見本・グラフの棒・点群）を作る。乱数は固定なので毎回同じ絵になる。
const SVG_NS = "http://www.w3.org/2000/svg";

function rng(seed) {
  return () => (seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648;
}

function el(parent, name, attrs) {
  const e = document.createElementNS(SVG_NS, name);
  for (const k in attrs) e.setAttribute(k, attrs[k]);
  parent.appendChild(e);
  return e;
}

// QR コード風の見本（読み取れない）。<g class="fakeqr" data-x data-y data-size [data-light]>
function drawFakeQr(g) {
  const x = +g.dataset.x, y = +g.dataset.y, size = +g.dataset.size, n = 21, m = size / n;
  const fg = g.dataset.light ? "#fff" : "#000", bg = g.dataset.light ? "#000" : "#fff";
  el(g, "rect", { x, y, width: size, height: size, fill: bg });
  const finder = (i, j) => i < 7 && j < 7 || i >= n - 7 && j < 7 || i < 7 && j >= n - 7;
  const rand = rng(7);
  let d = "";
  for (let j = 0; j < n; j++) for (let i = 0; i < n; i++) {
    let on;
    if (finder(i, j)) {
      const a = i >= n - 7 ? i - (n - 7) : i, b = j >= n - 7 ? j - (n - 7) : j;
      on = a === 0 || a === 6 || b === 0 || b === 6 || (a >= 2 && a <= 4 && b >= 2 && b <= 4);
    } else on = rand() < 0.45;
    if (on) d += `M${x + i * m} ${y + j * m}h${m}v${m}h${-m}z`;
  }
  el(g, "path", { d, fill: fg });
}

document.querySelectorAll(".fakeqr").forEach(drawFakeQr);
