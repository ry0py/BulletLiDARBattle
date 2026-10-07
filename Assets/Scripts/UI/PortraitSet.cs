using UnityEngine;

namespace LidarBattle.UI
{
    /// <summary>立ち絵の表情。</summary>
    public enum PortraitExpression { Base, Talk, Attack, Smile, Defeat }

    /// <summary>
    /// 1 難易度ぶんの立ち絵（表情ごとの画像）。難易度ごとに 1 つ作る（Assets/Settings/Portraits/）。
    /// 未設定の表情はベースで代用するので、素材が一部しか無くても動く。
    /// </summary>
    [CreateAssetMenu(menuName = "LiDAR Battle/Portrait Set", fileName = "PortraitSet")]
    public sealed class PortraitSet : ScriptableObject
    {
        [Tooltip("ベース")]
        [SerializeField] private Sprite _base;
        [Tooltip("口をあけている")]
        [SerializeField] private Sprite _talk;
        [Tooltip("技を出している")]
        [SerializeField] private Sprite _attack;
        [Tooltip("笑顔")]
        [SerializeField] private Sprite _smile;
        [Tooltip("負け顔")]
        [SerializeField] private Sprite _defeat;

        public Sprite Get(PortraitExpression expression)
        {
            Sprite sprite = expression switch
            {
                PortraitExpression.Talk => _talk,
                PortraitExpression.Attack => _attack,
                PortraitExpression.Smile => _smile,
                PortraitExpression.Defeat => _defeat,
                _ => _base,
            };
            return sprite != null ? sprite : _base;
        }
    }
}
