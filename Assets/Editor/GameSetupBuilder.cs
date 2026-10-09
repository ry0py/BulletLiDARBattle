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
        internal const string BulletDir = "Assets/Settings/Bullets";
        private const string TimelineDir = "Assets/Timelines";
        private const string PortraitDir = "Assets/Settings/Portraits";
        private const string SelectScenePath = "Assets/Scenes/Actual/SelectScene.unity";
        private const string BattleScenePath = "Assets/Scenes/Actual/BattleScene.unity";

        private static readonly Color SoulColor = new(0.55f, 0.55f, 0.55f);
        private const float SoulOutlineScale = 1.25f; // 白いふちの太さ（本体に対する倍率）

        private static Sprite _square, _circle;

        [MenuItem("Tools/LiDAR Battle/Rebuild Game Setup")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                foreach (var dir in new[] { ArtDir, BulletDir, TimelineDir }) Directory.CreateDirectory(dir);

                _square = MakeSprite("Square", 4, (x, y) => true);
                _circle = MakeSprite("Circle", 32, (x, y) => Sq(x - 15.5f) + Sq(y - 15.5f) <= Sq(15.5f));
                MakeSprite("Heart", 32, IsHeart); // LiDAR シミュレーションが使う

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

        /// <summary>難易度ごとに数本の Timeline（[Difficulty][何本目]）を作る。中身は BattleTimelineBuilder。</summary>
        private static TimelineAsset[][] BuildTimelines()
        {
            MakeBulletType("White", Color.white);
            MakeBulletType("Yellow", new Color(1f, 0.9f, 0.2f));
            return BattleTimelineBuilder.Build();
        }

        /// <summary>
        /// スキャナー版の難易度別 Timeline（難易度ごとに 1 本）を timelineDir に作る。弾幕の中身（飛ばし方・タイミング・セリフ）は見た目のテーマに依らず共通で、
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
                var (d, p, shake) = MakeTalkTracks(tl);
                Say(d, p, 0, 3, "いくよ！");
                Attack(p, shake, 3, 2);
                Say(d, p, 20, 3, "まだまだ！");
                Attack(p, shake, 40, 2);
                Say(d, p, 45, 3, "あと少し！");
                Roam(tl, 0.1f, (5, 20), (23, 45), (48, 60));
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
                var (d, p, shake) = MakeTalkTracks(tl);
                Say(d, p, 0, 3, "いくよ！");
                Attack(p, shake, 3, 2);
                Say(d, p, 20, 3, "なかなかやるね", PortraitExpression.Smile);
                Attack(p, shake, 25, 2);
                Say(d, p, 45, 3, "あと少し！");
                Roam(tl, 0.15f, (5, 20), (23, 45), (48, 60));
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
                var (d, p, shake) = MakeTalkTracks(tl);
                Say(d, p, 0, 3, "本気でいくよ！");
                Attack(p, shake, 3, 2);
                Say(d, p, 25, 3, "よけられるかな？", PortraitExpression.Smile);
                Attack(p, shake, 35, 2);
                Say(d, p, 50, 3, "あと少し！");
                Roam(tl, 0.2f, (5, 25), (28, 50), (53, 60));
            });

            return new[] { easy, medium, hard };
        }

        private static BulletType MakeBulletType(string name, Color color)
        {
            var type = CreateAsset<BulletType>($"{BulletDir}/{name}.asset");
            Set(type, ("_sprite", _circle), ("_color", color), ("_scale", 0.25f), ("_radius", 0.09f));
            return type;
        }

        internal static FirePattern MakePattern(string name, params (string, object)[] props)
        {
            var pattern = CreateAsset<FirePattern>($"{BulletDir}/{name}.asset");
            Set(pattern, props);
            return pattern;
        }

        internal static TimelineAsset MakeTimeline(string pathWithoutExtension, Action<TimelineAsset> fill, double duration = 60)
        {
            var timeline = CreateAsset<TimelineAsset>($"{pathWithoutExtension}.playable");
            fill(timeline);
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            timeline.fixedDuration = duration;
            EditorUtility.SetDirty(timeline);
            return timeline;
        }

        internal static void Shot(ShotTrack track, double start, double duration, BulletType type, FirePattern pattern,
            Vector2 position, float interval)
        {
            var clip = track.CreateClip<ShotClip>();
            clip.start = start;
            clip.duration = duration;
            clip.displayName = pattern.name;
            Set(clip.asset, ("_bulletType", type), ("_pattern", pattern), ("_position", position),
                ("_interval", interval), ("_useFixedSeed", true), ("_seed", (int)(start * 100)));
        }

        /// <summary>
        /// セリフ・表情・立ち絵の動きのトラックを作る。後半（30 秒〜）は全難易度共通で立ち絵をふわふわ上下させる。
        /// 震えは別トラックにする（同じトラックで重ねると、足し合わせずにクロスフェードになるため）。
        /// </summary>
        internal static (DialogueTrack, PortraitTrack, PortraitMotionTrack) MakeTalkTracks(TimelineAsset timeline)
        {
            var dialogue = timeline.CreateTrack<DialogueTrack>(null, "Dialogue");
            var portrait = timeline.CreateTrack<PortraitTrack>(null, "Portrait");
            var drift = timeline.CreateTrack<PortraitMotionTrack>(null, "Portrait Motion");
            Move(drift, 30, 30, PortraitMotion.Float, 12f, 0.5f).easeInDuration = 1;
            var shake = timeline.CreateTrack<PortraitMotionTrack>(null, "Portrait Shake");
            return (dialogue, portrait, shake);
        }

        // 立ち絵が歩き回る範囲（元の位置からの横のずれ、px）。左は画面の端、右は右上の残り時間の手前まで。
        private const float RoamLeft = -300f;
        private const float RoamRight = 860f;
        private const double RoamEase = 1.5;

        /// <summary>
        /// セリフの無い間（spans の各区間）、立ち絵に画面の上を左右に大きく行き来させる。会話ボックスはセリフの間しか出ないので重ならない。
        /// 区間の前後 RoamEase 秒で元の位置との間を移動するので、区間の終わりはセリフの始まりにそろえてよい。
        /// </summary>
        internal static void Roam(TimelineAsset timeline, float frequency, params (double start, double end)[] spans)
        {
            var track = timeline.CreateTrack<PortraitMotionTrack>(null, "Portrait Roam");
            foreach (var (start, end) in spans)
            {
                var clip = Move(track, start, end - start - 0.5, PortraitMotion.Roam, (RoamRight - RoamLeft) / 2f, frequency);
                clip.easeInDuration = RoamEase;
                clip.easeOutDuration = RoamEase;
                Set(clip.asset, ("_center", new Vector2((RoamLeft + RoamRight) / 2f, 0f)));
            }
        }

        /// <summary>技を出す顔にして、その間は小刻みに震わせる。</summary>
        internal static void Attack(PortraitTrack portrait, PortraitMotionTrack shake, double start, double duration)
        {
            Face(portrait, start, duration, PortraitExpression.Attack);
            Move(shake, start, duration, PortraitMotion.Shake, 6f, 25f);
        }

        private static TimelineClip Move(PortraitMotionTrack track, double start, double duration, PortraitMotion motion,
            float amplitude, float frequency)
        {
            var clip = track.CreateClip<PortraitMotionClip>();
            clip.start = start;
            clip.duration = duration;
            clip.displayName = motion.ToString();
            Set(clip.asset, ("_motion", motion), ("_amplitude", amplitude), ("_frequency", frequency));
            return clip;
        }

        /// <summary>セリフを出し、その間は立ち絵を expression（既定は口をあけた顔）にする。</summary>
        internal static void Say(DialogueTrack track, PortraitTrack portrait, double start, double duration, string text,
            PortraitExpression expression = PortraitExpression.Talk)
        {
            var clip = track.CreateClip<DialogueClip>();
            clip.start = start;
            clip.duration = duration;
            clip.displayName = text;
            Set(clip.asset, ("_text", text));
            Face(portrait, start, duration, expression);
        }

        /// <summary>この間だけ立ち絵を expression にする（クリップの無いところはベース）。</summary>
        private static void Face(PortraitTrack track, double start, double duration, PortraitExpression expression)
        {
            var clip = track.CreateClip<PortraitClip>();
            clip.start = start;
            clip.duration = duration;
            clip.displayName = expression.ToString();
            Set(clip.asset, ("_expression", expression));
        }

        /// <summary>
        /// 難易度別の立ち絵セット（Difficulty の順）。無いときだけ空で作る。画像は Inspector で割り当てるので、作り直しで消さない。
        /// シーンを作った後に呼ぶ（理由は BuildBattleScene の Timeline と同じ）。
        /// </summary>
        private static PortraitSet[] LoadPortraitSets()
        {
            Directory.CreateDirectory(PortraitDir);
            var names = new[] { "Easy", "Medium", "Hard" };
            var sets = new PortraitSet[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                string path = $"{PortraitDir}/{names[i]}.asset";
                sets[i] = AssetDatabase.LoadAssetAtPath<PortraitSet>(path);
                if (sets[i] != null) continue;
                sets[i] = ScriptableObject.CreateInstance<PortraitSet>();
                AssetDatabase.CreateAsset(sets[i], path);
            }
            return sets;
        }

        // ───────── シーン ─────────

        private static void BuildSelectScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var common = BuildCommon(new Vector2(0f, -2f), new Vector2(12f, 3f), new Vector2(0f, -3.1f));
            // 難易度を選ぶ前なので Easy の立ち絵を出す。
            Set(common.Portrait, ("_set", LoadPortraitSets()[(int)Difficulty.Easy]));

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

        private const float BattleZoom = 1.2f;

        private static void BuildBattleScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Timeline はシーンを作った後に作る。先に作ると NewScene の時点でどのシーンからも参照されていないため
            // アンロードされ、SerializedProperty で代入しても null になる（シーンに fileID: 0 で保存される）。
            var timelines = BuildTimelines();
            var portraitSets = LoadPortraitSets();
            var boardCenter = new Vector2(0f, -1.8f);
            var common = BuildCommon(boardCenter, new Vector2(5f, 3.2f), boardCenter);
            // 盤面を画面の上で 1.2 倍に見せる（技は盤面のワールドの大きさに合わせてあるので、盤面は広げずにカメラを寄せる）。
            common.Camera.GetComponent<Camera>().orthographicSize = 5f / BattleZoom;

            var director = new GameObject("Director").AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            Set(common.Clock, ("_director", director));

            var bullets = new GameObject("BulletSystem").AddComponent<BulletSystem>();
            Set(bullets, ("_clock", common.Clock), ("_board", common.Board), ("_soul", common.Soul));
            MakeSafeZone(common.Board);

            var score = bullets.gameObject.AddComponent<ScoreKeeper>();
            Set(score, ("_bullets", bullets));

            // 左下に今の難易度。被弾回数はドキドキ感のためプレイ中は出さず、終了時の会話でだけ出す。
            var difficultyLabel = MakeText(common.Canvas, "DifficultyLabel", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(20f, 20f), new Vector2(420f, 70f), 32f);

            // 右上に残り時間（会話ボックスより右の空き。左は立ち絵）。
            var timeLabel = MakeText(common.Canvas, "TimeLabel", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-370f, -100f), new Vector2(-20f, -30f), 40f);
            timeLabel.alignment = TextAlignmentOptions.TopRight;

            var debug = new GameObject("BattleDebug").AddComponent<BattleDebug>();
            Set(debug, ("_clock", common.Clock), ("_bullets", bullets), ("_soul", common.Soul));

            // 被弾フィードバック: 画面全体を覆う透明な Image（Canvas の最後 = 最前面）とカメラ振動。
            var flash = MakeRect("HitFlash", common.Canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var flashImage = flash.gameObject.AddComponent<Image>();
            flashImage.color = new Color(1f, 0f, 0f, 0f);
            flashImage.raycastTarget = false;
            var feedback = new GameObject("HitFeedback").AddComponent<HitFeedback>();
            Set(feedback, ("_bullets", bullets), ("_camera", common.Camera), ("_overlay", flashImage),
                ("_shakeAmplitude", 0.15f / BattleZoom)); // 寄せた分だけ揺れを小さくし、画面上の揺れ幅をそろえる

            var flow = new GameObject("BattleFlow").AddComponent<BattleFlow>();
            Set(flow, ("_director", director), ("_easyTimelines", timelines[(int)Difficulty.Easy]),
                ("_mediumTimelines", timelines[(int)Difficulty.Medium]), ("_hardTimelines", timelines[(int)Difficulty.Hard]),
                ("_clock", common.Clock), ("_bullets", bullets),
                ("_score", score), ("_dialogue", common.Dialogue), ("_portrait", common.Portrait), ("_portraitSets", portraitSets),
                ("_timeLabel", timeLabel), ("_difficultyLabel", difficultyLabel), ("_selectSceneName", "SelectScene"));

            EditorSceneManager.SaveScene(scene, BattleScenePath);
        }

        private struct Common
        {
            public BattleClock Clock;
            public BulletBoard Board;
            public SoulController Soul;
            public DialogueBox Dialogue;
            public PortraitView Portrait;
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
            // 自機は灰色の丸に白いふち。白い弾（ふちなしの丸）と見分けられるようにする。
            soulRenderer.sprite = _circle;
            soulRenderer.color = SoulColor;
            soulRenderer.sortingOrder = 20;
            MakeSpriteObject("Outline", soul.transform, _circle, Color.white, 19).localScale = Vector3.one * SoulOutlineScale;
            Set(soul, ("_inputSource", input), ("_board", board), ("_clock", clock),
                ("_halfSize", 0.15f), ("_hitRadius", 0.06f), ("_grazeRadius", 0.3f));

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var dialogue = MakeDialogueBox(canvasGo.transform);
            var portrait = MakePortrait(canvasGo.transform);

            return new Common
            {
                Clock = clock, Board = board, Soul = soul, Dialogue = dialogue, Portrait = portrait,
                Canvas = canvasGo.transform, Camera = camera.transform,
            };
        }

        // 会話ボックスの左端（画面幅に対する割合）。左の空きに立ち絵を置く。
        private const float DialogueLeft = 0.35f;

        private static DialogueBox MakeDialogueBox(Transform canvas)
        {
            // 白い枠の内側に黒い面を重ねて会話ボックスにする。
            var box = MakeRect("DialogueBox", canvas, new Vector2(DialogueLeft, 1f), new Vector2(0.8f, 1f),
                new Vector2(0f, -280f), new Vector2(0f, -40f));
            box.gameObject.AddComponent<Image>().color = Color.white;
            var inner = MakeRect("Inner", box, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            inner.gameObject.AddComponent<Image>().color = Color.black;
            var label = MakeText(inner, "Label", Vector2.zero, Vector2.one, new Vector2(30f, 20f), new Vector2(-30f, -20f), 44f);

            var dialogue = box.gameObject.AddComponent<DialogueBox>();
            Set(dialogue, ("_label", label));
            return dialogue;
        }

        /// <summary>
        /// 会話ボックスのすぐ左に立ち絵。上端を会話ボックスにそろえ、下は画面の上から 38% まで。
        /// 縦は画面の割合で決めるので、16:9 以外の画面でも上半分からはみ出さない。
        /// </summary>
        private static PortraitView MakePortrait(Transform canvas)
        {
            var rect = MakeRect("Portrait", canvas, new Vector2(DialogueLeft, 0.62f), new Vector2(DialogueLeft, 1f),
                new Vector2(-340f, 0f), new Vector2(-20f, -40f));
            var image = rect.gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            var portrait = rect.gameObject.AddComponent<PortraitView>();
            Set(portrait, ("_image", image));
            return portrait;
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

        /// <summary>
        /// 安置のシート（半透明の緑の長方形に「あんぜん」）を盤面の子に作る。ふだんは隠しておき、Timeline の SafeZoneTrack が出す。
        /// 枠（0, 1）より手前、弾（10）・SOUL（19, 20）より奥に描く。
        /// </summary>
        private static void MakeSafeZone(BulletBoard board)
        {
            var go = new GameObject("SafeZone");
            go.transform.SetParent(board.transform, false);
            var sheet = MakeSpriteObject("Sheet", go.transform, _square, new Color(0.3f, 1f, 0.45f, 0.35f), 3);

            var label = new GameObject("Label").AddComponent<TextMeshPro>();
            label.transform.SetParent(go.transform, false);
            label.text = "あんぜん";
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.8f, 1f, 0.85f, 0.9f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.5f;
            label.fontSizeMax = 3f;
            label.sortingOrder = 4;

            var view = go.AddComponent<SafeZoneView>();
            Set(view, ("_board", board), ("_sheet", sheet.GetComponent<SpriteRenderer>()), ("_label", label));
            go.SetActive(false);
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
