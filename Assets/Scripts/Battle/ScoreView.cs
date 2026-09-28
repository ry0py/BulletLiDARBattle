using TMPro;
using UnityEngine;

namespace UndertaleLiDAR.Battle
{
    /// <summary>現在のスコアと被弾回数を画面に出す。値が変わったときだけ文字列を作る（毎フレームの GC を避ける）。</summary>
    public class ScoreView : MonoBehaviour
    {
        [SerializeField] private ScoreKeeper _score;
        [SerializeField] private TMP_Text _label;

        private int _shownScore = -1;
        private int _shownHits = -1;

        private void Update()
        {
            if (_score.Score == _shownScore && _score.Hits == _shownHits) return;
            _shownScore = _score.Score;
            _shownHits = _score.Hits;
            _label.text = $"スコア {_shownScore}\n被弾 {_shownHits} 回";
        }
    }
}
