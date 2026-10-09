using System;
using System.IO;
using LidarBattle.Battle;
using LidarBattle.Flow;
using LidarBattle.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using static LidarBattle.EditorTools.GameSetupBuilder;

namespace LidarBattle.EditorTools
{
    /// <summary>
    /// 本番（元の版）の難易度別 Timeline を作る（Assets/Timelines/Battle/Easy1〜Hard4）。難易度ごとに数本あり、
    /// BattleFlow が毎回ランダムに 1 本選ぶ（列に並んでいる人が前のプレイを見て覚えられないように）。
    /// 1 本は 60 秒で、AttackLibraryBuilder の技を 8 秒ずつ並べ、セリフと立ち絵の動きを足したもの。
    /// 作り直しても GUID は変わらないので、本数を変えなければシーンを作り直さなくてよい（本数を変えたら Rebuild Game Setup）。
    /// </summary>
    public static class BattleTimelineBuilder
    {
        internal const string BattleTimelineDir = "Assets/Timelines/Battle";

        private const double FirstAttack = 3;
        private static readonly Difficulty[] Difficulties = { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard };

        // [何本目][何番目の技]。技は FirstAttack 秒から AttackSeconds 秒ずつ（3〜59 秒）。全難易度で同じ表を使い、
        // 難易度で変わるのは技の中身（弾の速さ・数・発射間隔・すき間の幅）だけ。技は重ねない（重ねると確実によけられなくなる）。
        // 4 本どれにも安置（SafeZone）と道が入る。道は 1 本の中では縦（Corridor）か横（CorridorSide）の 1 種類だけ。
        // 残りの 5 枠は 1 本ごとに使う技を変える（並べ替えだけにしない）。
        private static readonly string[][] Plans =
        {
            new[] { "GapWalls", "HomingFade", "Corridor", "Converge", "SafeZone", "FreezeAim", "CurveLattice" },
            new[] { "SideWalls", "Converge", "CorridorSide", "Fireworks", "SafeZone", "GapWallsSide", "HomingSwarm" },
            new[] { "CurveLattice", "SafeZone", "GapWallsSide", "HomingFade", "Corridor", "SideWalls", "Fireworks" },
            new[] { "FreezeAim", "GapWalls", "SafeZone", "HomingSwarm", "CorridorSide", "Converge", "Fireworks" },
        };

        [MenuItem("Tools/LiDAR Battle/Build Battle Timelines")]
        public static void BuildMenu()
        {
            try
            {
                Build();
                AssetDatabase.SaveAssets();
                Debug.Log("[BattleTimelineBuilder] 完了");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>難易度ごとの Timeline（[Difficulty][何本目]）を作る。White/Yellow の弾が先に要る。</summary>
        internal static TimelineAsset[][] Build()
        {
            Directory.CreateDirectory(BattleTimelineDir);
            AttackLibraryBuilder.LoadBulletTypes();
            var result = new TimelineAsset[Difficulties.Length][];
            foreach (var difficulty in Difficulties)
            {
                var timelines = result[(int)difficulty] = new TimelineAsset[Plans.Length];
                for (int i = 0; i < Plans.Length; i++)
                {
                    var plan = Plans[i];
                    timelines[i] = MakeTimeline($"{BattleTimelineDir}/{difficulty}{i + 1}", tl =>
                    {
                        AddAttacks(tl, plan, difficulty);
                        AddTalk(tl, difficulty);
                    });
                }
            }
            return result;
        }

        /// <summary>弾幕テストシーンで選べるよう、作った Timeline を難易度順に返す（無いものは飛ばす）。</summary>
        internal static TimelineAsset[] Load()
        {
            var list = new System.Collections.Generic.List<TimelineAsset>();
            foreach (var difficulty in Difficulties)
            for (int i = 0; i < Plans.Length; i++)
            {
                var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>($"{BattleTimelineDir}/{difficulty}{i + 1}.playable");
                if (timeline != null) list.Add(timeline);
            }
            return list.ToArray();
        }

        /// <summary>技を 8 秒ずつ並べる。2 つ目からは、技の始まる少し前に盤面の弾を全部消す（前の技の弾を残さない）。</summary>
        private static void AddAttacks(TimelineAsset timeline, string[] plan, Difficulty d)
        {
            var lanes = new AttackLibraryBuilder.Lanes(timeline);
            var clear = timeline.CreateTrack<ShotTrack>(null, "Clear");
            for (int i = 0; i < plan.Length; i++)
            {
                double start = FirstAttack + i * AttackLibraryBuilder.AttackSeconds;
                if (i > 0)
                {
                    var clip = clear.CreateClip<ClearClip>();
                    clip.start = start - 0.2;
                    clip.duration = 0.1;
                    clip.displayName = "Clear";
                }
                lanes.Rewind();
                AttackLibraryBuilder.Add(plan[i], lanes, start, d);
            }
        }

        /// <summary>
        /// セリフ（最初・半ば・終わり近く）と立ち絵の表情・動き。セリフの無い間は立ち絵が盤面の左の列を上下に歩き回る。
        /// 口調は Easy がやさしく、Medium はやわらかいけれど少し厳しく、Hard は厳しく。
        /// </summary>
        private static void AddTalk(TimelineAsset timeline, Difficulty d)
        {
            var (dialogue, p, shake) = MakeTalkTracks(timeline);
            switch (d)
            {
                case Difficulty.Easy:
                    Say(dialogue, p, 0, 3, "がんばっテ！");
                    Say(dialogue, p, 27, 3, "じょうずだネ！そのちょうしだヨ！", PortraitExpression.Smile);
                    Say(dialogue, p, 51, 3, "あと少しだヨ、ファイト！");
                    break;
                case Difficulty.Medium:
                    Say(dialogue, p, 0, 3, "さあ、始めよう～～！。がんばってね");
                    Say(dialogue, p, 27, 3, "なかなかやるね～～。でも油断は禁物だよ～～", PortraitExpression.Smile);
                    Say(dialogue, p, 51, 3, "あと少し。最後まで気を抜かないで～～");
                    break;
                default:
                    Say(dialogue, p, 0, 3, "本気でいくがお。ついてこられる？");
                    Say(dialogue, p, 27, 3, "その程度？まだまだこれからだ、がお", PortraitExpression.Smile);
                    Say(dialogue, p, 51, 3, "あと少し……バイバイ");
                    break;
            }
            Attack(p, shake, 3, 2);
            Attack(p, shake, 35, 2);
            Climb(timeline, d switch { Difficulty.Easy => 0.1f, Difficulty.Medium => 0.15f, _ => 0.2f }, (5, 27), (30, 51), (54, 60));
        }
    }
}
