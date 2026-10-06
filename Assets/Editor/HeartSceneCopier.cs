using System;
using System.IO;
using LidarBattle.Config;
using LidarBattle.Flow;
using LidarBattle.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using static LidarBattle.EditorTools.GameSetupBuilder;

namespace LidarBattle.EditorTools
{
    /// <summary>
    /// 円柱用のシーン（SelectScene/BattleScene）をコピーして、ハート用のシーン（HeartSelectScene/HeartBattleScene）を作る。
    /// 違いは LiDAR の設定アセットだけ（ハート用は円の半径が大きい）。シーン遷移先もハート用の組に書き換える。
    /// 元のシーンを直したら、もう一度実行すれば同じ状態に戻る（ハート用シーンの GUID は保つ）。
    /// ハート用の設定アセットは無いときだけ作り、以降は上書きしない（現場で調整した値を残すため）。
    /// </summary>
    public static class HeartSceneCopier
    {
        private const string SourceSettingsPath = "Assets/Settings/LidarSettings.asset";
        private const string HeartSettingsPath = "Assets/Settings/LidarSettings_Heart.asset";
        private const float HeartRadiusM = 0.040f; // 幅 9 cm のハート（lidar-simulation.md で最も偏りが小さかった値）

        private const string SelectScene = "SelectScene";
        private const string BattleScene = "BattleScene";
        private const string HeartSelectScene = "HeartSelectScene";
        private const string HeartBattleScene = "HeartBattleScene";

        [MenuItem("Tools/LiDAR Battle/Copy Scenes for Heart")]
        public static void Copy()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                // コピー先のシーンを開いたまま上書きしないよう、空のシーンにしておく。
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EnsureHeartSettings();

                CopyScene(SelectScene, HeartSelectScene, flow => Set(flow, ("_battleSceneName", HeartBattleScene)));
                CopyScene(BattleScene, HeartBattleScene, flow => Set(flow, ("_selectSceneName", HeartSelectScene)));

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AddToBuildSettings(ScenePath(HeartSelectScene), ScenePath(HeartBattleScene));
                AssetDatabase.SaveAssets();
                Debug.Log("[HeartSceneCopier] 完了");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void EnsureHeartSettings()
        {
            if (AssetDatabase.LoadAssetAtPath<LidarSettings>(HeartSettingsPath) != null) return;

            if (!AssetDatabase.CopyAsset(SourceSettingsPath, HeartSettingsPath))
                throw new InvalidOperationException($"{SourceSettingsPath} を {HeartSettingsPath} にコピーできません。");
            var settings = AssetDatabase.LoadAssetAtPath<LidarSettings>(HeartSettingsPath);
            settings.HeartRadiusM = HeartRadiusM;
            EditorUtility.SetDirty(settings);
            // 未保存のまま別のシーンを開くとアセットが読み込みから外れ、シーンからの参照が fileID: 0 になる。
            AssetDatabase.SaveAssets();
        }

        /// <summary>シーンファイルをコピーし、LiDAR の設定とシーン遷移先をハート用にする。</summary>
        private static void CopyScene(string source, string destination, Action<MonoBehaviour> patchFlow)
        {
            // AssetDatabase.CopyAsset は毎回新しい GUID を振るので、ファイルの中身だけ上書きして GUID を保つ。
            File.Copy(ScenePath(source), ScenePath(destination), overwrite: true);
            AssetDatabase.ImportAsset(ScenePath(destination), ImportAssetOptions.ForceUpdate);

            var scene = EditorSceneManager.OpenScene(ScenePath(destination));
            var settings = AssetDatabase.LoadAssetAtPath<LidarSettings>(HeartSettingsPath); // シーンを開いた後に読む
            var lidars = Object.FindObjectsByType<LidarInputSource>(FindObjectsSortMode.None);
            if (lidars.Length == 0) throw new InvalidOperationException($"{source} に LidarInputSource がありません。");
            foreach (var lidar in lidars) Set(lidar, ("_settings", settings));

            MonoBehaviour flow = Object.FindFirstObjectByType<SelectFlow>();
            if (flow == null) flow = Object.FindFirstObjectByType<BattleFlow>();
            if (flow == null) throw new InvalidOperationException($"{source} に SelectFlow / BattleFlow がありません。");
            patchFlow(flow);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static string ScenePath(string name) => $"Assets/Scenes/{name}.unity";
    }
}
