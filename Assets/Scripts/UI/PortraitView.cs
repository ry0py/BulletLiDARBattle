using UnityEngine;
using UnityEngine.UI;

namespace LidarBattle.UI
{
    /// <summary>
    /// 立ち絵を 1 枚表示する。どの表情にするか・どう動かすかは呼び出し側（PortraitTrack / PortraitMotionTrack / BattleFlow）が決める。
    /// 画像が無いときは配置確認用の半透明の枠を出す。
    /// </summary>
    public sealed class PortraitView : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [Tooltip("使う立ち絵。バトルシーンでは BattleFlow が難易度に合わせて差し替える")]
        [SerializeField] private PortraitSet _set;
        [Tooltip("画像が無いときの枠の色。透明にすれば何も出ない")]
        [SerializeField] private Color _placeholderColor = new(1f, 1f, 1f, 0.15f);

        private RectTransform _rect;
        private Vector2 _home;
        private Vector2 _offset;

        public PortraitExpression Expression { get; private set; }

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _home = _rect.anchoredPosition;
            Show(PortraitExpression.Base);
        }

        /// <summary>
        /// このフレームだけ元の位置からずらす。複数のトラックから呼ばれたら足し合わせる。
        /// 呼ばれなかったフレームは元の位置に戻る（Timeline は LateUpdate より前に評価される）。
        /// </summary>
        public void AddOffset(Vector2 offset) => _offset += offset;

        private void LateUpdate()
        {
            _rect.anchoredPosition = _home + _offset;
            _offset = Vector2.zero;
        }

        /// <summary>立ち絵のセットを差し替え、今の表情で出し直す。</summary>
        public void UseSet(PortraitSet set)
        {
            _set = set;
            Show(Expression);
        }

        public void Show(PortraitExpression expression)
        {
            Expression = expression;
            Sprite sprite = _set != null ? _set.Get(expression) : null;
            _image.sprite = sprite;
            _image.color = sprite != null ? Color.white : _placeholderColor;
        }
    }
}
