using UnityEngine;

namespace LidarBattle.Battle
{
    /// <summary>弾 1 発の状態。自分では何もせず、BulletSystem が読み書きする。</summary>
    public sealed class Bullet
    {
        public BulletType Type;
        public FirePattern Pattern;
        public Vector2 Origin;
        public Vector2 Position;
        /// <summary>直前のステップでの速度（ワールド単位/秒）。見た目には使わず、AI の観測用。</summary>
        public Vector2 Velocity;
        public float Angle;
        public float Speed;
        public float Age;
        public bool Grazed;
        public SpriteRenderer View;
    }
}
