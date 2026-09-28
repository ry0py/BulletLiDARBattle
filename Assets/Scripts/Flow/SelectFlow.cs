using System.Collections;
using LidarBattle.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LidarBattle.Flow
{
    /// <summary>選択シーンの進行: オープニング会話 → 難易度選択 → バトルシーンへ。</summary>
    public class SelectFlow : MonoBehaviour
    {
        [SerializeField] private DialogueBox _dialogue;
        [SerializeField, TextArea] private string[] _openingLines =
        {
            "今日は来てくれてありがとう。",
            "難易度を選んでね",
        };
        [SerializeField] private float _lineHoldSeconds = 1.5f;
        [SerializeField] private GameObject _optionsRoot;
        [SerializeField] private DifficultyOption[] _options;
        [SerializeField] private string _battleSceneName = "BattleScene";

        private IEnumerator Start()
        {
            _optionsRoot.SetActive(false);
            yield return _dialogue.PlayAuto(_openingLines, _lineHoldSeconds);

            // 最後のセリフ（「難易度を選んでね」）は表示したまま選ばせる。
            _optionsRoot.SetActive(true);
            DifficultyOption selected = null;
            while (selected == null)
            {
                foreach (var option in _options)
                {
                    if (option.IsSelected) selected = option;
                }
                yield return null;
            }

            GameSession.Difficulty = selected.Difficulty;
            SceneManager.LoadScene(_battleSceneName);
        }
    }
}
