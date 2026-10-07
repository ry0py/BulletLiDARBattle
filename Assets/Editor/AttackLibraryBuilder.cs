using System;
using System.IO;
using LidarBattle.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using static LidarBattle.EditorTools.GameSetupBuilder;

namespace LidarBattle.EditorTools
{
    /// <summary>
    /// 弾幕の「技」を 1 つずつ短い Timeline として作る（Assets/Timelines/Attacks/）。
    /// どの技も ShotTrack だけで、長さは AttackSeconds。難易度別 Timeline やシーンは変えない。
    /// FirePattern は Assets/Settings/Bullets/Attacks/、弾の種類は Assets/Settings/Bullets/ に置く
    /// （White/Yellow は Rebuild Game Setup が作ったものを使う）。既存の同名アセットは上書きする。
    /// </summary>
    public static class AttackLibraryBuilder
    {
        private const string AttackTimelineDir = "Assets/Timelines/Attacks";
        private const string AttackPatternDir = BulletDir + "/Attacks";
        private const double AttackSeconds = 8;

        // 発射位置（盤面の正規化座標）。枠の外から撃つ。
        private static readonly Vector2 Top = new(0.5f, 1.15f);
        private static readonly Vector2 TopLeft = new(0.1f, 1.15f);
        private static readonly Vector2 TopRight = new(0.9f, 1.15f);
        private static readonly Vector2 Left = new(-0.08f, 0.5f);
        private static readonly Vector2 Right = new(1.08f, 0.5f);
        private static readonly Vector2 Bottom = new(0.5f, -0.15f);

        [MenuItem("Tools/LiDAR Battle/Build Attack Library")]
        public static void Build()
        {
            try
            {
                Directory.CreateDirectory(AttackTimelineDir);
                Directory.CreateDirectory(AttackPatternDir);
                BuildAttacks();
                AssetDatabase.SaveAssets();
                Debug.Log("[AttackLibraryBuilder] 完了");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void BuildAttacks()
        {
            var white = LoadBulletType("White");
            var yellow = LoadBulletType("Yellow");
            var small = MakeBulletType("Small", new Color(0.4f, 0.9f, 1f), 0.15f, 0.05f);
            var big = MakeBulletType("Big", new Color(1f, 0.45f, 0.3f), 0.5f, 0.2f);

            // ── 全方位 ──
            var ring16 = P("Ring16", ("_shape", ShotShape.Ring), ("_count", 16), ("_speed", 1.8f));
            var ring16Offset = P("Ring16Offset", ("_shape", ShotShape.Ring), ("_count", 16), ("_angle", 11.25f), ("_speed", 1.8f));
            Attack("RingBurst", tl =>
            {
                Shot(Track(tl), 0, 7, white, ring16, Top, 1.6f);
                Shot(Track(tl), 0.8, 6.2, yellow, ring16Offset, Top, 1.6f);
            });

            var bloom = P("AccelBloom", ("_shape", ShotShape.Ring), ("_count", 20), ("_move", MoveType.Accelerate),
                ("_speed", 0.2f), ("_acceleration", 2.5f), ("_maxSpeed", 4f));
            Attack("AccelBloom", tl =>
            {
                var t = Track(tl);
                var spots = new[] { TopLeft, TopRight, Top };
                for (int i = 0; i < 6; i++) Shot(t, i * 1.1, 0.1, white, bloom, spots[i % spots.Length], 0f);
            });

            var curveL = P("CurveRingLeft", ("_shape", ShotShape.Ring), ("_count", 10), ("_move", MoveType.Curve),
                ("_speed", 1.8f), ("_angularVelocity", 50f));
            var curveR = P("CurveRingRight", ("_shape", ShotShape.Ring), ("_count", 10), ("_move", MoveType.Curve),
                ("_speed", 1.8f), ("_angularVelocity", -50f));
            Attack("CurveLattice", tl =>
            {
                Shot(Track(tl), 0, 7, white, curveL, Top, 1f);
                Shot(Track(tl), 0, 7, yellow, curveR, Top, 1f);
            });

            var firework = P("Firework", ("_shape", ShotShape.Ring), ("_count", 14), ("_move", MoveType.Gravity),
                ("_speed", 1.3f), ("_gravity", 1.2f));
            Attack("Fireworks", tl =>
            {
                var t = Track(tl);
                var spots = new[] { new Vector2(0.25f, 1f), new Vector2(0.75f, 1f), new Vector2(0.5f, 1f) };
                for (int i = 0; i < 5; i++) Shot(t, i * 1.4, 0.1, yellow, firework, spots[i % spots.Length], 0f);
            });

            // ── 渦巻き ──
            var spiralCw = P("SpiralCW", ("_shape", ShotShape.Spiral), ("_count", 3), ("_spiralStep", -11f), ("_speed", 2f));
            var spiralCcw = P("SpiralCCW", ("_shape", ShotShape.Spiral), ("_count", 3), ("_spiralStep", 11f), ("_speed", 2f));
            Attack("DoubleSpiral", tl =>
            {
                Shot(Track(tl), 0, 7, white, spiralCw, Top, 0.15f);
                Shot(Track(tl), 0, 7, yellow, spiralCcw, Top, 0.15f);
            });

            var flower = P("FlowerSpiral", ("_shape", ShotShape.Spiral), ("_count", 6), ("_spiralStep", 5f), ("_speed", 1.6f));
            Attack("FlowerSpiral", tl => Shot(Track(tl), 0, 7, small, flower, Top, 0.25f));

            var twist = P("TwistSpiral", ("_shape", ShotShape.Spiral), ("_count", 5), ("_spiralStep", 9f),
                ("_move", MoveType.Curve), ("_speed", 1.8f), ("_angularVelocity", 30f));
            Attack("TwistSpiral", tl => Shot(Track(tl), 0, 7, white, twist, Top, 0.3f));

            // ── 自機狙い ──
            var stream = P("AimedStream", ("_shape", ShotShape.Aimed), ("_count", 1), ("_speed", 4.5f));
            Attack("AimedStream", tl =>
            {
                var t = Track(tl);
                for (int i = 0; i < 4; i++) Shot(t, i * 1.8, 1, small, stream, i % 2 == 0 ? TopLeft : TopRight, 0.1f);
            });

            var fan5 = P("AimedFan5", ("_shape", ShotShape.Aimed), ("_count", 5), ("_spread", 12f), ("_speed", 2.8f));
            Attack("AimedFan", tl => Shot(Track(tl), 0, 7, white, fan5, Top, 0.9f));

            var even = P("AimedEven2", ("_shape", ShotShape.Aimed), ("_count", 2), ("_spread", 18f), ("_speed", 3.5f));
            Attack("EvenSnipe", tl => Shot(Track(tl), 0, 7, white, even, Top, 0.35f));

            var brake = P("AimedBrake", ("_shape", ShotShape.Aimed), ("_count", 3), ("_spread", 20f), ("_move", MoveType.Accelerate),
                ("_speed", 5.5f), ("_acceleration", -5f), ("_minSpeed", 1f), ("_maxSpeed", 6f));
            Attack("Brake", tl => Shot(Track(tl), 0, 7, white, brake, Top, 0.6f));

            var homing = P("HomingSlow", ("_shape", ShotShape.Aimed), ("_count", 1), ("_move", MoveType.Homing),
                ("_speed", 1.8f), ("_turnRate", 50f));
            Attack("HomingSwarm", tl =>
            {
                var spots = new[] { TopLeft, TopRight, Left, Right };
                for (int i = 0; i < spots.Length; i++) Shot(Track(tl), i * 0.3, 6.5 - i * 0.3, yellow, homing, spots[i], 1.2f);
            });

            var freeze = P("FreezeAim", ("_shape", ShotShape.Arc), ("_count", 9), ("_angle", -90f), ("_spread", 20f),
                ("_move", MoveType.StopAndAim), ("_speed", 2.5f), ("_stopTime", 0.7f), ("_waitTime", 0.5f), ("_maxSpeed", 3.5f));
            Attack("FreezeAim", tl => Shot(Track(tl), 0, 6, white, freeze, Top, 1.5f));

            // ── 向き固定の扇・ばらまき ──
            var arc9 = P("ArcDown9", ("_shape", ShotShape.Arc), ("_count", 9), ("_angle", -90f), ("_spread", 14f), ("_speed", 2f));
            var arc8 = P("ArcDown8", ("_shape", ShotShape.Arc), ("_count", 8), ("_angle", -90f), ("_spread", 14f), ("_speed", 2f));
            Attack("ArcCurtain", tl =>
            {
                Shot(Track(tl), 0, 7, white, arc9, Top, 1.2f);
                Shot(Track(tl), 0.6, 6.4, yellow, arc8, Top, 1.2f);
            });

            var pincerL = P("PincerLeft", ("_shape", ShotShape.Arc), ("_count", 5), ("_angle", 0f), ("_spread", 15f), ("_speed", 2.2f));
            var pincerR = P("PincerRight", ("_shape", ShotShape.Arc), ("_count", 5), ("_angle", 180f), ("_spread", 15f), ("_speed", 2.2f));
            Attack("Pincer", tl =>
            {
                Shot(Track(tl), 0, 7, white, pincerL, new Vector2(-0.08f, 0.7f), 0.8f);
                Shot(Track(tl), 0.4, 6.6, white, pincerR, new Vector2(1.08f, 0.3f), 0.8f);
            });

            var scatter = P("Scatter", ("_shape", ShotShape.Random), ("_count", 3), ("_angle", -90f), ("_spread", 140f),
                ("_move", MoveType.Accelerate), ("_speed", 1.2f), ("_acceleration", 1.5f), ("_maxSpeed", 3.5f));
            Attack("Scatter", tl => Shot(Track(tl), 0, 7, small, scatter, Top, 0.25f));

            var fountain = P("Fountain", ("_shape", ShotShape.Random), ("_count", 2), ("_angle", 90f), ("_spread", 50f),
                ("_move", MoveType.Gravity), ("_speed", 4.2f), ("_gravity", 3.2f));
            Attack("Fountain", tl => Shot(Track(tl), 0, 6.5, small, fountain, Bottom, 0.15f));

            // ── 列・壁 ──
            var rain = P("RainLine", ("_shape", ShotShape.RandomLine), ("_count", 2), ("_angle", -90f), ("_width", 5f), ("_speed", 2.2f));
            Attack("Rainfall", tl => Shot(Track(tl), 0, 7, small, rain, new Vector2(0.5f, 1.1f), 0.12f));

            var wallL = P("WallFromLeft", ("_shape", ShotShape.Line), ("_count", 7), ("_angle", 0f), ("_width", 3.2f), ("_speed", 1.6f));
            var wallR = P("WallFromRight", ("_shape", ShotShape.Line), ("_count", 6), ("_angle", 180f), ("_width", 3.2f), ("_speed", 1.6f));
            Attack("SideWalls", tl =>
            {
                Shot(Track(tl), 0, 6, white, wallL, Left, 1.4f);
                Shot(Track(tl), 0.7, 5.3, yellow, wallR, Right, 1.4f);
            });

            var wallDown = P("WallFromTop", ("_shape", ShotShape.Line), ("_count", 9), ("_angle", -90f), ("_width", 5f), ("_speed", 1.4f));
            Attack("CrossWalls", tl =>
            {
                Shot(Track(tl), 0, 6, white, wallDown, new Vector2(0.5f, 1.1f), 1.6f);
                Shot(Track(tl), 0.8, 5.2, yellow, wallL, Left, 1.6f);
            });

            var waveCurtain = P("WaveCurtain", ("_shape", ShotShape.Line), ("_count", 6), ("_angle", -90f), ("_width", 5f),
                ("_move", MoveType.SineWave), ("_speed", 1.5f), ("_amplitude", 0.4f), ("_frequency", 1f));
            Attack("WaveCurtain", tl => Shot(Track(tl), 0, 6.5, white, waveCurtain, new Vector2(0.5f, 1.1f), 0.8f));

            var bigRing = P("BigRing", ("_shape", ShotShape.Ring), ("_count", 8), ("_speed", 1.2f));
            Attack("BigBalls", tl =>
            {
                Shot(Track(tl), 0, 7, big, bigRing, Top, 1.8f);
                Shot(Track(tl), 0.9, 6, small, fan5, Top, 1.8f);
            });

            var waveL = P("WaveFromLeft", ("_shape", ShotShape.RandomLine), ("_count", 1), ("_angle", 0f), ("_width", 3f),
                ("_move", MoveType.SineWave), ("_speed", 2.2f), ("_amplitude", 0.35f), ("_frequency", 1.2f));
            var waveR = P("WaveFromRight", ("_shape", ShotShape.RandomLine), ("_count", 1), ("_angle", 180f), ("_width", 3f),
                ("_move", MoveType.SineWave), ("_speed", 2.2f), ("_amplitude", 0.35f), ("_frequency", 1.2f));
            Attack("SideWaves", tl =>
            {
                Shot(Track(tl), 0, 7, yellow, waveL, Left, 0.3f);
                Shot(Track(tl), 0.15, 6.85, yellow, waveR, Right, 0.3f);
            });
        }

        private static FirePattern P(string name, params (string, object)[] props) =>
            MakePattern($"Attacks/{name}", props);

        private static void Attack(string name, Action<TimelineAsset> fill) =>
            MakeTimeline($"{AttackTimelineDir}/{name}", fill, AttackSeconds);

        private static ShotTrack Track(TimelineAsset timeline) =>
            timeline.CreateTrack<ShotTrack>(null, $"Shots {timeline.outputTrackCount + 1}");

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
