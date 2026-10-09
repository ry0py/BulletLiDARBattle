using System.Collections.Generic;
using LidarBattle.LiDAR;
using UnityEngine;

namespace LidarBattle.Tracking
{
    /// <summary>
    /// 既知半径の円柱を探す。各点を「円柱の手前の表面」とみなして円を仮に置き、スキャンと突き合わせて点数を付け、
    /// 一番点数の高い円を採る (ノイズは半径に比べて大きく、表面の曲がり具合だけでは平らな壁と見分けられないため)。
    /// 点数 = 円の幅に入るビームのうち表面に当たっている割合 − 円の左右の脇がふさがっている割合 (悪い側)。
    /// 独立した円柱の脇は奥まで抜けるが、壁は脇にも同じくらいの距離で続くので点数が下がる。
    /// 最後に表面に当たった点だけから中心を求め直す: 向きは点の角度の平均、距離は各点が円周上にあるとしたときの
    /// 中心までの距離の平均 (円の当てはめより、止まっているときのぶれが小さかった。実機で左右 σ 4.4 → 0.9 mm)。
    /// </summary>
    public sealed class CircleFitTracker : IHeartTracker
    {
        /// <summary>脇として見る範囲。円の見かけの半幅 (角度) の何倍までか。</summary>
        private const float FlankWidth = 1.5f;
        /// <summary>脇の点が「円の中心までの距離 − 半径 × これ」より手前なら、脇がふさがっているとみなす。</summary>
        private const float FlankBlockRatio = 0.2f;
        /// <summary>円を仮置きするときに距離の中央値をとる点の数 (1 点のノイズで仮置きがずれないように)。</summary>
        private const int MedianWindow = 5;
        /// <summary>円を仮置きする点の間隔 (重さ対策)。</summary>
        private const int CandidateStride = 2;

        private readonly float _radiusM;
        private readonly float _toleranceM;
        private readonly int _minPoints;
        private readonly float _minScore;
        private readonly int _refineRounds;
        private readonly float _scanStartRad;
        private readonly float _scanEndRad;
        private readonly List<int> _support = new List<int>(256); // 表面に当たった点の番号
        private readonly float[] _window = new float[MedianWindow];
        // スキャンを配列に写したもの (角度の昇順)。点数の計算で何度も読むので速さのため。
        private float[] _angles = new float[0];
        private float[] _dists = new float[0];
        private int _count;

        /// <param name="toleranceM">円周からこの距離以内の点を「表面に当たった」とみなす [m]</param>
        /// <param name="minPoints">表面に当たった点がこれより少ない円は採らない</param>
        /// <param name="minScore">点数 (-1〜1) がこれより低ければ未検出</param>
        /// <param name="refineRounds">中心を求め直す回数</param>
        /// <param name="scanStartRad">センサーが測る角度の範囲の端 [rad]。その外にはみ出す脇は確かめられない</param>
        public CircleFitTracker(float radiusM, float toleranceM, int minPoints, float minScore, int refineRounds,
            float scanStartRad, float scanEndRad)
        {
            _scanStartRad = Mathf.Min(scanStartRad, scanEndRad);
            _scanEndRad = Mathf.Max(scanStartRad, scanEndRad);
            _radiusM = Mathf.Max(0.001f, radiusM);
            _toleranceM = Mathf.Max(0.001f, toleranceM);
            _minPoints = Mathf.Max(1, minPoints);
            _minScore = minScore;
            _refineRounds = Mathf.Max(1, refineRounds);
        }

        public bool TryTrack(LidarScan scan, out Vector2 positionM)
        {
            positionM = Vector2.zero;
            if (scan == null || scan.Count == 0) return false;
            CopyScan(scan);

            float bestScore = float.NegativeInfinity;
            float bestAngle = 0f, bestDist = 0f;
            // 仮置きは 1 点おき (隣どうしはほぼ同じ円になる。中心は最後に求め直すので精度は落ちない)。
            for (int i = 0; i < _count; i += CandidateStride)
            {
                float angle = _angles[i];
                float dist = MedianDistance(i) + _radiusM; // センサーから円の中心まで
                float score = Score(angle, dist, out int support, false);
                if (support >= _minPoints && score > bestScore)
                {
                    bestScore = score;
                    bestAngle = angle;
                    bestDist = dist;
                }
            }
            if (bestScore < _minScore) return false;

            Refine(ref bestAngle, ref bestDist);
            positionM = new Vector2(Mathf.Cos(bestAngle), Mathf.Sin(bestAngle)) * bestDist;
            return true;
        }

        /// <summary>中心が (angle, dist) の円の点数。collect なら表面に当たった点を _support に集める。</summary>
        private float Score(float angle, float dist, out int support, bool collect)
        {
            if (collect) _support.Clear();
            support = 0;
            float halfWidth = Mathf.Asin(Mathf.Min(1f, _radiusM / dist)); // 円の見かけの半幅 [rad]
            float flankEnd = halfWidth * FlankWidth;
            float blockDist = dist - _radiusM * FlankBlockRatio;
            int inside = 0, flankLow = 0, flankHigh = 0, blockedLow = 0, blockedHigh = 0;

            // 角度の昇順なので、脇の端より手前から見始め、脇の端を過ぎたら止める。
            int start = System.Array.BinarySearch(_angles, 0, _count, angle - flankEnd);
            if (start < 0) start = ~start;
            for (int j = start; j < _count; j++)
            {
                float delta = _angles[j] - angle;
                if (delta >= flankEnd) break;
                float absDelta = Mathf.Abs(delta);
                float measured = _dists[j];

                if (absDelta < halfWidth)
                {
                    // このビームが円に当たるはずの距離 (手前側の交点)。
                    float perp = dist * Mathf.Sin(absDelta);
                    float expected = dist * Mathf.Cos(absDelta) - Mathf.Sqrt(Mathf.Max(0f, _radiusM * _radiusM - perp * perp));
                    inside++;
                    if (Mathf.Abs(measured - expected) <= _toleranceM)
                    {
                        support++;
                        if (collect) _support.Add(j);
                    }
                }
                else if (delta < 0f)
                {
                    flankLow++;
                    if (measured < blockDist) blockedLow++;
                }
                else
                {
                    flankHigh++;
                    if (measured < blockDist) blockedHigh++;
                }
            }
            if (inside == 0) return float.NegativeInfinity;

            // 脇に点が無いのは奥まで抜けている (有効距離の外) とき。ただしスキャン範囲の外にはみ出す脇は確かめられないのでふさがり扱い。
            float low = angle - flankEnd < _scanStartRad ? 1f : flankLow > 0 ? (float)blockedLow / flankLow : 0f;
            float high = angle + flankEnd > _scanEndRad ? 1f : flankHigh > 0 ? (float)blockedHigh / flankHigh : 0f;
            return (float)support / inside - Mathf.Max(low, high);
        }

        private void CopyScan(LidarScan scan)
        {
            _count = scan.Count;
            if (_angles.Length < _count)
            {
                _angles = new float[_count];
                _dists = new float[_count];
            }
            for (int i = 0; i < _count; i++)
            {
                _angles[i] = scan[i].AngleRad;
                _dists[i] = scan[i].DistanceM;
            }
        }

        private float MedianDistance(int index)
        {
            int start = Mathf.Max(0, index - MedianWindow / 2);
            int end = Mathf.Min(_count, start + MedianWindow);
            int n = end - start;
            for (int k = 0; k < n; k++) _window[k] = _dists[start + k];
            System.Array.Sort(_window, 0, n);
            return _window[n / 2];
        }

        /// <summary>表面に当たった点から中心の向きと距離を求め直す。</summary>
        private void Refine(ref float angle, ref float dist)
        {
            for (int round = 0; round < _refineRounds; round++)
            {
                Score(angle, dist, out _, true);
                if (_support.Count == 0) return;

                float angleSum = 0f;
                for (int k = 0; k < _support.Count; k++) angleSum += _angles[_support[k]];
                angle = angleSum / _support.Count;

                // 角度 delta・距離 r の点が中心距離 D の円周上にあるなら (r cos δ - D)² + (r sin δ)² = R²。手前側の解は D = r cos δ + √(R² - (r sin δ)²)。
                float distSum = 0f;
                int n = 0;
                for (int k = 0; k < _support.Count; k++)
                {
                    float delta = _angles[_support[k]] - angle;
                    float r = _dists[_support[k]];
                    float side = r * Mathf.Sin(delta);
                    float q = _radiusM * _radiusM - side * side;
                    if (q <= 0f) continue;
                    distSum += r * Mathf.Cos(delta) + Mathf.Sqrt(q);
                    n++;
                }
                if (n > 0) dist = distSum / n;
            }
        }
    }
}
