using UnityEngine;

namespace LidarBattle.Battle
{
    /// <summary>
    /// 被弾数・グレイズ数を数えてスコアにする。減点はしない。
    /// プレイ中はグレイズで増えるだけで、被弾は終了時に「被弾ボーナス」（被弾が少ないほど大きい）として足す。
    /// </summary>
    public class ScoreKeeper : MonoBehaviour
    {
        [SerializeField] private BulletSystem _bullets;
        [Tooltip("グレイズ 1 回の点数")]
        [SerializeField] private int _grazePoints = 10;
        [Tooltip("被弾 0 回のときの被弾ボーナス")]
        [SerializeField] private int _maxHitBonus = 1000;
        [Tooltip("被弾 1 回ごとに被弾ボーナスが小さくなる量（0 未満にはならない）")]
        [SerializeField] private int _hitBonusStep = 100;

        public int Hits { get; private set; }
        public int Grazes { get; private set; }

        /// <summary>プレイ中に表示するスコア。グレイズで増えるだけ。</summary>
        public int GrazeScore => Grazes * _grazePoints;
        public int HitBonus => Mathf.Max(0, _maxHitBonus - Hits * _hitBonusStep);
        public int FinalScore => GrazeScore + HitBonus;

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
