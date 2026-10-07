using TMPro;
using UnityEngine;

namespace LidarBattle.Battle
{
    /// <summary>プレイ中の被弾回数を画面に出す。値が変わったときだけ文字列を作る（毎フレームの GC を避ける）。</summary>
    public class ScoreView : MonoBehaviour
    {
        [SerializeField] private ScoreKeeper _score;
        [SerializeField] private TMP_Text _label;
        [Tooltip("{0} = 被弾回数。リッチテキスト可")]
        [SerializeField, TextArea] private string _format = "被弾 {0} 回";

        private int _shownHits = -1;

        private void Update()
        {
            if (_score.Hits == _shownHits) return;
            _shownHits = _score.Hits;
            _label.text = string.Format(_format, _shownHits);
        }
    }
}
