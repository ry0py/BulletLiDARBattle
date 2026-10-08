using System;
using LidarBattle.Flow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace LidarBattle.EditorTools
{
    /// <summary>
    /// ユーザー設定シーン (UserSettingsScene) を生成する。選択シーンで [U] を 5 秒長押しすると来る（OperatorShortcuts）。
    /// 既存の同名シーンは上書きする。
    /// </summary>
    public static class UserSettingsSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Actual/UserSettingsScene.unity";

        [MenuItem("Tools/LiDAR Battle/Build User Settings Scene")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                new GameObject("Global Light 2D").AddComponent<Light2D>().lightType = Light2D.LightType.Global;

                var canvasGo = new GameObject("Canvas", typeof(RectTransform));
                canvasGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                var label = GameSetupBuilder.MakeText(canvasGo.transform, "Settings", Vector2.zero, Vector2.one,
                    new Vector2(160f, 160f), new Vector2(-160f, -160f), 56f);
                label.alignment = TextAlignmentOptions.Center;

                var view = canvasGo.AddComponent<UserSettingsView>();
                GameSetupBuilder.Set(view, ("_label", label));

                EditorSceneManager.SaveScene(scene, ScenePath);
                GameSetupBuilder.AddToBuildSettings(ScenePath);
                Debug.Log($"[UserSettingsSceneBuilder] 完了: {ScenePath}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
