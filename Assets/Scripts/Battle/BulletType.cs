using UnityEngine;

namespace LidarBattle.Battle
{
    /// <summary>弾の種類（見た目と当たり判定の大きさ）。</summary>
    [CreateAssetMenu(menuName = "LiDAR Battle/Bullet Type")]
    public class BulletType : ScriptableObject
    {
        [SerializeField] private Sprite _sprite;
        [SerializeField] private Color _color = Color.white;
        [SerializeField] private float _scale = 0.2f;
        [SerializeField] private float _radius = 0.08f;

        public Sprite Sprite => _sprite;
        public Color Color => _color;
        public float Scale => _scale;
        public float Radius => _radius;
    }
}
