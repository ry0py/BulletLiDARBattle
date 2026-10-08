using TMPro;
using UnityEngine;

namespace LidarBattle.Battle
{
    /// <summary>
    /// 安置のシート（半透明の緑の長方形に「あんぜん」）。見た目だけで当たり判定は無い。
    /// Timeline の SafeZoneTrack がクリップの間だけ出す。安置の外を埋める弾は FirePattern の Fill が撃つ。
    /// </summary>
    public class SafeZoneView : MonoBehaviour
    {
        [SerializeField] private BulletBoard _board;
        [SerializeField] private SpriteRenderer _sheet;
        [SerializeField] private TMP_Text _label;

        /// <summary>center は盤面の正規化座標、size はワールド単位の大きさ。</summary>
        public void Show(Vector2 center, Vector2 size)
        {
            transform.position = _board.NormalizedToWorld(center);
            _sheet.transform.localScale = new Vector3(size.x, size.y, 1f);
            _label.rectTransform.sizeDelta = size * 0.9f;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
