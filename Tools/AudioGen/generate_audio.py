"""ゲームの BGM / SE をチップチューン風に合成して Assets/Resources/Audio/ に WAV で書き出す。

使い方: python Tools/AudioGen/generate_audio.py
依存: numpy
音はすべてこのスクリプトで作った自作音源（外部素材なし）。
"""
import wave
from pathlib import Path

import numpy as np

RATE = 44100
OUT_DIR = Path(__file__).resolve().parents[2] / "Assets" / "Resources" / "Audio"

NOTE_INDEX = {"C": 0, "C#": 1, "D": 2, "D#": 3, "E": 4, "F": 5, "F#": 6, "G": 7, "G#": 8, "A": 9, "A#": 10, "B": 11}


def midi(name):
    """'A5' や 'G#4' を MIDI 番号に。"""
    return NOTE_INDEX[name[:-1]] + 12 * (int(name[-1]) + 1)


def freq(m):
    return 440.0 * 2 ** ((m - 69) / 12)


# ---------- 波形 ----------

def pulse(f, n, duty=0.5):
    phase = (np.arange(n) * f / RATE) % 1.0
    return np.where(phase < duty, 1.0, -1.0)


def triangle(f, n):
    phase = (np.arange(n) * f / RATE) % 1.0
    return 4.0 * np.abs(phase - 0.5) - 1.0


def noise(n, seed):
    return np.random.default_rng(seed).uniform(-1.0, 1.0, n)


def envelope(n, attack=0.005, release=0.03):
    env = np.ones(n)
    a = min(n, int(attack * RATE))
    r = min(n - a, int(release * RATE))
    if a > 0:
        env[:a] = np.linspace(0, 1, a)
    if r > 0:
        env[n - r:] = np.linspace(1, 0, r)
    return env


def decay(n, seconds):
    return np.exp(-np.arange(n) / (seconds * RATE))


def lowpass(x, cutoff):
    """1 次のローパス。矩形波の耳障りな高域を少し丸める。"""
    a = np.exp(-2 * np.pi * cutoff / RATE)
    y = np.empty_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - a) * v + a * acc
        y[i] = acc
    return y


# ---------- 楽器 ----------

def kick(n):
    t = np.arange(n) / RATE
    f = 45 + 110 * np.exp(-t / 0.03)
    phase = 2 * np.pi * np.cumsum(f) / RATE
    return np.sin(phase) * decay(n, 0.08)


def snare(n, seed):
    return 0.8 * noise(n, seed) * decay(n, 0.06) + 0.4 * triangle(190, n) * decay(n, 0.03)


def hat(n, seed):
    x = noise(n, seed)
    x = np.diff(x, prepend=0.0) * 0.5  # 高域だけ残す
    return x * decay(n, 0.015)


# ---------- 曲の組み立て ----------

class Song:
    """16 分音符を 1 ステップとして並べる。ループ末尾からはみ出た音は先頭に回し込んでつなぎ目を消す。"""

    def __init__(self, bpm, bars):
        self.step = int(round(RATE * 60 / bpm / 4))
        self.length = self.step * 16 * bars
        self.buf = np.zeros(self.length + RATE)  # 1 秒分の余白（はみ出し用）

    def add(self, start_step, signal, gain):
        s = start_step * self.step
        self.buf[s:s + len(signal)] += gain * signal

    def melody(self, notes, gain, duty=0.25, gate=0.85, lp=6000):
        """notes: (音名 or None, ステップ数) の並び。"""
        pos = 0
        for name, steps in notes:
            if name is not None:
                n = int(self.step * steps * gate)
                tone = pulse(freq(midi(name)), n, duty) * envelope(n, 0.003, 0.02)
                # 長い音は少し減衰させてチップっぽく
                tone *= 0.75 + 0.25 * decay(n, 0.25)
                self.add(pos, lowpass(tone, lp), gain)
            pos += steps
        return pos

    def finish(self, peak=0.85):
        out = self.buf[:self.length].copy()
        out[:len(self.buf) - self.length] += self.buf[self.length:]
        return out / np.max(np.abs(out)) * peak


CHORD_TONES = {
    "C": ["C", "E", "G"], "Am": ["A", "C", "E"], "F": ["F", "A", "C"], "G": ["G", "B", "D"],
    "Em": ["E", "G", "B"], "Dm": ["D", "F", "A"], "E": ["E", "G#", "B"],
}


def chord_midis(chord, octave):
    root = NOTE_INDEX[CHORD_TONES[chord][0]]
    result = []
    for name in CHORD_TONES[chord]:
        m = NOTE_INDEX[name] + 12 * (octave + 1)
        if NOTE_INDEX[name] < root:
            m += 12
        result.append(m)
    return result


def tone_steps(song, step, m, steps, gain, kind, duty=0.5, gate=0.8):
    n = int(song.step * steps * gate)
    f = freq(m)
    wave_ = triangle(f, n) if kind == "tri" else pulse(f, n, duty)
    song.add(step, wave_ * envelope(n, 0.002, 0.015), gain)


def select_bgm():
    """セレクト用: C メジャー、128 BPM。I-vi-IV-V の明るいポップ。"""
    chords = ["C", "Am", "F", "G", "C", "Am", "F", "G", "F", "G", "Em", "Am", "F", "G", "C", "G"]
    lead = [
        ("E5", 2), ("G5", 2), ("C6", 4), ("B5", 2), ("G5", 2), ("E5", 4),
        ("A5", 4), ("G5", 2), ("E5", 2), ("C5", 4), ("E5", 4),
        ("F5", 2), ("A5", 2), ("C6", 4), ("A5", 2), ("F5", 2), ("G5", 4),
        ("G5", 6), ("D5", 2), ("B4", 4), (None, 4),
        ("E5", 2), ("G5", 2), ("C6", 4), ("D6", 2), ("C6", 2), ("G5", 4),
        ("A5", 4), ("C6", 4), ("B5", 2), ("A5", 2), ("E5", 4),
        ("F5", 2), ("E5", 2), ("F5", 2), ("A5", 2), ("G5", 4), ("F5", 4),
        ("D5", 2), ("E5", 2), ("F5", 2), ("D5", 2), ("B4", 4), ("G4", 4),
        ("A5", 4), ("A5", 2), ("G5", 2), ("F5", 4), ("C5", 4),
        ("B5", 4), ("B5", 2), ("A5", 2), ("G5", 4), ("D5", 4),
        ("G5", 2), ("E5", 2), ("B4", 2), ("E5", 2), ("G5", 4), ("B5", 4),
        ("C6", 6), ("B5", 2), ("A5", 4), ("E5", 4),
        ("F5", 2), ("A5", 2), ("C6", 2), ("A5", 2), ("F5", 2), ("A5", 2), ("C6", 4),
        ("D6", 2), ("B5", 2), ("G5", 2), ("B5", 2), ("D6", 8),
        ("E6", 4), ("D6", 2), ("C6", 2), ("G5", 4), ("E5", 4),
        ("D5", 2), ("F5", 2), ("G5", 2), ("B5", 2), ("D6", 4), (None, 4),
    ]
    song = Song(128, len(chords))
    assert song.melody(lead, 0.22, duty=0.25) == 16 * len(chords)

    for bar, chord in enumerate(chords):
        base = bar * 16
        root = chord_midis(chord, 2)[0]
        # ベース: 8 分でルートとオクターブを跳ねさせる
        for i in range(8):
            tone_steps(song, base + i * 2, root + (12 if i % 2 else 0), 2, 0.30, "tri")
        # 裏拍のコードの刻み（ポップな跳ね）
        for i in range(4):
            for m in chord_midis(chord, 4):
                tone_steps(song, base + i * 4 + 2, m, 1, 0.045, "pulse", duty=0.5, gate=0.7)
        # ドラム
        for beat in range(4):
            s = base + beat * 4
            if beat in (0, 2):
                song.add(s, kick(int(0.2 * RATE)), 0.45)
            else:
                song.add(s, snare(int(0.15 * RATE), bar * 4 + beat), 0.22)
            song.add(s + 2, hat(int(0.05 * RATE), 1000 + bar * 4 + beat), 0.12)
    return song.finish()


def battle_bgm():
    """バトル用: A マイナー、150 BPM。i-VI-VII-V（V は E メジャー）で明るさの中に緊迫感。"""
    chords = ["Am", "F", "G", "E", "Am", "F", "G", "E", "Dm", "Am", "F", "E", "Dm", "Am", "F", "E"]
    lead = [
        ("A5", 3), ("A5", 3), ("C6", 2), ("B5", 2), ("A5", 2), ("E5", 4),
        ("F5", 3), ("F5", 3), ("A5", 2), ("G5", 2), ("F5", 2), ("C5", 4),
        ("G5", 3), ("G5", 3), ("B5", 2), ("D6", 2), ("B5", 2), ("G5", 4),
        ("G#5", 4), ("B5", 4), ("E6", 4), ("D6", 2), ("B5", 2),
        ("A5", 3), ("A5", 3), ("C6", 2), ("E6", 2), ("D6", 2), ("C6", 4),
        ("A5", 3), ("A5", 3), ("C6", 2), ("C6", 2), ("A5", 2), ("F5", 4),
        ("B5", 2), ("C6", 2), ("D6", 4), ("G5", 2), ("B5", 2), ("D6", 4),
        ("E6", 6), ("D6", 2), ("B5", 2), ("G#5", 2), ("E5", 4),
        ("D6", 2), ("A5", 2), ("F5", 2), ("A5", 2), ("D6", 2), ("E6", 2), ("F6", 4),
        ("E6", 2), ("C6", 2), ("A5", 2), ("C6", 2), ("E6", 4), ("A5", 4),
        ("F5", 2), ("A5", 2), ("C6", 2), ("F6", 2), ("E6", 2), ("C6", 2), ("A5", 4),
        ("G#5", 2), ("B5", 2), ("E6", 2), ("B5", 2), ("G#5", 2), ("B5", 2), ("E6", 4),
        ("D6", 4), ("C6", 2), ("D6", 2), ("F6", 4), ("E6", 4),
        ("C6", 4), ("B5", 2), ("C6", 2), ("E6", 4), ("A5", 4),
        ("F5", 2), ("G5", 2), ("A5", 2), ("C6", 2), ("F6", 4), ("E6", 4),
        ("E6", 2), (None, 2), ("E6", 2), (None, 2), ("D6", 2), ("B5", 2), ("G#5", 2), ("E5", 2),
    ]
    song = Song(150, len(chords))
    assert song.melody(lead, 0.20, duty=0.25) == 16 * len(chords)

    for bar, chord in enumerate(chords):
        base = bar * 16
        tones = chord_midis(chord, 4)
        root = chord_midis(chord, 2)[0]
        # ベース: 8 分で刻み続け、拍の裏でオクターブに跳ねる（駆り立てる感じ）
        for i in range(8):
            tone_steps(song, base + i * 2, root + (12 if i in (3, 7) else 0), 2, 0.30, "tri", gate=0.7)
        # 16 分のアルペジオ（細い矩形波）で落ち着かなさを出す
        arp = tones + [tones[0] + 12]
        for i in range(16):
            tone_steps(song, base + i, arp[[0, 1, 2, 3, 2, 1][i % 6]], 1, 0.05, "pulse", duty=0.125, gate=0.6)
        # ドラム: キック 1・2 裏・3、スネア 2・4、ハット 16 分
        for s, kind in ((0, "k"), (6, "k"), (8, "k"), (4, "s"), (12, "s")):
            if kind == "k":
                song.add(base + s, kick(int(0.2 * RATE)), 0.45)
            else:
                song.add(base + s, snare(int(0.15 * RATE), bar * 16 + s), 0.24)
        for i in range(16):
            song.add(base + i, hat(int(0.04 * RATE), 5000 + bar * 16 + i), 0.10 if i % 2 else 0.05)
    return song.finish()


# ---------- SE ----------

def normalize(x, peak=0.9):
    return x / np.max(np.abs(x)) * peak


def se_hit():
    """被弾: 下がる矩形波＋ノイズのザラつき。"""
    n = int(0.3 * RATE)
    t = np.arange(n) / RATE
    f = 520 * np.exp(-t / 0.08) + 70
    phase = np.cumsum(f) / RATE % 1.0
    tone = np.where(phase < 0.5, 1.0, -1.0) * decay(n, 0.12)
    crunch = noise(n, 7) * decay(n, 0.05)
    return normalize(0.7 * tone + 0.5 * crunch)


def se_select_tick():
    """選択中のチッチッ音。ピッチはゲーム側で上げていく。"""
    n = int(0.05 * RATE)
    return normalize(lowpass(pulse(freq(midi("A5")), n, 0.5) * decay(n, 0.015), 7000), 0.7)


def se_blips(notes, step_sec, length_sec, duty=0.5, peak=0.85):
    out = np.zeros(int(length_sec * RATE))
    for i, name in enumerate(notes):
        s = int(i * step_sec * RATE)
        n = min(len(out) - s, int(0.18 * RATE))
        out[s:s + n] += pulse(freq(midi(name)), n, duty) * decay(n, 0.07)
    return normalize(lowpass(out, 7000), peak)


def se_decide():
    """難易度決定（ゲーム開始）: 駆け上がるアルペジオ。"""
    return se_blips(["C5", "E5", "G5", "C6", "E6", "G6"], 0.045, 0.5, duty=0.25)


def se_battle_start():
    """バトル開始: ノイズが上がってから A マイナーのジャン。"""
    n = int(0.8 * RATE)
    out = np.zeros(n)
    rise_n = int(0.25 * RATE)
    rise = noise(rise_n, 11) * np.linspace(0, 1, rise_n) ** 2
    out[:rise_n] += 0.4 * np.diff(rise, prepend=0.0)
    s = rise_n
    hit_n = n - s
    for name in ("A4", "C5", "E5", "A5"):
        out[s:] += pulse(freq(midi(name)), hit_n, 0.25) * decay(hit_n, 0.18) * 0.35
    out[s:] += kick(hit_n) * 0.8
    return normalize(lowpass(out, 8000))


def voice_enemy():
    """敵のセリフ音: アンダーテールのような短い矩形波の「ポ」。難易度ごとのピッチはゲーム側で変える。"""
    n = int(0.045 * RATE)
    t = np.arange(n) / RATE
    f = freq(midi("C5")) * (1.0 - 0.15 * t / t[-1])  # 少しだけ下げて声っぽく
    phase = np.cumsum(f) / RATE % 1.0
    tone = np.where(phase < 0.5, 1.0, -1.0) * envelope(n, 0.002, 0.012)
    return normalize(lowpass(tone, 5000), 0.8)


def write_wav(name, samples):
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    data = (np.clip(samples, -1, 1) * 32767).astype("<i2").tobytes()
    with wave.open(str(OUT_DIR / f"{name}.wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data)
    print(f"{name}.wav  {len(samples) / RATE:.2f}s")


def main():
    write_wav("bgm_select", select_bgm())
    write_wav("bgm_battle", battle_bgm())
    write_wav("se_hit", se_hit())
    write_wav("se_select_tick", se_select_tick())
    write_wav("se_decide", se_decide())
    write_wav("se_battle_start", se_battle_start())
    write_wav("voice_enemy", voice_enemy())


if __name__ == "__main__":
    main()
