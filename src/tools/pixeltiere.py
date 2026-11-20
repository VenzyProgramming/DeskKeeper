"""Pixelart-Tiere für DeskKeeper: Bengalkatze, Mops, Deutscher Schäferhund.

Seitenansicht mit Blick nach rechts (für links spiegeln), Bodenlinie G, feste Bildgröße je Tier.
Gezeichnet wird in einer kleinen Indexleinwand aus Kapseln, Ellipsen und Polygonen, schattiert
(oben hell, unten dunkel, Beine hinten dunkel) und mit Außenkontur. Die Beine sind Zwei-Glied-Gelenke
(IK), deshalb laufen Gangarten, Sitzen, Liegen und Springen aus denselben Teilen.

Aufruf: python -I pixeltiere.py <Zielordner>
Ergebnis: <tier>.png (Spritesheet, 1 Pixel = 1 Pixel), <tier>.json (Rahmen, fps, Anker),
          uebersicht-<tier>.png (vergrößert, beschriftet), groessen.png (alle drei nebeneinander)."""
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont


# ---------------------------------------------------------------- Grundlagen

def rgba(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


class Pal:
    def __init__(self, cols):
        self.names = ["_"] + list(cols)
        self.rgba = [(0, 0, 0, 0)] + [rgba(c) for c in cols.values()]
        self.idx = {n: i for i, n in enumerate(self.names)}

    def __getitem__(self, n):
        return self.idx[n]


def shift(m, dx, dy):
    """out[y, x] = m[y - dy, x - dx]"""
    out = np.zeros_like(m)
    h, w = m.shape
    out[max(dy, 0):h + min(dy, 0), max(dx, 0):w + min(dx, 0)] = m[max(-dy, 0):h + min(-dy, 0), max(-dx, 0):w + min(-dx, 0)]
    return out


def dil(m):
    return m | shift(m, 1, 0) | shift(m, -1, 0) | shift(m, 0, 1) | shift(m, 0, -1)


def add(p, q, k=1.0):
    return (p[0] + q[0] * k, p[1] + q[1] * k)


def lerp(a, b, t):
    return a + (b - a) * t


def smooth(u):
    u = min(max(u, 0.0), 1.0)
    return u * u * (3 - 2 * u)


class Cv:
    def __init__(self, w, h, pal):
        self.w, self.h, self.pal = w, h, pal
        self.a = np.zeros((h, w), np.int16)
        yy, xx = np.mgrid[0:h, 0:w]
        self.X = xx + 0.5
        self.Y = yy + 0.5
        self.anchors = {}

    # Formen als Masken
    def cap(self, p0, r0, p1, r1):
        x0, y0 = p0
        dx, dy = p1[0] - x0, p1[1] - y0
        l2 = dx * dx + dy * dy
        t = np.zeros_like(self.X) if l2 < 1e-9 else np.clip(((self.X - x0) * dx + (self.Y - y0) * dy) / l2, 0, 1)
        d = np.hypot(self.X - (x0 + t * dx), self.Y - (y0 + t * dy))
        return d <= r0 + (r1 - r0) * t

    def ell(self, c, rx, ry, ang=0.0):
        ca, sa = math.cos(math.radians(ang)), math.sin(math.radians(ang))
        u = (self.X - c[0]) * ca + (self.Y - c[1]) * sa
        v = -(self.X - c[0]) * sa + (self.Y - c[1]) * ca
        return (u / rx) ** 2 + (v / ry) ** 2 <= 1

    def poly(self, pts):
        inside = np.zeros(self.X.shape, bool)
        n = len(pts)
        for i in range(n):
            (x1, y1), (x2, y2) = pts[i], pts[(i + 1) % n]
            if y1 == y2:
                continue
            cond = ((y1 > self.Y) != (y2 > self.Y)) & (self.X < (x2 - x1) * (self.Y - y1) / (y2 - y1) + x1)
            inside ^= cond
        return inside

    def put(self, mask, col, where=None):
        if where is not None:
            mask = mask & where
        self.a[mask] = self.pal[col]

    def dot(self, x, y, col):
        xi, yi = int(math.floor(x)), int(math.floor(y))
        if 0 <= xi < self.w and 0 <= yi < self.h:
            self.a[yi, xi] = self.pal[col]

    def has(self, *names):
        return np.isin(self.a, [self.pal[n] for n in names])

    def filled(self):
        return self.a > 0

    def solid(self, m, dark, base, light, drows=1, back=False, lrows=1):
        """Fläche mit Unterseite dunkel (drows Zeilen), Oberkante hell; back=True: Hinterkante (links) dunkel."""
        below = m.copy()
        for i in range(1, drows + 1):
            below &= shift(m, 0, -i)
        top = m & ~shift(m, 0, 1)
        if lrows > 1:
            top |= m & ~shift(m, 0, 2)
        self.a[m] = self.pal[dark]
        inner = m & below
        if back:
            inner &= shift(m, 1, 0)
        self.a[inner] = self.pal[base]
        self.a[top & inner] = self.pal[light]

    def outline(self, col):
        f = self.filled()
        self.a[dil(f) & ~f] = self.pal[col]

    def image(self, scale=1):
        rgb = np.array(self.pal.rgba, np.uint8)[self.a]
        img = Image.fromarray(rgb, "RGBA")
        return img.resize((self.w * scale, self.h * scale), Image.NEAREST) if scale != 1 else img


def ik(a, b, l1, l2, bend):
    """Zwei-Glied-Bein von a nach b; bend>0: Gelenk vorn (+x), bend<0: Gelenk hinten."""
    dx, dy = b[0] - a[0], b[1] - a[1]
    d = math.hypot(dx, dy)
    dd = min(max(d, abs(l1 - l2) + 0.05), l1 + l2 - 0.05)
    base = math.atan2(dy, dx)
    cosA = (l1 * l1 + dd * dd - l2 * l2) / (2 * l1 * dd)
    A = math.acos(min(max(cosA, -1), 1))
    j1 = (a[0] + l1 * math.cos(base + A), a[1] + l1 * math.sin(base + A))
    j2 = (a[0] + l1 * math.cos(base - A), a[1] + l1 * math.sin(base - A))
    j = j1 if (j1[0] - j2[0]) * bend > 0 else j2
    e = (a[0] + dd * math.cos(base), a[1] + dd * math.sin(base))
    return j, e


def gait(t, phases, stance, stride, lift):
    out = {}
    for leg, ph in phases.items():
        p = (t + ph) % 1.0
        if p < stance:
            u = p / stance
            out[leg] = (stride * (0.5 - u), 0.0)
        else:
            u = (p - stance) / (1 - stance)
            out[leg] = (stride * (-0.5 + smooth(u)), -lift * math.sin(math.pi * u))
    return out


WALK = {"LH": 0.0, "LF": 0.25, "RH": 0.5, "RF": 0.75}
TROT = {"LF": 0.0, "RH": 0.0, "RF": 0.5, "LH": 0.5}
GALLOP = {"LH": 0.0, "RH": 0.1, "LF": 0.48, "RF": 0.6}


def chain(start, a0, curl, n, seg, wave=0.0, phase=0.0, hook=0.0):
    """Schwanz als Punktkette; Winkel in Grad, 0 = vorn (+x), 90 = unten, 180 = hinten, 270 = oben."""
    pts = [start]
    a = a0
    p = start
    for i in range(n):
        a += curl + wave * math.sin(phase + i * 0.9) * (i + 1) / n + (hook if i >= n - 2 else 0)
        p = (p[0] + seg * math.cos(math.radians(a)), p[1] + seg * math.sin(math.radians(a)))
        pts.append(p)
    return pts


def draw_leg(cv, anchor, target, l1, l2, bend, r1, r2, cols, paw, contour, solid_back=True):
    j, e = ik(anchor, target, l1, l2, bend)
    m = cv.cap(anchor, r1, j, (r1 + r2) / 2 + 0.15) | cv.cap(j, r2 + 0.1, e, r2)
    m |= cv.ell((e[0] + paw[0] * 0.45, e[1] + 0.1), paw[0], paw[1])
    if contour:
        o = dil(m) & ~m & cv.filled() & (cv.Y > anchor[1] + r1 * 0.6)
        cv.put(o, "ol")
    cv.solid(m, *cols, drows=1, back=solid_back)
    return m, j, e


# ---------------------------------------------------------------- Tiere

class Animal:
    name = ""
    W = H = G = 0
    fps = 10

    def __init__(self):
        self.pal = Pal(self.COLORS)

    def rest(self):
        raise NotImplementedError

    def draw(self, P):
        raise NotImplementedError

    def frame(self, P):
        cv = Cv(self.W, self.H, self.pal)
        self.cv = cv
        self.draw(P)
        cv.outline("ol")
        return cv

    def legs_from(self, P, offs):
        R = P["rest_paws"]
        P["legs"] = {k: (R[k][0] + offs.get(k, (0, 0))[0], R[k][1] + offs.get(k, (0, 0))[1]) for k in R}


def base_pose(chest, hip, head, paws, **kw):
    P = {"chest": chest, "hip": hip, "head": head, "rest_paws": dict(paws), "legs": dict(paws),
         "mouth": 0, "tongue": 0, "eyes": "open", "ears": 0, "tail": {}, "bob": 0.0, "sit": False, "lie": False,
         "carry": False}
    P.update(kw)
    return P


def moved(P, dx=0.0, dy=0.0, parts=("chest", "hip", "head")):
    Q = dict(P)
    for k in parts:
        Q[k] = (P[k][0] + dx, P[k][1] + dy)
    return Q


# ---------------------------------------------------------------- Bengalkatze

class Bengal(Animal):
    name = "bengal"
    W, H, G = 52, 40, 36
    COLORS = {
        "ol": "#2a170b", "base": "#d8953a", "light": "#efb763", "sh": "#a7651f", "sh2": "#7d4816",
        "belly": "#f3dcb0", "bellysh": "#d9b884", "ros": "#3b2210", "rosin": "#9a541c",
        "eye": "#9bd04f", "pupil": "#14140f", "nose": "#e0868a", "white": "#fff4e2", "ear": "#e8a99a",
    }
    L1, L2 = 6.2, 6.6
    ROSETTES = [(0.08, -0.55, 1), (0.22, -0.05, 2), (0.30, -0.70, 1), (0.42, -0.25, 2), (0.55, -0.75, 1),
                (0.60, 0.10, 0), (0.70, -0.35, 2), (0.84, -0.70, 1), (0.88, -0.05, 0), (0.15, 0.25, 0),
                (0.48, 0.30, 0), (0.36, 0.05, 0)]

    def rest(self):
        G = self.G
        return base_pose((33, 25), (17, 25.5), (40.5, 19.0),
                         {"LF": (35.5, G - 0.9), "RF": (33.0, G - 0.9), "LH": (17.0, G - 0.9), "RH": (14.8, G - 0.9)},
                         tail={"a0": 196, "curl": 9, "wave": 4, "phase": 0})

    def draw(self, P):
        cv = self.cv
        b = P["bob"]
        chest, hip, head = add(P["chest"], (0, b)), add(P["hip"], (0, b)), add(P["head"], (0, b))
        shoulder, hipa = add(chest, (-0.5, 1.2)), add(hip, (0.5, 0.8))
        far = ("sh2", "sh", "base")
        near = ("sh", "base", "light")
        legs = P["legs"]
        fl = P.get("front_bend", 1)
        # Beine der fernen Seite
        draw_leg(cv, add(hipa, (-1.2, 0)), legs["RH"], self.L1, self.L2, -1, 1.7, 0.95, far, (1.3, 0.9), False)
        draw_leg(cv, add(shoulder, (-1.2, 0)), legs["RF"], self.L1, self.L2, fl, 1.4, 0.9, far, (1.3, 0.9), False)
        # Schwanz
        T = P["tail"]
        pts = chain(add(hip, T.get("start", (-3.6, -1.4))), T.get("a0", 196), T.get("curl", 9), 8, 2.3, T.get("wave", 4), T.get("phase", 0), T.get("hook", 0))
        tm = np.zeros_like(cv.filled())
        rings = []
        for i in range(len(pts) - 1):
            r = 1.25 if i < 6 else 1.05
            seg = cv.cap(pts[i], r, pts[i + 1], r)
            tm |= seg
            rings.append(seg)
        cv.solid(tm, "sh", "base", "light")
        for i, seg in enumerate(rings):
            if i in (2, 4, 6):
                cv.put(seg & ~cv.cap(pts[i], 1.3, pts[i], 1.3) & cv.cap(pts[i + 1], 1.0, pts[i + 1], 1.0), "ros", tm)
        cv.put(rings[-1] & cv.cap(pts[-2], 0.0, pts[-1], 1.4) & ~cv.cap(pts[-2], 1.2, pts[-2], 1.2), "ros", tm)
        # Rumpf
        if P["sit"]:
            body = cv.cap(hip, 4.8, chest, 4.2) | cv.ell(add(hip, (0.6, 0.2)), 5.6, 4.9)
        elif P["lie"]:
            body = cv.cap(hip, 4.2, chest, 4.2)
        else:
            body = cv.cap(hip, 4.4, chest, 4.6) | cv.ell(add(chest, (0.8, 1.0)), 4.0, 4.4)
        cv.solid(body, "sh", "base", "light", drows=1)
        # Bauch hell
        ax, ay = hip
        bx, by = chest
        dx, dy = bx - ax, by - ay
        L = math.hypot(dx, dy)
        nx, ny = -dy / L, dx / L
        if ny < 0:
            nx, ny = -nx, -ny
        u = ((cv.X - ax) * dx + (cv.Y - ay) * dy) / (L * L)
        v = ((cv.X - ax) * nx + (cv.Y - ay) * ny) / 4.5
        cv.put(body & (v > 0.55) & (u > 0.12), "belly")
        cv.put(body & (v > 0.85) & (u > 0.12), "bellysh")
        # Rosetten
        for (ru, rv, kind) in self.ROSETTES:
            px = ax + dx * ru + nx * rv * 4.5
            py = ay + dy * ru + ny * rv * 4.5
            if kind == 0:
                cv.dot(px, py, "ros")
            elif kind == 1:
                cv.dot(px, py, "ros")
                cv.dot(px + 1, py, "ros")
            else:
                for ox, oy in ((0, 0), (1, 0), (2, 1), (0, 1), (1, 2)):
                    cv.dot(px + ox - 1, py + oy - 1, "ros")
                cv.dot(px + 0, py + 0, "rosin")
        cv.a[~body & (cv.a == self.pal["ros"])] = 0
        # Ohr hinten
        hx, hy = head
        ear_back = -1.5 if P["ears"] < 0 else 0
        far_ear = cv.poly([(hx - 3.6, hy - 1.8), (hx - 2.6 + ear_back, hy - 7.4), (hx - 0.2, hy - 3.4)])
        cv.solid(far_ear, "sh2", "sh", "sh")
        # Hals und Kopf
        neck = cv.cap(add(chest, (1.8, -1.6)), 3.4, add(head, (-1.2, 1.0)), 2.8)
        hm = cv.ell(head, 4.7, 4.0) | cv.ell(add(head, (3.6, 1.5)), 2.6, 2.0) | neck
        if P["mouth"]:
            hm |= cv.ell(add(head, (2.8, 3.4)), 2.0, 0.9)
        cv.solid(hm, "sh", "base", "light")
        cv.put(cv.ell(add(head, (3.2, 2.6)), 2.6, 1.1) & hm, "belly")
        cv.put(cv.ell(add(chest, (3.6, 1.2)), 2.6, 3.0) & hm & ~cv.ell(head, 4.7, 4.0), "belly")
        if P["mouth"]:
            cv.dot(hx + 4.6, hy + 2.8, "ros")
            cv.dot(hx + 3.6, hy + 3.0, "nose")
        # Ohr vorn
        near_ear = cv.poly([(hx - 1.8, hy - 2.8), (hx + 0.1 + ear_back, hy - 8.4), (hx + 2.6, hy - 2.8)])
        cv.solid(near_ear, "sh", "base", "light")
        cv.dot(hx + 0.3 + ear_back * 0.5, hy - 5.2, "ear")
        cv.dot(hx + 0.4, hy - 4.2, "ear")
        cv.dot(hx + 1.2, hy - 4.2, "ear")
        # Gesicht
        ex, ey = hx + 1.5, hy - 0.6
        if P["eyes"] == "closed":
            cv.dot(ex, ey, "ros")
            cv.dot(ex + 1, ey, "ros")
        else:
            cv.dot(ex, ey, "eye")
            cv.dot(ex + 1, ey, "pupil" if P["eyes"] != "wide" else "eye")
            if P["eyes"] == "wide":
                cv.dot(ex, ey + 1, "pupil")
                cv.dot(ex + 1, ey + 1, "eye")
            cv.dot(ex, ey - 1, "ros")
            cv.dot(ex + 1, ey - 1, "ros")
        cv.dot(ex - 1, ey + 0.4, "ros")
        cv.dot(ex - 2, ey + 1, "ros")
        cv.dot(hx - 1.0, hy - 2.6, "ros")
        cv.dot(hx - 2.2, hy - 1.8, "ros")
        cv.dot(hx + 0.4, hy - 2.9, "ros")
        cv.dot(hx - 2.4, hy + 0.4, "rosin")
        cv.dot(hx + 6.0, hy + 0.6, "nose")
        cv.dot(hx + 5.2, hy + 1.8, "white")
        self.cv.anchors["mouth"] = (hx + 5.0, hy + 2.8)
        # Beine vorn
        draw_leg(cv, hipa, legs["LH"], self.L1, self.L2, -1, 2.1, 1.0, near, (1.4, 0.95), True)
        m, j, e = draw_leg(cv, shoulder, legs["LF"], self.L1, self.L2, fl, 1.6, 0.95, near, (1.4, 0.95), True)
        for k in (0.35, 0.6):
            cv.dot(lerp(shoulder[0], j[0], k * 2) if k < 0.5 else lerp(j[0], e[0], k), lerp(shoulder[1], j[1], k * 2) if k < 0.5 else lerp(j[1], e[1], k), "ros")
        cv.anchors["paw"] = e

    # ---- Bewegungen
    def anims(self):
        R = self.rest()
        A = {}

        def tail(P, **kw):
            T = dict(P["tail"])
            T.update(kw)
            P["tail"] = T

        idle = []
        for k in range(6):
            P = dict(R)
            tail(P, phase=k / 6 * 2 * math.pi, wave=7)
            P["eyes"] = "closed" if k == 4 else "open"
            P["ears"] = -1 if k == 2 else 0
            idle.append(P)
        A["idle"] = idle

        walk = []
        for k in range(8):
            t = k / 8
            P = moved(R, 0, 0.45 * math.cos(4 * math.pi * t))
            self.legs_from(P, gait(t, WALK, 0.6, 7.0, 2.2))
            tail(P, phase=t * 2 * math.pi, wave=3)
            P["head"] = add(P["head"], (0, 0.3 * math.sin(4 * math.pi * t)))
            walk.append(P)
        A["walk"] = walk

        run = []
        for k in range(6):
            t = k / 6
            s = math.sin(2 * math.pi * (t - 0.05))
            P = dict(R)
            P["chest"] = add(R["chest"], (1.8 * s, -1.2 * max(0, s)))
            P["hip"] = add(R["hip"], (-1.8 * s, -0.6 * max(0, -s)))
            P["head"] = add(R["head"], (1.8 * s + 0.6, 1.0 - 0.8 * max(0, s)))
            P["bob"] = -1.3 * abs(math.sin(2 * math.pi * t))
            self.legs_from(P, gait(t, GALLOP, 0.38, 13.0, 3.6))
            P["ears"] = -1
            tail(P, a0=184, curl=3, wave=6, phase=t * 6.28)
            run.append(P)
        A["run"] = run

        stalk = []
        for k in range(8):
            t = k / 8
            P = moved(R, 0, 4.2)
            P["head"] = add(R["head"], (1.6, 5.4))
            self.legs_from(P, gait(t, WALK, 0.7, 4.0, 1.4))
            P["rest_paws"] = R["rest_paws"]
            tail(P, a0=184, curl=-1.5, wave=3, phase=t * 6.28)
            P["eyes"] = "wide"
            P["ears"] = 0
            stalk.append(P)
        A["stalk"] = stalk

        wig = []
        for k in range(4):
            P = dict(R)
            sx = (0.9 if k % 2 else -0.9)
            P["chest"] = add(R["chest"], (0, 5.2))
            P["hip"] = add(R["hip"], (sx, 3.2))
            P["head"] = add(R["head"], (1.4, 6.2))
            P["legs"] = dict(R["rest_paws"])
            P["legs"]["LH"] = add(R["rest_paws"]["LH"], (sx, 0))
            P["legs"]["RH"] = add(R["rest_paws"]["RH"], (sx, 0))
            tail(P, a0=182, curl=-1, wave=6, phase=k * 1.6)
            P["eyes"] = "wide"
            wig.append(P)
        A["wiggle"] = wig

        G = self.G
        pounce = []
        specs = [  # chest, hip, head offsets, paws (absolute), bob
            ((2, -4.5), (0, 0.6), (3.5, -6.5), {"LF": (44, G - 7), "RF": (42, G - 5), "LH": (13, G - 0.9), "RH": (11, G - 0.9)}),
            ((4, -9), (2, -6), (6, -11), {"LF": (48.5, G - 10.5), "RF": (47, G - 9.5), "LH": (10, G - 4), "RH": (8.5, G - 3)}),
            ((5, -6), (3, -7.5), (7, -6.5), {"LF": (49, G - 2.5), "RF": (47.5, G - 2), "LH": (14, G - 6), "RH": (12.5, G - 5.5)}),
            ((4, 2.5), (2.5, -2.5), (6.5, 4.5), {"LF": (46, G - 0.9), "RF": (44.5, G - 0.9), "LH": (21, G - 2.5), "RH": (19, G - 2.0)}),
            ((3.5, 4.5), (2.5, 1.2), (6.0, 6.0), {"LF": (45.5, G - 0.9), "RF": (44.0, G - 0.9), "LH": (21.5, G - 0.9), "RH": (19.5, G - 0.9)}),
        ]
        for i, (c, h, hd, paws) in enumerate(specs):
            P = dict(R)
            P["chest"], P["hip"], P["head"] = add(R["chest"], c), add(R["hip"], h), add(R["head"], hd)
            P["legs"] = paws
            P["eyes"] = "wide"
            P["ears"] = -1 if i in (1, 2) else 0
            tail(P, a0=[200, 192, 186, 210, 200][i], curl=[3, -2, -2, 6, 7][i], wave=2, phase=i)
            pounce.append(P)
        A["pounce"] = pounce

        sit = []
        for k in range(4):
            P = self.sitpose(R)
            tail(P, phase=k * 1.57, wave=5)
            P["eyes"] = "closed" if k == 3 else "open"
            sit.append(P)
        A["sit"] = sit

        swat = []
        for k, (dx, dy) in enumerate([(0, 0), (4.0, -6.5), (8.5, -1.0), (7.0, 0.0)]):
            P = self.sitpose(R)
            sh = add(P["chest"], (-0.5, 1.2))
            if k:
                P["legs"]["LF"] = add(sh, (dx, dy + 8.5 if k == 2 else dy + 2)) if k != 3 else (sh[0] + 8.0, G - 0.9)
                P["front_bend"] = 1
            P["head"] = add(P["head"], (1.0, 1.0 if k >= 2 else 0))
            P["eyes"] = "wide"
            tail(P, phase=k, wave=6)
            swat.append(P)
        A["swat"] = swat
        return A

    def sitpose(self, R):
        G = self.G
        P = dict(R)
        P["sit"] = True
        P["hip"] = (21.5, G - 5.0)
        P["chest"] = (27.8, G - 13.0)
        P["head"] = (32.8, G - 20.0)
        P["legs"] = {"LF": (30.5, G - 0.9), "RF": (29.0, G - 0.9), "LH": (26.0, G - 0.9), "RH": (24.5, G - 0.9)}
        P["rest_paws"] = dict(P["legs"])
        P["tail"] = {"start": (-4.5, 1.0), "a0": 120, "curl": -15, "wave": 3, "phase": 0}
        return P


# ---------------------------------------------------------------- Mops

class Pug(Animal):
    name = "mops"
    W, H, G = 46, 34, 31
    COLORS = {
        "ol": "#2b1d13", "base": "#e2c08c", "light": "#f3dcb0", "sh": "#b98f5a", "sh2": "#8d6a40",
        "mask": "#2a2320", "maskl": "#4e443e", "eye": "#140f0c", "white": "#ffffff", "tongue": "#e8788a",
        "wr": "#a37b4b",
    }
    L1, L2 = 3.6, 3.9

    def rest(self):
        G = self.G
        return base_pose((25.5, 20.5), (15.0, 20.5), (31.5, 13.5),
                         {"LF": (27.2, G - 0.9), "RF": (25.0, G - 0.9), "LH": (15.2, G - 0.9), "RH": (13.2, G - 0.9)},
                         tail={"a0": 0}, tongue=0)

    def draw(self, P):
        cv = self.cv
        b = P["bob"]
        chest, hip, head = add(P["chest"], (0, b)), add(P["hip"], (0, b)), add(P["head"], (0, b))
        shoulder, hipa = add(chest, (0.5, 2.6)), add(hip, (0, 2.4))
        far = ("sh2", "sh", "base")
        near = ("sh", "base", "light")
        legs = P["legs"]
        if not P["lie"]:
            draw_leg(cv, add(hipa, (-1.2, 0)), legs["RH"], self.L1, self.L2, -1, 1.9, 1.3, far, (1.5, 1.0), False)
            draw_leg(cv, add(shoulder, (-1.4, 0)), legs["RF"], self.L1, self.L2, 1, 1.7, 1.3, far, (1.5, 1.0), False)
        # Rumpf
        if P["sit"]:
            body = cv.cap(hip, 5.8, chest, 5.4) | cv.ell(add(hip, (0.4, 0.4)), 6.4, 5.6)
        else:
            body = cv.cap(hip, 5.7, chest, 6.0) | cv.ell(add(chest, (0.5, 0.8)), 5.6, 5.6)
        cv.solid(body, "sh", "base", "light", drows=2)
        # Falte am Hals, Bauchschatten
        # Ringelschwanz
        tx, ty = add(hip, (-1.6, -5.3)) if not P["sit"] else add(hip, (-3.5, 2.0))
        ring = cv.ell((tx, ty), 2.6, 2.3) & ~cv.ell((tx + 0.3, ty + 0.2), 1.0, 0.8)
        cv.put(dil(ring) & ~ring & ~body, "ol")
        cv.solid(ring, "sh", "base", "light")
        cv.dot(tx + 0.3, ty + 0.2, "sh2")
        # Kopf (Dreiviertelansicht)
        hx, hy = head
        flap = P.get("flap", 0.0)
        far_ear = cv.poly([(hx + 3.2, hy - 4.4), (hx + 6.4, hy - 4.6 - flap), (hx + 6.6, hy - 1.6)])
        cv.put(far_ear, "mask")
        hm = cv.ell(head, 6.2, 5.5)
        cv.put(dil(hm) & ~hm & body, "ol")
        cv.solid(hm, "sh", "base", "light", drows=1)
        # Maske
        mx, my = hx + 3.0, hy + 1.8
        mask = cv.ell((mx, my), 3.4, 3.1) & hm
        cv.put(mask, "mask")
        cv.put(cv.ell((mx - 0.3, my - 1.6), 2.0, 0.8) & mask, "maskl")
        # Augen
        for ex, ey in ((hx + 0.2, hy - 1.6), (hx + 4.4, hy - 1.7)):
            cv.put(cv.ell((ex + 0.5, ey + 0.5), 1.6, 1.5) & hm, "mask")
            if P["eyes"] == "closed":
                cv.dot(ex, ey + 0.6, "eye")
                cv.dot(ex + 1, ey + 0.6, "eye")
                cv.dot(ex - 1, ey + 0.6, "mask")
            else:
                for ox in (0, 1):
                    for oy in (0, 1):
                        cv.dot(ex + ox, ey + oy, "eye")
                cv.dot(ex, ey, "white")
        # Falten auf der Stirn
        for ox, oy in ((1.8, -3.6), (2.8, -4.0), (3.8, -3.6), (2.3, -2.9), (3.3, -2.9)):
            cv.dot(hx + ox, hy + oy, "wr")
        # Nase und Maul
        cv.dot(mx - 0.4, my - 0.6, "eye")
        cv.dot(mx + 0.6, my - 0.6, "eye")
        cv.dot(mx - 0.4, my - 1.4, "maskl")
        if P["mouth"] or P["tongue"]:
            cv.put(cv.ell((mx + 0.2, my + 2.4), 1.4, 1.2), "tongue")
            cv.dot(mx - 0.6, my + 1.8, "mask")
        cv.anchors["mouth"] = (mx + 0.4, my + 2.6)
        # Ohr vorn
        near_ear = cv.poly([(hx - 5.0, hy - 3.0), (hx - 2.0, hy - 5.8 - flap), (hx - 1.4, hy - 2.6), (hx - 3.6, hy - 0.4 - flap * 0.5)])
        cv.put(dil(near_ear) & ~near_ear & hm, "ol")
        cv.put(near_ear, "mask")
        cv.dot(hx - 2.6, hy - 4.2 - flap, "maskl")
        # Beine nah
        if P["lie"]:
            draw_leg(cv, add(hipa, (0, -1.5)), legs["LH"], self.L1, self.L2, -1, 2.0, 1.4, near, (1.6, 1.0), True)
            draw_leg(cv, add(shoulder, (0, -1.5)), legs["LF"], self.L1, self.L2, 1, 1.8, 1.4, near, (1.6, 1.0), True)
        else:
            draw_leg(cv, hipa, legs["LH"], self.L1, self.L2, -1, 2.2, 1.4, near, (1.6, 1.0), True)
            m, j, e = draw_leg(cv, shoulder, legs["LF"], self.L1, self.L2, 1, 1.9, 1.4, near, (1.6, 1.0), True)
        cv.anchors["belly"] = (lerp(hip[0], chest[0], 0.5), self.G)

    def anims(self):
        R = self.rest()
        G = self.G
        A = {}
        idle = []
        for k in range(4):
            P = moved(R, 0, 0.5 if k in (1, 2) else 0)
            P["tongue"] = 1 if k % 2 == 0 else 0
            P["head"] = add(P["head"], (0, 0.4 * (k % 2)))
            idle.append(P)
        A["idle"] = idle

        walk = []
        for k in range(8):
            t = k / 8
            P = moved(R, 0, 0.6 * math.cos(4 * math.pi * t))
            P["head"] = add(P["head"], (0, 0.4 * math.cos(4 * math.pi * t + 0.8)))
            self.legs_from(P, gait(t, WALK, 0.62, 4.6, 1.8))
            P["flap"] = 0.6 * math.sin(4 * math.pi * t)
            walk.append(P)
        A["walk"] = walk

        run = []
        for k in range(6):
            t = k / 6
            s = math.sin(2 * math.pi * (t - 0.05))
            P = dict(R)
            P["chest"] = add(R["chest"], (1.2 * s, -0.8 * max(0, s)))
            P["hip"] = add(R["hip"], (-1.2 * s, -0.4 * max(0, -s)))
            P["head"] = add(R["head"], (1.2 * s, 0.6 - 0.6 * max(0, s)))
            P["bob"] = -1.6 * abs(math.sin(2 * math.pi * t))
            self.legs_from(P, gait(t, GALLOP, 0.4, 8.0, 2.6))
            P["tongue"] = 1
            P["flap"] = 1.4 * s
            run.append(P)
        A["run"] = run

        sniff = []
        for k in range(4):
            P = dict(R)
            P["head"] = add(R["head"], (1.8 + (0.5 if k % 2 else 0), 4.6))
            P["chest"] = add(R["chest"], (0, 1.2))
            P["eyes"] = "open"
            sniff.append(P)
        A["sniff"] = sniff

        carry = []
        for P in walk:
            Q = dict(P)
            Q["mouth"] = 1
            Q["tongue"] = 0
            Q["carry"] = True
            carry.append(Q)
        A["carry"] = carry

        sploot = []
        for k in range(4):
            P = dict(R)
            P["lie"] = True
            br = 0.4 if k in (1, 2) else 0.0
            P["chest"] = (24.5, G - 5.6 - br)
            P["hip"] = (14.5, G - 5.3 - br)
            P["head"] = (31.5, G - 6.2)
            P["legs"] = {"LF": (35.5, G - 0.9), "RF": (34, G - 0.9), "LH": (4.5, G - 1.0), "RH": (6, G - 1.2)}
            P["eyes"] = "closed" if k >= 2 else "open"
            P["tongue"] = 1 if k == 1 else 0
            sploot.append(P)
        A["sploot"] = sploot

        sit = []
        for k in range(2):
            P = dict(R)
            P["sit"] = True
            P["hip"] = (17.0, G - 6.0)
            P["chest"] = (22.0, G - 12.5)
            P["head"] = (27.0, G - 19.5 + 0.4 * k)
            P["legs"] = {"LF": (26.0, G - 0.9), "RF": (24.0, G - 0.9), "LH": (21.5, G - 0.9), "RH": (19.5, G - 0.9)}
            P["tongue"] = k
            sit.append(P)
        A["sit"] = sit
        return A


# ---------------------------------------------------------------- Deutscher Schäferhund

class Shepherd(Animal):
    name = "schaeferhund"
    W, H, G = 70, 48, 45
    COLORS = {
        "ol": "#160f0b", "base": "#c9843f", "light": "#e7a95f", "sh": "#93582a", "sh2": "#6c3f1d",
        "blk": "#262019", "blkl": "#463b32", "blksh": "#161210", "eye": "#5a3213", "nose": "#0e0c0b",
        "tongue": "#e0707e", "white": "#f6e7d0",
    }
    L1, L2 = 7.6, 9.0

    def rest(self):
        G = self.G
        return base_pose((47, 25), (25, 27.0), (57, 14.0),
                         {"LF": (49.5, G - 0.9), "RF": (46.8, G - 0.9), "LH": (22.5, G - 0.9), "RH": (20.0, G - 0.9)},
                         tail={"a0": 132, "curl": -5, "wave": 3, "phase": 0, "hook": 22})

    def draw(self, P):
        cv = self.cv
        b = P["bob"]
        chest, hip, head = add(P["chest"], (0, b)), add(P["hip"], (0, b)), add(P["head"], (0, b))
        shoulder, hipa = add(chest, (0.2, 2.0)), add(hip, (0.5, 1.2))
        far = ("sh2", "sh", "base")
        near = ("sh", "base", "light")
        legs = P["legs"]
        draw_leg(cv, add(hipa, (-1.5, 0)), legs["RH"], self.L1, self.L2, -1, 2.8, 1.35, far, (1.8, 1.05), False)
        draw_leg(cv, add(shoulder, (-1.5, 0)), legs["RF"], self.L1, self.L2, 1, 2.2, 1.3, far, (1.8, 1.05), False)
        # Rute: buschig, hängend
        T = P["tail"]
        pts = chain(add(hip, (-4.6, -2.2)), T.get("a0", 140), T.get("curl", -5), 7, 2.7, T.get("wave", 3), T.get("phase", 0), T.get("hook", 22))
        tm = np.zeros_like(cv.filled())
        tops = np.zeros_like(tm)
        for i in range(len(pts) - 1):
            r0 = 1.6 + 0.9 * math.sin(math.pi * min(1, (i + 0.5) / 5.5))
            r1 = 1.6 + 0.9 * math.sin(math.pi * min(1, (i + 1.5) / 5.5))
            if i >= 5:
                r1 = 1.2
            tm |= cv.cap(pts[i], r0, pts[i + 1], r1)
        cv.solid(tm, "sh", "base", "light")
        # Oberseite der Rute schwarz
        cv.put(tm & ~shift(tm, 0, 2), "blk")
        cv.put(tm & ~shift(tm, 0, 1), "blkl")
        cv.put(cv.cap(pts[-2], 1.5, pts[-1], 1.4) & tm, "blk")
        # Rumpf
        if P["lie"]:
            body = cv.cap(hip, 6.4, chest, 6.8) | cv.ell(add(hip, (0.5, 0.2)), 7.4, 6.2)
        elif P["sit"]:
            body = cv.cap(hip, 6.4, chest, 6.6) | cv.ell(add(hip, (0, 0.6)), 7.0, 6.2)
        else:
            body = cv.cap(hip, 6.0, chest, 6.8) | cv.ell(add(chest, (1.2, 1.4)), 6.4, 6.8) | cv.ell(add(hip, (1.0, -0.4)), 6.4, 5.6)
        cv.solid(body, "sh", "base", "light", drows=2)
        # Sattel
        ax, ay = hip
        bx, by = chest
        dx, dy = bx - ax, by - ay
        L = math.hypot(dx, dy)
        nx, ny = -dy / L, dx / L
        if ny < 0:
            nx, ny = -nx, -ny
        u = ((cv.X - ax) * dx + (cv.Y - ay) * dy) / (L * L)
        v = ((cv.X - ax) * nx + (cv.Y - ay) * ny) / 6.3
        edge = 0.15 + 0.12 * np.sin(u * 19.0)
        saddle = body & (v < edge) & (u > -0.35) & (u < 0.86 + 0.08 * np.sin(v * 5))
        cv.put(saddle, "blk")
        cv.put(saddle & ~shift(body, 0, 1), "blkl")
        cv.put(saddle & ~shift(body, 0, 2) & (np.sin(cv.X * 1.7) > 0.3), "blkl")
        # Ohr hinten
        hx, hy = head
        eb = -2.5 if P["ears"] < 0 else 0.0
        far_ear = cv.poly([(hx - 3.6, hy - 2.0), (hx - 3.4 + eb, hy - 9.6 - eb * 0.4), (hx - 0.6, hy - 3.6)])
        cv.solid(far_ear, "blksh", "blk", "blk")
        # Hals und Kopf
        neck = cv.cap(add(chest, (2.4, -2.6)), 5.0, add(head, (-1.6, 1.6)), 3.4)
        hm = neck | cv.ell(head, 4.6, 4.0)
        jaw = P["mouth"]
        muzzle = cv.cap(add(head, (2.0, 0.6)), 2.6, add(head, (7.6, 1.2)), 1.5)
        lower = cv.cap(add(head, (2.0, 2.4)), 1.8, add(head, (6.6, 2.8 + 1.6 * jaw)), 1.0)
        hm |= muzzle | lower
        cv.solid(hm, "sh", "base", "light")
        cv.put(neck & ~cv.ell(head, 4.6, 4.0) & (cv.Y < lerp(chest[1] - 6, head[1] + 1, 0.5) + (cv.X - head[0]) * 0.2), "blk")
        cv.put(neck & ~shift(hm, 0, 1), "blkl")
        # Maske: Fang oben schwarz, um die Augen dunkel
        cv.put(muzzle & (cv.Y < hy + 1.4) & (cv.X > hx + 2.5), "blk")
        cv.put(muzzle & (cv.X > hx + 6.2), "blk")
        cv.put(cv.ell((hx + 2.2, hy - 0.4), 2.0, 1.2) & hm, "blk")
        cv.put(cv.ell((hx - 0.8, hy - 2.8), 2.6, 1.3) & hm, "blkl")
        if jaw:
            cv.put(cv.cap(add(head, (3.0, 2.0)), 0.6, add(head, (6.8, 2.6 + 0.8 * jaw)), 0.6), "blksh")
        if P["tongue"]:
            cv.put(cv.ell((hx + 4.4, hy + 4.4), 1.3, 1.7), "tongue")
        cv.dot(hx + 8.6, hy + 0.6, "nose")
        cv.dot(hx + 8.6, hy + 1.6, "nose")
        cv.dot(hx + 7.8, hy + 0.3, "blkl")
        if P["eyes"] == "closed":
            cv.dot(hx + 1.6, hy - 0.4, "blksh")
            cv.dot(hx + 2.6, hy - 0.4, "blksh")
        else:
            cv.dot(hx + 1.8, hy - 0.8, "eye")
            cv.dot(hx + 2.8, hy - 0.8, "nose")
        cv.dot(hx + 1.8, hy - 1.8, "base")
        cv.anchors["mouth"] = (hx + 6.0, hy + 3.4)
        # Ohr vorn
        near_ear = cv.poly([(hx - 1.8, hy - 2.6), (hx + 0.2 + eb, hy - 11.0 - eb * 0.3), (hx + 2.8, hy - 2.8)])
        cv.solid(near_ear, "blk", "blkl", "blkl")
        inner = cv.poly([(hx - 0.4, hy - 3.4), (hx + 0.4 + eb, hy - 8.4), (hx + 1.6, hy - 3.6)])
        cv.put(inner & near_ear, "sh")
        # Beine nah
        draw_leg(cv, hipa, legs["LH"], self.L1, self.L2, -1, 3.2, 1.45, near, (1.9, 1.05), True)
        m, j, e = draw_leg(cv, shoulder, legs["LF"], self.L1, self.L2, 1, 2.5, 1.4, near, (1.9, 1.05), True)
        cv.anchors["paw"] = e

    def anims(self):
        R = self.rest()
        G = self.G
        A = {}

        def tail(P, **kw):
            T = dict(P["tail"])
            T.update(kw)
            P["tail"] = T

        idle = []
        for k in range(4):
            P = moved(R, 0, 0.5 if k in (1, 2) else 0)
            P["tongue"] = 1
            P["mouth"] = 1
            tail(P, phase=k * 1.57, wave=3)
            idle.append(P)
        A["idle"] = idle

        trot = []
        for k in range(8):
            t = k / 8
            P = moved(R, 0, -0.7 * abs(math.sin(2 * math.pi * t)))
            self.legs_from(P, gait(t, TROT, 0.5, 11.0, 3.2))
            P["head"] = add(P["head"], (1.0, 1.4))
            tail(P, a0=145, curl=-4, phase=t * 6.28, wave=4)
            trot.append(P)
        A["trot"] = trot

        run = []
        for k in range(6):
            t = k / 6
            s = math.sin(2 * math.pi * (t - 0.05))
            P = dict(R)
            P["chest"] = add(R["chest"], (2.4 * s, -1.4 * max(0, s)))
            P["hip"] = add(R["hip"], (-2.4 * s, -0.8 * max(0, -s)))
            P["head"] = add(R["head"], (2.4 * s + 1.5, 3.0 - 1.0 * max(0, s)))
            P["bob"] = -2.0 * abs(math.sin(2 * math.pi * t))
            self.legs_from(P, gait(t, GALLOP, 0.36, 19.0, 5.0))
            P["ears"] = -1
            P["mouth"] = 1
            P["tongue"] = 1
            tail(P, a0=178, curl=-2, hook=8, wave=5, phase=t * 6.28)
            run.append(P)
        A["run"] = run

        dig = []
        for k in range(6):
            P = dict(R)
            P["chest"] = add(R["chest"], (0, 4.5))
            P["head"] = add(R["head"], (2.5, 14.0))
            P["hip"] = add(R["hip"], (0, -0.5))
            sh = add(P["chest"], (0.2, 2.0))
            a = k % 2
            P["legs"] = dict(R["rest_paws"])
            P["legs"]["LF"] = (sh[0] + (3.5 if a else -3.0), G - (0.9 if a else 2.8))
            P["legs"]["RF"] = (sh[0] - 1.5 + (-3.0 if a else 3.5), G - (2.8 if a else 0.9))
            P["legs"]["LH"] = add(R["rest_paws"]["LH"], (-1.5, 0))
            P["legs"]["RH"] = add(R["rest_paws"]["RH"], (-1.5, 0))
            tail(P, a0=215, curl=4, hook=10, wave=10, phase=k * 2.1)
            dig.append(P)
        A["dig"] = dig

        carry = []
        for P in trot:
            Q = dict(P)
            Q["mouth"] = 0.6
            Q["carry"] = True
            Q["head"] = add(P["head"], (0, -1.2))
            carry.append(Q)
        A["carry"] = carry

        shake = []
        for k in range(6):
            P = dict(R)
            dx = [-1.6, 1.6, -1.2, 1.2, -0.6, 0][k]
            P["head"] = add(R["head"], (dx, 0.5 * (k % 2)))
            P["mouth"] = 0.6
            P["carry"] = True
            P["ears"] = -1 if k < 4 else 0
            P["eyes"] = "closed" if k in (1, 2) else "open"
            tail(P, phase=k * 2, wave=6)
            shake.append(P)
        A["shake"] = shake

        bark = []
        for k in range(2):
            P = dict(R)
            P["head"] = add(R["head"], (0.5, -1.0 * k))
            P["mouth"] = 1.6 if k else 0
            P["chest"] = add(R["chest"], (0, -0.5 * k))
            tail(P, a0=185, curl=2, hook=12, wave=6, phase=k * 3)
            bark.append(P)
        A["bark"] = bark

        lie = []
        for k in range(4):
            P = dict(R)
            P["lie"] = True
            br = 0.5 if k in (1, 2) else 0.0
            P["chest"] = (45.5, G - 7.0 - br)
            P["hip"] = (24.0, G - 6.2 - br * 0.6)
            P["head"] = (55.0, G - 19.0 - br * 0.5)
            P["legs"] = {"LF": (61.5, G - 0.9), "RF": (59.5, G - 1.2), "LH": (33.5, G - 0.9), "RH": (31.5, G - 1.2)}
            P["mouth"] = 1 if k % 2 else 0.4
            P["tongue"] = 1 if k % 2 else 0
            tail(P, a0=168, curl=2, hook=6, wave=2, phase=k)
            lie.append(P)
        A["lie"] = lie
        return A


ANIMALS = [Bengal, Pug, Shepherd]


# ---------------------------------------------------------------- Ausgabe

def render_all(animal):
    A = animal.anims()
    out = {}
    for name, poses in A.items():
        frames = []
        for P in poses:
            cv = animal.frame(P)
            frames.append((cv.image(), dict(cv.anchors), bool(P.get("carry"))))
        out[name] = frames
    return out


def font(size):
    for f in ("C:/Windows/Fonts/segoeui.ttf", "C:/Windows/Fonts/arial.ttf"):
        try:
            return ImageFont.truetype(f, size)
        except OSError:
            pass
    return ImageFont.load_default()


def export(dst):
    dst.mkdir(parents=True, exist_ok=True)
    sizes = []
    for cls in ANIMALS:
        an = cls()
        R = render_all(an)
        rows = list(R.items())
        cols = max(len(f) for _, f in rows)
        sheet = Image.new("RGBA", (cols * an.W, len(rows) * an.H), (0, 0, 0, 0))
        meta = {"frame": [an.W, an.H], "pivot": [an.W // 2, an.G], "fps": an.fps, "facing": "right", "animations": {}}
        for r, (name, frames) in enumerate(rows):
            meta["animations"][name] = {"row": r, "frames": len(frames),
                                        "anchors": [{k: [round(v[0], 1), round(v[1], 1)] for k, v in a.items()} for _, a, _ in frames],
                                        "carry": any(c for _, _, c in frames)}
            for c, (img, _, _) in enumerate(frames):
                sheet.paste(img, (c * an.W, r * an.H))
        sheet.save(dst / f"{an.name}.png")
        (dst / f"{an.name}.json").write_text(json.dumps(meta, indent=1), encoding="utf-8")
        # Übersicht
        S = 5
        lab = 150
        ov = Image.new("RGB", (lab + cols * an.W * S, len(rows) * an.H * S), (238, 236, 230))
        d = ImageDraw.Draw(ov)
        f = font(20)
        for r, (name, frames) in enumerate(rows):
            y0 = r * an.H * S
            if r % 2:
                d.rectangle([0, y0, ov.width, y0 + an.H * S - 1], fill=(228, 226, 219))
            d.text((12, y0 + an.H * S // 2 - 12), name, fill=(40, 40, 40), font=f)
            for c, (img, _, _) in enumerate(frames):
                x0 = lab + c * an.W * S
                d.line([x0, y0 + an.G * S + S, x0 + an.W * S - 8, y0 + an.G * S + S], fill=(190, 186, 176), width=2)
                big = img.resize((an.W * S, an.H * S), Image.NEAREST)
                ov.paste(big, (x0, y0), big)
        ov.save(dst / f"uebersicht-{an.name}.png")
        sizes.append((an, R["idle"][0][0]))
    # Größenvergleich mit einem 16-px-Symbol
    S = 6
    W = sum(a.W for a, _ in sizes) * S + 260
    H = max(a.H for a, _ in sizes) * S + 40
    img = Image.new("RGB", (W, H), (238, 236, 230))
    d = ImageDraw.Draw(img)
    x = 20
    base = H - 20
    for a, fr in sizes:
        big = fr.resize((a.W * S, a.H * S), Image.NEAREST)
        img.paste(big, (x, base - a.G * S - S), big)
        x += a.W * S + 20
    icon = Image.open(dst.parent / "symbol-ordner.png") if (dst.parent / "symbol-ordner.png").exists() else None
    d.rectangle([x + 40, base - 16 * S, x + 40 + 16 * S, base], outline=(80, 120, 200), width=3)
    d.text((x + 40, base - 16 * S - 30), "Symbol 16 px", fill=(40, 40, 40), font=font(18))
    d.line([0, base, W, base], fill=(170, 165, 155), width=2)
    img.save(dst / "groessen.png")


# ---------------------------------------------------------------- Zubehör: Hundekorb, Erdhügel

EXTRA_COLORS = {
    "ol": "#1f1712", "rim": "#5d7193", "riml": "#8299bd", "rimd": "#3e4c66", "cush": "#d8c8aa", "cushl": "#ebdfc6", "cushd": "#b7a382",
    "dirt": "#7b5230", "dirtl": "#9c6e43", "dirtd": "#573820", "clod": "#40281a", "peb": "#b4936a",
}
BED = (40, 18)
MOUND = (44, 16)
MOUND_STAGES = 6


def bed_image():
    cv = Cv(*BED, Pal(EXTRA_COLORS))
    back = cv.ell((20, 9), 19, 7.6)
    cv.solid(back, "rimd", "rim", "riml")
    inner = cv.ell((20, 9.6), 14.8, 4.7)
    cv.solid(inner, "cushd", "cush", "cushl")
    front = cv.ell((20, 11.2), 19, 6.2) & ~cv.ell((20, 9.4), 15.6, 4.9) & (cv.Y > 10.5)
    cv.solid(front, "rimd", "rim", "riml")
    cv.outline("ol")
    return cv.image()


def mound_image(stage):
    cv = Cv(*MOUND, Pal(EXTRA_COLORS))
    s = stage / (MOUND_STAGES - 1)
    rx, ry = 6 + 15 * s, 2.2 + 8.8 * s
    m = cv.ell((22, 14.6), rx, ry) & (cv.Y < 14.8)
    cv.solid(m, "dirtd", "dirt", "dirtl", drows=1)
    rnd = np.random.default_rng(5)
    for _ in range(int(6 + 26 * s)):
        x, y = rnd.uniform(22 - rx, 22 + rx), rnd.uniform(14.6 - ry, 14.6)
        cv.dot(x, y, "clod" if rnd.random() < 0.6 else "peb")
    cv.a[~m & (cv.a > 0)] = 0
    cv.outline("ol")
    return cv.image()


def export_extras(dst):
    img = Image.new("RGBA", (max(BED[0], MOUND[0] * MOUND_STAGES), BED[1] + MOUND[1]), (0, 0, 0, 0))
    b = bed_image()
    img.paste(b, (0, 0), b)
    for i in range(MOUND_STAGES):
        m = mound_image(i)
        img.paste(m, (i * MOUND[0], BED[1]), m)
    img.save(dst / "tiere-extras.png")


# ---------------------------------------------------------------- Daten für DeskKeeper (C#)

FPS = {
    "bengal": {"idle": 6, "walk": 12, "run": 16, "stalk": 8, "wiggle": 10, "pounce": 9, "sit": 4, "swat": 9},
    "mops": {"idle": 6, "walk": 13, "run": 16, "sniff": 10, "carry": 13, "sploot": 3, "sit": 4},
    "schaeferhund": {"idle": 8, "trot": 13, "run": 22, "dig": 12, "carry": 13, "shake": 12, "bark": 6, "lie": 3},
}
CS_NAME = {"bengal": "Cat", "mops": "Pug", "schaeferhund": "Shepherd"}


def export_cs(path):
    def f(v):
        return f"{v:.1f}"

    def pt(a, key, fb):
        p = a.get(key, a.get(fb, None)) if fb else a.get(key)
        return p

    out = ["// Erzeugt von werkzeuge/tiere/pixeltiere.py – nicht von Hand ändern.",
           "namespace DeskKeeper;", "",
           "/// <summary>Maße, Bewegungen und Ankerpunkte (Maul, Pfote) der Pixeltiere je Bild; Kunstpixel, Blick nach rechts.</summary>",
           "static partial class PetSprites", "{"]
    for cls in ANIMALS:
        an = cls()
        R = render_all(an)
        anims = []
        for r, (name, frames) in enumerate(R.items()):
            mouth = ", ".join("({}, {})".format(*map(f, a.get("mouth", (an.W / 2, an.G - 8)))) for _, a, _ in frames)
            paw = ", ".join("({}, {})".format(*map(f, a.get("paw", a.get("belly", (an.W / 2, an.G))))) for _, a, _ in frames)
            anims.append(f'        new("{name}", {r}, {len(frames)}, {FPS[an.name][name]}, [{mouth}], [{paw}]),')
        out.append(f'    public static readonly Sheet {CS_NAME[an.name]} = new("tiere-{an.name}.png", {an.W}, {an.H}, {an.G}, {an.W // 2}, [')
        out += anims
        out.append("    ]);")
    out.append(f"    public const int BedW = {BED[0]}, BedH = {BED[1]}, MoundW = {MOUND[0]}, MoundH = {MOUND[1]}, MoundStages = {MOUND_STAGES};")
    out.append("}")
    Path(path).write_text("\n".join(out) + "\n", encoding="utf-8")


if __name__ == "__main__":
    dst = Path(sys.argv[1])
    export(dst)
    export_extras(dst)
    if len(sys.argv) > 2:   # DeskKeeper: Assets und C#-Daten
        proj = Path(sys.argv[2])
        for cls in ANIMALS:
            (proj / "Assets" / f"tiere-{cls.name}.png").write_bytes((dst / f"{cls.name}.png").read_bytes())
        (proj / "Assets" / "tiere-extras.png").write_bytes((dst / "tiere-extras.png").read_bytes())
        export_cs(proj / "PetSprites.Data.cs")
    print("ok")
