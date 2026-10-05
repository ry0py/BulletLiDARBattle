using System.Collections;
using LidarBattle.Battle;
using LidarBattle.UI;
using TMPro;
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
        [Tooltip("残り秒数を出すテキスト（任意）")]
        [SerializeField] private TMP_Text _timeLabel;
        [Tooltip("{0} に残り秒数（切り上げ）が入る。リッチテキスト可")]
        [SerializeField] private string _timeFormat = "残り {0} 秒";
        [Tooltip("{0} = 最終スコア, {1} = グレイズ点, {2} = 被弾ボーナス, {3} = 被弾回数")]
        [SerializeField, TextArea] private string[] _endingLines =
        {
            "ゲームをプレイしてくれてありがとう！",
            "グレイズ {1} 点 ＋ 被弾ボーナス {2} 点（被弾 {3} 回）",
            "スコアは {0} でした",
        };
        [SerializeField] private float _lineHoldSeconds = 2f;
        [SerializeField] private string _selectSceneName = "SelectScene";

        private int _shownSeconds = -1;

        private IEnumerator Start()
        {
            _dialogue.Hide();
            PlayTimeline(_timelines[(int)GameSession.Difficulty]);

            // 一時停止中は進めないよう BattleClock の時間で数える。
            float elapsed = 0f;
            while (elapsed < _battleSeconds)
            {
                ShowRemaining(_battleSeconds - elapsed);
                elapsed += _clock.DeltaTime;
                yield return null;
            }

            ShowRemaining(0f);
            _director.Stop();
            _bullets.ClearAll();

            var lines = new string[_endingLines.Length];
            for (int i = 0; i < lines.Length; i++)
                lines[i] = string.Format(_endingLines[i], _score.FinalScore, _score.GrazeScore, _score.HitBonus, _score.Hits);
            yield return _dialogue.PlayAuto(lines, _lineHoldSeconds);

            SceneManager.LoadScene(_selectSceneName);
        }

        /// <summary>残り時間をバーと秒数で出す。秒数の文字列は値が変わったときだけ作る（毎フレームの GC を避ける）。</summary>
        private void ShowRemaining(float remaining)
        {
            remaining = Mathf.Max(0f, remaining);
            if (_timeBar != null) _timeBar.fillAmount = remaining / _battleSeconds;

            int seconds = Mathf.CeilToInt(remaining);
            if (_timeLabel == null || seconds == _shownSeconds) return;
            _shownSeconds = seconds;
            _timeLabel.text = string.Format(_timeFormat, seconds);
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
