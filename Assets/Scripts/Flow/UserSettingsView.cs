using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LidarBattle.Flow
{
    /// <summary>
    /// ユーザー設定シーンの画面。キーで設定を切り替え、今の値を表示する。
    /// [D] デバッグモードのオン/オフ。[S] スコアボードのサーバー、[C] カメラ検出（ArUco）は押すと起動、
    /// 起動中は 5 秒長押しで停止（押し間違いで止めないように）。選択シーンへは OperatorShortcuts の [P] 長押しで戻る。
    /// </summary>
    public sealed class UserSettingsView : MonoBehaviour
    {
        private const string ScoreBoardUrl = "http://localhost:8000/";
        private const float StopHoldSeconds = 5f;
        private const float StatusInterval = 0.5f; // 窓を閉じて止まったときなどに表示を追いつかせる間隔

        /// <summary>ツール 1 つ分のキーと長押しの状態。</summary>
        private sealed class ToolKey
        {
            public readonly Key Key;
            public readonly ToolProcess Tool;
            public float HeldSeconds;
            public bool WaitRelease; // 起動・停止したキーを離すまでは数えない

            public ToolKey(Key key, ToolProcess tool)
            {
                Key = key;
                Tool = tool;
            }
        }

        [SerializeField] private TMP_Text _label;

        private readonly ToolKey _scoreBoard = new(Key.S, ToolProcess.ScoreBoard);
        private readonly ToolKey _camera = new(Key.C, ToolProcess.CameraTracker);
        private float _nextStatus;
        private string _shown;

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
            if (UpdateTool(keyboard, _scoreBoard) && _scoreBoard.Tool.IsRunning) StartCoroutine(OpenScoreBoardWhenReady());
            UpdateTool(keyboard, _camera);

            if (Time.unscaledTime >= _nextStatus) Refresh();
        }

        /// <summary>止まっていれば押して起動、動いていれば長押しで停止。起動・停止したら true。</summary>
        private bool UpdateTool(Keyboard keyboard, ToolKey t)
        {
            var key = keyboard[t.Key];
            if (!key.isPressed)
            {
                if (t.HeldSeconds > 0f) Refresh(); // 途中で離したら長押しの表示を消す
                t.HeldSeconds = 0f;
                t.WaitRelease = false;
                return false;
            }
            if (t.WaitRelease) return false;

            if (!t.Tool.IsRunning)
            {
                if (!key.wasPressedThisFrame) return false;
                t.Tool.Start();
                t.WaitRelease = true;
                Refresh();
                return t.Tool.IsRunning;
            }

            t.HeldSeconds += Time.unscaledDeltaTime;
            if (t.HeldSeconds >= StopHoldSeconds)
            {
                t.Tool.Stop();
                t.HeldSeconds = 0f;
                t.WaitRelease = true;
                Refresh();
                return true;
            }
            Refresh();
            return false;
        }

        /// <summary>サーバーが受け付けるようになってからブラウザで開く（先に開くとエラーの画面になる）。</summary>
        private static IEnumerator OpenScoreBoardWhenReady()
        {
            for (float waited = 0f; waited < 5f; waited += 0.25f)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                if (!ToolProcess.ScoreBoard.IsRunning) yield break;
                if (ToolProcess.ScoreBoard.IsListening) break;
            }
            Application.OpenURL(ScoreBoardUrl);
        }

        private void Refresh()
        {
            _nextStatus = Time.unscaledTime + StatusInterval;
            string text =
                "ユーザー設定\n\n" +
                $"[D] デバッグモード: {(GameSession.DebugMode ? "<color=#ffd040>ON</color>" : "<color=#80ff80>OFF</color>")}\n" +
                $"[S] スコアボード: {Status(_scoreBoard)}\n" +
                $"[C] カメラ検出 (ArUco): {Status(_camera)}\n\n" +
                "<size=70%>[S] [C] は押すと起動、起動中は 5 秒長押しで停止\n" +
                "[P] を 5 秒長押しで選択画面へ戻る</size>";
            if (text == _shown) return; // 変わったときだけ書き換える
            _shown = text;
            _label.text = text;
        }

        private static string Status(ToolKey t)
        {
            if (t.Tool.IsRunning)
            {
                if (t.HeldSeconds <= 0f) return "<color=#80ff80>起動中</color>";
                int left = Mathf.CeilToInt(StopHoldSeconds - t.HeldSeconds);
                return $"<color=#80ff80>起動中</color><size=70%><color=#ffd040>（あと {left} 秒で停止）</color></size>";
            }
            if (t.Tool.IsRunningElsewhere) return "<color=#80ff80>起動中</color><size=70%>（別に起動済み）</size>";
            if (t.Tool.Error != null) return $"<color=#ff6060>停止中</color><size=70%>（{t.Tool.Error}）</size>";
            return "<color=#8a8a8a>停止中</color>";
        }
    }
}
