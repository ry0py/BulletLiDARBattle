using System.Collections;
using LidarBattle.Audio;
using LidarBattle.Battle;
using LidarBattle.Input;
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
        [Tooltip("Difficulty の順（Easy, Medium, Hard）。下の候補が空の難易度だけ使う")]
        [SerializeField] private TimelineAsset[] _timelines = new TimelineAsset[3];
        [Tooltip("難易度ごとの弾幕の候補。毎回ランダムに 1 本選ぶ（列で前のプレイを見て覚えられないように）")]
        [SerializeField] private TimelineAsset[] _easyTimelines, _mediumTimelines, _hardTimelines;
        [SerializeField] private BattleClock _clock;
        [SerializeField] private BulletSystem _bullets;
        [SerializeField] private ScoreKeeper _score;
        [SerializeField] private DialogueBox _dialogue;
        [Tooltip("立ち絵（任意）。表情は Timeline の PortraitTrack、動きは PortraitMotionTrack で決める")]
        [SerializeField] private PortraitView _portrait;
        [Tooltip("Difficulty の順（Easy, Medium, Hard）")]
        [SerializeField] private PortraitSet[] _portraitSets = new PortraitSet[3];
        [Tooltip("エンディング会話の間の表情")]
        [SerializeField] private PortraitExpression _endingExpression = PortraitExpression.Defeat;
        [SerializeField] private float _battleSeconds = 60f;
        [Tooltip("残り時間を Fill で表す Image（任意）")]
        [SerializeField] private Image _timeBar;
        [Tooltip("残り秒数を出すテキスト（任意）")]
        [SerializeField] private TMP_Text _timeLabel;
        [Tooltip("{0} に残り秒数（切り上げ）が入る。リッチテキスト可")]
        [SerializeField] private string _timeFormat = "残り {0} 秒";
        [Tooltip("今の難易度を出すテキスト（任意）")]
        [SerializeField] private TMP_Text _difficultyLabel;
        [Tooltip("{0} に難易度名（Easy, Medium, Hard）が入る。リッチテキスト可")]
        [SerializeField] private string _difficultyFormat = "難易度 {0}";
        [Tooltip("{0} = 被弾回数")]
        [SerializeField, TextArea] private string[] _endingLines =
        {
            "ゲームをプレイしてくれてありがとう！",
            "被弾は {0} 回でした",
        };
        [SerializeField] private float _lineHoldSeconds = 2f;
        [SerializeField] private string _selectSceneName = "SelectScene";
        [Tooltip("敵のセリフ音のピッチ。Difficulty の順（Easy, Medium, Hard）。Easy を基準に下げていく")]
        [SerializeField] private float[] _voicePitches = { 1f, 0.85f, 0.7f };

        // 難易度ごとに前回選んだ候補の番号。続けて同じ弾幕にならないようにする（シーンをまたいで覚える）。
        private static readonly int[] LastPicked = { -1, -1, -1 };

        private int _shownSeconds = -1;
        private PlayRecorder _recorder;

        private void OnEnable() => _bullets.Hit += OnHit;
        private void OnDisable() => _bullets.Hit -= OnHit;
        // やり直し（R）などでバトルの途中に抜けても、スコアボードを被弾ランキングに戻す。
        private void OnDestroy() => LiveFeed.WriteIdle();

        private void OnHit(Bullet bullet)
        {
            GameAudio.PlaySe(Se.Hit);
            _recorder?.AddHit(bullet, _bullets.Soul.Position);
        }

        private IEnumerator Start()
        {
            _dialogue.Hide();
            _dialogue.VoicePitch = _voicePitches[(int)GameSession.Difficulty];
            if (_portrait != null) _portrait.UseSet(_portraitSets[(int)GameSession.Difficulty]);
            if (_difficultyLabel != null) _difficultyLabel.text = string.Format(_difficultyFormat, GameSession.Difficulty);
            GameAudio.PlayBgm(Bgm.Battle, restart: true);
            GameAudio.PlaySe(Se.BattleStart);
            var timeline = PickTimeline(GameSession.Difficulty);
            PlayTimeline(timeline);

            // 一時停止中は進めないよう BattleClock の時間で数える。
            var board = _bullets.Board;
            _recorder = new PlayRecorder(GameSession.Difficulty, timeline.name, GameSession.DebugMode, new Rect(board.Min, board.Size));
            var live = new LiveFeed(FindAnyObjectByType<LidarInputSource>(), _bullets.Soul, GameSession.Difficulty, board.Size.x / board.Size.y);
            float elapsed = 0f;
            while (elapsed < _battleSeconds)
            {
                _recorder.Tick(elapsed, _bullets.Soul.Position);
                live.Tick(_battleSeconds - elapsed);
                ShowRemaining(_battleSeconds - elapsed);
                elapsed += _clock.DeltaTime;
                yield return null;
            }

            ShowRemaining(0f);
            _director.Stop();
            _bullets.ClearAll();
            PlayLog.Save(_recorder);
            LiveFeed.WriteIdle();
            if (_portrait != null) _portrait.Show(_endingExpression);

            var lines = new string[_endingLines.Length];
            for (int i = 0; i < lines.Length; i++)
                lines[i] = string.Format(_endingLines[i], _score.Hits);
            yield return _dialogue.PlayAuto(lines, _lineHoldSeconds);

            SceneManager.LoadScene(_selectSceneName);
        }

        /// <summary>難易度の候補からランダムに 1 本選ぶ（候補が 2 本以上なら前回と違うもの）。候補が無ければ _timelines。</summary>
        private TimelineAsset PickTimeline(Difficulty difficulty)
        {
            var candidates = difficulty switch
            {
                Difficulty.Easy => _easyTimelines,
                Difficulty.Medium => _mediumTimelines,
                _ => _hardTimelines,
            };
            if (candidates == null || candidates.Length == 0) return _timelines[(int)difficulty];

            int last = LastPicked[(int)difficulty];
            int index;
            do index = Random.Range(0, candidates.Length);
            while (candidates.Length > 1 && index == last);
            LastPicked[(int)difficulty] = index;
            return candidates[index];
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
                else if (track is SafeZoneTrack) _director.SetGenericBinding(track, _bullets.Board.GetComponentInChildren<SafeZoneView>(true));
                else if (track is DialogueTrack) _director.SetGenericBinding(track, _dialogue);
                else if (track is PortraitTrack or PortraitMotionTrack) _director.SetGenericBinding(track, _portrait);
            }
            _director.Play();
        }
    }
}
