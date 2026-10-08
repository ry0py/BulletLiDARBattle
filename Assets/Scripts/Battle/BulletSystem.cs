using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace LidarBattle.Battle
{
    /// <summary>撃たれた後の弾をすべて管理する（移動・画面外消去・被弾/グレイズ判定・全消去）。</summary>
    public class BulletSystem : MonoBehaviour
    {
        [SerializeField] private BattleClock _clock;
        [SerializeField] private BulletBoard _board;
        [SerializeField] private SoulController _soul;
        [Tooltip("枠からこれだけ離れたら弾を消す")]
        [SerializeField] private float _cullMargin = 1f;
        [SerializeField] private int _sortingOrder = 10;
        [Tooltip("弾を進める固定ステップ（秒）。フレームレートに依らず毎回同じ弾道にするため")]
        [SerializeField] private float _stepTime = 1f / 60f;
        [Tooltip("寿命のある弾が消える前に薄くなっていく秒数")]
        [SerializeField] private float _fadeSeconds = 0.4f;

        public event Action<Bullet> Hit;
        public event Action<Bullet> Grazed;

        private readonly List<Bullet> _active = new();
        private readonly List<(Vector2 offset, float angle)> _shots = new();
        private ObjectPool<Bullet> _pool;
        private float _accumulator;

        public IReadOnlyList<Bullet> Active => _active;
        public SoulController Soul => _soul;
        public BulletBoard Board => _board;

        private void Awake()
        {
            _pool = new ObjectPool<Bullet>(CreateBullet,
                b => b.View.gameObject.SetActive(true),
                b => b.View.gameObject.SetActive(false));
        }

        /// <summary>boardPosition は盤面の正規化座標（枠外に置いてもよい）。</summary>
        public void Fire(BulletType type, FirePattern pattern, Vector2 boardPosition, int shotIndex, System.Random random)
        {
            var origin = _board.NormalizedToWorld(boardPosition);
            pattern.GetShots(origin, _soul.Position, new Rect(_board.Min, _board.Size), shotIndex, random, _shots);

            foreach (var (offset, angle) in _shots)
            {
                var b = _pool.Get();
                b.Type = type;
                b.Pattern = pattern;
                b.Origin = origin + offset;
                b.Position = b.Origin;
                b.Angle = angle;
                b.Speed = pattern.Speed;
                b.Age = 0f;
                b.Grazed = false;
                b.View.sprite = type.Sprite;
                b.View.color = type.Color;
                b.View.transform.localScale = Vector3.one * type.Scale;
                b.View.transform.position = b.Origin;
                _active.Add(b);
            }
        }

        public void ClearAll()
        {
            foreach (var b in _active) _pool.Release(b);
            _active.Clear();
        }

        private void Update()
        {
            _accumulator += _clock.BulletDeltaTime;
            while (_accumulator >= _stepTime)
            {
                _accumulator -= _stepTime;
                MoveBullets(_stepTime);
                CheckSoul();
            }
        }

        private void MoveBullets(float dt)
        {
            var target = _soul.Position;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var b = _active[i];
                b.Age += dt;
                b.Pattern.Move(b, dt, target);
                SwitchPattern(b);

                // 寿命のある弾は寿命で消す（盤面の外から入ってくる Converge の輪などを途中で消さないため）。
                float lifetime = b.Pattern.Lifetime;
                if (lifetime > 0f ? b.Age >= lifetime : _board.IsOutside(b.Position, _cullMargin))
                {
                    _pool.Release(b);
                    _active[i] = _active[^1];
                    _active.RemoveAt(_active.Count - 1);
                    continue;
                }
                b.View.transform.position = b.Position;
                if (lifetime > 0f && lifetime - b.Age < _fadeSeconds)
                {
                    var color = b.Type.Color;
                    color.a *= (lifetime - b.Age) / _fadeSeconds;
                    b.View.color = color;
                }
            }
        }

        /// <summary>FirePattern.Next があれば、NextTime 秒たった弾をその飛び方に切り替える（経過時間は 0 から数え直す）。</summary>
        private static void SwitchPattern(Bullet b)
        {
            var next = b.Pattern.Next;
            if (next == null || b.Age < b.Pattern.NextTime) return;
            b.Pattern = next;
            b.Origin = b.Position;
            b.Speed = next.Speed;
            b.Age = 0f;
        }

        private void CheckSoul()
        {
            var soulPos = _soul.Position;
            // イベント先で ClearAll されても安全なように、毎回 Count を見る。
            for (int i = 0; i < _active.Count; i++)
            {
                var b = _active[i];
                float sqrDist = (b.Position - soulPos).sqrMagnitude;

                float hit = b.Type.Radius + _soul.HitRadius;
                if (!_soul.IsInvincible && sqrDist <= hit * hit)
                {
                    _soul.StartInvincibility();
                    Hit?.Invoke(b);
                    continue;
                }

                float graze = b.Type.Radius + _soul.GrazeRadius;
                if (!b.Grazed && sqrDist <= graze * graze)
                {
                    b.Grazed = true;
                    Grazed?.Invoke(b);
                }
            }
        }

        private Bullet CreateBullet()
        {
            var go = new GameObject("Bullet");
            go.transform.SetParent(transform, false);
            var view = go.AddComponent<SpriteRenderer>();
            view.sortingOrder = _sortingOrder;
            return new Bullet { View = view };
        }
    }
}
