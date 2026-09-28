using UnityEngine;

namespace LidarBattle.Battle
{
    /// <summary>弾幕の枠。transform の位置を中心とした矩形で、正規化座標 (0〜1) とワールド座標を変換する。</summary>
    public class BulletBoard : MonoBehaviour
    {
        [SerializeField] private Vector2 _size = new(4f, 3f);

        public Vector2 Size => _size;
        public Vector2 Min => (Vector2)transform.position - _size * 0.5f;

        public Vector2 NormalizedToWorld(Vector2 normalized) => Min + normalized * _size;
        public Vector2 WorldToNormalized(Vector2 world) => (world - Min) / _size;

        /// <summary>中心から padding だけ内側に収める（SOUL の見た目が枠からはみ出さないように）。</summary>
        public Vector2 Clamp(Vector2 world, float padding)
        {
            var min = Min + Vector2.one * padding;
            var max = Min + _size - Vector2.one * padding;
            return new Vector2(Mathf.Clamp(world.x, min.x, max.x), Mathf.Clamp(world.y, min.y, max.y));
        }

        public bool IsOutside(Vector2 world, float margin)
        {
            var min = Min - Vector2.one * margin;
            var max = Min + _size + Vector2.one * margin;
            return world.x < min.x || world.x > max.x || world.y < min.y || world.y > max.y;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(transform.position, _size);
        }
    }
}
