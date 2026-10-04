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
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using static LidarBattle.EditorTools.GameSetupBuilder;

namespace LidarBattle.EditorTools
{
    /// <summary>
    /// 独自の見た目「スキャナー」版のゲーム一式を、元の版（GameSetupBuilder）とは別のシーン・アセットとして生成する。
    /// 紺色の背景にシアンの方眼、盤面の上にいる LiDAR 風の敵（スキャナー）が扇形のビームで盤面を掃きながら弾を撃つ。
    /// SOUL は琥珀色のハートで、周りの輪がグレイズ範囲を表す。
    /// 弾幕の中身（飛ばし方・タイミング・セリフ）は GameSetupBuilder.BuildTimelines を共用し、弾の見た目だけ変える。
    /// 既存の同名アセット・シーンは上書きする。
    /// </summary>
    public static class ScannerSetupBuilder
    {
        private const string ArtDir = "Assets/Art/Scanner";
        private const string BulletSpriteDir = "Assets/Art/Bullets";
        private const string SettingsDir = "Assets/Settings/Scanner";
        private const string TimelineDir = "Assets/Timelines/Scanner";
        private const string VolumeProfilePath = SettingsDir + "/ScannerVolume.asset";
        private const string SelectSceneName = "ScannerSelectScene";
        private const string BattleSceneName = "ScannerBattleScene";
        private const string SelectScenePath = "Assets/Scenes/" + SelectSceneName + ".unity";
        private const string BattleScenePath = "Assets/Scenes/" + BattleSceneName + ".unity";

        // ───────── 配色 ─────────
        private static readonly Color Bg = Hex("#060E18");
        private static readonly Color BoardFill = Hex("#0A1A2B");
        private static readonly Color Cyan = Hex("#19C8E6");
        private static readonly Color CyanBright = Hex("#8FF6FF");
        private static readonly Color Amber = Hex("#FFC24B");
        private static readonly Color AmberGlow = Hex("#FF9F1C");
        private static readonly Color Rose = Hex("#FB7185");
        private static readonly Color TextMain = Hex("#E6F4F8");
        private static readonly Color TextMuted = Hex("#9FB3C8");
        private static readonly Color PanelBg = Hex("#07131E");
        private static readonly Color[] DifficultyAccent = { Hex("#4ADE80"), Hex("#FBBF24"), Hex("#FB7185") };

        private static Sprite _square, _heart, _glow, _ring, _panel, _panelLine, _corner, _grid, _wedge;

        [MenuItem("Tools/LiDAR Battle/Rebuild Scanner Setup")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                foreach (var dir in new[] { ArtDir, SettingsDir, TimelineDir }) Directory.CreateDirectory(dir);

                MakeSprites();
                MakeVolumeProfile();
                BuildSelectScene();
                BuildBattleScene();

                AddToBuildSettings(SelectScenePath, BattleScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("[ScannerSetupBuilder] 完了");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        // ───────── スプライト ─────────

        private static void MakeSprites()
        {
            // alpha(u, v) は中心が原点、端が ±1 の座標で不透明度を返す。
            _square = MakeSoftSprite("Square", 4, (u, v) => 1f);
            _heart = MakeSoftSprite("Heart", 64, (u, v) => HeartCurve(u * 1.333f, v * 1.333f + 0.167f) <= 0f ? 1f : 0f);
            _glow = MakeSoftSprite("Glow", 64, (u, v) => Sq(Mathf.Clamp01(1f - Len(u, v))));
            _ring = MakeSoftSprite("Ring", 64, (u, v) => 1f - Mathf.Clamp01(Mathf.Abs(Len(u, v) - 0.9f) / 0.06f));
            _panel = MakeSoftSprite("Panel", 64, (u, v) => RoundedRect(u, v, 0.3f) <= 0f ? 1f : 0f, border: 16);
            _panelLine = MakeSoftSprite("PanelLine", 64, (u, v) => 1f - Mathf.Clamp01(Mathf.Abs(RoundedRect(u, v, 0.3f)) / 0.07f), border: 16);
            _corner = MakeSoftSprite("Corner", 32, (u, v) => u < -0.75f || v > 0.75f ? 1f : 0f);
            _grid = MakeSoftSprite("Grid", 64, (u, v) => u < -0.94f || v < -0.94f ? 1f : 0f);
            _wedge = MakeSoftSprite("Wedge", 256, Wedge, pivot: new Vector2(0.5f, 1f), ppu: 128);
        }

        /// <summary>ハート曲線 (x²+y²−1)³ − x²y³。0 以下が内側。</summary>
        private static float HeartCurve(float x, float y) => Mathf.Pow(x * x + y * y - 1f, 3f) - x * x * y * y * y;

        /// <summary>角丸矩形の符号付き距離。負が内側。</summary>
        private static float RoundedRect(float u, float v, float radius)
        {
            float qx = Mathf.Abs(u) - (1f - radius), qy = Mathf.Abs(v) - (1f - radius);
            return Len(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        /// <summary>上端中央を頂点にして真下へ伸びる扇。進行方向の縁が明るく、後ろへ行くほど薄くなる（レーダー風）。</summary>
        private static float Wedge(float u, float v)
        {
            float dx = u, dy = v - 1f;
            float r = Len(dx, dy) * 0.5f;
            if (r > 1f) return 0f;
            float angle = Mathf.Atan2(dx, -dy); // 0 = 真下、正 = +x 側。時計回りに回すので +x 側が後ろ
            const float trail = 0.6f;
            float tail = angle >= 0f && angle <= trail ? Mathf.Pow(1f - angle / trail, 1.5f) : 0f;
            float edge = 1f - Mathf.Clamp01(Mathf.Abs(angle) / 0.012f);
            return Mathf.Max(tail * 0.8f, edge) * (1f - 0.4f * r);
        }

        // ───────── 弾・ポストエフェクト ─────────

        /// <summary>弾の見た目は Assets/Art/Bullets の白い素材 (Kenney Particle Pack, CC0) を色付けして使う。</summary>
        private static BulletType MakeBulletType(string name, Color color, string spriteName, float scale)
        {
            string spritePath = $"{BulletSpriteDir}/{spriteName}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath)
                ?? throw new InvalidOperationException($"{spritePath} がありません。");
            var type = CreateAsset<BulletType>($"{SettingsDir}/{name}.asset");
            Set(type, ("_sprite", sprite), ("_color", color), ("_scale", scale), ("_radius", 0.09f));
            return type;
        }

        /// <summary>
        /// Bloom は閾値を高めにして、弾や SOUL の輪のような明るい部分だけがにじむようにする。
        /// Vignette で画面の縁を落とし、盤面に視線を集める。両シーンの Global Volume が共有する。
        /// </summary>
        private static void MakeVolumeProfile()
        {
            var profile = CreateAsset<VolumeProfile>(VolumeProfilePath);
            var bloom = AddOverride<Bloom>(profile);
            bloom.threshold.value = 0.8f;
            bloom.intensity.value = 1.5f;
            bloom.scatter.value = 0.6f;
            var vignette = AddOverride<Vignette>(profile);
            vignette.color.value = Bg;
            vignette.intensity.value = 0.32f;
            vignette.smoothness.value = 0.45f;
        }

        private static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>(overrides: true);
            component.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // ───────── シーン ─────────

        private static void BuildSelectScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var common = BuildCommon(new Vector2(0f, -2f), new Vector2(12f, 3f), new Vector2(0f, -3.1f));

            var optionsRoot = new GameObject("Options");
            var names = new[] { "Easy", "Medium", "Hard" };
            var captions = new[] { "やさしい", "ふつう", "むずかしい" };
            var options = new DifficultyOption[names.Length];
            for (int i = 0; i < names.Length; i++)
                options[i] = MakeOption(optionsRoot.transform, (Difficulty)i, names[i], captions[i], DifficultyAccent[i],
                    new Vector2(-4f + 4f * i, -2f), common.Soul);

            var hint = MakeWorldText(optionsRoot.transform, "Hint", new Vector2(0f, -1f), new Vector2(10f, 0.6f), 2.6f, TextMuted);
            hint.text = "ハートを置いて 2 秒待つと決定";

            var title = MakeLabel(common.Canvas, "Title", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-400f, 28f), new Vector2(400f, 70f), 24f, TextMuted);
            title.text = "LIDAR BATTLE";
            title.characterSpacing = 14f;
            title.alignment = TextAlignmentOptions.Center;

            var flow = new GameObject("SelectFlow").AddComponent<SelectFlow>();
            Set(flow, ("_dialogue", common.Dialogue), ("_optionsRoot", optionsRoot), ("_options", options),
                ("_battleSceneName", BattleSceneName));

            EditorSceneManager.SaveScene(scene, SelectScenePath);
        }

        private static void BuildBattleScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // 弾と Timeline はシーンを作った後に作る（理由は GameSetupBuilder.BuildBattleScene）。
            var white = MakeBulletType("White", Hex("#DDF7FF"), "kenney_ring_glow", 0.45f);
            var magenta = MakeBulletType("Magenta", Hex("#FF5CD6"), "kenney_star_09", 0.5f);
            var timelines = BuildTimelines(TimelineDir, white, magenta);

            var boardCenter = new Vector2(0f, -1.8f);
            var common = BuildCommon(boardCenter, new Vector2(5f, 3.2f), boardCenter);

            var director = new GameObject("Director").AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            Set(common.Clock, ("_director", director));

            var bullets = new GameObject("BulletSystem").AddComponent<BulletSystem>();
            Set(bullets, ("_clock", common.Clock), ("_board", common.Board), ("_soul", common.Soul));
            Set(common.SoulView, ("_bullets", bullets));

            var score = bullets.gameObject.AddComponent<ScoreKeeper>();
            Set(score, ("_bullets", bullets));

            // 右上の HUD: スコア・被弾・グレイズと、残り時間のバー。
            var hud = MakePanel("Hud", common.Canvas, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-400f, -290f), new Vector2(-30f, -40f), WithAlpha(PanelBg, 0.75f));
            var scoreLabel = MakeLabel(hud, "ScoreLabel", Vector2.zero, Vector2.one, new Vector2(28f, 80f), new Vector2(-28f, -18f), 56f, TextMain);
            var scoreView = scoreLabel.gameObject.AddComponent<ScoreView>();
            Set(scoreView, ("_score", score), ("_label", scoreLabel),
                ("_format", "<size=40%><color=#19C8E6>SCORE</color></size>\n<b>{0}</b>\n<size=40%><color=#9FB3C8>HIT {1}   GRAZE {2}</color></size>"));
            var timeLabel = MakeLabel(hud, "TimeLabel", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(28f, 26f), new Vector2(-28f, 50f), 22f, Cyan);
            timeLabel.text = "TIME";
            timeLabel.characterSpacing = 6f;
            var track = MakeRect("TimeTrack", hud, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(28f, 18f), new Vector2(-28f, 24f));
            track.gameObject.AddComponent<Image>().color = WithAlpha(TextMuted, 0.2f);
            var timeBar = MakeRect("TimeBar", track, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            timeBar.sprite = _square;
            timeBar.type = Image.Type.Filled;
            timeBar.fillMethod = Image.FillMethod.Horizontal;
            timeBar.color = Cyan;

            var debug = new GameObject("BattleDebug").AddComponent<BattleDebug>();
            Set(debug, ("_clock", common.Clock), ("_bullets", bullets), ("_soul", common.Soul));

            // 被弾フィードバック: 画面全体を覆う透明な Image（Canvas の最後 = 最前面）とカメラ振動。
            var flash = MakeRect("HitFlash", common.Canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var flashImage = flash.gameObject.AddComponent<Image>();
            flashImage.color = WithAlpha(Rose, 0f);
            flashImage.raycastTarget = false;
            var feedback = new GameObject("HitFeedback").AddComponent<HitFeedback>();
            Set(feedback, ("_bullets", bullets), ("_camera", common.Camera), ("_overlay", flashImage),
                ("_flashColor", WithAlpha(Rose, 0.3f)));

            var flow = new GameObject("BattleFlow").AddComponent<BattleFlow>();
            Set(flow, ("_director", director), ("_timelines", timelines), ("_clock", common.Clock), ("_bullets", bullets),
                ("_score", score), ("_dialogue", common.Dialogue), ("_timeBar", timeBar), ("_selectSceneName", SelectSceneName));

            EditorSceneManager.SaveScene(scene, BattleScenePath);
        }

        private struct Common
        {
            public BattleClock Clock;
            public BulletBoard Board;
            public SoulController Soul;
            public SoulView SoulView;
            public DialogueBox Dialogue;
            public Transform Canvas;
            public Transform Camera;
        }

        /// <summary>両シーン共通: カメラ・ライト・ポストエフェクト・時間・盤面・スキャナー・SOUL・入力・会話パネル。</summary>
        private static Common BuildCommon(Vector2 boardCenter, Vector2 boardSize, Vector2 soulStart)
        {
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Bg;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true; // Bloom で弾を発光させる

            new GameObject("Global Light 2D").AddComponent<Light2D>().lightType = Light2D.LightType.Global;

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);

            // 画面全体にうっすらとした方眼。盤面の外も「計測空間」に見せる。
            var bgGrid = MakeSpriteObject("Background", null, _grid, WithAlpha(Cyan, 0.04f), -10);
            SetTiled(bgGrid, new Vector2(40f, 20f));

            var clock = new GameObject("BattleClock").AddComponent<BattleClock>();

            var board = new GameObject("BulletBoard").AddComponent<BulletBoard>();
            board.transform.position = boardCenter;
            Set(board, ("_size", boardSize));
            MakeBoardVisual(board.transform, boardSize);

            // カメラのトラッカーが動いていなければ LiDAR、LiDAR も使えなければキーボードにフォールバックする。
            var keyboard = new GameObject("Input").AddComponent<KeyboardInputSource>();
            Set(keyboard, ("_board", board), ("_speed", 3f));
            var lidar = keyboard.gameObject.AddComponent<LidarInputSource>();
            var lidarSettings = AssetDatabase.LoadAssetAtPath<LidarSettings>("Assets/Settings/LidarSettings.asset")
                ?? throw new InvalidOperationException("Assets/Settings/LidarSettings.asset がありません。");
            Set(lidar, ("_settings", lidarSettings), ("_fallback", keyboard));
            var input = keyboard.gameObject.AddComponent<CameraInputSource>();
            Set(input, ("_fallback", lidar));

            var soul = new GameObject("Soul").AddComponent<SoulController>();
            soul.transform.position = soulStart;
            soul.transform.localScale = Vector3.one * 0.3f;
            var heart = soul.gameObject.AddComponent<SpriteRenderer>();
            heart.sprite = _heart;
            heart.color = Amber;
            heart.sortingOrder = 20;
            const float grazeRadius = 0.3f;
            Set(soul, ("_inputSource", input), ("_board", board), ("_clock", clock),
                ("_halfSize", 0.15f), ("_hitRadius", 0.06f), ("_grazeRadius", grazeRadius));
            var soulView = MakeSoulView(soul, heart, grazeRadius);

            MakeScanner(new Vector2(boardCenter.x, 0.6f), soul);

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var dialogue = MakeDialogueBox(canvasGo.transform);

            return new Common
            {
                Clock = clock, Board = board, Soul = soul, SoulView = soulView, Dialogue = dialogue,
                Canvas = canvasGo.transform, Camera = camera.transform,
            };
        }

        /// <summary>盤面の見た目: 暗い面・方眼・細い枠線・四隅のブラケット。扇ビーム用の SpriteMask も兼ねる。</summary>
        private static void MakeBoardVisual(Transform parent, Vector2 size)
        {
            var fill = MakeSpriteObject("Fill", parent, _square, WithAlpha(BoardFill, 0.92f), 0);
            fill.localScale = new Vector3(size.x, size.y, 1f);

            var grid = MakeSpriteObject("Grid", parent, _grid, WithAlpha(Cyan, 0.12f), 1);
            grid.localScale = Vector3.one * 0.5f; // 0.5 単位の方眼
            SetTiled(grid, size * 2f);

            const float line = 0.02f;
            var lineColor = WithAlpha(Cyan, 0.55f);
            foreach (var (name, pos, scale) in new[]
                     {
                         ("Top", new Vector2(0f, size.y * 0.5f), new Vector2(size.x, line)),
                         ("Bottom", new Vector2(0f, -size.y * 0.5f), new Vector2(size.x, line)),
                         ("Left", new Vector2(-size.x * 0.5f, 0f), new Vector2(line, size.y)),
                         ("Right", new Vector2(size.x * 0.5f, 0f), new Vector2(line, size.y)),
                     })
            {
                var edge = MakeSpriteObject(name, parent, _square, lineColor, 3);
                edge.localPosition = pos;
                edge.localScale = new Vector3(scale.x, scale.y, 1f);
            }

            const float cornerSize = 0.35f;
            foreach (var (sx, sy) in new[] { (-1, 1), (1, 1), (-1, -1), (1, -1) })
            {
                var corner = MakeSpriteObject("Corner", parent, _corner, CyanBright, 4);
                corner.localPosition = new Vector3(sx * (size.x - cornerSize) * 0.5f, sy * (size.y - cornerSize) * 0.5f, 0f);
                corner.localScale = Vector3.one * cornerSize;
                var renderer = corner.GetComponent<SpriteRenderer>();
                renderer.flipX = sx > 0;
                renderer.flipY = sy < 0;
            }

            var mask = new GameObject("SweepMask").AddComponent<SpriteMask>();
            mask.transform.SetParent(parent, false);
            mask.transform.localScale = new Vector3(size.x, size.y, 1f);
            mask.sprite = _square;
        }

        /// <summary>盤面の上にいる敵「スキャナー」: 光る輪と瞳、盤面を掃く扇ビーム。</summary>
        private static void MakeScanner(Vector2 position, SoulController soul)
        {
            var root = new GameObject("Scanner").transform;
            root.position = position;

            MakeSpriteObject("Halo", root, _glow, WithAlpha(Cyan, 0.35f), 3).localScale = Vector3.one * 1.8f;
            MakeSpriteObject("Ring", root, _ring, CyanBright, 4).localScale = Vector3.one * 0.9f;
            var pupil = MakeSpriteObject("Pupil", root, _glow, Color.white, 5);
            pupil.localScale = Vector3.one * 0.4f;

            var sweep = MakeSpriteObject("Sweep", root, _wedge, WithAlpha(Cyan, 0.5f), 2);
            sweep.localScale = Vector3.one * 4f; // 扇の長さ 8 単位。盤面の外は SpriteMask で消える
            sweep.GetComponent<SpriteRenderer>().maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            sweep.gameObject.AddComponent<ScanSweep>();

            var eye = root.gameObject.AddComponent<ScannerEye>();
            Set(eye, ("_soul", soul), ("_pupil", pupil));
        }

        /// <summary>SOUL の光と、グレイズ範囲を示す輪。</summary>
        private static SoulView MakeSoulView(SoulController soul, SpriteRenderer heart, float grazeRadius)
        {
            var glow = MakeSpriteObject("Glow", soul.transform, _glow, WithAlpha(AmberGlow, 0.45f), 19);
            glow.localScale = Vector3.one * 2.4f;
            var ring = MakeSpriteObject("GrazeRing", soul.transform, _ring, WithAlpha(Amber, 0.35f), 21);
            // Ring スプライトは半径 0.9 の位置に線がある。SOUL のスケールを打ち消してグレイズ半径に合わせる。
            ring.localScale = Vector3.one * (grazeRadius / 0.9f / soul.transform.localScale.x * 2f);

            var view = soul.gameObject.AddComponent<SoulView>();
            Set(view, ("_soul", soul), ("_ring", ring.GetComponent<SpriteRenderer>()),
                ("_blinkTargets", new Object[] { heart, glow.GetComponent<SpriteRenderer>() }));
            return view;
        }

        private static DialogueBox MakeDialogueBox(Transform canvas)
        {
            // 角丸の暗いパネル。左にシアンのアクセント、上に話し手のタグ。
            var box = MakePanel("DialogueBox", canvas, new Vector2(0.22f, 1f), new Vector2(0.78f, 1f),
                new Vector2(0f, -230f), new Vector2(0f, -40f), WithAlpha(PanelBg, 0.9f));
            var accent = MakeRect("Accent", box, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(18f, 18f), new Vector2(26f, -18f));
            accent.gameObject.AddComponent<Image>().color = Cyan;
            var speaker = MakeLabel(box, "Speaker", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(44f, -52f), new Vector2(-30f, -18f), 22f, Cyan);
            speaker.text = "SCANNER";
            speaker.characterSpacing = 8f;
            var label = MakeLabel(box, "Label", Vector2.zero, Vector2.one, new Vector2(44f, 20f), new Vector2(-30f, -56f), 40f, TextMain);

            var dialogue = box.gameObject.AddComponent<DialogueBox>();
            Set(dialogue, ("_label", label), ("_linePrefix", ""));
            return dialogue;
        }

        private static DifficultyOption MakeOption(Transform parent, Difficulty difficulty, string label, string caption,
            Color accent, Vector2 position, SoulController soul)
        {
            var size = new Vector2(2f, 1.2f);
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            MakeSliced("Fill", go.transform, _panel, WithAlpha(accent, 0.1f), 2, size);
            MakeSliced("Line", go.transform, _panelLine, WithAlpha(accent, 0.9f), 3, size);

            var text = MakeWorldText(go.transform, "Label", Vector2.zero, size, 4f, accent);
            text.text = $"{label.ToUpperInvariant()}\n<size=55%>{caption}</size>";
            text.fontStyle = FontStyles.Bold;

            // 左端を原点にしたゲージ。pivot の x スケールを 0→1 にすると左から伸びる。
            const float inset = 0.18f;
            var pivot = new GameObject("GaugePivot").transform;
            pivot.SetParent(go.transform, false);
            pivot.localPosition = new Vector3(-size.x * 0.5f + inset, -size.y * 0.5f + 0.14f, 0f);
            pivot.localScale = new Vector3(0f, 1f, 1f);
            var bar = MakeSpriteObject("Gauge", pivot, _square, accent, 6);
            bar.localPosition = new Vector3((size.x - inset * 2f) * 0.5f, 0f, 0f);
            bar.localScale = new Vector3(size.x - inset * 2f, 0.06f, 1f);

            var option = go.AddComponent<DifficultyOption>();
            Set(option, ("_difficulty", difficulty), ("_soul", soul), ("_size", size), ("_gauge", pivot));
            return option;
        }

        // ───────── 部品 ─────────

        /// <summary>9 スライスで角を保ったまま size に広げる。</summary>
        private static void MakeSliced(string name, Transform parent, Sprite sprite, Color color, int order, Vector2 size)
        {
            var renderer = MakeSpriteObject(name, parent, sprite, color, order).GetComponent<SpriteRenderer>();
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
        }

        private static void SetTiled(Transform t, Vector2 size)
        {
            var renderer = t.GetComponent<SpriteRenderer>();
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = size;
        }

        private static TextMeshPro MakeWorldText(Transform parent, string name, Vector2 localPosition, Vector2 size, float fontSize, Color color)
        {
            var text = new GameObject(name).AddComponent<TextMeshPro>();
            text.transform.SetParent(parent, false);
            text.transform.localPosition = localPosition;
            text.rectTransform.sizeDelta = size;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.sortingOrder = 5;
            return text;
        }

        /// <summary>色付きの UI テキスト。クリックを受けないようにする。</summary>
        private static TextMeshProUGUI MakeLabel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, float fontSize, Color color)
        {
            var text = MakeText(parent, name, anchorMin, anchorMax, offsetMin, offsetMax, fontSize);
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>角丸パネルの Image。</summary>
        private static RectTransform MakePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var rect = MakeRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = _panel;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 0.5f;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>4x4 のスーパーサンプリングで縁を滑らかにしたスプライト。alpha(u, v) の u, v は −1〜1。</summary>
        private static Sprite MakeSoftSprite(string name, int size, Func<float, float, float> alpha,
            Vector2? pivot = null, int border = 0, int ppu = 0)
        {
            const int samples = 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float sum = 0f;
                for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    float u = (x + (sx + 0.5f) / samples) / size * 2f - 1f;
                    float v = (y + (sy + 0.5f) / samples) / size * 2f - 1f;
                    sum += Mathf.Clamp01(alpha(u, v));
                }
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, sum / (samples * samples)));
            }

            string path = $"{ArtDir}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu > 0 ? ppu : size;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // Tiled / Sliced 描画に必要
            settings.spriteBorder = Vector4.one * border;
            settings.spriteAlignment = (int)(pivot.HasValue ? SpriteAlignment.Custom : SpriteAlignment.Center);
            settings.spritePivot = pivot ?? new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static float Sq(float v) => v * v;
        private static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);

        private static Color Hex(string html)
        {
            if (!ColorUtility.TryParseHtmlString(html, out var color)) throw new ArgumentException($"色が不正: {html}");
            return color;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
