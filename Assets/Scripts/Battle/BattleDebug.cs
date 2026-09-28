using UnityEngine;
using UnityEngine.InputSystem;

namespace LidarBattle.Battle
{
    /// <summary>
    /// デバッグ用: 当たり判定/グレイズ範囲の Gizmo、被弾/グレイズのログ、操作キー。
    /// P = 一時停止、Tab = スロー、X = 全弾消去。
    /// </summary>
    public class BattleDebug : MonoBehaviour
    {
        [SerializeField] private BattleClock _clock;
        [SerializeField] private BulletSystem _bullets;
        [SerializeField] private SoulController _soul;
        [SerializeField] private bool _logEvents = true;

        private void OnEnable()
        {
            _bullets.Hit += OnHit;
            _bullets.Grazed += OnGrazed;
        }

        private void OnDisable()
        {
            _bullets.Hit -= OnHit;
            _bullets.Grazed -= OnGrazed;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.pKey.wasPressedThisFrame) _clock.SetPaused(!_clock.IsPaused);
            if (keyboard.tabKey.wasPressedThisFrame) _clock.SetSlow(!_clock.IsSlow);
            if (keyboard.xKey.wasPressedThisFrame) _bullets.ClearAll();
        }

        private void OnHit(Bullet b)
        {
            if (_logEvents) Debug.Log($"[Battle] Hit ({b.Type.name})");
        }

        private void OnGrazed(Bullet b)
        {
            if (_logEvents) Debug.Log($"[Battle] Graze ({b.Type.name})");
        }

        private void OnDrawGizmos()
        {
            if (_soul != null)
            {
                Gizmos.color = _soul.IsInvincible ? Color.magenta : Color.red;
                Gizmos.DrawWireSphere(_soul.Position, _soul.HitRadius);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_soul.Position, _soul.GrazeRadius);
            }

            if (_bullets == null || !Application.isPlaying) return;
            Gizmos.color = Color.green;
            foreach (var b in _bullets.Active) Gizmos.DrawWireSphere(b.Position, b.Type.Radius);
        }
    }
}
