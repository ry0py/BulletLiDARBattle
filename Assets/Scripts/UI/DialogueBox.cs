using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UndertaleLiDAR.Audio;
using UnityEngine;

namespace UndertaleLiDAR.UI
{
    /// <summary>
    /// Undertale 風のタイプライター表示。1 文字ずつ送り、ブリップ音を鳴らす。
    /// 行キューを持ち、Advance() で「全文表示 → 次の行 → 終了通知」と進む。
    /// 表示専用であり、バトル進行ロジックは持たない (SRP)。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class DialogueBox : MonoBehaviour
    {
        [SerializeField] private TMP_Text _label;
        [Tooltip("ブリップ音の再生に使う (任意)")]
        [SerializeField] private MusicManager _music;
        [Tooltip("1 文字送るたびのブリップ音 (任意)")]
        [SerializeField] private AudioClip _blip;
        [Tooltip("1 秒あたりの表示文字数")]
        [SerializeField] private float _charsPerSecond = 30f;
        [Tooltip("この文字数ごとにブリップを鳴らす")]
        [SerializeField] private int _blipEvery = 2;
        [Tooltip("Undertale 風に各行頭へ付ける接頭辞")]
        [SerializeField] private string _linePrefix = "* ";

        private readonly Queue<string> _queue = new Queue<string>();
        private Coroutine _typing;
        private string _full = string.Empty;

        /// <summary>キューを出し切ったとき (最後の行を Advance で送った直後)。</summary>
        public event Action Finished;

        public bool IsTyping => _typing != null;

        /// <summary>1 行だけ表示する。</summary>
        public void Show(string line)
        {
            _queue.Clear();
            _queue.Enqueue(line ?? string.Empty);
            gameObject.SetActive(true);
            Next();
        }

        /// <summary>複数行をキューして先頭から表示する。</summary>
        public void Show(IEnumerable<string> lines)
        {
            _queue.Clear();
            if (lines != null)
            {
                foreach (string l in lines) _queue.Enqueue(l ?? string.Empty);
            }
            gameObject.SetActive(true);
            Next();
        }

        public void Clear()
        {
            StopTyping();
            _queue.Clear();
            if (_label != null) _label.text = string.Empty;
        }

        public void Hide()
        {
            Clear();
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

        /// <summary>
        /// 確定入力 (Z/Enter/Space) で呼ぶ。タイプ中なら即全文表示、
        /// 完了済みなら次の行へ、キューが空なら Finished を発火する。
        /// </summary>
        public void Advance()
        {
            if (IsTyping)
            {
                StopTyping();
                if (_label != null) _label.text = _full;
                return;
            }
            Next();
        }

        private void Next()
        {
            if (_queue.Count == 0)
            {
                Finished?.Invoke();
                return;
            }
            _full = _linePrefix + _queue.Dequeue();
            _typing = StartCoroutine(Type());
        }

        private IEnumerator Type()
        {
            if (_label == null)
            {
                _typing = null;
                yield break;
            }
            _label.text = string.Empty;
            float perChar = 1f / Mathf.Max(1f, _charsPerSecond);
            int every = Mathf.Max(1, _blipEvery);
            int shown = 0;
            while (shown < _full.Length)
            {
                shown++;
                _label.text = _full.Substring(0, shown);
                char c = _full[shown - 1];
                if (_music != null && _blip != null && c != ' ' && shown % every == 0)
                {
                    _music.PlaySfx(_blip);
                }
                yield return new WaitForSeconds(perChar);
            }
            _typing = null;
        }

        private void StopTyping()
        {
            if (_typing != null)
            {
                StopCoroutine(_typing);
                _typing = null;
            }
        }
    }
}
