using LidarBattle.Battle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LidarBattle.Input
{
    /// <summary>
    /// 矢印キー / WASD で SOUL を動かす。速度はワールド単位で指定し、盤面サイズで正規化する。
    /// 移動キーを押していないときは位置を出さない（次の入力源に譲る）。
    /// </summary>
    public class KeyboardInputSource : MonoBehaviour, IHeartInputSource
    {
        [SerializeField] private BulletBoard _board;
        [SerializeField] private float _speed = 3f;

        public bool TryReadTarget(Vector2 currentNormalized, float deltaTime, out Vector2 target)
        {
            target = currentNormalized;
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;

            var dir = Vector2.zero;
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) dir.x -= 1f;
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) dir.x += 1f;
            if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) dir.y -= 1f;
            if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) dir.y += 1f;

            if (dir == Vector2.zero) return false;
            target = currentNormalized + dir * _speed * deltaTime / _board.Size;
            return true;
        }
    }
}
