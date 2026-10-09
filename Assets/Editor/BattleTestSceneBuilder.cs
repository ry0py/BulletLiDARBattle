using System;
using System.IO;
using System.Linq;
using LidarBattle.Battle;
using LidarBattle.Flow;
using LidarBattle.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LidarBattle.EditorTools
{
    /// <summary>
    /// 弾幕テストシーン (BattleTestScene) を BattleScene のコピーから作る。BattleFlow を BattleTestFlow に差し替え、
    /// 本番の難易度別 Timeline と Assets/Timelines/Attacks/ の技を左上のドロップダウンで選んで再生できるようにする。会話ボックスと立ち絵は隠す。
    /// 選択シーンでデバッグモードのときに [T] を 2 秒長押しすると来る（OperatorShortcuts）。
    /// BattleScene や技を作り直したら、これも作り直す。既存の同名シーンは上書きする。
    /// </summary>
    public static class BattleTestSceneBuilder
    {
        private const string SourcePath = "Assets/Scenes/Actual/BattleScene.unity";
        private const string ScenePath = "Assets/Scenes/Test/BattleTestScene.unity";

        [MenuItem("Tools/LiDAR Battle/Build Battle Test Scene")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
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
                var label = (TMP_Text)so.FindProperty("_difficultyLabel").objectReferenceValue;
                var timeLabel = (TMP_Text)so.FindProperty("_timeLabel").objectReferenceValue;
                var go = battle.gameObject;
                Object.DestroyImmediate(battle);
                go.name = nameof(BattleTestFlow);

                // 技の名前が収まるよう、左下のテキストを横に広げて上の帯（会話ボックスの場所。ここでは出さない）に移す。
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0f, 1f);
                label.rectTransform.offsetMin = new Vector2(560f, -84f); // 左上のドロップダウンの右
                label.rectTransform.offsetMax = new Vector2(1520f, -20f);
                Object.FindFirstObjectByType<DialogueBox>(FindObjectsInactive.Include).gameObject.SetActive(false);
                Object.FindFirstObjectByType<PortraitView>(FindObjectsInactive.Include)?.gameObject.SetActive(false);

                var dropdown = MakeDropdown(label.canvas.transform);

                var flow = go.AddComponent<BattleTestFlow>();
                GameSetupBuilder.Set(flow, ("_director", director), ("_attacks", LoadAttacks()), ("_clock", clock),
                    ("_bullets", bullets), ("_dropdown", dropdown), ("_label", label), ("_timeLabel", timeLabel));

                EditorSceneManager.SaveScene(scene);
                GameSetupBuilder.AddToBuildSettings(ScenePath);
                Debug.Log($"[BattleTestSceneBuilder] 完了: {ScenePath}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        /// <summary>左上に技を選ぶドロップダウンを置く。マウスで操作するので Canvas のレイキャストと EventSystem も足す。</summary>
        private static TMP_Dropdown MakeDropdown(Transform canvas)
        {
            if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var resources = new TMP_DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
            };
            var dropdown = TMP_DefaultControls.CreateDropdown(resources).GetComponent<TMP_Dropdown>();
            dropdown.name = "AttackDropdown";
            var rect = (RectTransform)dropdown.transform;
            rect.SetParent(canvas, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(20f, -20f);
            rect.sizeDelta = new Vector2(520f, 64f);

            // 1920x1080 基準の Canvas では既定の大きさだと小さすぎるので、文字・項目・一覧を大きくする。
            const float itemHeight = 48f;
            dropdown.captionText.fontSize = 34f;
            dropdown.itemText.fontSize = 30f;
            ((RectTransform)dropdown.template).sizeDelta = new Vector2(0f, itemHeight * 14f);
            var item = (RectTransform)dropdown.itemText.transform.parent;
            item.sizeDelta = new Vector2(item.sizeDelta.x, itemHeight);
            var content = (RectTransform)item.parent;
            content.sizeDelta = new Vector2(content.sizeDelta.x, itemHeight);
            return dropdown;
        }

        /// <summary>本番の難易度別 Timeline（Easy1〜Hard4）→ 本番で使う技（AttackLibraryBuilder.BattleAttacks の順）→ 残りの技を名前順で並べる。</summary>
        private static Object[] LoadAttacks()
        {
            var guids = AssetDatabase.FindAssets("t:TimelineAsset", new[] { AttackLibraryBuilder.AttackTimelineDir });
            var attacks = BattleTimelineBuilder.Load().Concat(guids
                .Select(g => AssetDatabase.LoadAssetAtPath<TimelineAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .OrderBy(t => Array.IndexOf(AttackLibraryBuilder.BattleAttacks, t.name) is var i && i >= 0 ? i : int.MaxValue)
                .ThenBy(t => t.name, StringComparer.Ordinal))
                .ToArray<Object>();
            if (attacks.Length == 0)
                throw new InvalidOperationException($"{AttackLibraryBuilder.AttackTimelineDir} に技がありません。先に Build Attack Library を実行してください");
            return attacks;
        }
    }
}
