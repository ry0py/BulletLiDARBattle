using System.Collections;
using LidarBattle.Battle;
using LidarBattle.UI;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace LidarBattle.Flow
{
    /// <summary>バトルシーンの進行: 難易度の Timeline を再生 → 制限時間で終了 → エンディング会話 → 選択シーンへ。</summary>
    public class BattleFlow : MonoBehaviour
    {
        [SerializeField] private PlayableDirector _director;
        [Tooltip("Difficulty の順（Easy, Medium, Hard）")]
        [SerializeField] private TimelineAsset[] _timelines = new TimelineAsset[3];
        [SerializeField] private BattleClock _clock;
        [SerializeField] private BulletSystem _bullets;
        [SerializeField] private ScoreKeeper _score;
        [SerializeField] private DialogueBox _dialogue;
        [SerializeField] private float _battleSeconds = 60f;
        [Tooltip("残り時間を Fill で表す Image（任意）")]
        [SerializeField] private Image _timeBar;
        [Tooltip("{0} にスコアが入る")]
        [SerializeField, TextArea] private string[] _endingLines =
        {
            "ゲームをプレイしてくれてありがとう！",
            "スコアは {0} でした",
        };
        [SerializeField] private float _lineHoldSeconds = 2f;
        [SerializeField] private string _selectSceneName = "SelectScene";

        private IEnumerator Start()
        {
            _dialogue.Hide();
            PlayTimeline(_timelines[(int)GameSession.Difficulty]);

            // 一時停止中は進めないよう BattleClock の時間で数える。
            float elapsed = 0f;
            while (elapsed < _battleSeconds)
            {
                elapsed += _clock.DeltaTime;
                if (_timeBar != null) _timeBar.fillAmount = 1f - elapsed / _battleSeconds;
                yield return null;
            }

            if (_timeBar != null) _timeBar.fillAmount = 0f;
            _director.Stop();
            _bullets.ClearAll();

            var lines = new string[_endingLines.Length];
            for (int i = 0; i < lines.Length; i++) lines[i] = string.Format(_endingLines[i], _score.Score);
            yield return _dialogue.PlayAuto(lines, _lineHoldSeconds);

            SceneManager.LoadScene(_selectSceneName);
        }

        /// <summary>トラックの種類からバインド先を決めるので、Timeline 側でバインドを設定しなくてよい。</summary>
        private void PlayTimeline(TimelineAsset timeline)
        {
            _director.playableAsset = timeline;
            foreach (var track in timeline.GetOutputTracks())
            {
                if (track is ShotTrack) _director.SetGenericBinding(track, _bullets);
                else if (track is DialogueTrack) _director.SetGenericBinding(track, _dialogue);
            }
            _director.Play();
        }
    }
}
