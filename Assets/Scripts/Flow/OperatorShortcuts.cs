using System;
using LidarBattle.Audio;
using LidarBattle.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LidarBattle.Flow
{
    /// <summary>
    /// 展示運用のためのキー長押しショートカット（表示は出さない）。起動時に自動で作られ、シーンをまたいで残る。
    /// バトル: [R] やり直し / [P] 選択シーンへ。
    /// 選択: [E][M][H] 難易度を選んでバトルへ / [L] LiDAR 確認シーンへ / [U] ユーザー設定シーンへ。
    /// LidarLiveScene・UserSettingsScene: [P] 直前に使っていた選択シーンへ。
    /// 選択とバトルのシーンは組（円柱用 SelectScene/BattleScene、ハート用 HeartSelectScene/HeartBattleScene）で扱い、
    /// 組の中だけで移る。
    /// </summary>
    public sealed class OperatorShortcuts : MonoBehaviour
    {
        private const float HoldSeconds = 5f;
        private const string DebugScene = "LidarLiveScene";
        private const string SettingsScene = "UserSettingsScene";

        // (選択シーン, バトルシーン) の組。
        private static readonly (string Select, string Battle)[] ScenePairs =
        {
            ("SelectScene", "BattleScene"),
            ("HeartSelectScene", "HeartBattleScene"),
        };

        private static string s_lastSelectScene = ScenePairs[0].Select; // LiDAR 確認・ユーザー設定シーンから戻る先

        private static readonly Key[] Keys = { Key.R, Key.P, Key.E, Key.M, Key.H, Key.L, Key.U };

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

        private static Action ActionFor(string scene, Key key)
        {
            if (scene == DebugScene || scene == SettingsScene) return key == Key.P ? () => Load(s_lastSelectScene) : null;

            foreach (var (select, battle) in ScenePairs)
            {
                if (scene == battle)
                {
                    return key switch
                    {
                        Key.R => () => Load(battle),
                        Key.P => () => Load(select),
                        _ => null,
                    };
                }
                if (scene == select)
                {
                    return key switch
                    {
                        Key.E => () => StartBattle(battle, Difficulty.Easy),
                        Key.M => () => StartBattle(battle, Difficulty.Medium),
                        Key.H => () => StartBattle(battle, Difficulty.Hard),
                        Key.L => () => OpenDebugScene(select),
                        Key.U => () => OpenSettingsScene(select),
                        _ => null,
                    };
                }
            }
            return null;
        }

        private static void StartBattle(string battleScene, Difficulty difficulty)
        {
            GameSession.Difficulty = difficulty;
            Load(battleScene);
        }

        private static void OpenDebugScene(string fromSelectScene)
        {
            s_lastSelectScene = fromSelectScene;
            // センサーは同時に 1 接続しか受け付けないので、ゲーム側の接続を切ってから確認シーンに直接つながせる。
            LidarInputSource.Shutdown();
            GameAudio.StopBgm(); // 確認シーンは無音でよい。選択シーンに戻ると SelectFlow が流し直す
            Load(DebugScene);
        }

        private static void OpenSettingsScene(string fromSelectScene)
        {
            s_lastSelectScene = fromSelectScene;
            Load(SettingsScene);
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
