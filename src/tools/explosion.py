"""Erzeugt eine Explosions-Bildfolge (RGBA, 256×256): klumpiger Feuerball aus mehreren Ballen,
Temperaturverlauf (weiß → gelb → orange → rot → Rauch), Rauch mit Licht von oben links (Volumen aus dem Dichtegradienten).
Aufruf: python -I explosion.py <Zielordner> [Bilder] [Seed]"""
import sys
from pathlib import Path
import numpy as np
from PIL import Image

N = 256
out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
frames = int(sys.argv[2]) if len(sys.argv) > 2 else 28
rng = np.random.default_rng(int(sys.argv[3]) if len(sys.argv) > 3 else 7)

def value_noise(size, cells):
    grid = rng.random((cells + 1, cells + 1)).astype(np.float32)
    img = Image.fromarray((grid * 255).astype(np.uint8)).resize((size, size), Image.BICUBIC)
    return np.asarray(img, dtype=np.float32) / 255.0

def fbm(octaves=6, base=4, gain=0.5):
    total = np.zeros((N, N), np.float32); amp = 1.0; norm = 0.0
    for o in range(octaves):
        total += amp * value_noise(N, base * 2 ** o); norm += amp; amp *= gain
    return total / norm

def billow(octaves=6, base=4):
    # „Blumenkohl“: Beträge um die Mitte → runde Ballen
    total = np.zeros((N, N), np.float32); amp = 1.0; norm = 0.0
    for o in range(octaves):
        n = value_noise(N, base * 2 ** o)
        total += amp * (1 - np.abs(2 * n - 1)); norm += amp; amp *= 0.52
    return total / norm

A, B = billow(), billow()
Dx, Dy = fbm(4, 3), fbm(4, 3)
yy, xx = np.mgrid[0:N, 0:N].astype(np.float32)
cx = cy = N / 2

# Ballen: Mittelpunkte wandern beim Wachsen nach außen
lobes = [(0, 0, 1.0)] + [(np.cos(a), np.sin(a) * 0.8, 0.62 + 0.2 * rng.random()) for a in rng.uniform(0, 2 * np.pi, 6)]

ramp_t = np.array([0.0, 0.2, 0.38, 0.58, 0.78, 1.0])
ramp_c = np.array([[52, 40, 34], [110, 36, 14], [205, 72, 16], [255, 146, 36], [255, 214, 120], [255, 251, 238]], np.float32)

def color(temp):
    return np.stack([np.interp(temp, ramp_t, ramp_c[:, i]) for i in range(3)], -1)

for f in range(frames):
    t = f / (frames - 1)
    grow = 1 - np.exp(-4.0 * t)
    blend = 0.5 - 0.5 * np.cos(np.pi * t)
    noise = A * (1 - blend) + B * blend
    rise = N * 0.14 * t * t
    wx = xx + (Dx - 0.5) * N * 0.16 * (0.5 + t)
    wy = yy + (Dy - 0.5) * N * 0.16 * (0.5 + t) + rise
    field = np.full((N, N), -10.0, np.float32)
    for (ox, oy, size) in lobes:
        r = N * (0.08 + 0.2 * grow) * size
        px = cx + ox * N * 0.16 * grow
        py = cy + oy * N * 0.14 * grow
        d = np.sqrt((wx - px) ** 2 + (wy - py) ** 2) / r
        field = np.maximum(field, 1 - d)
    mask = np.clip((field + 0.3) / 0.3, 0, 1)          # Rauschen nur nahe der Ballen
    edge = field + (noise - 0.55) * 0.75 * mask
    density = np.clip(edge * 2.8, 0, 1) * mask
    # Temperatur: Kern heiß, zum Rand und mit der Zeit kühler; Rauschen reißt Flammenzungen auf
    heat = np.clip(field * 1.25 + (noise - 0.5) * 1.5 - 0.1, 0, 1) * np.clip(1.25 - t * 1.75, 0, 1)
    temp = heat ** 0.9
    rgb = color(temp)
    # Volumen: Licht von oben links aus dem Dichtegradienten
    gy, gx = np.gradient(np.clip(edge, 0, 1) * 4)
    light = np.clip(0.62 + (-gx * -0.7 + -gy * -0.7) * 0.9, 0.25, 1.25)[..., None]
    smoke = np.clip(t * 1.5 - temp * 1.2, 0, 1)[..., None]
    smoke_rgb = np.array([96, 88, 82], np.float32) * light
    fire_rgb = rgb * np.clip(0.7 + (light - 0.62) * 0.6, 0.55, 1.15)[..., :] * (0.72 + 0.45 * noise)[..., None]
    rgb = fire_rgb * (1 - smoke) + smoke_rgb * smoke
    fade = np.clip(1.0 - (t - 0.5) / 0.5, 0, 1) ** 1.4
    alpha = density * fade
    alpha = np.maximum(alpha, np.clip(temp * 1.4, 0, 1) * density)
    rgba = np.dstack([np.clip(rgb, 0, 255), np.clip(alpha * 255, 0, 255)]).astype(np.uint8)
    Image.fromarray(rgba, "RGBA").save(out / f"explosion-{f:02d}.png", optimize=True)
print("ok", frames)
