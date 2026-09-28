using UnityEngine;

namespace UndertaleLiDAR.Battle
{
    /// <summary>弾 1 発の状態。自分では何もせず、BulletSystem が読み書きする。</summary>
    public sealed class Bullet
    {
        public BulletType Type;
        public FirePattern Pattern;
        public Vector2 Origin;
        public Vector2 Position;
        public float Angle;
        public float Speed;
        public float Age;
        public bool Grazed;
        public SpriteRenderer View;
    }
}
