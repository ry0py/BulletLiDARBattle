using TMPro;
using UnityEngine;

namespace LidarBattle.Battle
{
    /// <summary>プレイ中のスコア（グレイズ点。被弾ボーナスは終了時に足す）・被弾・グレイズ回数を画面に出す。値が変わったときだけ文字列を作る（毎フレームの GC を避ける）。</summary>
    public class ScoreView : MonoBehaviour
    {
        [SerializeField] private ScoreKeeper _score;
        [SerializeField] private TMP_Text _label;
        [Tooltip("{0} = スコア, {1} = 被弾, {2} = グレイズ。リッチテキスト可")]
        [SerializeField, TextArea] private string _format = "スコア {0}\n被弾 {1} 回";

        private int _shownScore = -1;
        private int _shownHits = -1;
        private int _shownGrazes = -1;

        private void Update()
        {
            if (_score.GrazeScore == _shownScore && _score.Hits == _shownHits && _score.Grazes == _shownGrazes) return;
            _shownScore = _score.GrazeScore;
            _shownHits = _score.Hits;
            _shownGrazes = _score.Grazes;
            _label.text = string.Format(_format, _shownScore, _shownHits, _shownGrazes);
        }
    }
}
