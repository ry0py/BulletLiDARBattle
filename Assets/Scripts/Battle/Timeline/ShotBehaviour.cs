using UnityEngine;
using UnityEngine.Playables;

namespace LidarBattle.Battle
{
    /// <summary>クリップ再生中、経過時間から「今までに撃つべき回数」を求めて足りない分を撃つ。</summary>
    public class ShotBehaviour : PlayableBehaviour
    {
        private BulletType _bulletType;
        private FirePattern _pattern;
        private Vector2 _position;
        private float _interval;
        private System.Random _random;
        private int _shotCount;
        private double _lastTime;

        public void Setup(BulletType bulletType, FirePattern pattern, Vector2 position, float interval, System.Random random)
        {
            _bulletType = bulletType;
            _pattern = pattern;
            _position = position;
            _interval = interval;
            _random = random;
        }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            // Timeline 画面でシークしただけでは撃たない。クリップ範囲外（weight 0）でも撃たない。
            if (!Application.isPlaying || info.effectiveWeight <= 0f) return;
            if (playerData is not BulletSystem system || _bulletType == null || _pattern == null) return;

            double time = playable.GetTime();
            if (time < _lastTime) _shotCount = 0; // ループや巻き戻し
            _lastTime = time;

            int due = _interval > 0f ? (int)(time / _interval) + 1 : 1;
            while (_shotCount < due)
            {
                system.Fire(_bulletType, _pattern, _position, _shotCount, _random);
                _shotCount++;
            }
        }
    }
}
