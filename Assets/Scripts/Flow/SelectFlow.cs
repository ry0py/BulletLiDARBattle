using System.Collections;
using LidarBattle.Audio;
using LidarBattle.Battle;
using LidarBattle.Input;
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
        [Tooltip("決定音を聞かせてからバトルへ移るまでの秒数")]
        [SerializeField] private float _decideWaitSeconds = 0.6f;

        private LiveFeed _live;

        // 別の PC の「LiDAR の視界」（live.html）に、選択中も点群を出す。
        private void Update() => _live?.Tick();

        private IEnumerator Start()
        {
            var soul = FindAnyObjectByType<SoulController>();
            var board = FindAnyObjectByType<BulletBoard>();
            if (soul != null && board != null)
                _live = new LiveFeed(FindAnyObjectByType<LidarInputSource>(), soul, null, board.Size.x / board.Size.y);

            _optionsRoot.SetActive(false);
            GameAudio.PlayBgm(Bgm.Select);
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
            GameAudio.StopBgm();
            GameAudio.PlaySe(Se.Decide);
            yield return new WaitForSeconds(_decideWaitSeconds);
            SceneManager.LoadScene(_battleSceneName);
        }
    }
}
