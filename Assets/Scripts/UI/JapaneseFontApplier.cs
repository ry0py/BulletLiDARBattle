using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LidarBattle.UI
{
    /// <summary>
    /// 同梱した日本語フォント (Resources/JapaneseFont) から TMP の動的フォントを生成し、
    /// シーン内の全 TMP_Text に適用する。動的フォントなので必要な字だけ実行時にラスタライズされ、
    /// 数千字を事前ベイクせずに日本語が表示できる。シーンや既存アセットは編集しない。
    /// </summary>
    public static class JapaneseFontApplier
    {
        private const string FontResourceName = "JapaneseFont";
        private static TMP_FontAsset _cached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            // 最初のシーンだけでなく、以降に読み込むシーンにも適用する。
            SceneManager.sceneLoaded += (_, _) => Apply();
        }

        private static void Apply()
        {
            TMP_FontAsset jp = LoadJpFont();
            if (jp == null)
            {
                return;
            }
            var texts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (TMP_Text t in texts)
            {
                t.font = jp;
                t.fontSharedMaterial = jp.material;
            }
        }

        private static TMP_FontAsset LoadJpFont()
        {
            if (_cached != null)
            {
                return _cached;
            }
            Font src = Resources.Load<Font>(FontResourceName);
            if (src == null)
            {
                Debug.LogWarning($"[JapaneseFontApplier] Resources/{FontResourceName} が見つかりません。" +
                                 "日本語フォントを Assets/Resources/JapaneseFont.ttc に配置してください。");
                return null;
            }
            _cached = TMP_FontAsset.CreateFontAsset(src); // 動的 SDF フォント
            if (_cached == null)
            {
                Debug.LogWarning("[JapaneseFontApplier] TMP 動的フォントの生成に失敗しました。");
            }
            return _cached;
        }
    }
}
