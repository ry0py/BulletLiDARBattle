using System;
using System.Collections.Generic;
using System.IO;
using LidarBattle.Battle;
using LidarBattle.Flow;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using static LidarBattle.EditorTools.GameSetupBuilder;

namespace LidarBattle.EditorTools
{
    /// <summary>
    /// 弾幕の「技」。1 つの技は「start 秒から AttackSeconds 秒の間にどう撃つか」で、難易度で発射間隔や弾の速さが変わる。
    /// 本番の難易度別 Timeline（BattleTimelineBuilder）は技を並べて作る。
    /// Build Attack Library は技を 1 つずつ 8 秒の Timeline（Medium）にする（Assets/Timelines/Attacks/。弾幕テストシーンで確かめる用）。
    /// FirePattern は Assets/Settings/Bullets/Attacks/、弾の種類は Assets/Settings/Bullets/ に置く
    /// （White/Yellow は Rebuild Game Setup が作ったものを使う）。既存の同名アセットは上書きする。
    /// </summary>
    public static class AttackLibraryBuilder
    {
        internal const string AttackTimelineDir = "Assets/Timelines/Attacks";
        private const string AttackPatternDir = BulletDir + "/Attacks";
        internal const double AttackSeconds = 8;

        /// <summary>技の中身。start 秒から撃ち始め、start + 7 秒までに撃ち終える。トラックは lanes から借りる。</summary>
        private delegate void AttackFill(Lanes lanes, double start, Difficulty d);

        // 前の 12 個が本番で使う技（盤面の全体を動き回らせるもの）。残りは上から撃つだけで下に居座れるので本番では使わない。
        private static readonly Dictionary<string, AttackFill> Attacks = new()
        {
            ["SafeZone"] = SafeZone,
            ["Corridor"] = Corridor,
            ["CorridorSide"] = CorridorSide,
            ["HomingFade"] = HomingFade,
            ["Converge"] = Converge,
            ["GapWalls"] = GapWalls,
            ["GapWallsSide"] = GapWallsSide,
            ["Fireworks"] = Fireworks,
            ["FreezeAim"] = FreezeAim,
            ["HomingSwarm"] = HomingSwarm,
            ["SideWalls"] = SideWalls,
            ["CurveLattice"] = CurveLattice,
            ["AccelBloom"] = AccelBloom,
            ["AimedStream"] = AimedStream,
            ["AimedFan"] = AimedFan,
            ["ArcCurtain"] = ArcCurtain,
            ["BigBalls"] = BigBalls,
            ["WaveCurtain"] = WaveCurtain,
        };

        /// <summary>本番で使う技。弾幕テストシーンでは先に並べる。</summary>
        internal static readonly string[] BattleAttacks =
        {
            "SafeZone", "Corridor", "CorridorSide", "HomingFade", "Converge", "GapWalls", "GapWallsSide",
            "Fireworks", "FreezeAim", "HomingSwarm", "SideWalls", "CurveLattice",
        };

        // 発射位置（盤面の正規化座標）。枠の外から撃つ。
        private static readonly Vector2 Top = new(0.5f, 1.15f);
        private static readonly Vector2 TopLeft = new(0.1f, 1.15f);
        private static readonly Vector2 TopRight = new(0.9f, 1.15f);
        private static readonly Vector2 Left = new(-0.08f, 0.5f);
        private static readonly Vector2 Right = new(1.08f, 0.5f);
        private static readonly Vector2 Bottom = new(0.5f, -0.15f);
        private static readonly Vector2 BottomLeft = new(0.1f, -0.15f);
        private static readonly Vector2 BottomRight = new(0.9f, -0.15f);
        private static readonly Vector2[] Corners = { TopLeft, BottomRight, TopRight, BottomLeft };

        private static BulletType _white, _yellow, _big;

        [MenuItem("Tools/LiDAR Battle/Build Attack Library")]
        public static void Build()
        {
            try
            {
                Directory.CreateDirectory(AttackTimelineDir);
                LoadBulletTypes();
                foreach (string name in Attacks.Keys)
                    MakeTimeline($"{AttackTimelineDir}/{name}", tl => Add(name, new Lanes(tl), 0, Difficulty.Medium), AttackSeconds);
                AssetDatabase.SaveAssets();
                Debug.Log("[AttackLibraryBuilder] 完了");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>技を使う前に 1 回呼ぶ。White/Yellow を読み、Big（大きい赤）を作る。</summary>
        internal static void LoadBulletTypes()
        {
            Directory.CreateDirectory(AttackPatternDir);
            _white = LoadBulletType("White");
            _yellow = LoadBulletType("Yellow");
            _big = MakeBulletType("Big", new Color(1f, 0.45f, 0.3f), 0.5f, 0.2f);
        }

        /// <summary>技 name を start 秒から撃つ。トラックは lanes の前から順に使う。</summary>
        internal static void Add(string name, Lanes lanes, double start, Difficulty d)
        {
            if (!Attacks.TryGetValue(name, out var fill)) throw new ArgumentException($"技 {name} がありません");
            fill(lanes, start, d);
        }

        /// <summary>
        /// Timeline の ShotTrack を前から順に貸す。時間の重ならない技どうしは同じトラックを使い回す
        /// （60 秒の Timeline でトラックが技の数だけ増えないように）。
        /// </summary>
        internal sealed class Lanes
        {
            private readonly TimelineAsset _timeline;
            private readonly List<ShotTrack> _tracks = new();
            private int _used;
            private SafeZoneTrack _zone;

            public Lanes(TimelineAsset timeline) => _timeline = timeline;

            /// <summary>次の時間帯へ。それまでの技と時間が重ならないので、トラックを最初から使い直す。</summary>
            public void Rewind() => _used = 0;

            public ShotTrack Next()
            {
                if (_used == _tracks.Count) _tracks.Add(_timeline.CreateTrack<ShotTrack>(null, $"Shots {_tracks.Count + 1}"));
                return _tracks[_used++];
            }

            /// <summary>安置のシートのトラック（1 本を使い回す）。</summary>
            public SafeZoneTrack Zone() => _zone ??= _timeline.CreateTrack<SafeZoneTrack>(null, "Safe Zone");
        }

        // ───────── 本番で使う技（盤面の全体を動き回らせる） ─────────

        /// <summary>
        /// 安置。緑のシート（「あんぜん」）で安置を先に見せ、少したつと盤面の安置以外を弾で埋める（すぐ消える）。
        /// 安置は毎回、前の安置から離れた場所に出るので、盤面を大きく動く。
        /// </summary>
        private static void SafeZone(Lanes l, double t, Difficulty d)
        {
            const float fillSeconds = 1f;
            var (size, warnSeconds) = Pick(d, (new Vector2(1.9f, 1.4f), 2.6f), (new Vector2(1.3f, 1f), 1.8f), (new Vector2(1.1f, 0.85f), 1.5f));
            var fill = P($"SafeFill{d}", ("_shape", ShotShape.Fill), ("_width", 0.28f), ("_holeSize", size),
                ("_speed", 0f), ("_lifetime", fillSeconds));

            var zoneTrack = l.Zone();
            var fillTrack = l.Next();
            // 安置の場所は開始時刻から決める（同じ Timeline なら毎回同じ。Timeline ごとには違う）。
            var random = new System.Random((int)(t * 100));
            var spot = new Vector2(0.5f, 0.5f);
            double cycle = warnSeconds + fillSeconds + 0.3;
            for (int i = 0; i * cycle + warnSeconds + fillSeconds <= 7.5; i++)
            {
                spot = FarSpot(spot, random);
                var zone = zoneTrack.CreateClip<SafeZoneClip>();
                zone.start = t + i * cycle;
                zone.duration = warnSeconds + fillSeconds;
                zone.displayName = "あんぜん";
                Set(zone.asset, ("_center", spot), ("_size", size));
                Shot(fillTrack, t + i * cycle + warnSeconds, 0.1, _white, fill, spot, 0f);
            }
        }

        /// <summary>盤面の内側（安置が枠からはみ出さない範囲）で、previous から離れた場所。何回か選んでいちばん遠いものにする。</summary>
        private static Vector2 FarSpot(Vector2 previous, System.Random random)
        {
            var best = previous;
            for (int i = 0; i < 8; i++)
            {
                var spot = new Vector2(Mathf.Lerp(0.2f, 0.8f, (float)random.NextDouble()), Mathf.Lerp(0.25f, 0.75f, (float)random.NextDouble()));
                if ((spot - previous).sqrMagnitude > (best - previous).sqrMagnitude) best = spot;
            }
            return best;
        }

        /// <summary>
        /// うねる一本道。すき間のある壁を細かい間隔で上から流し、すき間を左右になめらかにずらす。
        /// 道を外れると当たるので、道に沿って左右に動き続ける。
        /// </summary>
        private static void Corridor(Lanes l, double t, Difficulty d)
        {
            var (gap, step) = Pick(d, (1.6f, 6f), (1f, 11f), (0.85f, 14f));
            const float speed = 1.2f;
            var walls = P($"Corridor{d}", ("_shape", ShotShape.GapWall), ("_count", 19), ("_angle", -90f), ("_width", 5.4f),
                ("_gap", gap), ("_gapMode", GapMode.Wave), ("_spiralStep", step), ("_speed", speed));
            // 壁どうしの間を弾の間隔（0.3）にそろえて、道の両側をすき間なくつなげる。間隔は難易度で変えない。
            Shot(l.Next(), t, 7, _white, walls, new Vector2(0.5f, 1.08f), 0.3f / speed);
        }

        /// <summary>横のうねる一本道。右から左へ流し、すき間を上下にずらす。</summary>
        private static void CorridorSide(Lanes l, double t, Difficulty d)
        {
            var (gap, step) = Pick(d, (1.4f, 7f), (0.95f, 14f), (0.8f, 18f));
            const float speed = 1.4f;
            var walls = P($"CorridorSide{d}", ("_shape", ShotShape.GapWall), ("_count", 13), ("_angle", 180f), ("_width", 3.6f),
                ("_gap", gap), ("_gapMode", GapMode.Wave), ("_spiralStep", step), ("_speed", speed));
            Shot(l.Next(), t, 7, _yellow, walls, Right, 0.3f / speed);
        }

        /// <summary>四隅から順に、一定秒で消えるホーミング。逃げ回らせる。</summary>
        private static void HomingFade(Lanes l, double t, Difficulty d)
        {
            var homing = HomingFadePattern(d);
            for (int i = 0; i < Corners.Length; i++) Fire(l.Next(), t + i * 0.5, 7 - i * 0.5, _yellow, homing, Corners[i], 2f, d);
        }

        /// <summary>自機を囲んだ大きな輪がゆっくり縮んでくる。同じ場所に居続けると当たるので、輪のすき間から外へ出る。</summary>
        private static void Converge(Lanes l, double t, Difficulty d) =>
            Fire(l.Next(), t, 7, _white, ConvergePattern(d), Top, 1.8f, d);

        /// <summary>
        /// すき間のある壁が下から上・上から下へ。Easy はすき間の位置がランダム、Medium/Hard は左右の端寄りに交互に空くので、
        /// 左右に大きく動かされる。
        /// </summary>
        private static void GapWalls(Lanes l, double t, Difficulty d)
        {
            var (gap, speed) = Pick(d, (1.5f, 1f), (1f, 1.3f), (0.85f, 1.5f));
            var up = GapWall($"GapWallUp{d}", 90f, 15, 5.4f, gap, speed, d);
            var down = GapWall($"GapWallDown{d}", -90f, 15, 5.4f, gap, speed, d);
            Fire(l.Next(), t, 6, _white, up, new Vector2(0.5f, -0.08f), 2.4f, d);
            Fire(l.Next(), t + 1.2 * Pace(d), 6 - 1.2 * Pace(d), _yellow, down, new Vector2(0.5f, 1.08f), 2.4f, d);
        }

        /// <summary>すき間のある壁が左右から。GapWalls と同じく、Medium/Hard はすき間が上下の端寄りに交互に空く。</summary>
        private static void GapWallsSide(Lanes l, double t, Difficulty d)
        {
            var (gap, speed) = Pick(d, (1.3f, 1.1f), (0.85f, 1.5f), (0.75f, 1.7f));
            var toRight = GapWall($"GapWallRight{d}", 0f, 10, 3.4f, gap, speed, d);
            var toLeft = GapWall($"GapWallLeft{d}", 180f, 10, 3.4f, gap, speed, d);
            Fire(l.Next(), t, 6, _white, toRight, Left, 2.4f, d);
            Fire(l.Next(), t + 1.2 * Pace(d), 6 - 1.2 * Pace(d), _yellow, toLeft, Right, 2.4f, d);
        }

        /// <summary>花火。上の縁で弾けて放物線を描いた破片が、少したつと自機を追いかけ（ホーミング）、一定秒で消える。</summary>
        private static void Fireworks(Lanes l, double t, Difficulty d)
        {
            var (count, chaseSpeed, turnRate) = Pick(d, (8, 1f, 25f), (14, 1.5f, 45f), (16, 1.7f, 60f));
            var chase = P($"FireworkChase{d}", ("_move", MoveType.Homing),
                ("_speed", chaseSpeed), ("_turnRate", turnRate), ("_lifetime", 2.5f));
            var firework = P($"Firework{d}", ("_shape", ShotShape.Ring), ("_count", count), ("_move", MoveType.Gravity),
                ("_speed", 1.3f), ("_gravity", 1.2f), ("_next", chase), ("_nextTime", 1f));
            var track = l.Next();
            var spots = new[] { new Vector2(0.25f, 1f), new Vector2(0.75f, 1f), new Vector2(0.5f, 1f) };
            double gap = 1.4 * Pace(d);
            for (int i = 0; i * gap < 6.5; i++) Shot(track, t + i * gap, 0.1, _yellow, firework, spots[i % spots.Length], 0f);
        }

        /// <summary>上と下から交互に扇を撃ち、止まってから自機へ撃ち直す。Easy は弾が少なく遅い。</summary>
        private static void FreezeAim(Lanes l, double t, Difficulty d)
        {
            var down = FreezePattern(d == Difficulty.Easy ? "FreezeAimEasy" : "FreezeAim", -90f, d);
            var up = FreezePattern(d == Difficulty.Easy ? "FreezeAimUpEasy" : "FreezeAimUp", 90f, d);
            Fire(l.Next(), t, 6, _white, down, Top, 3f, d);
            Fire(l.Next(), t + 1.5 * Pace(d), 6 - 1.5 * Pace(d), _white, up, Bottom, 3f, d);
        }

        /// <summary>上の隅と左右からゆっくりのホーミング。Easy はもっと遅く、一定秒で消える。</summary>
        private static void HomingSwarm(Lanes l, double t, Difficulty d)
        {
            var homing = d == Difficulty.Easy
                ? P("HomingSlowFade", ("_shape", ShotShape.Aimed), ("_count", 1), ("_move", MoveType.Homing),
                    ("_speed", 1.5f), ("_turnRate", 40f), ("_lifetime", 3f))
                : P("HomingSlow", ("_shape", ShotShape.Aimed), ("_count", 1), ("_move", MoveType.Homing),
                    ("_speed", 1.8f), ("_turnRate", 50f));
            var spots = new[] { TopLeft, TopRight, Left, Right };
            for (int i = 0; i < spots.Length; i++) Fire(l.Next(), t + i * 0.3, 6.5 - i * 0.3, _yellow, homing, spots[i], 1.2f, d);
        }

        /// <summary>左右から弾の列（壁）。弾のすき間を抜ける。Easy は弾が少なく、すき間が広い。</summary>
        private static void SideWalls(Lanes l, double t, Difficulty d)
        {
            string suffix = d == Difficulty.Easy ? "Easy" : "";
            var (countL, countR) = d == Difficulty.Easy ? (5, 4) : (7, 6);
            var wallL = P($"WallFromLeft{suffix}", ("_shape", ShotShape.Line), ("_count", countL), ("_angle", 0f), ("_width", 3.2f), ("_speed", 1.6f));
            var wallR = P($"WallFromRight{suffix}", ("_shape", ShotShape.Line), ("_count", countR), ("_angle", 180f), ("_width", 3.2f), ("_speed", 1.6f));
            Fire(l.Next(), t, 6, _white, wallL, Left, 1.4f, d);
            Fire(l.Next(), t + 0.7, 5.3, _yellow, wallR, Right, 1.4f, d);
        }

        /// <summary>左回り・右回りに曲がる輪を重ねて格子にする。Easy は弾が少なく遅い。</summary>
        private static void CurveLattice(Lanes l, double t, Difficulty d)
        {
            string suffix = d == Difficulty.Easy ? "Easy" : "";
            var (count, speed) = d == Difficulty.Easy ? (8, 1.5f) : (10, 1.8f);
            var curveL = P($"CurveRingLeft{suffix}", ("_shape", ShotShape.Ring), ("_count", count), ("_move", MoveType.Curve),
                ("_speed", speed), ("_angularVelocity", 50f));
            var curveR = P($"CurveRingRight{suffix}", ("_shape", ShotShape.Ring), ("_count", count), ("_move", MoveType.Curve),
                ("_speed", speed), ("_angularVelocity", -50f));
            Fire(l.Next(), t, 7, _white, curveL, Top, 1f, d);
            Fire(l.Next(), t, 7, _yellow, curveR, Top, 1f, d);
        }

        // ───────── 本番では使わない技（上から撃つだけ） ─────────

        private static void AccelBloom(Lanes l, double t, Difficulty d)
        {
            var bloom = P("AccelBloom", ("_shape", ShotShape.Ring), ("_count", 20), ("_move", MoveType.Accelerate),
                ("_speed", 0.2f), ("_acceleration", 2.5f), ("_maxSpeed", 4f));
            var track = l.Next();
            var spots = new[] { TopLeft, TopRight, Top };
            double gap = 1.1 * Pace(d);
            for (int i = 0; i * gap < 6.5; i++) Shot(track, t + i * gap, 0.1, _white, bloom, spots[i % spots.Length], 0f);
        }

        private static void AimedStream(Lanes l, double t, Difficulty d)
        {
            var stream = P("AimedStream", ("_shape", ShotShape.Aimed), ("_count", 1), ("_speed", 4.5f));
            var track = l.Next();
            for (int i = 0; i < 4; i++) Fire(track, t + i * 1.8, 1, _white, stream, i % 2 == 0 ? TopLeft : TopRight, 0.1f, d);
        }

        private static void AimedFan(Lanes l, double t, Difficulty d) => Fire(l.Next(), t, 7, _white, Fan5(), Top, 0.9f, d);

        private static void ArcCurtain(Lanes l, double t, Difficulty d)
        {
            var arc9 = P("ArcDown9", ("_shape", ShotShape.Arc), ("_count", 9), ("_angle", -90f), ("_spread", 14f), ("_speed", 2f));
            var arc8 = P("ArcDown8", ("_shape", ShotShape.Arc), ("_count", 8), ("_angle", -90f), ("_spread", 14f), ("_speed", 2f));
            Fire(l.Next(), t, 7, _white, arc9, Top, 1.2f, d);
            Fire(l.Next(), t + 0.6, 6.4, _yellow, arc8, Top, 1.2f, d);
        }

        private static void BigBalls(Lanes l, double t, Difficulty d)
        {
            var bigRing = P("BigRing", ("_shape", ShotShape.Ring), ("_count", 8), ("_speed", 1.2f));
            Fire(l.Next(), t, 7, _big, bigRing, Top, 1.8f, d);
            Fire(l.Next(), t + 0.9, 6, _white, Fan5(), Top, 1.8f, d);
        }

        private static void WaveCurtain(Lanes l, double t, Difficulty d)
        {
            var wave = P("WaveCurtain", ("_shape", ShotShape.Line), ("_count", 6), ("_angle", -90f), ("_width", 5f),
                ("_move", MoveType.SineWave), ("_speed", 1.5f), ("_amplitude", 0.4f), ("_frequency", 1f));
            Fire(l.Next(), t, 6.5, _white, wave, new Vector2(0.5f, 1.1f), 0.8f, d);
        }

        // ───────── 飛ばし方（複数の技で使うもの） ─────────

        /// <summary>一定秒で消えるホーミング。Easy は遅く、曲がりにくく、早く消える。</summary>
        private static FirePattern HomingFadePattern(Difficulty d) => d == Difficulty.Easy
            ? P("HomingFadeEasy", ("_shape", ShotShape.Aimed), ("_count", 1), ("_move", MoveType.Homing),
                ("_speed", 1.7f), ("_turnRate", 70f), ("_lifetime", 2.8f))
            : P("HomingFade", ("_shape", ShotShape.Aimed), ("_count", 1), ("_move", MoveType.Homing),
                ("_speed", 2.2f), ("_turnRate", 110f), ("_lifetime", 3.2f));

        /// <summary>縮む輪。大きな輪からゆっくり縮み、中心（撃ったときの自機の位置）を少し過ぎたところで消える。</summary>
        private static FirePattern ConvergePattern(Difficulty d)
        {
            const float radius = 2.4f;
            var (count, speed) = Pick(d, (6, 0.5f), (10, 0.7f), (12, 0.8f));
            return P($"Converge{d}", ("_shape", ShotShape.Converge), ("_count", count), ("_radius", radius), ("_spiralStep", 17f),
                ("_speed", speed), ("_lifetime", radius / speed + 0.6f));
        }

        private static FirePattern GapWall(string name, float angle, int count, float width, float gap, float speed, Difficulty d) =>
            P(name, ("_shape", ShotShape.GapWall), ("_count", count), ("_angle", angle), ("_width", width),
                ("_gap", gap), ("_gapMode", d == Difficulty.Easy ? GapMode.Random : GapMode.Alternate), ("_speed", speed));

        private static FirePattern FreezePattern(string name, float angle, Difficulty d)
        {
            var (count, aimSpeed) = d == Difficulty.Easy ? (7, 2.8f) : (9, 3.5f);
            return P(name, ("_shape", ShotShape.Arc), ("_count", count), ("_angle", angle), ("_spread", 20f),
                ("_move", MoveType.StopAndAim), ("_speed", 2.5f), ("_stopTime", 0.7f), ("_waitTime", 0.5f), ("_maxSpeed", aimSpeed));
        }

        private static FirePattern Fan5() =>
            P("AimedFan5", ("_shape", ShotShape.Aimed), ("_count", 5), ("_spread", 12f), ("_speed", 2.8f));

        // ───────── 共通 ─────────

        /// <summary>発射間隔の倍率。Easy は間隔を空け、Hard は詰める。</summary>
        private static float Pace(Difficulty d) => Pick(d, 1.7f, 1f, 0.75f);

        private static T Pick<T>(Difficulty d, T easy, T medium, T hard) =>
            d switch { Difficulty.Easy => easy, Difficulty.Medium => medium, _ => hard };

        /// <summary>Shot と同じだが、発射間隔を難易度で変える。</summary>
        private static void Fire(ShotTrack track, double start, double duration, BulletType type, FirePattern pattern,
            Vector2 position, float interval, Difficulty d) =>
            Shot(track, start, duration, type, pattern, position, interval * Pace(d));

        private static FirePattern P(string name, params (string, object)[] props) =>
            MakePattern($"Attacks/{name}", props);

        private static BulletType LoadBulletType(string name)
        {
            var type = AssetDatabase.LoadAssetAtPath<BulletType>($"{BulletDir}/{name}.asset");
            if (type == null)
                throw new InvalidOperationException($"{BulletDir}/{name}.asset がありません。先に Rebuild Game Setup を実行してください");
            return type;
        }

        private static BulletType MakeBulletType(string name, Color color, float scale, float radius)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Circle.png");
            var type = CreateAsset<BulletType>($"{BulletDir}/{name}.asset");
            Set(type, ("_sprite", sprite), ("_color", color), ("_scale", scale), ("_radius", radius));
            return type;
        }
    }
}
