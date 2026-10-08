using System.Collections;
using System.Linq;
using LidarBattle.Audio;
using LidarBattle.Battle;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LidarBattle.Flow
{
    /// <summary>
    /// 弾幕テストシーンの進行: ドロップダウンで選んだ技の Timeline を再生し、終わったら同じ技を繰り返す。
    /// 技ごとの被弾回数を出す。記録（PlayLog）は残さない。シーンは Build Battle Test Scene が BattleScene から作る。
    /// </summary>
    public class BattleTestFlow : MonoBehaviour
    {
        [SerializeField] private PlayableDirector _director;
        [SerializeField] private TimelineAsset[] _attacks;
        [SerializeField] private BattleClock _clock;
        [SerializeField] private BulletSystem _bullets;
        [SerializeField] private TMP_Dropdown _dropdown;
        [Tooltip("技の名前・被弾回数を出すテキスト")]
        [SerializeField] private TMP_Text _label;
        [Tooltip("技の残り秒数を出すテキスト（任意）")]
        [SerializeField] private TMP_Text _timeLabel;
        [Tooltip("繰り返すときの間の秒数（弾を消してから撃ち直す）")]
        [SerializeField] private float _gapSeconds = 1f;

        private TimelineAsset _current;
        private int _hits;
        private Coroutine _running;

        private void OnEnable() => _bullets.Hit += OnHit;
        private void OnDisable() => _bullets.Hit -= OnHit;

        private void OnHit(Bullet bullet)
        {
            GameAudio.PlaySe(Se.Hit);
            _hits++;
            ShowLabel();
        }

        private void Start()
        {
            if (_attacks.Length == 0) return;
            GameAudio.PlayBgm(Bgm.Battle, restart: true);
            _dropdown.ClearOptions();
            _dropdown.AddOptions(_attacks.Select(a => a.name).ToList());
            _dropdown.onValueChanged.AddListener(Select);
            Select(0);
        }

        private void Update()
        {
            // 閉じた後もドロップダウンが選択中のままだと、キー操作（Space/Enter など）が UI に取られる。
            var events = EventSystem.current;
            if (events != null && !_dropdown.IsExpanded && events.currentSelectedGameObject == _dropdown.gameObject)
                events.SetSelectedGameObject(null);
        }

        private void Select(int index)
        {
            if (_running != null) StopCoroutine(_running);
            _director.Stop();
            _bullets.ClearAll();
            _current = _attacks[index];
            _running = StartCoroutine(Repeat());
        }

        private IEnumerator Repeat()
        {
            while (true)
            {
                _hits = 0;
                ShowLabel();
                Play(_current);

                // 一時停止中は進めないよう BattleClock の時間で数える。
                float elapsed = 0f;
                int shownSeconds = -1;
                while (elapsed < _current.duration)
                {
                    // 文字列は秒が変わったときだけ作る（毎フレームの GC を避ける）。
                    int seconds = Mathf.CeilToInt((float)_current.duration - elapsed);
                    if (_timeLabel != null && seconds != shownSeconds) _timeLabel.text = $"残り {shownSeconds = seconds} 秒";
                    elapsed += _clock.DeltaTime;
                    yield return null;
                }

                Debug.Log($"[BattleTest] {_current.name}: 被弾 {_hits} 回");
                _director.Stop();
                _bullets.ClearAll();
                yield return new WaitForSeconds(_gapSeconds);
            }
        }

        private void ShowLabel() => _label.text = $"弾幕テスト {_current.name}  被弾 {_hits}";

        private void Play(TimelineAsset timeline)
        {
            _director.playableAsset = timeline;
            foreach (var track in timeline.GetOutputTracks())
            {
                if (track is ShotTrack) _director.SetGenericBinding(track, _bullets);
                else if (track is SafeZoneTrack) _director.SetGenericBinding(track, _bullets.Board.GetComponentInChildren<SafeZoneView>(true));
            }
            _director.Play();
        }
    }
}
