"""Musik für das Showcase-Video (20.11.2026: Instrumental statt Lied).

Synthwave-Instrumental, 120 BPM, a-Moll (Am – F – C – G je Takt), vollständig synthetisiert (keine Samples, keine Lizenzen,
jeder Lauf gleich). Die Teile kommen aus der Zeitleiste des Videos, damit Einsätze und Brüche auf den Schnitten liegen:
intro (Fläche + Arpeggio), build (Hi-Hats, Bass, Kick auf 1 und 3, Anlauf), drop (volles Schlagzeug, pumpender Bass, Crash),
break (Kapitelkarte: nur Fläche + Arpeggio + Anlauf), groove (ohne Clap), outro (leiser), end (Schlussakkord, Ausklang).
"""
import numpy as np

SR = 48000
rng = np.random.default_rng(2026)

PROG = [  # Bassgrundton, Akkordtöne (Hz)
    (110.00, (220.00, 261.63, 329.63)),   # Am
    (87.31, (174.61, 220.00, 261.63)),    # F
    (130.81, (261.63, 329.63, 392.00)),   # C
    (98.00, (196.00, 246.94, 293.66)),    # G
]


def t_axis(n):
    return np.arange(n) / SR


def band(x, lo, hi):
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    spec[(f < lo) | (f > hi)] = 0
    return np.fft.irfft(spec, len(x))


def norm(x):
    m = np.max(np.abs(x)) or 1
    return x / m


# ---------------------------------------------------------------- Klänge (einmal erzeugt, dann gesetzt)
def kick():
    n = int(0.38 * SR); t = t_axis(n)
    f = 45 + 115 * np.exp(-t / 0.032)
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.17)
    click = band(rng.standard_normal(n), 1500, 8000) * np.exp(-t / 0.003) * 0.25
    return norm(body + click)


def clap():
    n = int(0.3 * SR); t = t_axis(n)
    noise = band(rng.standard_normal(n), 900, 6000)
    envs = sum(np.exp(-np.clip(t - d, 0, None) / 0.008) * (t >= d) for d in (0, 0.011, 0.022))
    tail = np.exp(-np.clip(t - 0.022, 0, None) / 0.09) * (t >= 0.022)
    body = np.sin(2 * np.pi * 190 * t) * np.exp(-t / 0.03) * 0.3
    return norm(noise * (envs * 0.6 + tail * 0.8) + body)


def hat(open_=False):
    n = int((0.16 if open_ else 0.05) * SR); t = t_axis(n)
    return norm(band(rng.standard_normal(n), 7000, 18000) * np.exp(-t / (0.06 if open_ else 0.012)))


def crash():
    n = int(1.8 * SR); t = t_axis(n)
    return norm(band(rng.standard_normal(n), 3000, 15000) * np.exp(-t / 0.7) * np.minimum(1, t / 0.002))


def riser(dur):
    n = int(dur * SR); t = t_axis(n); k = t / dur
    air = band(rng.standard_normal(n), 2000, 12000) * k ** 2.5
    sweep = np.sin(2 * np.pi * np.cumsum(300 + 1500 * k ** 2) / SR) * k ** 3 * 0.25
    return norm(air + sweep)


def saw(freq, n, harmonics, detune=0.0, phase=0.0):
    t = t_axis(n)
    out = np.zeros(n)
    f = freq * (1 + detune)
    for h in range(1, harmonics + 1):
        if f * h > 9000: break
        out += np.sin(2 * np.pi * f * h * t + phase * h) / h ** 1.15
    return out


def bass_note(freq, dur):
    n = int(dur * SR); t = t_axis(n)
    x = saw(freq, n, 9) + 0.6 * np.sin(2 * np.pi * freq / 2 * t)   # Sub-Oktave
    env = np.minimum(1, t / 0.004) * (0.55 + 0.45 * np.exp(-t / 0.06)) * np.minimum(1, (dur - t) / 0.02)
    return norm(x) * env


def pluck(freq, dur):
    n = int(dur * SR); t = t_axis(n)
    x = sum(np.sin(2 * np.pi * freq * h * t) / h ** 2 * (1 if h % 2 else 0.35) for h in range(1, 7))
    return norm(x) * np.minimum(1, t / 0.002) * np.exp(-t / 0.11)


def pad_chord(freqs, dur):
    n = int(dur * SR); t = t_axis(n)
    left = sum(saw(f, n, 6, -0.0025, i) + saw(f, n, 6, 0.0035, i * 2) for i, f in enumerate(freqs))
    right = sum(saw(f, n, 6, 0.0025, i * 3) + saw(f, n, 6, -0.0035, i) for i, f in enumerate(freqs))
    env = np.minimum(1, t / 0.35) * np.minimum(1, (dur - t) / 0.3)
    return norm(left) * env, norm(right) * env


# ---------------------------------------------------------------- Arrangement
def track(total, bpm, parts):
    beat = 60.0 / bpm; bar = 4 * beat
    n = int(total * SR)
    TL = np.zeros(n); TR = np.zeros(n)   # Töne (pumpen unter der Kick)
    DL = np.zeros(n); DR = np.zeros(n)   # Trommeln
    pump = np.ones(n)
    K, C, H, HO, X = kick(), clap(), hat(), hat(True), crash()

    def put(t, x, g, pan=0.0, drum=False):
        i = int(round(t * SR))
        if i >= n or i < 0: return
        x = x[: n - i] * g
        a = (pan + 1) * np.pi / 4
        bl, br = (DL, DR) if drum else (TL, TR)
        bl[i:i + len(x)] += x * np.cos(a); br[i:i + len(x)] += x * np.sin(a)

    def duck(t):
        i = int(round(t * SR)); j = min(n, i + int(0.24 * SR))
        if 0 <= i < n: pump[i:j] = np.minimum(pump[i:j], np.linspace(0.3, 1, j - i))

    def part_at(t):
        cur = parts[0][1]
        for pt, name in parts:
            if pt <= t + 1e-6: cur = name
        return cur

    def next_part(t):
        for pt, name in parts:
            if pt > t + 1e-6: return pt, name
        return None, None

    gpad = {"intro": 0.30, "build": 0.26, "drop": 0.22, "break": 0.30, "groove": 0.22, "outro": 0.28, "end": 0.34}
    garp = {"intro": 0.10, "build": 0.12, "drop": 0.11, "break": 0.12, "groove": 0.10, "outro": 0.09}
    bars = int(np.ceil(total / bar))
    prev = None
    for b in range(bars):
        t0 = b * bar
        part = part_at(t0)
        root, chord = PROG[b % 4]
        nxt_t, nxt = next_part(t0)
        into_drop = nxt in ("drop", "groove") and abs(nxt_t - (t0 + bar)) < 1e-3
        if part in ("drop", "groove") and prev not in ("drop", "groove"): put(t0, X, 0.5, 0.15, drum=True)
        if part == "end":
            if prev != "end":   # Schlussakkord einmal, lang ausklingend, mit Crash
                dur = total - t0
                pl, pr = pad_chord((110.00, 220.00, 261.63, 329.63, 493.88), dur)
                fade = np.linspace(1, 0, len(pl)) ** 1.6
                put(t0, pl * fade, gpad["end"], -0.2); put(t0, pr * fade, gpad["end"], 0.2)
                put(t0, X, 0.3, 0, drum=True); put(t0, K, 0.6, 0, drum=True)
            prev = part
            continue
        prev = part
        pl, pr = pad_chord(chord, bar + 0.3)
        put(t0, pl, gpad[part], -0.6); put(t0, pr, gpad[part], 0.6)
        notes = [chord[0] * 2, chord[1] * 2, chord[2] * 2, chord[1] * 2, chord[0] * 4, chord[2] * 2, chord[1] * 2, chord[2] * 2]
        for s in range(16):
            if part == "intro" and b == 0 and s < 8: continue
            put(t0 + s * beat / 4, pluck(notes[s % 8], beat / 4 + 0.08), garp[part] * (1.0 if s % 4 == 0 else 0.75), -0.35 if s % 2 else 0.35)
        if part in ("drop", "groove"):
            for q in range(4): put(t0 + q * beat, K, 0.85, drum=True); duck(t0 + q * beat)
            if part == "drop":
                for q in (1, 3): put(t0 + q * beat, C, 0.42, 0.05, drum=True)
            steps = 16 if part == "drop" else 8
            for s in range(steps):
                off = s % 2 == 1
                put(t0 + s * bar / steps, HO if (part == "drop" and s % 4 == 2) or (part == "groove" and off) else H, 0.11 if off else 0.06, 0.25, drum=True)
            for e in range(8):
                if e % 2: put(t0 + e * beat / 2, bass_note(root, beat / 2 - 0.02), 0.34)
        elif part == "build":
            for q in ((0, 1, 2, 3) if into_drop else (0, 2)): put(t0 + q * beat, K, 0.6, drum=True); duck(t0 + q * beat)
            for s in range(8):
                if s % 2: put(t0 + s * beat / 2, H, 0.08, 0.25, drum=True)
            for q in range(4): put(t0 + q * beat, bass_note(root, beat - 0.05), 0.2)
        elif part == "outro":
            put(t0, K, 0.45, drum=True); duck(t0)
            for s in range(8):
                if s % 2: put(t0 + s * beat / 2, H, 0.05, 0.25, drum=True)
        if into_drop:
            put(t0, riser(bar), 0.2, drum=True)
            for s in range(4): put(t0 + 3 * beat + s * beat / 4, C, 0.12 + 0.06 * s, 0.05, drum=True)
    return TL * pump + DL, TR * pump + DR
