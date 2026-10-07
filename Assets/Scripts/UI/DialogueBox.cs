using System.Collections;
using System.Collections.Generic;
using LidarBattle.Audio;
using TMPro;
using UnityEngine;

namespace LidarBattle.UI
{
    /// <summary>
    /// タイプライター表示。1 行を 1 文字ずつ送る。
    /// 表示専用であり、バトル進行ロジックは持たない (SRP)。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class DialogueBox : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [Tooltip("1 秒あたりの表示文字数")]
        [SerializeField] private float _charsPerSecond = 30f;
        [Tooltip("各行頭へ付ける接頭辞")]
        [SerializeField] private string _linePrefix = "* ";
        [Tooltip("セリフ音を鳴らす最短間隔（秒）。毎文字だと細かすぎるため")]
        [SerializeField] private float _voiceInterval = 0.06f;

        private Coroutine _typing;
        private float _lastVoiceTime = float.NegativeInfinity;

        public bool IsTyping => _typing != null;

        /// <summary>セリフ音のピッチ。難易度ごとに変えるのは呼び出し側（BattleFlow）が決める。</summary>
        public float VoicePitch { get; set; } = 1f;

        /// <summary>1 行を表示する。表示中の行があれば打ち切って差し替える。</summary>
        public void Show(string line)
        {
            StopTyping();
            gameObject.SetActive(true);
            _typing = StartCoroutine(Type(_linePrefix + (line ?? string.Empty)));
        }

        public void Hide()
        {
            StopTyping();
            _label.text = string.Empty;
            gameObject.SetActive(false);
        }

        /// <summary>ボタン操作なしで、各行を打ち終えてから holdSeconds 待って次へ進む。</summary>
        public IEnumerator PlayAuto(IReadOnlyList<string> lines, float holdSeconds)
        {
            foreach (string line in lines)
            {
                Show(line);
                while (IsTyping) yield return null;
                yield return new WaitForSeconds(holdSeconds);
            }
        }

        private IEnumerator Type(string full)
        {
            _label.text = string.Empty;
            float perChar = 1f / Mathf.Max(1f, _charsPerSecond);
            for (int shown = 1; shown <= full.Length; shown++)
            {
                _label.text = full.Substring(0, shown);
                PlayVoice(full[shown - 1]);
                yield return new WaitForSeconds(perChar);
            }
            _typing = null;
        }

        /// <summary>空白・記号では鳴らさない（アンダーテールのように文字のところだけ「ポポポ」と鳴る）。</summary>
        private void PlayVoice(char c)
        {
            if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c)) return;
            if (Time.time - _lastVoiceTime < _voiceInterval) return;
            _lastVoiceTime = Time.time;
            GameAudio.PlayVoice(VoicePitch);
        }

        private void StopTyping()
        {
            if (_typing == null) return;
            StopCoroutine(_typing);
            _typing = null;
        }
    }
}
