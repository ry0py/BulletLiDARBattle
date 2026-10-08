using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LidarBattle.Flow
{
    /// <summary>
    /// ユーザー設定シーンの画面。キーで設定を切り替え、今の値を表示する。
    /// [D] デバッグモードのオン/オフ。選択シーンへは OperatorShortcuts の [P] 長押しで戻る。
    /// </summary>
    public sealed class UserSettingsView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;

        private void Start() => Refresh();

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.dKey.wasPressedThisFrame)
            {
                GameSession.DebugMode = !GameSession.DebugMode;
                Refresh();
            }
        }

        private void Refresh()
        {
            _label.text =
                "ユーザー設定\n\n" +
                $"[D] デバッグモード: {(GameSession.DebugMode ? "<color=#ffd040>ON</color>" : "<color=#80ff80>OFF</color>")}\n\n" +
                "<size=70%>[P] を 5 秒長押しで選択画面へ戻る</size>";
        }
    }
}
