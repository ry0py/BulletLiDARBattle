using System.Collections.Generic;
using UnityEngine;

namespace LidarBattle.Battle
{
    public enum ShotShape { Ring, Aimed, Spiral, Random, Arc, Line, RandomLine, GapWall, Converge, Fill }
    /// <summary>GapWall の隙間の位置。Random = 毎回ランダム、Alternate = 線の両端寄りに交互、Wave = 1 回ごとになめらかにずらす（うねる道）。</summary>
    public enum GapMode { Random, Alternate, Wave }
    public enum MoveType { Linear, Accelerate, Curve, SineWave, Homing, StopAndAim, Gravity }

    /// <summary>
    /// 飛ばし方（撃ち方＋飛び方）。種類は enum で選び、使うパラメータだけ設定する。
    /// 角度はすべて度数法で、0° = 右、反時計回り。
    /// </summary>
    [CreateAssetMenu(menuName = "LiDAR Battle/Fire Pattern")]
    public class FirePattern : ScriptableObject
    {
        [Header("撃ち方")]
        [SerializeField] private ShotShape _shape = ShotShape.Ring;
        [Tooltip("1 回に撃つ弾数。Aimed は奇数なら自機に当たる向き、偶数なら両脇を通る")]
        [SerializeField, Min(1)] private int _count = 8;
        [Tooltip("Ring/Spiral/Converge: 基準角。Random/Arc: 中心角。Line/RandomLine/GapWall/Fill: 進む向き")]
        [SerializeField] private float _angle;
        [Tooltip("Aimed/Arc: 弾どうしの間隔角。Random: ばらつく範囲の角度")]
        [SerializeField] private float _spread = 15f;
        [Tooltip("Spiral/Converge: 1 回撃つごとに回す角度。GapWall (Wave): 1 回ごとに進める隙間の揺れの位相")]
        [SerializeField] private float _spiralStep = 10f;
        [Tooltip("Line/RandomLine/GapWall: 弾を並べる線の長さ（ワールド単位）。線は進む向きと直交し、発射位置が中央。Fill: 格子の間隔")]
        [SerializeField] private float _width = 5f;
        [Tooltip("GapWall: 壁に空ける隙間の幅（ワールド単位）。隙間の位置は毎回ランダム")]
        [SerializeField] private float _gap = 1f;
        [Tooltip("GapWall: 隙間の位置の決め方")]
        [SerializeField] private GapMode _gapMode;
        [Tooltip("Converge: 自機を囲む円の半径（ワールド単位）。発射位置は使わず、自機の位置を中心に内向きに撃つ")]
        [SerializeField] private float _radius = 1.5f;
        [Tooltip("Fill: 弾を置かない長方形（安置）の大きさ（ワールド単位）。発射位置が中心")]
        [SerializeField] private Vector2 _holeSize = new(1.5f, 1f);

        [Header("飛び方")]
        [SerializeField] private MoveType _move = MoveType.Linear;
        [SerializeField] private float _speed = 3f;
        [Tooltip("Accelerate: 加速度（負で減速）")]
        [SerializeField] private float _acceleration;
        [SerializeField] private float _minSpeed;
        [Tooltip("Accelerate: 速さの上限。StopAndAim: 自機へ撃ち直す速さ")]
        [SerializeField] private float _maxSpeed = 10f;
        [Tooltip("Curve: 角速度 (度/秒)")]
        [SerializeField] private float _angularVelocity = 90f;
        [Tooltip("SineWave: 振れ幅")]
        [SerializeField] private float _amplitude = 0.5f;
        [Tooltip("SineWave: 周波数 (Hz)")]
        [SerializeField] private float _frequency = 1f;
        [Tooltip("Homing: 最大旋回量 (度/秒)")]
        [SerializeField] private float _turnRate = 90f;
        [Tooltip("StopAndAim: 発射から止まるまでの秒数")]
        [SerializeField, Min(0.01f)] private float _stopTime = 0.6f;
        [Tooltip("StopAndAim: 止まっている秒数")]
        [SerializeField, Min(0f)] private float _waitTime = 0.4f;
        [Tooltip("Gravity: 下向きの重力加速度")]
        [SerializeField] private float _gravity = 3f;
        [Tooltip("撃ってから消えるまでの秒数（0 = 盤面の外に出るまで消えない）。消える前に少し薄くなる")]
        [SerializeField, Min(0f)] private float _lifetime;

        [Header("切り替え")]
        [Tooltip("撃ってから NextTime 秒たったら、この飛び方に切り替える（任意）。位置と向きはそのまま、速さ・寿命は切り替え先のもの")]
        [SerializeField] private FirePattern _next;
        [SerializeField, Min(0f)] private float _nextTime = 1f;

        public float Speed => _speed;
        public float Lifetime => _lifetime;
        public FirePattern Next => _next;
        public float NextTime => _nextTime;

        /// <summary>
        /// 1 回分の弾（発射位置からのずれ・発射角）を results に入れる。target は自機の位置、board は盤面（ワールド座標。Fill 用）。
        /// shotIndex は何回目の発射か（Spiral など用）。
        /// </summary>
        public void GetShots(Vector2 origin, Vector2 target, Rect board, int shotIndex, System.Random random,
            List<(Vector2 offset, float angle)> results)
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
                    AddFan(AngleTo(origin, target), results);
                    break;
                case ShotShape.Arc:
                    AddFan(_angle, results);
                    break;
                case ShotShape.Random:
                    for (int i = 0; i < _count; i++)
                        results.Add((Vector2.zero, _angle + ((float)random.NextDouble() - 0.5f) * _spread));
                    break;
                case ShotShape.Line:
                case ShotShape.RandomLine:
                case ShotShape.GapWall:
                    var dir = Direction(_angle);
                    var across = new Vector2(-dir.y, dir.x);
                    // GapWall は Line から、線上のランダムな位置の _gap の幅にかかる弾を抜く。
                    float gapRange = Mathf.Max(0f, _width - _gap) * 0.5f;
                    float gapRandom = (float)random.NextDouble();
                    float gapCenter = _gapMode switch
                    {
                        // 1 回ごとに線の片側の端寄り（中央から 35〜85%）へ交互に空ける。
                        GapMode.Alternate => (shotIndex % 2 == 0 ? -1f : 1f) * gapRange * Mathf.Lerp(0.35f, 0.85f, gapRandom),
                        // 隙間を正弦波でずらす。細かい間隔で撃つと、隙間がつながってうねる一本道になる。
                        GapMode.Wave => gapRange * 0.85f * Mathf.Sin(_spiralStep * shotIndex * Mathf.Deg2Rad),
                        _ => (gapRandom * 2f - 1f) * gapRange,
                    };
                    for (int i = 0; i < _count; i++)
                    {
                        // Line/GapWall は等間隔（1 発なら中央）、RandomLine は線上のランダムな位置。
                        float t = _shape == ShotShape.RandomLine
                            ? (float)random.NextDouble()
                            : (_count > 1 ? (float)i / (_count - 1) : 0.5f);
                        float along = (t - 0.5f) * _width;
                        if (_shape == ShotShape.GapWall && Mathf.Abs(along - gapCenter) < _gap * 0.5f) continue;
                        results.Add((across * along, _angle));
                    }
                    break;
                case ShotShape.Converge:
                    // 自機の周りの円上に並べ、中心（自機）へ向けて撃つ。同じ場所に居続けると当たる。
                    float baseAngle = _angle + _spiralStep * shotIndex;
                    for (int i = 0; i < _count; i++)
                    {
                        float a = baseAngle + 360f / _count * i;
                        results.Add((target - origin + Direction(a) * _radius, a + 180f));
                    }
                    break;
                case ShotShape.Fill:
                    // 盤面を格子で埋める。発射位置を中心とする _holeSize の長方形（安置）には置かない。
                    float cell = Mathf.Max(0.05f, _width);
                    var half = _holeSize * 0.5f;
                    for (float x = board.xMin + cell * 0.5f; x < board.xMax; x += cell)
                    for (float y = board.yMin + cell * 0.5f; y < board.yMax; y += cell)
                    {
                        var offset = new Vector2(x, y) - origin;
                        if (Mathf.Abs(offset.x) < half.x && Mathf.Abs(offset.y) < half.y) continue;
                        results.Add((offset, _angle));
                    }
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
                case MoveType.StopAndAim:
                    // 止まるまで線形に減速し、待ち時間を過ぎたステップで 1 回だけ自機へ向け直す。
                    float launch = _stopTime + _waitTime;
                    if (bullet.Age < _stopTime) bullet.Speed = _speed * (1f - bullet.Age / _stopTime);
                    else if (bullet.Age < launch) bullet.Speed = 0f;
                    else if (bullet.Age - dt < launch)
                    {
                        bullet.Angle = AngleTo(bullet.Position, target);
                        bullet.Speed = _maxSpeed;
                    }
                    break;
                case MoveType.Gravity:
                    var velocity = Direction(bullet.Angle) * bullet.Speed + Vector2.down * (_gravity * dt);
                    bullet.Angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
                    bullet.Speed = velocity.magnitude;
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

        private void AddRing(float baseAngle, List<(Vector2, float)> results)
        {
            float step = 360f / _count;
            for (int i = 0; i < _count; i++) results.Add((Vector2.zero, baseAngle + step * i));
        }

        /// <summary>center を中心に _spread 間隔で _count 発の扇形。</summary>
        private void AddFan(float center, List<(Vector2, float)> results)
        {
            float start = center - _spread * (_count - 1) * 0.5f;
            for (int i = 0; i < _count; i++) results.Add((Vector2.zero, start + _spread * i));
        }

        private static float AngleTo(Vector2 from, Vector2 to) =>
            Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;

        private static Vector2 Direction(float angle) =>
            new(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
    }
}
