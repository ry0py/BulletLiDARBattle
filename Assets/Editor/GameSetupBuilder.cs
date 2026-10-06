using System;
using System.IO;
using TMPro;
using LidarBattle.Battle;
using LidarBattle.Config;
using LidarBattle.Flow;
using LidarBattle.Input;
using LidarBattle.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LidarBattle.EditorTools
{
    /// <summary>
    /// 最低限動くゲーム一式（スプライト・弾アセット・難易度別 Timeline・選択/バトルシーン）を生成する。
    /// batchmode: Unity.exe -batchmode -quit -projectPath . -executeMethod LidarBattle.EditorTools.GameSetupBuilder.Build
    /// 既存の同名アセット・シーンは上書きする。
    /// </summary>
    public static class GameSetupBuilder
    {
        private const string ArtDir = "Assets/Art";
        private const string BulletDir = "Assets/Settings/Bullets";
        private const string TimelineDir = "Assets/Timelines";
        private const string SelectScenePath = "Assets/Scenes/SelectScene.unity";
        private const string BattleScenePath = "Assets/Scenes/BattleScene.unity";

        private static Sprite _square, _circle, _heart;

        [MenuItem("Tools/LiDAR Battle/Rebuild Game Setup")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                foreach (var dir in new[] { ArtDir, BulletDir, TimelineDir }) Directory.CreateDirectory(dir);

                _square = MakeSprite("Square", 4, (x, y) => true);
                _circle = MakeSprite("Circle", 32, (x, y) => Sq(x - 15.5f) + Sq(y - 15.5f) <= Sq(15.5f));
                _heart = MakeSprite("Heart", 32, IsHeart);

                BuildSelectScene();
                BuildBattleScene();

                AddToBuildSettings(SelectScenePath, BattleScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("[GameSetupBuilder] 完了");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        // ───────── 弾・Timeline ─────────

        private static TimelineAsset[] BuildTimelines()
        {
            var white = MakeBulletType("White", Color.white);
            var yellow = MakeBulletType("Yellow", new Color(1f, 0.9f, 0.2f));
            return BuildTimelines(TimelineDir, white, yellow);
        }

        /// <summary>
        /// 難易度別の Timeline を timelineDir に作る。弾幕の中身（飛ばし方・タイミング・セリフ）は見た目のテーマに依らず共通で、
        /// 弾の見た目だけを white（主弾）と yellow（差し色の弾）で差し替える。FirePattern は BulletDir で共有する。
        /// </summary>
        internal static TimelineAsset[] BuildTimelines(string timelineDir, BulletType white, BulletType yellow)
        {
            Directory.CreateDirectory(BulletDir);
            Directory.CreateDirectory(timelineDir);

            var ring = MakePattern("Ring12", ("_shape", ShotShape.Ring), ("_count", 12), ("_speed", 2f));
            var aimed3 = MakePattern("Aimed3", ("_shape", ShotShape.Aimed), ("_count", 3), ("_spread", 15f), ("_speed", 3f));
            var aimed4 = MakePattern("Aimed4", ("_shape", ShotShape.Aimed), ("_count", 4), ("_spread", 12f), ("_speed", 3f));
            var spiral = MakePattern("Spiral", ("_shape", ShotShape.Spiral), ("_count", 4), ("_spiralStep", 13f), ("_speed", 2.2f));
            var rain = MakePattern("Rain", ("_shape", ShotShape.Random), ("_count", 1), ("_angle", -90f), ("_spread", 50f),
                ("_move", MoveType.Accelerate), ("_speed", 1f), ("_acceleration", 2f), ("_maxSpeed", 5f));
            var wave = MakePattern("Wave", ("_shape", ShotShape.Aimed), ("_count", 1), ("_move", MoveType.SineWave),
                ("_speed", 2f), ("_amplitude", 0.4f), ("_frequency", 1.5f));
            var curve = MakePattern("Curve", ("_shape", ShotShape.Ring), ("_count", 8), ("_move", MoveType.Curve),
                ("_speed", 2f), ("_angularVelocity", 40f));
            var homing = MakePattern("Homing", ("_shape", ShotShape.Aimed), ("_count", 1), ("_move", MoveType.Homing),
                ("_speed", 2.2f), ("_turnRate", 60f));

            var top = new Vector2(0.5f, 1.15f);
            var topLeft = new Vector2(0.1f, 1.15f);
            var topRight = new Vector2(0.9f, 1.15f);

            var easy = MakeTimeline($"{timelineDir}/Easy", tl =>
            {
                var t1 = tl.CreateTrack<ShotTrack>(null, "Shots");
                Shot(t1, 3, 17, white, ring, top, 1.5f);
                Shot(t1, 20, 20, white, aimed3, top, 1.2f);
                Shot(t1, 40, 20, white, rain, top, 0.35f);
                var d = tl.CreateTrack<DialogueTrack>(null, "Dialogue");
                Say(d, 0, 3, "いくよ！");
                Say(d, 20, 3, "まだまだ！");
                Say(d, 45, 3, "あと少し！");
            });

            var medium = MakeTimeline($"{timelineDir}/Medium", tl =>
            {
                var t1 = tl.CreateTrack<ShotTrack>(null, "Shots");
                Shot(t1, 2, 18, white, spiral, top, 0.15f);
                Shot(t1, 20, 20, white, aimed4, top, 0.8f);
                Shot(t1, 40, 20, white, rain, top, 0.2f);
                var t2 = tl.CreateTrack<ShotTrack>(null, "Shots 2");
                Shot(t2, 10, 15, white, ring, top, 2.5f);
                Shot(t2, 25, 15, yellow, wave, topLeft, 1f);
                Shot(t2, 42, 16, white, curve, top, 2f);
                var d = tl.CreateTrack<DialogueTrack>(null, "Dialogue");
                Say(d, 0, 3, "いくよ！");
                Say(d, 20, 3, "なかなかやるね");
                Say(d, 45, 3, "あと少し！");
            });

            var hard = MakeTimeline($"{timelineDir}/Hard", tl =>
            {
                var t1 = tl.CreateTrack<ShotTrack>(null, "Shots");
                Shot(t1, 2, 23, white, spiral, top, 0.1f);
                Shot(t1, 25, 15, yellow, homing, topLeft, 1.2f);
                Shot(t1, 40, 20, white, rain, top, 0.12f);
                var t2 = tl.CreateTrack<ShotTrack>(null, "Shots 2");
                Shot(t2, 5, 53, white, aimed3, top, 0.9f);
                var t3 = tl.CreateTrack<ShotTrack>(null, "Shots 3");
                Shot(t3, 15, 20, white, curve, top, 1.5f);
                Shot(t3, 35, 23, yellow, homing, topRight, 1.2f);
                var d = tl.CreateTrack<DialogueTrack>(null, "Dialogue");
                Say(d, 0, 3, "本気でいくよ！");
                Say(d, 25, 3, "よけられるかな？");
                Say(d, 50, 3, "あと少し！");
            });

            return new[] { easy, medium, hard };
        }

        private static BulletType MakeBulletType(string name, Color color)
        {
            var type = CreateAsset<BulletType>($"{BulletDir}/{name}.asset");
            Set(type, ("_sprite", _circle), ("_color", color), ("_scale", 0.25f), ("_radius", 0.09f));
            return type;
        }

        private static FirePattern MakePattern(string name, params (string, object)[] props)
        {
            var pattern = CreateAsset<FirePattern>($"{BulletDir}/{name}.asset");
            Set(pattern, props);
            return pattern;
        }

        private static TimelineAsset MakeTimeline(string pathWithoutExtension, Action<TimelineAsset> fill)
        {
            var timeline = CreateAsset<TimelineAsset>($"{pathWithoutExtension}.playable");
            fill(timeline);
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            timeline.fixedDuration = 60;
            EditorUtility.SetDirty(timeline);
            return timeline;
        }

        private static void Shot(ShotTrack track, double start, double duration, BulletType type, FirePattern pattern,
            Vector2 position, float interval)
        {
            var clip = track.CreateClip<ShotClip>();
            clip.start = start;
            clip.duration = duration;
            clip.displayName = pattern.name;
            Set(clip.asset, ("_bulletType", type), ("_pattern", pattern), ("_position", position),
                ("_interval", interval), ("_useFixedSeed", true), ("_seed", (int)(start * 100)));
        }

        private static void Say(DialogueTrack track, double start, double duration, string text)
        {
            var clip = track.CreateClip<DialogueClip>();
            clip.start = start;
            clip.duration = duration;
            clip.displayName = text;
            Set(clip.asset, ("_text", text));
        }

        // ───────── シーン ─────────

        private static void BuildSelectScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var common = BuildCommon(new Vector2(0f, -2f), new Vector2(12f, 3f), new Vector2(0f, -3.1f));

            var optionsRoot = new GameObject("Options");
            var names = new[] { "Easy", "Medium", "Hard" };
            var options = new DifficultyOption[names.Length];
            for (int i = 0; i < names.Length; i++)
                options[i] = MakeOption(optionsRoot.transform, (Difficulty)i, names[i], new Vector2(-4f + 4f * i, -2f), common.Soul);

            var flow = new GameObject("SelectFlow").AddComponent<SelectFlow>();
            Set(flow, ("_dialogue", common.Dialogue), ("_optionsRoot", optionsRoot), ("_options", options),
                ("_battleSceneName", "BattleScene"));

            EditorSceneManager.SaveScene(scene, SelectScenePath);
        }

        private static void BuildBattleScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Timeline はシーンを作った後に作る。先に作ると NewScene の時点でどのシーンからも参照されていないため
            // アンロードされ、SerializedProperty で代入しても null になる（シーンに fileID: 0 で保存される）。
            var timelines = BuildTimelines();
            var boardCenter = new Vector2(0f, -1.8f);
            var common = BuildCommon(boardCenter, new Vector2(5f, 3.2f), boardCenter);

            var director = new GameObject("Director").AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            Set(common.Clock, ("_director", director));

            var bullets = new GameObject("BulletSystem").AddComponent<BulletSystem>();
            Set(bullets, ("_clock", common.Clock), ("_board", common.Board), ("_soul", common.Soul));

            var score = bullets.gameObject.AddComponent<ScoreKeeper>();
            Set(score, ("_bullets", bullets));

            // 左下にスコアと被弾回数（2 行）。
            var scoreLabel = MakeText(common.Canvas, "ScoreLabel", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(20f, 20f), new Vector2(420f, 140f), 32f);
            var scoreView = scoreLabel.gameObject.AddComponent<ScoreView>();
            Set(scoreView, ("_score", score), ("_label", scoreLabel));

            // 左上に残り時間（会話ボックスより左の空き）。
            var timeLabel = MakeText(common.Canvas, "TimeLabel", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -100f), new Vector2(370f, -30f), 40f);

            var debug = new GameObject("BattleDebug").AddComponent<BattleDebug>();
            Set(debug, ("_clock", common.Clock), ("_bullets", bullets), ("_soul", common.Soul));

            // 被弾フィードバック: 画面全体を覆う透明な Image（Canvas の最後 = 最前面）とカメラ振動。
            var flash = MakeRect("HitFlash", common.Canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var flashImage = flash.gameObject.AddComponent<Image>();
            flashImage.color = new Color(1f, 0f, 0f, 0f);
            flashImage.raycastTarget = false;
            var feedback = new GameObject("HitFeedback").AddComponent<HitFeedback>();
            Set(feedback, ("_bullets", bullets), ("_camera", common.Camera), ("_overlay", flashImage));

            var flow = new GameObject("BattleFlow").AddComponent<BattleFlow>();
            Set(flow, ("_director", director), ("_timelines", timelines), ("_clock", common.Clock), ("_bullets", bullets),
                ("_score", score), ("_dialogue", common.Dialogue), ("_timeLabel", timeLabel), ("_selectSceneName", "SelectScene"));

            EditorSceneManager.SaveScene(scene, BattleScenePath);
        }

        private struct Common
        {
            public BattleClock Clock;
            public BulletBoard Board;
            public SoulController Soul;
            public DialogueBox Dialogue;
            public Transform Canvas;
            public Transform Camera;
        }

        /// <summary>両シーン共通: カメラ・ライト・時間・枠・SOUL・入力 (LiDAR / キーボード)・会話ボックス。</summary>
        private static Common BuildCommon(Vector2 boardCenter, Vector2 boardSize, Vector2 soulStart)
        {
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.transform.position = new Vector3(0f, 0f, -10f);

            new GameObject("Global Light 2D").AddComponent<Light2D>().lightType = Light2D.LightType.Global;

            var clock = new GameObject("BattleClock").AddComponent<BattleClock>();

            var board = new GameObject("BulletBoard").AddComponent<BulletBoard>();
            board.transform.position = boardCenter;
            Set(board, ("_size", boardSize));
            MakeFrame(board.transform, boardSize, 0.08f, 0);

            // キーボード → LiDAR → カメラ (ArUco) の順に、位置を出せた入力源を使う（HeartInputSelector）。
            var keyboard = new GameObject("Input").AddComponent<KeyboardInputSource>();
            Set(keyboard, ("_board", board), ("_speed", 3f));
            var lidar = keyboard.gameObject.AddComponent<LidarInputSource>();
            var lidarSettings = AssetDatabase.LoadAssetAtPath<LidarSettings>("Assets/Settings/LidarSettings.asset")
                ?? throw new InvalidOperationException("Assets/Settings/LidarSettings.asset がありません。");
            Set(lidar, ("_settings", lidarSettings));
            keyboard.gameObject.AddComponent<CameraInputSource>();
            var input = keyboard.gameObject.AddComponent<HeartInputSelector>();

            var soul = new GameObject("Soul").AddComponent<SoulController>();
            soul.transform.position = soulStart;
            soul.transform.localScale = Vector3.one * 0.3f;
            var soulRenderer = soul.gameObject.AddComponent<SpriteRenderer>();
            soulRenderer.sprite = _heart;
            soulRenderer.color = Color.red;
            soulRenderer.sortingOrder = 20;
            Set(soul, ("_inputSource", input), ("_board", board), ("_clock", clock),
                ("_halfSize", 0.15f), ("_hitRadius", 0.06f), ("_grazeRadius", 0.3f));

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var dialogue = MakeDialogueBox(canvasGo.transform);

            return new Common
            {
                Clock = clock, Board = board, Soul = soul, Dialogue = dialogue,
                Canvas = canvasGo.transform, Camera = camera.transform,
            };
        }

        private static DialogueBox MakeDialogueBox(Transform canvas)
        {
            // 白い枠の内側に黒い面を重ねて会話ボックスにする。
            var box = MakeRect("DialogueBox", canvas, new Vector2(0.2f, 1f), new Vector2(0.8f, 1f),
                new Vector2(0f, -280f), new Vector2(0f, -40f));
            box.gameObject.AddComponent<Image>().color = Color.white;
            var inner = MakeRect("Inner", box, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            inner.gameObject.AddComponent<Image>().color = Color.black;
            var label = MakeText(inner, "Label", Vector2.zero, Vector2.one, new Vector2(30f, 20f), new Vector2(-30f, -20f), 44f);

            var dialogue = box.gameObject.AddComponent<DialogueBox>();
            Set(dialogue, ("_label", label));
            return dialogue;
        }

        private static DifficultyOption MakeOption(Transform parent, Difficulty difficulty, string label, Vector2 position,
            SoulController soul)
        {
            var size = new Vector2(2f, 1.2f);
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            MakeFrame(go.transform, size, 0.05f, 2);

            var text = new GameObject("Label").AddComponent<TextMeshPro>();
            text.transform.SetParent(go.transform, false);
            text.rectTransform.sizeDelta = size;
            text.text = label;
            text.fontSize = 4f;
            text.alignment = TextAlignmentOptions.Center;
            text.sortingOrder = 5;

            // 左端を原点にしたゲージ。pivot の x スケールを 0→1 にすると左から伸びる。
            var pivot = new GameObject("GaugePivot").transform;
            pivot.SetParent(go.transform, false);
            pivot.localPosition = new Vector3(-size.x * 0.5f, -size.y * 0.5f + 0.1f, 0f);
            pivot.localScale = new Vector3(0f, 1f, 1f);
            var bar = MakeSpriteObject("Gauge", pivot, _square, Color.yellow, 6);
            bar.localPosition = new Vector3(size.x * 0.5f, 0f, 0f);
            bar.localScale = new Vector3(size.x, 0.12f, 1f);

            var option = go.AddComponent<DifficultyOption>();
            Set(option, ("_difficulty", difficulty), ("_soul", soul), ("_size", size), ("_gauge", pivot));
            return option;
        }

        // ───────── 部品 ─────────

        private static void MakeFrame(Transform parent, Vector2 size, float border, int order)
        {
            var outer = MakeSpriteObject("Frame", parent, _square, Color.white, order);
            outer.localScale = new Vector3(size.x + border * 2f, size.y + border * 2f, 1f);
            var inner = MakeSpriteObject("FrameInner", parent, _square, Color.black, order + 1);
            inner.localScale = new Vector3(size.x, size.y, 1f);
        }

        internal static Transform MakeSpriteObject(string name, Transform parent, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return go.transform;
        }

        internal static RectTransform MakeRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        internal static TextMeshProUGUI MakeText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, float fontSize)
        {
            var rect = MakeRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            return text;
        }

        private static Sprite MakeSprite(string name, int size, Func<int, int, bool> fill)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, fill(x, y) ? Color.white : Color.clear);

            string path = $"{ArtDir}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>ハート曲線 (x²+y²−1)³ − x²y³ ≤ 0 で塗る。</summary>
        private static bool IsHeart(int px, int py)
        {
            float x = (px - 15.5f) / 12f;
            float y = (py - 13.5f) / 12f;
            return Mathf.Pow(x * x + y * y - 1f, 3f) - x * x * y * y * y <= 0f;
        }

        private static float Sq(float v) => v * v;

        /// <summary>
        /// 既存のアセットは中身を初期値に戻して使い回す（GUID と参照を保つ）。
        /// 同じパスで DeleteAsset → CreateAsset すると新しいオブジェクトが永続化されずシーンからの参照が fileID: 0 になり、
        /// CreateAsset で上書きすると再インポートで後から設定した値が消えるため。
        /// </summary>
        internal static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            var fresh = ScriptableObject.CreateInstance<T>();
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }

            // Timeline のトラックや Volume のコンポーネントなどのサブアセットも消す。
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
                if (sub != existing) Object.DestroyImmediate(sub, true);
            EditorUtility.CopySerialized(fresh, existing);
            existing.name = Path.GetFileNameWithoutExtension(path);
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>ビルド設定にシーンを足す（他のビルダーが足したシーンは残す）。先頭のシーンが起動時のシーンになる。</summary>
        internal static void AddToBuildSettings(params string[] paths)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (string path in paths)
                if (!scenes.Exists(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>private な [SerializeField] を名前で設定する。</summary>
        internal static void Set(Object target, params (string name, object value)[] props)
        {
            var so = new SerializedObject(target);
            foreach (var (name, value) in props)
            {
                var p = so.FindProperty(name) ?? throw new ArgumentException($"{target.GetType().Name}.{name} がありません");
                switch (value)
                {
                    case Enum e: p.enumValueIndex = Convert.ToInt32(e); break;
                    case int i: p.intValue = i; break;
                    case float f: p.floatValue = f; break;
                    case bool b: p.boolValue = b; break;
                    case string s: p.stringValue = s; break;
                    case Vector2 v: p.vector2Value = v; break;
                    case Color c: p.colorValue = c; break;
                    case Object o: p.objectReferenceValue = o; break;
                    case Object[] array:
                        p.arraySize = array.Length;
                        for (int i = 0; i < array.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = array[i];
                        break;
                    default: throw new ArgumentException($"{name}: 未対応の型 {value?.GetType().Name}");
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
