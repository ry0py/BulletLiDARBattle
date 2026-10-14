"""工大祭のプレイ記録（plays.jsonl と replays/）を集計し、論文用の図（figs/）と数値（results.json）を作る。

    python Tools/Stats/analyze.py

記録は Application.persistentDataPath（%USERPROFILE%/AppData/LocalLow/DefaultCompany/LidarBattle）から読む。
本番（debug=false）のプレイだけを使い、SOUL がほとんど動かなかったプレイ（静止率 85% 以上）は分析から外す。
"""
import json, os
from collections import Counter, defaultdict
from datetime import datetime

import numpy as np
from scipy import stats
from scipy.ndimage import gaussian_filter
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import matplotlib.patheffects as pe
from matplotlib.ticker import MaxNLocator

HERE = os.path.dirname(os.path.abspath(__file__))
FIGS = os.path.join(HERE, "figs")
DATA = os.path.expandvars(r"%USERPROFILE%\AppData\LocalLow\DefaultCompany\LidarBattle")
os.makedirs(FIGS, exist_ok=True)

plt.rcParams.update({"font.family": "Yu Gothic", "font.size": 10, "axes.spines.top": False,
                     "axes.spines.right": False, "savefig.dpi": 200, "savefig.bbox": "tight"})

DIFFS = ["Easy", "Medium", "Hard"]
JA = {"Easy": "かんたん", "Medium": "中くらい", "Hard": "むずかしい"}
COL = {"Easy": "#2f9e44", "Medium": "#f08c00", "Hard": "#e03131"}

# BattleTimelineBuilder.Plans と同じ（技は 3 秒から 8 秒ずつ 7 個）
PLANS = [
    ["GapWalls", "HomingFade", "Corridor", "Converge", "SafeZone", "FreezeAim", "CurveLattice"],
    ["SideWalls", "Converge", "CorridorSide", "Fireworks", "SafeZone", "GapWallsSide", "HomingSwarm"],
    ["CurveLattice", "SafeZone", "GapWallsSide", "HomingFade", "Corridor", "SideWalls", "Fireworks"],
    ["FreezeAim", "GapWalls", "SafeZone", "HomingSwarm", "CorridorSide", "Converge", "Fireworks"],
]
FIRST, SLOT = 3.0, 8.0
ATTACK_JA = {
    "SafeZone": "安置", "Corridor": "縦の一本道", "CorridorSide": "横の一本道",
    "HomingFade": "四隅からのホーミング", "HomingSwarm": "ゆっくりホーミング", "Converge": "縮む輪",
    "GapWalls": "すき間の壁（上下）", "GapWallsSide": "すき間の壁（左右）", "SideWalls": "左右の弾の列",
    "Fireworks": "花火→ホーミング", "FreezeAim": "止まって自機狙い", "CurveLattice": "曲がる輪の格子",
}
STILL_LIMIT = 0.85   # 静止率がこれ以上のプレイは外す
STILL_STEP = 0.005   # 0.1 秒で動いた距離がこれ未満なら「静止」
SIGMA = 0.15         # ヒートマップのぼかし半径（盤面の単位）
CELL = 0.02

R = {}  # results.json に書く数値


def slot_of(t):
    return min(6, max(0, int((t - FIRST) // SLOT)))


# ---- 読み込み ----
plays = []
for line in open(os.path.join(DATA, "plays.jsonl"), encoding="utf-8"):
    if not line.strip():
        continue
    r = json.loads(line)
    if r.get("debug"):
        continue
    d = json.load(open(os.path.join(DATA, "replays", r["id"] + ".json"), encoding="utf-8"))
    p = np.array(d["path"], dtype=float)
    step = np.linalg.norm(np.diff(p, axis=0), axis=1)
    r.update(
        dt=datetime.fromisoformat(r["time"]),
        day=r["time"][:10],
        events=d["hitEvents"],
        path=p,
        board=d["board"],
        length=float(step.sum()),
        still=float((step < STILL_STEP).mean()),
        tl_index=int(r["timeline"][-1]) - 1,
    )
    plays.append(r)

bx, by, bw, bh = plays[0]["board"]
cx, cy = bx + bw / 2, by + bh / 2
excluded = [r for r in plays if r["still"] >= STILL_LIMIT]
valid = [r for r in plays if r["still"] < STILL_LIMIT]
R["n_all"] = len(plays)
R["n_valid"] = len(valid)
R["excluded"] = [{k: r[k] for k in ("id", "difficulty", "hits")} | {"still": round(r["still"], 3),
                 "length": round(r["length"], 2)} for r in excluded]
R["days"] = {day: len([r for r in plays if r["day"] == day]) for day in sorted({r["day"] for r in plays})}
R["day_span"] = {day: [min(r["time"] for r in plays if r["day"] == day)[11:16],
                       max(r["time"] for r in plays if r["day"] == day)[11:16]] for day in R["days"]}
R["n_by_diff_all"] = {d: len([r for r in plays if r["difficulty"] == d]) for d in DIFFS}
R["n_by_diff"] = {d: len([r for r in valid if r["difficulty"] == d]) for d in DIFFS}
R["hits_total"] = sum(r["hits"] for r in plays)

by_diff = {d: [r for r in valid if r["difficulty"] == d] for d in DIFFS}
hits = {d: np.array([r["hits"] for r in by_diff[d]]) for d in DIFFS}


def describe(h):
    v, c = np.unique(h, return_counts=True)
    return dict(n=int(len(h)), mean=float(h.mean()), median=float(np.median(h)), sd=float(h.std(ddof=1)),
                mode=[int(x) for x in v[c == c.max()]], mode_n=int(c.max()), min=int(h.min()), max=int(h.max()),
                q1=float(np.percentile(h, 25)), q3=float(np.percentile(h, 75)), zero=int((h == 0).sum()),
                skew=float(stats.skew(h)), kurt=float(stats.kurtosis(h)),
                shapiro_p=float(stats.shapiro(h).pvalue), disp=float(h.var(ddof=1) / h.mean()))


R["hits"] = {d: describe(hits[d]) for d in DIFFS}
R["hits_all405"] = {d: describe(np.array([r["hits"] for r in plays if r["difficulty"] == d])) for d in DIFFS}

# ---- 図 1: 時間帯ごとのプレイ数 ----
days = sorted(R["days"])
fig, axes = plt.subplots(1, 2, figsize=(10, 3.2), sharey=True)
hours = np.arange(10, 19)
R["hourly"] = {}
for ax, day in zip(axes, days):
    bottom = np.zeros(len(hours))
    R["hourly"][day] = {}
    for d in DIFFS:
        c = np.array([len([r for r in plays if r["day"] == day and r["difficulty"] == d and r["dt"].hour == h])
                      for h in hours])
        ax.bar(hours + 0.5, c, bottom=bottom, width=0.85, color=COL[d], label=JA[d])
        bottom += c
        R["hourly"][day][d] = c.tolist()
    for h, b in zip(hours, bottom):
        if b:
            ax.text(h + 0.5, b + 0.6, int(b), ha="center", fontsize=8)
    n = len([r for r in plays if r["day"] == day])
    ax.set_title(f"{int(day[5:7])}/{int(day[8:])}（{'1' if day == days[0] else '2'} 日目）：{n} プレイ")
    ax.set_xticks(hours)
    ax.set_xticklabels([f"{h}時" for h in hours], fontsize=8)
    ax.set_xlim(10, 19)
axes[0].set_ylabel("プレイ数（1 時間ごと）")
fig.legend(*axes[0].get_legend_handles_labels(), loc="upper center", ncol=3, frameon=False, fontsize=9,
           bbox_to_anchor=(0.5, 1.07))
fig.savefig(os.path.join(FIGS, "fig1_hourly.png"))
plt.close(fig)

R["diff_share_by_day"] = {day: {d: len([r for r in plays if r["day"] == day and r["difficulty"] == d]) for d in DIFFS}
                          for day in days}

# ---- 図 2: 被弾回数の分布 ----
fig, axes = plt.subplots(1, 3, figsize=(11, 3.4), sharex=True)
for ax, d in zip(axes, DIFFS):
    h, s = hits[d], R["hits"][d]
    ax.hist(h, bins=np.arange(-0.5, 30.5, 1), color=COL[d], alpha=0.85, edgecolor="white")
    ax.axvline(s["mean"], ymax=0.62, color="k", lw=1.3)
    ax.axvline(s["median"], ymax=0.62, color="k", lw=1.3, ls=":")
    ax.set_ylim(0, s["mode_n"] * 1.8)
    ax.yaxis.set_major_locator(MaxNLocator(integer=True))
    ax.set_title(f"{JA[d]}（{s['n']} プレイ）")
    ax.set_xlabel("被弾回数")
    ax.text(0.97, 0.96, f"平均：{s['mean']:.1f}（実線）\n中央値：{s['median']:g}（点線）\n"
            f"最頻値：{'・'.join(map(str, s['mode']))}\n範囲：{s['min']}〜{s['max']}",
            transform=ax.transAxes, ha="right", va="top", fontsize=8.5, linespacing=1.45, multialignment="left",
            bbox=dict(fc="white", ec="#ccc"))
axes[0].set_ylabel("プレイ数")
fig.savefig(os.path.join(FIGS, "fig2_hits_hist.png"))
plt.close(fig)

# 難易度の差（Kruskal-Wallis と、組ごとの Mann-Whitney。Holm で補正）
R["kruskal_p"] = float(stats.kruskal(*[hits[d] for d in DIFFS]).pvalue)
pairs = [("Easy", "Medium"), ("Medium", "Hard"), ("Easy", "Hard")]
raw = []
for a, b in pairs:
    u = stats.mannwhitneyu(hits[a], hits[b])
    # 効果量 r = |Z| / sqrt(N)。Z は U の正規近似
    n1, n2 = len(hits[a]), len(hits[b])
    z = (u.statistic - n1 * n2 / 2) / np.sqrt(n1 * n2 * (n1 + n2 + 1) / 12)
    raw.append((a, b, float(u.pvalue), float(abs(z) / np.sqrt(n1 + n2)),
                float(hits[b].mean() - hits[a].mean())))
order = np.argsort([x[2] for x in raw])
holm = [0.0] * len(raw)
running = 0.0
for k, i in enumerate(order):
    running = max(running, min(1.0, raw[i][2] * (len(raw) - k)))
    holm[i] = running
R["pairwise"] = [dict(a=a, b=b, p=p, p_holm=holm[i], r=r, diff=df) for i, (a, b, p, r, df) in enumerate(raw)]

# 分布の当てはめ（ポアソン・負の二項）を対数尤度と AIC で比べる
R["fit"] = {}
for d in DIFFS:
    h = hits[d]
    m, v = h.mean(), h.var(ddof=1)
    ll_pois = stats.poisson.logpmf(h, m).sum()
    # 負の二項はモーメント法で初期値をとり、最尤で詰める
    from scipy.optimize import minimize

    def nll(x):
        k, mu = np.exp(x)
        return -stats.nbinom.logpmf(h, k, k / (k + mu)).sum()
    k0 = m * m / max(v - m, 1e-3)
    res = minimize(nll, np.log([k0, m]), method="Nelder-Mead")
    k, mu = np.exp(res.x)
    ll_norm = stats.norm.logpdf(h, m, h.std(ddof=1)).sum()
    R["fit"][d] = dict(aic_norm=float(4 - 2 * ll_norm), aic_pois=float(2 - 2 * ll_pois),
                       aic_nb=float(4 + 2 * res.fun), nb_k=float(k))

# ---- 図 3: Timeline ごとの被弾 ----
fig, ax = plt.subplots(figsize=(8, 3.2))
R["timeline"] = {}
for i, d in enumerate(DIFFS):
    groups = [np.array([r["hits"] for r in by_diff[d] if r["tl_index"] == k]) for k in range(4)]
    p = float(stats.kruskal(*groups).pvalue)
    R["timeline"][d] = dict(n=[len(g) for g in groups], mean=[float(g.mean()) for g in groups],
                            sd=[float(g.std(ddof=1)) for g in groups], kruskal_p=p)
    xs = i * 5 + np.arange(4)
    ax.bar(xs, [g.mean() for g in groups], color=COL[d], alpha=0.85,
           yerr=[g.std(ddof=1) / np.sqrt(len(g)) for g in groups], capsize=3)
    for x, g in zip(xs, groups):
        ax.text(x, 0.6, f"n={len(g)}", ha="center", fontsize=7, color="white")
    ax.text(i * 5 + 1.5, max(g.mean() for g in groups) + 3, f"{JA[d]}\nKruskal-Wallis p={p:.2f}",
            ha="center", fontsize=8.5)
ax.set_xticks([i * 5 + k for i in range(3) for k in range(4)])
ax.set_xticklabels([f"{k + 1} 本目" for _ in range(3) for k in range(4)], fontsize=8)
ax.set_ylabel("平均被弾回数（誤差棒は標準誤差）")
ax.set_ylim(0, 26)
fig.savefig(os.path.join(FIGS, "fig3_timeline.png"))
plt.close(fig)

# ---- 技ごとの被弾（8 秒の技 1 回あたり）----
att_hits = defaultdict(lambda: defaultdict(list))   # [attack][diff] -> 1 回ごとの被弾数
att_patterns = defaultdict(Counter)
for r in valid:
    per_slot = Counter(slot_of(e["t"]) for e in r["events"])
    plan = PLANS[r["tl_index"]]
    for s, a in enumerate(plan):
        att_hits[a][r["difficulty"]].append(per_slot.get(s, 0))
    for e in r["events"]:
        att_patterns[plan[slot_of(e["t"])]][e["pattern"]] += 1
attacks = sorted(att_hits, key=lambda a: -np.mean(sum((att_hits[a][d] for d in DIFFS), [])))
R["attacks"] = {a: {d: dict(n=len(att_hits[a][d]), mean=float(np.mean(att_hits[a][d])),
                            zero=float(np.mean(np.array(att_hits[a][d]) == 0))) for d in DIFFS}
                | {"all_mean": float(np.mean(sum((att_hits[a][d] for d in DIFFS), []))),
                   "patterns": att_patterns[a].most_common(3)} for a in attacks}

M = np.array([[R["attacks"][a][d]["mean"] for d in DIFFS] for a in attacks])
fig, ax = plt.subplots(figsize=(6.2, 5.2))
im = ax.imshow(M, cmap="OrRd", aspect="auto", vmin=0)
for i in range(len(attacks)):
    for j in range(3):
        ax.text(j, i, f"{M[i, j]:.2f}", ha="center", va="center", fontsize=9,
                color="white" if M[i, j] > M.max() * 0.6 else "black")
ax.set_xticks(range(3))
ax.set_xticklabels([JA[d] for d in DIFFS])
ax.set_yticks(range(len(attacks)))
ax.set_yticklabels([ATTACK_JA[a] for a in attacks])
ax.tick_params(length=0)
for s in ax.spines.values():
    s.set_visible(False)
cb = fig.colorbar(im, ax=ax, fraction=0.05, pad=0.03)
cb.set_label("8 秒の技 1 回あたりの平均被弾回数")
fig.savefig(os.path.join(FIGS, "fig4_attacks.png"))
plt.close(fig)

# ---- 図 5: 60 秒の中の被弾の時間変化 ----
fig, ax = plt.subplots(figsize=(9, 3.2))
edges = np.arange(0, 61, 1)
R["time_profile"] = {}
for d in DIFFS:
    t = np.array([e["t"] for r in by_diff[d] for e in r["events"]])
    c, _ = np.histogram(t, edges)
    rate = c / len(by_diff[d])
    ax.plot(edges[:-1] + 0.5, rate, color=COL[d], lw=1.6, label=JA[d])
    # 前半（3〜31 秒）と後半（31〜59 秒）
    per_play = [(sum(1 for e in r["events"] if e["t"] < 31), sum(1 for e in r["events"] if e["t"] >= 31))
                for r in by_diff[d]]
    a, b = np.array(per_play).T
    R["time_profile"][d] = dict(first=float(a.mean()), second=float(b.mean()),
                                wilcoxon_p=float(stats.wilcoxon(a, b).pvalue))
for k in range(8):
    ax.axvline(FIRST + k * SLOT, color="#aaa", lw=0.7, ls="--")
for k in range(7):
    ax.text(FIRST + k * SLOT + SLOT / 2, ax.get_ylim()[1] * 0.98, f"{k + 1} 個目", ha="center", va="top",
            fontsize=7.5, color="#777")
ax.set_xlim(0, 60)
ax.set_xlabel("バトル開始からの時間（秒）")
ax.set_ylabel("1 プレイあたりの被弾回数（1 秒ごと）")
ax.legend(frameon=False, fontsize=9, loc="upper left", bbox_to_anchor=(0, 0.9))
fig.savefig(os.path.join(FIGS, "fig5_time.png"))
plt.close(fig)

# ---- 図 6・7: 位置のヒートマップ ----
xe = np.arange(bx, bx + bw + 1e-6, CELL)
ye = np.arange(by, by + bh + 1e-6, CELL)


def density(points, weights=None):
    a = np.asarray(points)
    H, _, _ = np.histogram2d(a[:, 0], a[:, 1], bins=[xe, ye], weights=weights)
    return gaussian_filter(H, SIGMA / CELL, mode="constant")


def mark_center(ax):
    ax.axvline(cx, color="white", lw=0.7, ls="--", alpha=0.6)
    ax.axhline(cy, color="white", lw=0.7, ls="--", alpha=0.6)
    ax.plot(cx, cy, "+", color="cyan", ms=11, mew=1.8, path_effects=[pe.withStroke(linewidth=3.5, foreground="black")])
    ax.text(cx + 0.06, cy + 0.06, "中央", color="cyan", fontsize=8.5, fontweight="bold",
            path_effects=[pe.withStroke(linewidth=2.5, foreground="black")])


def show(ax, D, title, cmap="inferno", label=("少ない", "多い")):
    im = ax.imshow((D / D.max()).T, origin="lower", extent=[bx, bx + bw, by, by + bh], cmap=cmap,
                   interpolation="bilinear", aspect="equal", vmin=0, vmax=1)
    mark_center(ax)
    ax.set_title(title, fontsize=10)
    ax.set_xticks([]); ax.set_yticks([])
    for s in ax.spines.values():
        s.set_visible(True); s.set_color("#888")
    cb = plt.colorbar(im, ax=ax, fraction=0.035, pad=0.02, ticks=[0, 1])
    cb.ax.set_yticklabels(label, fontsize=8)


hit_pts = {d: [(e["x"], e["y"]) for r in by_diff[d] for e in r["events"]] for d in DIFFS}
fig, axes = plt.subplots(2, 2, figsize=(10, 6.6))
for ax, d in zip(axes.flat, DIFFS):
    show(ax, density(hit_pts[d]), f"{JA[d]}：被弾 {len(hit_pts[d])} 回 / {len(by_diff[d])} プレイ")
allp = sum(hit_pts.values(), [])
show(axes[1, 1], density(allp), f"全難易度：被弾 {len(allp)} 回 / {len(valid)} プレイ")
fig.savefig(os.path.join(FIGS, "fig6_hit_heatmap.png"))
plt.close(fig)


def region3(points):
    a = np.asarray(points)
    fx = np.clip((a[:, 0] - bx) / bw, 0, 0.9999)
    fy = np.clip((a[:, 1] - by) / bh, 0, 0.9999)
    g = np.zeros((3, 3))
    np.add.at(g, (2 - (fy * 3).astype(int), (fx * 3).astype(int)), 1)
    return (g / g.sum()).round(3).tolist()   # [上段・中段・下段][左・中・右]


R["hit_region3"] = {d: region3(hit_pts[d]) for d in DIFFS} | {"All": region3(allp)}

# 滞在（0.1 秒おきの SOUL 位置）と、滞在時間あたりの被弾（危険度）
stay = np.concatenate([r["path"] for r in valid])
R["stay_region3"] = region3(stay)
Ds = density(stay)
Dh = density(allp)
risk = np.where(Ds > Ds.max() * 0.10, Dh / np.maximum(Ds, 1e-9), np.nan) * 10   # 1 秒いたときの被弾数
fig, axes = plt.subplots(1, 2, figsize=(10, 3.4))
show(axes[0], Ds, f"SOUL がいた場所（{len(valid)} プレイ × 60 秒）", cmap="viridis", label=("短い", "長い"))
im = axes[1].imshow(risk.T, origin="lower", extent=[bx, bx + bw, by, by + bh], cmap="magma",
                    interpolation="bilinear", aspect="equal", vmin=0, vmax=np.nanpercentile(risk, 99))
axes[1].set_facecolor("#ddd")
mark_center(axes[1])
axes[1].set_title("そこに 1 秒いたときの被弾回数（危険度）\n灰色は滞在が少なく求めない所", fontsize=10)
axes[1].set_xticks([]); axes[1].set_yticks([])
cb = plt.colorbar(im, ax=axes[1], fraction=0.035, pad=0.02)
cb.set_label("回 / 秒", fontsize=8)
fig.savefig(os.path.join(FIGS, "fig7_stay_risk.png"))
plt.close(fig)
# 危険度を中央からの距離で見る
dist_c = np.hypot(*(np.meshgrid(xe[:-1] + CELL / 2 - cx, ye[:-1] + CELL / 2 - cy, indexing="ij")))
R["risk_by_dist"] = []
for lo, hi in [(0, 0.5), (0.5, 1.0), (1.0, 1.5), (1.5, 3.5)]:
    m = (dist_c >= lo) & (dist_c < hi)
    R["risk_by_dist"].append(dict(lo=lo, hi=hi, hits=float(Dh[m].sum() / Dh.sum()),
                                  stay=float(Ds[m].sum() / Ds.sum()),
                                  rate=float(Dh[m].sum() / Ds[m].sum() * 10 / len(valid) * len(valid))))

# ---- 図 8: 動きと被弾 ----
fig, axes = plt.subplots(1, 3, figsize=(11, 3.3), sharey=False)
R["movement"] = {}
for ax, d in zip(axes, DIFFS):
    L = np.array([r["length"] for r in by_diff[d]])
    S = np.array([r["still"] for r in by_diff[d]])
    Dc = np.array([np.hypot(r["path"][:, 0] - cx, r["path"][:, 1] - cy).mean() for r in by_diff[d]])
    h = hits[d]
    rho, p = stats.spearmanr(L, h)
    rho_s, p_s = stats.spearmanr(S, h)
    rho_d, p_d = stats.spearmanr(Dc, h)
    R["movement"][d] = dict(length_median=float(np.median(L)), speed_mean=float(L.mean() / 60),
                            still_median=float(np.median(S)), dist_center_median=float(np.median(Dc)),
                            rho_len=float(rho), p_len=float(p), rho_still=float(rho_s), p_still=float(p_s),
                            rho_dist=float(rho_d), p_dist=float(p_d))
    ax.scatter(L, h, s=12, color=COL[d], alpha=0.6, edgecolor="none")
    k, b0 = np.polyfit(L, h, 1)
    xs = np.linspace(L.min(), L.max(), 10)
    ax.plot(xs, k * xs + b0, color="k", lw=1)
    ax.set_title(f"{JA[d]}　ρ = {rho:+.2f}（p = {p:.2g}）", fontsize=10)
    ax.set_xlabel("60 秒間に SOUL が動いた距離（盤面の単位）")
axes[0].set_ylabel("被弾回数")
fig.savefig(os.path.join(FIGS, "fig8_move.png"))
plt.close(fig)

# ---- 図 9: 被弾の間隔 ----
gaps = {d: np.concatenate([np.diff([e["t"] for e in r["events"]]) for r in by_diff[d] if len(r["events"]) > 1])
        for d in DIFFS}
fig, ax = plt.subplots(figsize=(7, 3))
for d in DIFFS:
    ax.hist(gaps[d], bins=np.arange(0, 12.25, 0.25), histtype="step", lw=1.6, color=COL[d], label=JA[d],
            density=True)
ax.axvline(1.0, color="k", ls=":", lw=1)
ax.text(1.05, ax.get_ylim()[1] * 0.92, "無敵時間 1 秒", fontsize=8.5)
ax.set_xlabel("ひとつ前の被弾からの時間（秒）")
ax.set_ylabel("割合（密度）")
ax.legend(frameon=False, fontsize=9)
fig.savefig(os.path.join(FIGS, "fig9_gaps.png"))
plt.close(fig)
R["gaps"] = {d: dict(n=int(len(gaps[d])), min=float(gaps[d].min()), median=float(np.median(gaps[d])),
                     within2=float((gaps[d] < 2).mean()),
                     within125=float((gaps[d] < 1.25).mean())) for d in DIFFS}

# ---- 弾の色（ビルド差し替え後＝青い弾があるプレイ以降だけ）----
first_blue = min(r["dt"] for r in plays if any(e["bullet"] == "Blue" for e in r["events"]))
last_old = max(r["dt"] for r in plays if r["dt"] < first_blue)
R["color_switch"] = dict(last_old=last_old.isoformat(), first_blue=first_blue.isoformat())
new = [r for r in valid if r["dt"] >= first_blue]
old = [r for r in valid if r["dt"] < first_blue]
R["color_new_n"] = len(new)
R["color_old_n"] = len(old)
R["colors"] = {d: dict(Counter(e["bullet"] for r in new if r["difficulty"] == d for e in r["events"])) for d in DIFFS}
R["old_vs_new"] = {d: dict(old_n=len([r for r in old if r["difficulty"] == d]),
                           old_mean=float(np.mean([r["hits"] for r in old if r["difficulty"] == d])),
                           new_n=len([r for r in new if r["difficulty"] == d]),
                           new_mean=float(np.mean([r["hits"] for r in new if r["difficulty"] == d])),
                           p=float(stats.mannwhitneyu([r["hits"] for r in old if r["difficulty"] == d],
                                                      [r["hits"] for r in new if r["difficulty"] == d]).pvalue))
                   for d in DIFFS}

# ---- 1 日目と 2 日目 ----
R["by_day"] = {d: {day: dict(n=len(g := [r["hits"] for r in by_diff[d] if r["day"] == day]),
                             mean=float(np.mean(g))) for day in days}
               | {"p": float(stats.mannwhitneyu(*[[r["hits"] for r in by_diff[d] if r["day"] == day]
                                                  for day in days]).pvalue)} for d in DIFFS}

json.dump(R, open(os.path.join(HERE, "results.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1,
          default=lambda o: o.tolist() if hasattr(o, "tolist") else str(o))
print("figs ->", FIGS)
