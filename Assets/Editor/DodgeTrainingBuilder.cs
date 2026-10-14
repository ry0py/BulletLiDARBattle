using System;
using System.IO;
using LidarBattle.Battle;
using LidarBattle.Flow;
using LidarBattle.Input;
using LidarBattle.Training;
using LidarBattle.UI;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.MLAgents;
using Object = UnityEngine.Object;

namespace LidarBattle.EditorTools
{
    /// <summary>
    /// 強化学習（ML-Agents）で弾をよける AI を学習させるためのシーンと exe を作る。詳細は .claude/docs/rl-training.md。
    /// - Build RL Training Scene: 本番の 12 技を難易度ごとに 8 秒の Timeline にし（Assets/Timelines/Training/）、
    ///   BattleScene のコピーから会話・入力源を外して DodgeArena/DodgeAgent を置いたシーンを作る。
    /// - Build RL Training Exe: そのシーンだけの exe を Builds/RLTraining/ に作る（複数同時に回す用）。
    /// BattleScene や技を作り直したら、これも作り直す。既存の同名シーン・Timeline は上書きする。
    /// </summary>
    public static class DodgeTrainingBuilder
    {
        private const string SourcePath = "Assets/Scenes/Actual/BattleScene.unity";
        internal const string ScenePath = "Assets/Scenes/Test/RLTrainingScene.unity";
        private const string TimelineDir = "Assets/Timelines/Training";
        private const string ExePath = "Builds/RLTraining/BulletDodge.exe";
        private const string BehaviorName = "Dodge";
        private const int MaxBullets = 128;
        private const int DecisionPeriod = 4; // 60 fps で 1 秒に 15 回決める

        private static readonly Difficulty[] Difficulties = { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard };
        /// <summary>技の番号（YAML の attack）はこの並び。本番で使う 12 技。</summary>
        private static string[] Attacks => AttackLibraryBuilder.BattleAttacks;
        private static readonly string[] BulletTypeNames = { "White", "Yellow", "Blue", "Big" };

        [MenuItem("Tools/LiDAR Battle/Build RL Training Scene")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                BuildTimelines();
                BuildScene();
                Debug.Log($"[DodgeTrainingBuilder] 完了: {ScenePath}\n技の番号: " +
                          string.Join(", ", Array.ConvertAll(Attacks, a => $"{Array.IndexOf(Attacks, a)}={a}")));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/LiDAR Battle/Build RL Training Exe")]
        public static void BuildExe()
        {
            if (!File.Exists(ScenePath)) Build();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ExePath,
                target = EditorUserBuildSettings.activeBuildTarget,
                options = BuildOptions.None,
            });
            Debug.Log($"[DodgeTrainingBuilder] exe: {report.summary.result} {Path.GetFullPath(ExePath)}");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        private static void BuildTimelines()
        {
            AttackLibraryBuilder.LoadBulletTypes();
            foreach (var d in Difficulties)
            {
                Directory.CreateDirectory($"{TimelineDir}/{d}");
                foreach (string name in Attacks)
                    GameSetupBuilder.MakeTimeline(TimelinePath(d, name),
                        tl => AttackLibraryBuilder.Add(name, new AttackLibraryBuilder.Lanes(tl), 0, d), AttackLibraryBuilder.AttackSeconds);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// [難易度][技] の順に 1 列に並べて返す（DodgeArena._timelines の並び）。
        /// シーンを開くと使われていないアセットが解放されるので、シーンを開いた後に呼ぶ（先に読むと参照が fileID: 0 になる）。
        /// </summary>
        private static Object[] LoadTimelines()
        {
            var result = new Object[Difficulties.Length * Attacks.Length];
            foreach (var d in Difficulties)
                for (int i = 0; i < Attacks.Length; i++)
                    result[(int)d * Attacks.Length + i] = AssetDatabase.LoadAssetAtPath<TimelineAsset>($"{TimelinePath(d, Attacks[i])}.playable")
                        ?? throw new InvalidOperationException($"{TimelinePath(d, Attacks[i])} を作れませんでした");
            return result;
        }

        private static string TimelinePath(Difficulty d, string attack) => $"{TimelineDir}/{d}/{attack}";

        private static void BuildScene()
        {
            AssetDatabase.DeleteAsset(ScenePath);
            if (!AssetDatabase.CopyAsset(SourcePath, ScenePath))
                throw new InvalidOperationException($"{SourcePath} をコピーできません");
            var scene = EditorSceneManager.OpenScene(ScenePath);

            var battle = Object.FindFirstObjectByType<BattleFlow>();
            var so = new SerializedObject(battle);
            var director = (PlayableDirector)so.FindProperty("_director").objectReferenceValue;
            var clock = (BattleClock)so.FindProperty("_clock").objectReferenceValue;
            var bullets = (BulletSystem)so.FindProperty("_bullets").objectReferenceValue;
            var go = battle.gameObject;
            Object.DestroyImmediate(battle);
            go.name = nameof(DodgeArena);

            Object.FindFirstObjectByType<DialogueBox>(FindObjectsInactive.Include).gameObject.SetActive(false);
            Object.FindFirstObjectByType<PortraitView>(FindObjectsInactive.Include)?.gameObject.SetActive(false);

            // 人の入力源は外す（カメラ入力は UDP ポートを開くので、exe を複数同時に動かすとぶつかる）。速さだけ引き継ぐ。
            var selector = Object.FindFirstObjectByType<HeartInputSelector>();
            float speed = new SerializedObject(selector.GetComponent<KeyboardInputSource>()).FindProperty("_speed").floatValue;
            var inputObject = selector.gameObject;
            Object.DestroyImmediate(selector);
            Object.DestroyImmediate(inputObject.GetComponent<KeyboardInputSource>());
            Object.DestroyImmediate(inputObject.GetComponent<LidarInputSource>());
            Object.DestroyImmediate(inputObject.GetComponent<CameraInputSource>());

            var soul = bullets.Soul;
            var arena = go.AddComponent<DodgeArena>();
            GameSetupBuilder.Set(arena, ("_director", director), ("_clock", clock), ("_bullets", bullets), ("_soul", soul),
                ("_timelines", LoadTimelines()));

            var agentObject = new GameObject(nameof(DodgeAgent));
            var behavior = agentObject.AddComponent<BehaviorParameters>();
            behavior.BehaviorName = BehaviorName;
            behavior.BrainParameters.VectorObservationSize = DodgeAgent.SelfObservationSize;
            behavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(DodgeAgent.ActionCount);

            var sensor = agentObject.AddComponent<BufferSensorComponent>();
            sensor.SensorName = "Bullets";
            sensor.ObservableSize = DodgeAgent.BulletObservationSize(BulletTypeNames.Length);
            sensor.MaxNumObservables = MaxBullets;

            var agent = agentObject.AddComponent<DodgeAgent>();
            agent.MaxStep = 0; // エピソードの終わりは DodgeArena の時間で決める
            GameSetupBuilder.Set(agent, ("_arena", arena), ("_speed", speed), ("_bulletTypes", LoadBulletTypes()));

            var requester = agentObject.AddComponent<DecisionRequester>();
            requester.DecisionPeriod = DecisionPeriod;
            requester.TakeActionsBetweenDecisions = false;

            GameSetupBuilder.Set(soul, ("_inputSource", agent));
            EditorUtility.SetDirty(behavior);
            EditorUtility.SetDirty(sensor);
            EditorUtility.SetDirty(requester);
            EditorSceneManager.SaveScene(scene);
        }

        private static Object[] LoadBulletTypes() => Array.ConvertAll(BulletTypeNames, name =>
            (Object)(AssetDatabase.LoadAssetAtPath<BulletType>($"{GameSetupBuilder.BulletDir}/{name}.asset")
                     ?? throw new InvalidOperationException($"{name}.asset がありません")));
    }
}
