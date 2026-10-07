using UnityEngine;

namespace LidarBattle.Audio
{
    public enum Bgm { Select, Battle }
    public enum Se { Hit, SelectTick, Decide, BattleStart }

    /// <summary>
    /// BGM・SE・セリフ音を鳴らす。起動時に自動で作られ、シーンをまたいで残る（BGM が途切れない）。
    /// 音は Resources/Audio/ から読む（Tools/AudioGen/generate_audio.py で生成）。シーン側の設定は不要。
    /// シーンに AudioListener が無いので、ここに 1 つだけ持つ。
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        private const string Folder = "Audio/";
        private const float BgmVolume = 0.45f;
        private const float SeVolume = 0.8f;
        private const float VoiceVolume = 0.5f;

        private static GameAudio s_instance;

        private AudioSource _bgm;
        private AudioSource _se;     // 重ねて鳴らす SE（ピッチ 1 固定）
        private AudioSource _tick;   // ピッチを変える選択中の音
        private AudioSource _voice;  // ピッチを変えるセリフ音
        private AudioClip[] _bgmClips;
        private AudioClip[] _seClips;
        private AudioClip _voiceClip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            var go = new GameObject(nameof(GameAudio));
            DontDestroyOnLoad(go);
            go.AddComponent<AudioListener>();
            s_instance = go.AddComponent<GameAudio>();
        }

        private void Awake()
        {
            _bgm = NewSource(BgmVolume);
            _bgm.loop = true;
            _se = NewSource(SeVolume);
            _tick = NewSource(SeVolume);
            _voice = NewSource(VoiceVolume);

            _bgmClips = new[] { Load("bgm_select"), Load("bgm_battle") };
            _seClips = new[] { Load("se_hit"), Load("se_select_tick"), Load("se_decide"), Load("se_battle_start") };
            _voiceClip = Load("voice_enemy");
        }

        /// <summary>BGM を流す。同じ曲が流れていれば restart のときだけ頭から流し直す。</summary>
        public static void PlayBgm(Bgm bgm, bool restart = false)
        {
            var source = s_instance._bgm;
            var clip = s_instance._bgmClips[(int)bgm];
            if (!restart && source.isPlaying && source.clip == clip) return;
            source.clip = clip;
            source.Play();
        }

        public static void StopBgm() => s_instance._bgm.Stop();

        public static void PlaySe(Se se) => s_instance._se.PlayOneShot(s_instance._seClips[(int)se]);

        /// <summary>選択中の音。pitch を上げていくと「溜まっていく」感じになる。</summary>
        public static void PlaySelectTick(float pitch) =>
            Restart(s_instance._tick, s_instance._seClips[(int)Se.SelectTick], pitch);

        /// <summary>セリフ音。前の音を切って鳴らし直す（重ねると濁るため）。</summary>
        public static void PlayVoice(float pitch) => Restart(s_instance._voice, s_instance._voiceClip, pitch);

        private static void Restart(AudioSource source, AudioClip clip, float pitch)
        {
            source.pitch = pitch;
            source.clip = clip;
            source.Play();
        }

        private AudioSource NewSource(float volume)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.volume = volume;
            return source;
        }

        private static AudioClip Load(string name)
        {
            var clip = Resources.Load<AudioClip>(Folder + name);
            if (clip == null) Debug.LogWarning($"[GameAudio] Resources/{Folder}{name} が見つかりません");
            return clip;
        }
    }
}
