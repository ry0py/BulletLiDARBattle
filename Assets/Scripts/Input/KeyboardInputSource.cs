using UndertaleLiDAR.Battle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UndertaleLiDAR.Input
{
    /// <summary>矢印キー / WASD で SOUL を動かす。速度はワールド単位で指定し、盤面サイズで正規化する。</summary>
    public class KeyboardInputSource : MonoBehaviour, IHeartInputSource
    {
        [SerializeField] private BulletBoard _board;
        [SerializeField] private float _speed = 3f;

        public Vector2 ReadTarget(Vector2 currentNormalized, float deltaTime)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return currentNormalized;

            var dir = Vector2.zero;
            if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) dir.x -= 1f;
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) dir.x += 1f;
            if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) dir.y -= 1f;
            if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) dir.y += 1f;

            return currentNormalized + dir * _speed * deltaTime / _board.Size;
        }
    }
}
