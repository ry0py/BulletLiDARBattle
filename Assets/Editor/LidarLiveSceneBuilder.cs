using System;
using LidarBattle.Config;
using LidarBattle.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace LidarBattle.EditorTools
{
    /// <summary>
    /// 実機 LiDAR 確認シーン (LidarLiveScene) を生成する。1 unit = 1 m、センサーを原点に正面を上向きで置く。
    /// 接続先は Assets/Settings/LidarSettings.asset の HostName。既存の同名シーンは上書きする。
    /// </summary>
    public static class LidarLiveSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Actual/LidarLiveScene.unity";
        private const string SettingsPath = "Assets/Settings/LidarSettings.asset";

        [MenuItem("Tools/LiDAR Battle/Build LiDAR Live Scene")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                var settings = AssetDatabase.LoadAssetAtPath<LidarSettings>(SettingsPath);
                if (settings == null) throw new InvalidOperationException($"{SettingsPath} がありません。");
                var circle = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Circle.png");
                if (circle == null) throw new InvalidOperationException("Assets/Art/Circle.png がありません。先に Rebuild Game Setup を実行してください。");

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true;
                camera.orthographicSize = 2f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
                camera.transform.position = new Vector3(0f, 1f, -10f);
                new GameObject("Global Light 2D").AddComponent<Light2D>().lightType = Light2D.LightType.Global;

                // 点群メッシュはワールド座標で書くので原点・無回転のルートに置く。
                var view = new GameObject("PointCloud", typeof(MeshFilter), typeof(MeshRenderer)).AddComponent<PointCloudView>();
                view.GetComponent<MeshRenderer>().sortingOrder = 10;
                GameSetupBuilder.Set(view, ("_pointSizeM", 0.015f));

                var sensor = new GameObject("LidarLive");
                sensor.transform.rotation = Quaternion.Euler(0f, 0f, 90f); // 正面 (+X) をワールド上向きに
                var body = GameSetupBuilder.MakeSpriteObject("Body", sensor.transform, circle, Color.white, 15);
                body.localScale = Vector3.one * 0.05f;

                // 正面 (センサーの +X) を明るい線、取得範囲 (StartStep〜EndStep) の両端を薄い線で示す。
                var square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Square.png");
                float degPerStep = 360f / settings.AngularResolution;
                MakeRay("Front", sensor.transform, square, 0f, new Color(0f, 1f, 1f, 0.9f), 0.012f);
                MakeRay("RangeStart", sensor.transform, square, (settings.StartStep - settings.FrontStep) * degPerStep, new Color(1f, 1f, 1f, 0.3f), 0.006f);
                MakeRay("RangeEnd", sensor.transform, square, (settings.EndStep - settings.FrontStep) * degPerStep, new Color(1f, 1f, 1f, 0.3f), 0.006f);

                var marker = GameSetupBuilder.MakeSpriteObject("HeartMarker", null, circle, Color.red, 30);
                marker.localScale = Vector3.one * (settings.HeartRadiusM * 2f);

                var canvasGo = new GameObject("Canvas", typeof(RectTransform));
                canvasGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                var label = GameSetupBuilder.MakeText(canvasGo.transform, "Status", new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(20f, -240f), new Vector2(-20f, -20f), 28f);

                var live = sensor.AddComponent<LidarLiveView>();
                GameSetupBuilder.Set(live, ("_settings", settings), ("_view", view),
                    ("_marker", marker.GetComponent<SpriteRenderer>()), ("_label", label));

                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log($"[LidarLiveSceneBuilder] 完了: {ScenePath}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        /// <summary>センサーから angleDeg 方向 (正面 0°、反時計回りが正) に伸びる長さ 2 m の線。点群より奥に描く。</summary>
        private static void MakeRay(string name, Transform sensor, Sprite square, float angleDeg, Color color, float widthM)
        {
            const float lengthM = 2f;
            var ray = GameSetupBuilder.MakeSpriteObject(name, sensor, square, color, 5);
            ray.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
            ray.localPosition = ray.localRotation * new Vector3(lengthM * 0.5f, 0f, 0f);
            ray.localScale = new Vector3(lengthM, widthM, 1f);
        }
    }
}
