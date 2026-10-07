using UnityEngine;

namespace LidarBattle.Battle
{
    /// <summary>被弾回数を数える。スコアは出さず、結果は被弾回数だけ。</summary>
    public class ScoreKeeper : MonoBehaviour
    {
        [SerializeField] private BulletSystem _bullets;

        public int Hits { get; private set; }

        private void OnEnable() => _bullets.Hit += OnHit;
        private void OnDisable() => _bullets.Hit -= OnHit;

        private void OnHit(Bullet _) => Hits++;
    }
}
