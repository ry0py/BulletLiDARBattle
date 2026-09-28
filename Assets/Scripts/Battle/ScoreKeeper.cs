using UnityEngine;

namespace UndertaleLiDAR.Battle
{
    /// <summary>被弾数・グレイズ数を数えてスコアにする。計算式は仮。</summary>
    public class ScoreKeeper : MonoBehaviour
    {
        [SerializeField] private BulletSystem _bullets;
        [SerializeField] private int _baseScore = 1000;
        [SerializeField] private int _hitPenalty = 100;
        [SerializeField] private int _grazeBonus = 10;

        public int Hits { get; private set; }
        public int Grazes { get; private set; }
        public int Score => Mathf.Max(0, _baseScore - Hits * _hitPenalty + Grazes * _grazeBonus);

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

        private void OnHit(Bullet _) => Hits++;
        private void OnGrazed(Bullet _) => Grazes++;
    }
}
