"""Tonspur für das DeskKeeper-Werbevideo (SM 19.11.2026).

Vorgehen nach dem Sounddesign-Skill der Skill-Bibliothek (whaleyxbt sound-design): Bild und Ton lesen dieselbe Zeitleiste
(timeline.json, von DeskKeeper.exe --promo geschrieben), jedes Geräusch wird hier synthetisiert (keine Samples, keine Lizenzen,
jeder Lauf gleich), eine Tonart (a-Moll, Erfolg in A-Dur), Pegel nach Rangordnung, Stereoposition nach Bildposition. Das Mastern auf
−14 LUFS mit Spitzenwert ≤ −2 dBFS übernimmt ffmpeg (loudnorm) beim Zusammenfügen.

Aufruf: python promo_audio.py <timeline.json> <ausgabe.wav>
"""
import json, sys, wave
import numpy as np

SR = 48000
rng = np.random.default_rng(136)


def t_axis(dur):
    return np.arange(int(dur * SR)) / SR


def band(x, lo, hi):
    """Bandpass per FFT (statisch)."""
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    spec[(f < lo) | (f > hi)] = 0
    return np.fft.irfft(spec, len(x))


def noise(dur):
    return rng.standard_normal(int(dur * SR))


def norm(x):
    m = np.max(np.abs(x)) or 1
    return x / m


def env(dur, attack, decay):
    t = t_axis(dur)
    return np.minimum(1, t / max(attack, 1e-4)) * np.exp(-t / decay)


# ---------------------------------------------------------------- Einzelgeräusche
def boom(size=1.0):
    """Einschlag: tiefer Schlag mit absinkender Tonhöhe, Krachen, Nachgrollen."""
    dur = 0.9 + 0.8 * size
    t = t_axis(dur)
    thump = np.sin(2 * np.pi * (48 + 60 * np.exp(-t / 0.08)) * t) * np.exp(-t / (0.22 * size + 0.1))
    crack = band(noise(dur), 300, 4500) * env(dur, 0.002, 0.07)
    rumble = band(noise(dur), 30, 380) * env(dur, 0.01, 0.45 * size + 0.2)
    return norm(1.0 * thump + 0.55 * norm(crack) + 0.8 * norm(rumble))


def nuke():
    """Atombombe: Blitz-Knall, dann langes Grollen der Druckwelle."""
    dur = 4.5
    t = t_axis(dur)
    hit = boom(2.2)
    rumble = band(noise(dur), 20, 220) * np.minimum(1, t / 0.4) * np.exp(-t / 1.6)
    out = norm(rumble) * 0.9
    out[: len(hit)] += hit
    return norm(out)


def tick():
    """25-mm-Schuss bzw. kleines Klacken."""
    dur = 0.05
    return norm(band(noise(dur), 1500, 7000) * env(dur, 0.0005, 0.008))


def pom():
    """40-mm-Bofors: dumpfes Pom."""
    dur = 0.25
    t = t_axis(dur)
    return norm(np.sin(2 * np.pi * (110 + 90 * np.exp(-t / 0.02)) * t) * env(dur, 0.001, 0.06) + 0.4 * band(noise(dur), 200, 2500) * env(dur, 0.001, 0.03))


def cannon():
    """105-mm-Haubitze: kurzer schwerer Knall."""
    return boom(0.45)


def brrt():
    """Kanone der A-10 / Su-25: kurzer schnarrender Stoß (mehrere hintereinander ergeben das BRRRT)."""
    dur = 0.06
    t = t_axis(dur)
    saw = 2 * ((t * 75) % 1) - 1
    return norm((0.7 * saw + 0.6 * band(noise(dur), 200, 3000)) * env(dur, 0.001, 0.025))


def rocket():
    """Rakete: zischender Abgang."""
    dur = 0.5
    t = t_axis(dur)
    x = band(noise(dur), 900, 6000) * np.minimum(1, t / 0.02) * np.exp(-t / 0.18)
    return norm(x)


def whoosh(dur=0.5, lo=300, hi=3000):
    """Übergang: Luftzug, der an- und abschwillt."""
    t = t_axis(dur)
    x = band(noise(dur), lo, hi) * np.sin(np.pi * np.clip(t / dur, 0, 1)) ** 2
    return norm(x)


def riser(dur=1.0):
    """Anlauf vor der Szenenfolge: steigendes Rauschen und Ton."""
    t = t_axis(dur)
    k = t / dur
    tone = np.sin(2 * np.pi * (220 + 660 * k ** 2) * t) * k ** 2
    air = band(noise(dur), 1500, 8000) * k ** 3
    return norm(0.5 * tone + 0.8 * norm(air))


def chime(freqs=(440.0, 554.37, 659.25), dur=1.4):
    """Erfolg: Akkord in A-Dur (löst das a-Moll des Teppichs auf)."""
    t = t_axis(dur)
    x = sum(np.sin(2 * np.pi * f * t) * np.exp(-t / (0.5 + i * 0.15)) for i, f in enumerate(freqs))
    return norm(x * np.minimum(1, t / 0.005))


def drop():
    """Symbol abgesetzt: weiches Ploppen."""
    dur = 0.12
    t = t_axis(dur)
    return norm(np.sin(2 * np.pi * (880 - 300 * t / dur) * t) * env(dur, 0.002, 0.03))


# ---------------------------------------------------------------- Dauergeräusche (mit Länge)
def jet(dur):
    """Düsenflugzeug: anschwellendes Fauchen und Grollen (Vorbeiflug)."""
    t = t_axis(dur)
    swell = np.sin(np.pi * np.clip(t / dur, 0, 1)) ** 1.5
    return norm(band(noise(dur), 250, 3500) * 0.7 + band(noise(dur), 30, 250)) * swell


def props(dur):
    """Turboprop: brummende Grundfrequenz mit Schwebung, dazu Luft."""
    t = t_axis(dur)
    hum = sum(np.sin(2 * np.pi * 68 * h * t) / h for h in range(1, 7)) * (0.75 + 0.25 * np.sin(2 * np.pi * 7 * t))
    swell = np.minimum(1, t / 0.4) * np.minimum(1, (dur - t) / 0.4)
    return norm(norm(hum) + 0.35 * norm(band(noise(dur), 300, 2500))) * swell


def rotor(dur, rate=4.6):
    """Hubschrauber: tiefes Wummern im Blatttakt."""
    t = t_axis(dur)
    chop = (0.5 + 0.5 * np.cos(2 * np.pi * rate * t)) ** 6
    body = band(noise(dur), 40, 420) * (0.25 + chop)
    whine = 0.15 * np.sin(2 * np.pi * 310 * t)
    swell = np.minimum(1, t / 0.3) * np.minimum(1, (dur - t) / 0.3)
    return norm(norm(body) + whine) * swell


def moped(dur):
    """Shahed-136: das typische Mofaknattern des Zweitakters, mehrere Drohnen leicht verstimmt."""
    t = t_axis(dur)
    x = sum(2 * ((t * f * (1 + 0.01 * np.sin(2 * np.pi * 0.7 * t + i))) % 1) - 1 for i, f in enumerate((92, 97, 103, 88)))
    swell = np.minimum(1, t / 0.4) * np.minimum(1, (dur - t) / 0.3)
    return norm(band(x, 120, 2200)) * swell


def drones(dur):
    """Lieferdrohnen: helles Surren."""
    t = t_axis(dur)
    x = sum(np.sin(2 * np.pi * f * t) for f in (232, 241, 466, 475)) + 0.3 * band(noise(dur), 2000, 6000)
    swell = np.minimum(1, t / 0.2) * np.minimum(1, (dur - t) / 0.2)
    return norm(x) * swell


def servo(dur):
    """Roboterarm, Stapler, Kran: Elektromotor mit wechselnder Drehzahl."""
    t = t_axis(dur)
    f = 330 + 120 * np.sin(2 * np.pi * 0.9 * t)
    phase = 2 * np.pi * np.cumsum(f) / SR
    x = np.sin(phase) + 0.4 * np.sin(2 * phase) + 0.2 * band(noise(dur), 100, 800)
    swell = np.minimum(1, t / 0.2) * np.minimum(1, (dur - t) / 0.2)
    return norm(x) * swell


def pad(dur):
    """Musikteppich: a-Moll-Klang (A2, E3, A3, C4, E4), langsam atmend."""
    t = t_axis(dur)
    voices = [(110.0, 1.0), (164.81, 0.7), (220.0, 0.6), (261.63, 0.45), (329.63, 0.35)]
    left = sum(a * np.sin(2 * np.pi * f * 1.002 * t + i) for i, (f, a) in enumerate(voices))
    right = sum(a * np.sin(2 * np.pi * f * 0.998 * t + 2 * i) for i, (f, a) in enumerate(voices))
    breath = 0.75 + 0.25 * np.sin(2 * np.pi * t / 8)
    fade = np.minimum(1, t / 2) * np.minimum(1, (dur - t) / 3)
    return norm(left) * breath * fade, norm(right) * breath * fade


# ---------------------------------------------------------------- Mischung
SHORT = {"boom": (boom, 0.30), "bigboom": (lambda: boom(1.8), 0.42), "nuke": (nuke, 0.50), "tick": (tick, 0.10), "pom": (pom, 0.20),
         "cannon": (cannon, 0.30), "brrt": (brrt, 0.16), "rocket": (rocket, 0.14), "drop": (drop, 0.14), "success": (chime, 0.22),
         "riser": (lambda: riser(1.0), 0.18)}
BEDS = {"jet": (jet, 0.16), "props": (props, 0.14), "rotor": (rotor, 0.18), "moped": (moped, 0.12), "drones": (drones, 0.08), "servo": (servo, 0.07)}
TRANSITION = {"title": 0.10, "caption": 0.07, "card": 0.10, "clip": 0.06, "end": 0.12}


def shuffle():
    """Windows mischt die Symbole: viele kurze Luftzüge und Klacken durcheinander."""
    dur = 0.9
    out = np.zeros(int(dur * SR))
    for i in range(14):
        x = whoosh(0.12, 600, 5000) * 0.5 + np.pad(tick(), (0, int(0.12 * SR) - len(tick())))
        j = int(rng.random() * (len(out) - len(x)))
        out[j:j + len(x)] += x
    return norm(out)


def cut():
    """Harter Schnitt im Kurzvideo: kurzer tiefer Schlag mit Luft."""
    dur = 0.3
    t = t_axis(dur)
    return norm(np.sin(2 * np.pi * (70 + 80 * np.exp(-t / 0.03)) * t) * env(dur, 0.001, 0.07) + 0.5 * whoosh(dur, 400, 4000))


SHORT["shuffle"] = (shuffle, 0.22)
SHORT["cut"] = (cut, 0.16)


def main(timeline, out):
    tl = json.load(open(timeline, encoding="utf-8"))
    total = tl["total"] + 1.0
    n = int(total * SR)
    L = np.zeros(n); R = np.zeros(n)
    duck = np.ones(n)
    cache = {}

    def put(t, x, gain, pan=0.0):
        i = int(t * SR)
        if i >= n: return
        x = x[: n - i]
        a = (pan + 1) * np.pi / 4   # gleiche Leistung beim Schwenken
        L[i:i + len(x)] += x * gain * np.cos(a)
        R[i:i + len(x)] += x * gain * np.sin(a)

    for c in tl["cues"]:
        k, t, pan, g = c["kind"], c["t"], c["pan"], c["gain"]
        if k in SHORT:
            fn, base = SHORT[k]
            if k not in cache: cache[k] = fn()
            # Einschläge leicht variieren (Tonhöhe), damit eine Salve nicht maschinell klingt
            x = cache[k]
            if k == "boom":
                rate = 0.85 + 0.3 * rng.random()
                idx = np.arange(0, len(x) - 1, rate)
                x = np.interp(idx, np.arange(len(x)), x)
            put(t, x, base * g, pan)
            if k in ("bigboom", "nuke", "boom"):
                i = int(t * SR); m = int((2.5 if k == "nuke" else 0.8) * SR)
                d = duck[i:i + m]; d *= np.linspace(0.35 if k != "boom" else 0.8, 1, len(d))
        elif k in BEDS:
            fn, base = BEDS[k]
            put(t, fn(c["len"]), base, pan)
        elif k in TRANSITION:
            put(max(0, t - 0.18), whoosh(0.45), TRANSITION[k])
            if k == "end": put(t + 0.2, chime((440.0, 554.37, 659.25, 880.0), 2.5), 0.2)
    pl, pr = pad(total)
    L += pl * 0.08 * duck; R += pr * 0.08 * duck
    peak = max(np.max(np.abs(L)), np.max(np.abs(R)), 1e-9)
    L = np.tanh(L / peak * 1.4) / np.tanh(1.4); R = np.tanh(R / peak * 1.4) / np.tanh(1.4)   # sanfte Begrenzung vor dem Mastern
    data = (np.stack([L, R], axis=1) * 0.7 * 32767).astype(np.int16)
    with wave.open(out, "wb") as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes(data.tobytes())
    print(f"ok {total:.1f} s, {len(tl['cues'])} Ereignisse")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
