"""Retro chiptune music and arcade SFX synthesised with numpy (square / triangle / noise channels, 8-bit flavour).

  Tools/.venv/bin/python Tools/audio/chiptune.py
Writes WAVs into Assets/Resources/Audio/Music and Assets/Resources/Audio/SFX.
"""
import os
import wave

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
MUSIC = os.path.join(ROOT, "Assets", "Resources", "Audio", "Music")
SFX = os.path.join(ROOT, "Assets", "Resources", "Audio", "SFX")
RATE = 22050
rng = np.random.default_rng(13)

NOTE = {"C": 0, "C#": 1, "D": 2, "D#": 3, "E": 4, "F": 5, "F#": 6, "G": 7, "G#": 8, "A": 9, "A#": 10, "B": 11}


def freq(name):
    """'A4', 'C#3' -> Hz; '-' is a rest."""
    if name in ("-", None):
        return 0.0
    pitch, octave = name[:-1], int(name[-1])
    semitone = NOTE[pitch] + 12 * (octave + 1)
    return 440.0 * 2 ** ((semitone - 69) / 12)


def square(f, n, duty=0.25):
    if f <= 0:
        return np.zeros(n)
    t = np.arange(n) / RATE
    return np.where((t * f) % 1.0 < duty, 1.0, -1.0)


def triangle(f, n):
    if f <= 0:
        return np.zeros(n)
    t = np.arange(n) / RATE
    x = (t * f) % 1.0
    return 4 * np.abs(x - 0.5) - 1


def noise(n, period=1):
    raw = rng.uniform(-1, 1, n // period + 1)
    return np.repeat(raw, period)[:n]


def env(n, attack=0.005, decay=0.12, sustain=0.6, release=0.03):
    e = np.full(n, sustain)
    a = max(1, int(attack * RATE))
    d = max(1, int(decay * RATE))
    r = max(1, int(release * RATE))
    e[:a] = np.linspace(0, 1, a)[:n]
    if a < n:
        seg = np.linspace(1, sustain, d)[:max(0, n - a)]
        e[a:a + len(seg)] = seg
    e[-r:] *= np.linspace(1, 0, min(r, n))
    return e


def sequence(pattern, step, voice, volume, **kw):
    """pattern: list of note names (or '-'), one per step."""
    n = int(step * RATE)
    out = []
    for note in pattern:
        f = freq(note)
        if voice == "square":
            wave_ = square(f, n, kw.get("duty", 0.25))
        else:
            wave_ = triangle(f, n)
        out.append(wave_ * env(n, decay=kw.get("decay", 0.1), sustain=kw.get("sustain", 0.5)) * (f > 0))
    return np.concatenate(out) * volume


def drums(pattern, step, volume):
    """'K' kick, 'S' snare, 'H' hat, '-' rest."""
    n = int(step * RATE)
    out = []
    t = np.arange(n) / RATE
    for hit in pattern:
        if hit == "K":
            sweep = np.sin(2 * np.pi * np.cumsum(np.linspace(140, 45, n)) / RATE)
            out.append(sweep * np.exp(-t * 18))
        elif hit == "S":
            out.append(noise(n, 2) * np.exp(-t * 22) * 0.8 + square(190, n, 0.5) * np.exp(-t * 40) * 0.3)
        elif hit == "H":
            out.append(noise(n, 1) * np.exp(-t * 80) * 0.35)
        else:
            out.append(np.zeros(n))
    return np.concatenate(out) * volume


def mix(*tracks):
    length = max(len(t) for t in tracks)
    out = np.zeros(length)
    for t in tracks:
        out[:len(t)] += t
    peak = np.max(np.abs(out)) or 1
    return out / peak * 0.85


def crush(signal, bits=8):
    levels = 2 ** (bits - 1)
    return np.round(signal * levels) / levels


def save(path, signal):
    data = (np.clip(crush(signal), -1, 1) * 32767).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data.tobytes())


def expand(bars):
    """Expand ['A3 . C4 E4 ...'] bar strings into a flat list; '.' holds the previous step as a rest."""
    out = []
    for bar in bars:
        out += ["-" if token == "." else token for token in bar.split()]
    return out


# ---------------------------------------------------------------- music

def theme(lead_bars, bass_bars, drum_bars, bpm, repeats, lead_duty=0.125):
    step = 60.0 / bpm / 4
    lead = sequence(expand(lead_bars) * repeats, step, "square", 0.32, duty=lead_duty, decay=0.12, sustain=0.45)
    harmony = sequence(expand(lead_bars) * repeats, step, "square", 0.1, duty=0.5, decay=0.05, sustain=0.2)
    bass = sequence(expand(bass_bars) * repeats, step, "triangle", 0.55, decay=0.2, sustain=0.7)
    beat = drums(" ".join(drum_bars).split() * repeats, step, 0.5)
    # Slight delay on the harmony square fattens the lead like an echo channel.
    delay = int(step * 3 * RATE)
    harmony = np.concatenate([np.zeros(delay), harmony])
    return mix(lead, harmony, bass, beat)


MENU = theme(
    ["A4 . C5 . E5 . A5 . G#5 . E5 . C5 . B4 .", "A4 . C5 . E5 . F5 . E5 . C5 . B4 . G#4 .",
     "F4 . A4 . C5 . F5 . E5 . C5 . A4 . G#4 .", "E4 . G#4 . B4 . E5 . D5 . B4 . G#4 . . ."],
    ["A2 . . . A2 . . . A2 . . . E2 . . .", "A2 . . . A2 . . . A2 . . . E2 . . .",
     "F2 . . . F2 . . . F2 . . . C3 . . .", "E2 . . . E2 . . . E2 . G#2 . B2 . . ."],
    ["K - H - S - H - K - K - S - H -"] * 4, 96, 6)

SELECT = theme(
    ["E5 E5 . D5 E5 . G5 . E5 . D5 . C5 . B4 .", "C5 C5 . B4 C5 . E5 . D5 . C5 . B4 . A4 .",
     "E5 E5 . D5 E5 . G5 . A5 . G5 . E5 . D5 .", "C5 . B4 . A4 . G#4 . A4 . . . . . . ."],
    ["A2 . A3 . A2 . A3 . A2 . A3 . A2 . A3 .", "F2 . F3 . F2 . F3 . G2 . G3 . G2 . G3 .",
     "A2 . A3 . A2 . A3 . C3 . C4 . C3 . C4 .", "E2 . E3 . E2 . E3 . A2 . A3 . A2 . . ."],
    ["K H S H K H S H K H S H K K S H"] * 4, 132, 4, lead_duty=0.25)

RESULTS = theme(
    ["A4 . . . E5 . . . D5 . C5 . B4 . . .", "C5 . . . A4 . . . G#4 . . . . . . ."],
    ["A2 . . . . . . . F2 . . . . . . .", "D2 . . . . . . . E2 . . . . . . ."],
    ["K - - - S - - - K - - - S - - -"] * 2, 72, 6)

FIGHT_UTEZ = theme(
    ["D5 . D5 F5 . D5 G5 . D5 A5 . G5 F5 . E5 .", "D5 . D5 F5 . D5 G5 . A#5 . A5 . G5 . F5 .",
     "C5 . C5 E5 . C5 F5 . C5 G5 . F5 E5 . D5 .", "A4 . C#5 . E5 . G5 . F5 . E5 . C#5 . . ."],
    ["D2 D3 D2 D3 D2 D3 D2 D3 A#1 A#2 A#1 A#2 A#1 A#2 A#1 A#2",
     "D2 D3 D2 D3 D2 D3 D2 D3 G1 G2 G1 G2 A1 A2 A1 A2",
     "C2 C3 C2 C3 C2 C3 C2 C3 F1 F2 F1 F2 F1 F2 F1 F2",
     "A1 A2 A1 A2 A1 A2 A1 A2 A1 A2 A1 A2 C#2 C#3 E2 E3"],
    ["K H S H K K S H K H S H K K S S"] * 4, 150, 6, lead_duty=0.25)

FIGHT_WOODSBORO = theme(
    ["E5 . G5 . B5 . A5 G5 F#5 . E5 . D#5 . B4 .", "E5 . G5 . B5 . C6 B5 A5 . G5 . F#5 . . .",
     "C5 . E5 . G5 . F#5 E5 D#5 . C5 . B4 . A4 .", "B4 . D#5 . F#5 . A5 . G5 . F#5 . D#5 . . ."],
    ["E2 . E3 . E2 . E3 . E2 . E3 . E2 . E3 .", "E2 . E3 . E2 . E3 . A1 . A2 . B1 . B2 .",
     "C2 . C3 . C2 . C3 . A1 . A2 . A1 . A2 .", "B1 . B2 . B1 . B2 . B1 . B2 . B1 . B2 ."],
    ["K H H S K H S H K H H S K K S H"] * 4, 140, 6)

VERSUS = theme(["A5 G#5 G5 F#5 F5 E5 D#5 D5 C#5 C5 B4 A#4 A4 . . ."], ["A1 . . . . . . . . . . . A1 . . ."],
               ["K - - - K - - - K - K - S S S S"], 120, 1)


# ---------------------------------------------------------------- sfx

def sweep(f0, f1, seconds, duty=0.5, decay=8.0):
    n = int(seconds * RATE)
    f = np.linspace(f0, f1, n)
    phase = np.cumsum(f) / RATE
    wave_ = np.where(phase % 1.0 < duty, 1.0, -1.0)
    return wave_ * np.exp(-np.linspace(0, seconds, n) * decay)


def sfx():
    out = {
        "ui_move_1": sweep(880, 1320, 0.05, 0.25, 20),
        "ui_select_1": np.concatenate([sweep(660, 660, 0.06, 0.5, 6), sweep(990, 990, 0.12, 0.5, 10)]),
        "ui_back_1": sweep(700, 260, 0.14, 0.5, 8),
        "ui_start_1": np.concatenate([sweep(f, f, 0.07, 0.25, 4) for f in (523, 659, 784, 1046)] +
                                     [sweep(1046, 1046, 0.3, 0.25, 4)]),
        "vs_hit_1": mix(sweep(220, 55, 0.6, 0.5, 4), noise(int(0.6 * RATE), 3) * np.exp(-np.linspace(0, 0.6,
                                                                                                    int(0.6 * RATE)) * 6)),
    }
    n = int(1.6 * RATE)
    t = np.linspace(0, 1.6, n)
    chord = sum(square(freq(note), n, 0.5) for note in ("A2", "C3", "D#3", "A3")) / 4
    out["fatality_sting_1"] = mix(chord * np.exp(-t * 1.4), noise(n, 4) * np.exp(-t * 3) * 0.6,
                                  np.sin(2 * np.pi * np.cumsum(np.linspace(90, 30, n)) / RATE) * np.exp(-t * 2))
    n = int(1.2 * RATE)
    t = np.linspace(0, 1.2, n)
    out["explosion_gore_1"] = mix(noise(n, 6) * np.exp(-t * 3.5), noise(n, 2) * np.exp(-t * 9) * 0.7,
                                  np.sin(2 * np.pi * np.cumsum(np.linspace(110, 28, n)) / RATE) * np.exp(-t * 3))
    n = int(0.5 * RATE)
    t = np.linspace(0, 0.5, n)
    out["dismember_1"] = mix(noise(n, 3) * np.exp(-t * 9), sweep(300, 80, 0.5, 0.5, 6) * 0.5)
    n = int(0.25 * RATE)
    t = np.linspace(0, 0.25, n)
    out["blood_splat_1"] = noise(n, 5) * np.exp(-t * 26)
    out["blood_splat_2"] = noise(n, 7) * np.exp(-t * 20)
    return out


def main():
    os.makedirs(MUSIC, exist_ok=True)
    os.makedirs(SFX, exist_ok=True)
    for name, signal in (("menu", MENU), ("select", SELECT), ("results", RESULTS),
                         ("fight_utez", FIGHT_UTEZ), ("fight_woodsboro", FIGHT_WOODSBORO)):
        save(os.path.join(MUSIC, name + ".wav"), signal)
        print(f"music/{name}: {len(signal) / RATE:.1f}s")
    save(os.path.join(SFX, "versus_sting_1.wav"), VERSUS)
    for name, signal in sfx().items():
        peak = np.max(np.abs(signal)) or 1
        save(os.path.join(SFX, name + ".wav"), signal / peak * 0.8)
    print("sfx written")


if __name__ == "__main__":
    main()
