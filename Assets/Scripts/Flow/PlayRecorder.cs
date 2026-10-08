using System;
using System.Collections.Generic;
using LidarBattle.Battle;
using UnityEngine;

namespace LidarBattle.Flow
{
    /// <summary>1 プレイ分の記録（被弾と SOUL の軌跡）を集める。ファイルへの保存は PlayLog。</summary>
    public sealed class PlayRecorder
    {
        /// <summary>SOUL の位置をとる間隔（秒）。</summary>
        public const float PathInterval = 0.1f;

        public readonly struct HitRecord
        {
            public readonly float Time;
            public readonly Vector2 Position;
            public readonly string BulletType;
            public readonly string Pattern;

            public HitRecord(float time, Vector2 position, string bulletType, string pattern)
            {
                Time = time;
                Position = position;
                BulletType = bulletType;
                Pattern = pattern;
            }
        }

        public DateTime StartedAt { get; } = DateTime.Now;
        public Difficulty Difficulty { get; }
        /// <summary>再生した弾幕の Timeline の名前（難易度ごとに何本かからランダムに選ぶため）。</summary>
        public string Timeline { get; }
        public bool DebugMode { get; }
        /// <summary>盤面の左下（ワールド座標）と大きさ。軌跡を盤面に重ねて描くのに使う。</summary>
        public Rect Board { get; }
        public List<HitRecord> Hits { get; } = new();
        /// <summary>i 番目が i × PathInterval 秒の SOUL のワールド座標。</summary>
        public List<Vector2> Path { get; } = new(700);

        private float _time;

        public PlayRecorder(Difficulty difficulty, string timeline, bool debugMode, Rect board)
        {
            Difficulty = difficulty;
            Timeline = timeline;
            DebugMode = debugMode;
            Board = board;
        }

        /// <summary>毎フレーム呼ぶ。サンプル時刻は個数から求めるので、誤差がたまらない。</summary>
        public void Tick(float elapsed, Vector2 soul)
        {
            _time = elapsed;
            while (elapsed >= Path.Count * PathInterval) Path.Add(soul);
        }

        public void AddHit(Bullet bullet, Vector2 soul) =>
            Hits.Add(new HitRecord(_time, soul, bullet.Type.name, bullet.Pattern.name));
    }
}
