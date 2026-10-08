using System;
using LidarBattle.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace LidarBattle.EditorTools
{
    /// <summary>
    /// LiDAR シミュレーション検証シーン (LidarSimScene) を生成する。
    /// 1 unit = 1 m。センサー (UST-20LX 相当) を原点に上向きで置き、ハート＋手・壁・置物をコライダとして配置する。
    /// スプライトは Rebuild Game Setup が生成した Assets/Art を再利用する。
    /// batchmode: Unity.exe -batchmode -quit -projectPath . -executeMethod LidarBattle.EditorTools.LidarSimSceneBuilder.Build
    /// 既存の同名シーンは上書きする。
    /// </summary>
    public static class LidarSimSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Test/LidarSimScene.unity";

        [MenuItem("Tools/LiDAR Battle/Build LiDAR Sim Scene")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                var square = LoadSprite("Square");
                var circle = LoadSprite("Circle");
                var heart = LoadSprite("Heart");

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                var camera = new GameObject("Main Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true;
                camera.orthographicSize = 0.65f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
                camera.transform.position = new Vector3(0f, 0.55f, -10f);
                new GameObject("Global Light 2D").AddComponent<Light2D>().lightType = Light2D.LightType.Global;

                BuildEnvironment(square);
                var simulator = BuildSensor(circle);
                var target = BuildHeart(heart, square);
                BuildEvaluator(simulator, target, circle);

                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log($"[LidarSimSceneBuilder] 完了: {ScenePath}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static Sprite LoadSprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/{name}.png");
            if (sprite == null)
                throw new InvalidOperationException($"Assets/Art/{name}.png がありません。先に Rebuild Game Setup を実行してください。");
            return sprite;
        }

        /// <summary>壁 (3 辺) と置物。背景差分の効果と最近点法の弱点を見るための静的物体。</summary>
        private static void BuildEnvironment(Sprite square)
        {
            var env = new GameObject("Environment");
            var walls = env.AddComponent<EdgeCollider2D>();
            walls.points = new[]
            {
                new Vector2(-0.6f, 0f), new Vector2(-0.6f, 1.1f), new Vector2(0.6f, 1.1f), new Vector2(0.6f, 0f),
            };
            Wall(env.transform, square, new Vector2(-0.6f, 0.55f), new Vector2(0.01f, 1.1f));
            Wall(env.transform, square, new Vector2(0f, 1.1f), new Vector2(1.2f, 0.01f));
            Wall(env.transform, square, new Vector2(0.6f, 0.55f), new Vector2(0.01f, 1.1f));

            // ハートより手前に来ることがある置物 (コップ等) 。
            var clutter = GameSetupBuilder.MakeSpriteObject("Clutter", env.transform, square, Color.gray, 5);
            clutter.position = new Vector2(0.45f, 0.35f);
            clutter.localScale = new Vector3(0.06f, 0.06f, 1f);
            clutter.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one;
        }

        private static void Wall(Transform parent, Sprite square, Vector2 position, Vector2 size)
        {
            var wall = GameSetupBuilder.MakeSpriteObject("Wall", parent, square, new Color(0.5f, 0.5f, 0.5f), 5);
            wall.position = position;
            wall.localScale = new Vector3(size.x, size.y, 1f);
        }

        private static LidarSimulator BuildSensor(Sprite circle)
        {
            // 点群メッシュはワールド座標で書くので原点・無回転のルートに置く。
            var view = new GameObject("PointCloud", typeof(MeshFilter), typeof(MeshRenderer)).AddComponent<PointCloudView>();
            view.GetComponent<MeshRenderer>().sortingOrder = 10;

            var sensor = new GameObject("LidarSimulator");
            sensor.transform.rotation = Quaternion.Euler(0f, 0f, 90f); // 正面 (+X) をワールド上向きに
            var simulator = sensor.AddComponent<LidarSimulator>();
            GameSetupBuilder.Set(simulator, ("_view", view));
            var body = GameSetupBuilder.MakeSpriteObject("Body", sensor.transform, circle, Color.white, 15);
            body.localScale = Vector3.one * 0.05f;
            return simulator;
        }

        private static HeartTarget BuildHeart(Sprite heartSprite, Sprite square)
        {
            var go = new GameObject("HeartTarget");
            go.transform.position = new Vector3(0f, 0.6f, 0f);

            // 見た目は子に置き、ルートのスケールは 1 のまま (コライダをメートルで定義するため)。
            var visual = GameSetupBuilder.MakeSpriteObject("Visual", go.transform, heartSprite, Color.red, 20);
            visual.localScale = Vector3.one * 0.1f;
            go.AddComponent<PolygonCollider2D>().SetPath(0, HeartOutline(32, 0.09f));

            // ハートを持つ手。センサーから見て奥 (+Y) に伸びる。
            var hand = GameSetupBuilder.MakeSpriteObject("Hand", go.transform, square, new Color(1f, 0.85f, 0.7f, 0.5f), 18);
            hand.localPosition = new Vector3(0f, 0.19f, 0f);
            hand.localScale = new Vector3(0.06f, 0.3f, 1f);
            hand.gameObject.AddComponent<BoxCollider2D>().size = Vector2.one;

            return go.AddComponent<HeartTarget>();
        }

        /// <summary>ハート曲線 x=16sin³t, y=13cos t−5cos2t−2cos3t−cos4t を幅 width [m] に縮め、外接矩形の中心を原点にする。</summary>
        private static Vector2[] HeartOutline(int count, float width)
        {
            var points = new Vector2[count];
            float scale = width / 32f;
            for (int i = 0; i < count; i++)
            {
                float t = i * Mathf.PI * 2f / count;
                float s = Mathf.Sin(t);
                float x = 16f * s * s * s;
                float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
                points[i] = new Vector2(x * scale, (y + 2f) * scale); // y は -17..13 なので +2 で中心合わせ
            }
            return points;
        }

        private static void BuildEvaluator(LidarSimulator simulator, HeartTarget target, Sprite circle)
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var label = GameSetupBuilder.MakeText(canvasGo.transform, "Report", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(20f, -320f), new Vector2(-20f, -20f), 26f);

            var evaluator = new GameObject("TrackerEvaluator").AddComponent<TrackerEvaluator>();
            GameSetupBuilder.Set(evaluator, ("_simulator", simulator), ("_heart", target), ("_label", label),
                ("_markerSprite", circle));
        }
    }
}
