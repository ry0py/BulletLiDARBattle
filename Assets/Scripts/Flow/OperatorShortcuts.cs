using System;
using LidarBattle.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LidarBattle.Flow
{
    /// <summary>
    /// 展示運用のためのキー長押しショートカット（表示は出さない）。起動時に自動で作られ、シーンをまたいで残る。
    /// BattleScene: [R] やり直し / [P] 選択シーンへ。SelectScene: [E][M][H] 難易度を選んでバトルへ / [L] LiDAR 確認シーンへ。
    /// LidarLiveScene: [P] 選択シーンへ。
    /// </summary>
    public sealed class OperatorShortcuts : MonoBehaviour
    {
        private const float HoldSeconds = 5f;
        private const string SelectScene = "SelectScene";
        private const string BattleScene = "BattleScene";
        private const string DebugScene = "LidarLiveScene";

        private static readonly Key[] Keys = { Key.R, Key.P, Key.E, Key.M, Key.H, Key.L };

        private Key _heldKey = Key.None;
        private Action _action;
        private float _heldSeconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject(nameof(OperatorShortcuts));
            DontDestroyOnLoad(go);
            go.AddComponent<OperatorShortcuts>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            // 離すまでは同じキーを数え続ける。発動後も離すまでは次を数えない（R の連続リスタートを防ぐ）。
            if (_heldKey != Key.None)
            {
                if (keyboard[_heldKey].isPressed) Tick();
                else _heldKey = Key.None;
                return;
            }

            foreach (Key key in Keys)
            {
                if (!keyboard[key].isPressed) continue;
                _heldKey = key;
                _action = ActionFor(SceneManager.GetActiveScene().name, key);
                _heldSeconds = 0f;
                return;
            }
        }

        private void Tick()
        {
            if (_action == null) return;
            _heldSeconds += Time.unscaledDeltaTime;
            if (_heldSeconds < HoldSeconds) return;

            Action action = _action;
            _action = null;
            action();
        }

        private static Action ActionFor(string scene, Key key) => (scene, key) switch
        {
            (BattleScene, Key.R) => () => Load(BattleScene),
            (BattleScene, Key.P) => () => Load(SelectScene),
            (SelectScene, Key.E) => () => StartBattle(Difficulty.Easy),
            (SelectScene, Key.M) => () => StartBattle(Difficulty.Medium),
            (SelectScene, Key.H) => () => StartBattle(Difficulty.Hard),
            (SelectScene, Key.L) => OpenDebugScene,
            (DebugScene, Key.P) => () => Load(SelectScene),
            _ => null,
        };

        private static void StartBattle(Difficulty difficulty)
        {
            GameSession.Difficulty = difficulty;
            Load(BattleScene);
        }

        private static void OpenDebugScene()
        {
            // センサーは同時に 1 接続しか受け付けないので、ゲーム側の接続を切ってから確認シーンに直接つながせる。
            LidarInputSource.Shutdown();
            Load(DebugScene);
        }

        private static void Load(string scene)
        {
            // LoadScene は新シーンの Awake が旧シーンの破棄より先に走る。LidarLiveScene が持つ LiDAR 接続を
            // 選択シーンの再接続より先に閉じたいので、今のシーンはこのフレームの終わりに先に壊しておく。
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects()) Destroy(root);
            SceneManager.LoadScene(scene);
        }
    }
}
