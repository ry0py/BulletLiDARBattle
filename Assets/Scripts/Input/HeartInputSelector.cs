using UnityEngine;
using UnityEngine.InputSystem;

namespace LidarBattle.Input
{
    /// <summary>
    /// キーボード → LiDAR → カメラ (ArUco) の順に聞き、最初に位置を出せた入力源を使う。
    /// キーボードは移動キーを押している間だけ位置を出すので、押せば常にキーボードが優先される。
    /// どれも出せなければ false を返し、SOUL はその場で止まる。[F1] 使用中の入力源を表示。
    /// </summary>
    [RequireComponent(typeof(KeyboardInputSource), typeof(LidarInputSource), typeof(CameraInputSource))]
    public sealed class HeartInputSelector : MonoBehaviour, IHeartInputSource
    {
        private IHeartInputSource[] _sources;
        private string[] _names;
        private int _activeIndex = -1;
        private static bool s_showStatus; // 展示中は出さない。[F1] で表示（シーンをまたいで保持）

        private void Awake()
        {
            // 優先順はここで固定する（Inspector では並べ替えない）。
            _sources = new IHeartInputSource[]
            {
                GetComponent<KeyboardInputSource>(),
                GetComponent<LidarInputSource>(),
                GetComponent<CameraInputSource>(),
            };
            _names = new[] { "keyboard", "LiDAR", "camera (ArUco)" };
        }

        public bool TryReadTarget(Vector2 currentNormalized, float deltaTime, out Vector2 target)
        {
            for (int i = 0; i < _sources.Length; i++)
            {
                if (!_sources[i].TryReadTarget(currentNormalized, deltaTime, out target)) continue;
                _activeIndex = i;
                return true;
            }
            _activeIndex = -1;
            target = currentNormalized;
            return false;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) s_showStatus = !s_showStatus;
        }

        private void OnGUI()
        {
            if (!s_showStatus) return;
            GUI.Box(new Rect(10, 160, 620, 30), GUIContent.none);
            GUI.Label(new Rect(20, 165, 600, 20),
                $"Input: {(_activeIndex >= 0 ? _names[_activeIndex] : "none (stopped)")}   order: keyboard > LiDAR > camera");
        }
    }
}
