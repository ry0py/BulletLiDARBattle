using System.Collections.Generic;
using UnityEngine;

namespace UndertaleLiDAR.Battle
{
    public enum ShotShape { Ring, Aimed, Spiral, Random }
    public enum MoveType { Linear, Accelerate, Curve, SineWave, Homing }

    /// <summary>
    /// 飛ばし方（撃ち方＋飛び方）。種類は enum で選び、使うパラメータだけ設定する。
    /// 角度はすべて度数法で、0° = 右、反時計回り。
    /// </summary>
    [CreateAssetMenu(menuName = "Undertale LiDAR/Fire Pattern")]
    public class FirePattern : ScriptableObject
    {
        [Header("撃ち方")]
        [SerializeField] private ShotShape _shape = ShotShape.Ring;
        [Tooltip("1 回に撃つ弾数。Aimed は奇数なら自機に当たる向き、偶数なら両脇を通る")]
        [SerializeField, Min(1)] private int _count = 8;
        [Tooltip("Ring/Spiral: 基準角。Random: 中心角")]
        [SerializeField] private float _angle;
        [Tooltip("Aimed: 弾どうしの間隔角。Random: ばらつく範囲の角度")]
        [SerializeField] private float _spread = 15f;
        [Tooltip("Spiral: 1 回撃つごとに回す角度")]
        [SerializeField] private float _spiralStep = 10f;

        [Header("飛び方")]
        [SerializeField] private MoveType _move = MoveType.Linear;
        [SerializeField] private float _speed = 3f;
        [Tooltip("Accelerate: 加速度（負で減速）")]
        [SerializeField] private float _acceleration;
        [SerializeField] private float _minSpeed;
        [SerializeField] private float _maxSpeed = 10f;
        [Tooltip("Curve: 角速度 (度/秒)")]
        [SerializeField] private float _angularVelocity = 90f;
        [Tooltip("SineWave: 振れ幅")]
        [SerializeField] private float _amplitude = 0.5f;
        [Tooltip("SineWave: 周波数 (Hz)")]
        [SerializeField] private float _frequency = 1f;
        [Tooltip("Homing: 最大旋回量 (度/秒)")]
        [SerializeField] private float _turnRate = 90f;

        public float Speed => _speed;

        /// <summary>1 回分の発射角を results に入れる。shotIndex は何回目の発射か（Spiral 用）。</summary>
        public void GetAngles(Vector2 origin, Vector2 target, int shotIndex, System.Random random, List<float> results)
        {
            results.Clear();
            switch (_shape)
            {
                case ShotShape.Ring:
                    AddRing(_angle, results);
                    break;
                case ShotShape.Spiral:
                    AddRing(_angle + _spiralStep * shotIndex, results);
                    break;
                case ShotShape.Aimed:
                    float start = AngleTo(origin, target) - _spread * (_count - 1) * 0.5f;
                    for (int i = 0; i < _count; i++) results.Add(start + _spread * i);
                    break;
                case ShotShape.Random:
                    for (int i = 0; i < _count; i++) results.Add(_angle + ((float)random.NextDouble() - 0.5f) * _spread);
                    break;
            }
        }

        /// <summary>弾を dt 秒分動かす。Age は呼び出し側で進めておくこと。</summary>
        public void Move(Bullet bullet, float dt, Vector2 target)
        {
            switch (_move)
            {
                case MoveType.Linear:
                    break;
                case MoveType.Accelerate:
                    bullet.Speed = Mathf.Clamp(bullet.Speed + _acceleration * dt, _minSpeed, _maxSpeed);
                    break;
                case MoveType.Curve:
                    bullet.Angle += _angularVelocity * dt;
                    break;
                case MoveType.Homing:
                    bullet.Angle = Mathf.MoveTowardsAngle(bullet.Angle, AngleTo(bullet.Position, target), _turnRate * dt);
                    break;
                case MoveType.SineWave:
                    // 発射位置と経過時間から直接求めるので、誤差が積み重ならない。
                    var dir = Direction(bullet.Angle);
                    var normal = new Vector2(-dir.y, dir.x);
                    float wave = _amplitude * Mathf.Sin(2f * Mathf.PI * _frequency * bullet.Age);
                    bullet.Position = bullet.Origin + dir * (bullet.Speed * bullet.Age) + normal * wave;
                    return;
            }
            bullet.Position += Direction(bullet.Angle) * (bullet.Speed * dt);
        }

        private void AddRing(float baseAngle, List<float> results)
        {
            float step = 360f / _count;
            for (int i = 0; i < _count; i++) results.Add(baseAngle + step * i);
        }

        private static float AngleTo(Vector2 from, Vector2 to) =>
            Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;

        private static Vector2 Direction(float angle) =>
            new(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
    }
}
