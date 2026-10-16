using System.Collections.Generic;
using LidarBattle.Battle;
using LidarBattle.Input;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LidarBattle.Training
{
    /// <summary>
    /// 弾をよける強化学習のエージェント。SOUL の入力源（IHeartInputSource）として SoulController につなぐ。
    /// 観測: 自分（位置・無敵中か・技の進み具合）、安置のシート、盤面のすべての弾（近い順、最大 Max Num Observables 個）。
    /// 行動: 9 方向（0 = 止まる、1〜8 = 上から時計回り）。速さはキーボードと同じ。
    /// 報酬・技の選び方は mlagents-learn の YAML（environment_parameters）で変えられる。詳細は .claude/docs/rl-training.md。
    /// </summary>
    [RequireComponent(typeof(BufferSensorComponent))]
    public class DodgeAgent : Agent, IHeartInputSource
    {
        /// <summary>弾 1 発ぶんの観測の数: 相対位置 2・速度 2・半径 1・弾の種類 (one-hot)。</summary>
        public static int BulletObservationSize(int typeCount) => 5 + typeCount;
        /// <summary>
        /// 弾以外の観測の数: 位置 2・無敵中か 1・技の進み具合 1・安置のシート 5（出ているか 1・自分からの相対位置 2・大きさ 2）。
        /// 安置の技はシートの外が一度に弾で埋まるので、弾だけ見ていては間に合わない。シートを見て先に入っておけるように見せる。
        /// </summary>
        public const int SelfObservationSize = 9;
        public const int ActionCount = 9;

        // 0 = 止まる、1〜8 = 上から時計回り。斜めはキーボードと同じく正規化しない（同じ速さで比べるため）。
        private static readonly Vector2[] Directions =
        {
            Vector2.zero, new(0, 1), new(1, 1), new(1, 0), new(1, -1), new(0, -1), new(-1, -1), new(-1, 0), new(-1, 1),
        };

        [SerializeField] private DodgeArena _arena;
        [Tooltip("SOUL の速さ（ワールド単位/秒）。KeyboardInputSource と同じ値にする")]
        [SerializeField] private float _speed = 3f;
        [Tooltip("観測で見分ける弾の種類（White/Yellow/Blue/Big）。ここに無い種類は全部 0 になる")]
        [SerializeField] private BulletType[] _bulletTypes;

        [Header("YAML の environment_parameters が無いときの値")]
        [Tooltip("0 = Easy, 1 = Medium, 2 = Hard")]
        [SerializeField] private int _difficulty;
        [Tooltip("技の番号（DodgeTrainingBuilder.Attacks の順）。-1 なら毎回ランダム")]
        [SerializeField] private int _attack;

        private BufferSensorComponent _bulletSensor;
        private float[] _bulletObservation;
        private readonly List<Bullet> _sorted = new();
        private System.Comparison<Bullet> _byDistance;
        private Vector2 _soulPosition;

        private Vector2 _direction;
        private int _hits;
        private float _hitPenalty, _aliveReward, _nearPenalty;
        private bool _endOnHit;

        public override void Initialize()
        {
            _bulletSensor = GetComponent<BufferSensorComponent>();
            _bulletObservation = new float[BulletObservationSize(_bulletTypes.Length)];
            _byDistance = (a, b) => (a.Position - _soulPosition).sqrMagnitude.CompareTo((b.Position - _soulPosition).sqrMagnitude);
            _arena.Bullets.Hit += OnHit;
        }

        private void OnDestroy()
        {
            if (_arena != null) _arena.Bullets.Hit -= OnHit;
        }

        public override void OnEpisodeBegin()
        {
            var p = Academy.Instance.EnvironmentParameters;
            _hitPenalty = p.GetWithDefault("hit_penalty", 1f);
            _aliveReward = p.GetWithDefault("alive_reward", 0f);
            _nearPenalty = p.GetWithDefault("near_penalty", 0f);
            _endOnHit = p.GetWithDefault("end_on_hit", 0f) > 0.5f;
            bool randomStart = p.GetWithDefault("random_start", 0f) > 0.5f;

            var start = randomStart ? new Vector2(Random.Range(0.1f, 0.9f), Random.Range(0.1f, 0.9f)) : new Vector2(0.5f, 0.5f);
            _arena.Restart(Mathf.RoundToInt(p.GetWithDefault("difficulty", _difficulty)),
                Mathf.RoundToInt(p.GetWithDefault("attack", _attack)), start);
            _direction = Vector2.zero;
            _hits = 0;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            var soul = _arena.Soul;
            sensor.AddObservation(soul.Normalized);
            sensor.AddObservation(soul.IsInvincible);
            sensor.AddObservation(_arena.Progress);

            // 位置・速度・大きさは盤面の大きさで割って -1..1 程度にそろえる。
            var size = _arena.Bullets.Board.Size;
            _soulPosition = soul.Position;

            // 安置のシート（出ていなければ全部 0）。
            var zone = _arena.SafeZone;
            sensor.AddObservation(zone.IsShown);
            sensor.AddObservation(zone.IsShown ? ((Vector2)zone.transform.position - _soulPosition) / size : Vector2.zero);
            sensor.AddObservation(zone.IsShown ? zone.Size / size : Vector2.zero);

            // 弾は近い順に入れる（上限を超えたら遠い弾を落とす）。
            _sorted.Clear();
            _sorted.AddRange(_arena.Bullets.Active);
            _sorted.Sort(_byDistance);
            int count = Mathf.Min(_sorted.Count, _bulletSensor.MaxNumObservables);
            for (int i = 0; i < count; i++)
            {
                var b = _sorted[i];
                var o = _bulletObservation;
                o[0] = (b.Position.x - _soulPosition.x) / size.x;
                o[1] = (b.Position.y - _soulPosition.y) / size.y;
                o[2] = b.Velocity.x / size.x;
                o[3] = b.Velocity.y / size.y;
                o[4] = b.Type.Radius / size.x;
                for (int t = 0; t < _bulletTypes.Length; t++) o[5 + t] = b.Type == _bulletTypes[t] ? 1f : 0f;
                _bulletSensor.AppendObservation(o);
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            _direction = Directions[actions.DiscreteActions[0]];

            AddReward(_aliveReward);
            if (_nearPenalty > 0f && IsNearBullet()) AddReward(-_nearPenalty);

            if (_arena.Finished)
            {
                RecordStats();
                // 時間切れは「失敗」ではないので EndEpisode ではなく EpisodeInterrupted（その先も続いていた扱い）。
                EpisodeInterrupted();
            }
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            // 学習の前に、手で動かして観測・報酬を確かめる用。矢印キー / WASD。
            var keyboard = Keyboard.current;
            var dir = Vector2Int.zero;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) dir.x -= 1;
                if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) dir.x += 1;
                if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed) dir.y -= 1;
                if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed) dir.y += 1;
            }
            var discrete = actionsOut.DiscreteActions;
            discrete[0] = 0;
            for (int i = 1; i < Directions.Length; i++)
                if (Directions[i] == (Vector2)dir) discrete[0] = i;
        }

        public bool TryReadTarget(Vector2 currentNormalized, float deltaTime, out Vector2 target)
        {
            target = currentNormalized + _direction * _speed * deltaTime / _arena.Bullets.Board.Size;
            return true;
        }

        private void OnHit(Bullet bullet)
        {
            _hits++;
            AddReward(-_hitPenalty);
            if (!_endOnHit) return;
            RecordStats();
            EndEpisode();
        }

        /// <summary>グレイズ範囲（SOUL のまわり）に弾が 1 発でもあるか。</summary>
        private bool IsNearBullet()
        {
            var soul = _arena.Soul;
            foreach (var b in _arena.Bullets.Active)
            {
                float r = b.Type.Radius + soul.GrazeRadius;
                if ((b.Position - soul.Position).sqrMagnitude <= r * r) return true;
            }
            return false;
        }

        /// <summary>TensorBoard に出す。Dodge/Hits = 1 回の技での被弾回数、Dodge/NoHit = 被弾 0 回で終えた割合。</summary>
        private void RecordStats()
        {
            var stats = Academy.Instance.StatsRecorder;
            stats.Add("Dodge/Hits", _hits);
            stats.Add("Dodge/NoHit", _hits == 0 ? 1f : 0f);
            stats.Add($"Dodge/Hits by attack/{_arena.CurrentName}", _hits);
        }
    }
}
