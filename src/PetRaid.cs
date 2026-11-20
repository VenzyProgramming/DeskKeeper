using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DeskKeeper;

/// <summary>
/// Tiere als eigene Szenen (20.11.2026: „Jedes Tier ist seine eigene Szene“): Pixelart-Bengalkatze, Mops und Deutscher Schäferhund
/// verwüsten die Symbole. Der ganze Ablauf wird beim Erstellen einmal in Schritten von 1/60 s simuliert (Tier nach Drehbuch, Symbole
/// gleiten, hüpfen und stoßen sich) und aufgezeichnet; die Szene liest daraus wie alle anderen nur nach der Zeit.
/// Katze: schleicht an, springt auf ein Symbol, wirft dann alles vom Tisch (über den Bildschirmrand), zwischendurch Zoomies.
/// Mops: trägt ein Symbol in seinen Korb, pustet die Gruppen mit einem Nieser vom Bildschirm, plättet die letzten und schläft im Korb.
/// Schäferhund: bellt, treibt die Symbole jedes Bildschirms zu einem Haufen zusammen, holt Ausreißer und vergräbt den Haufen.
/// Die Tiere stehen vor den Symbolen (DrawAbove), getragene Symbole liegen im Maul obenauf.
/// </summary>
sealed class PetRaid : IAttack
{
    public enum Kind { Cat, Pug, Shepherd }
    const double Dt = 1 / 60.0, Friction = 3.2, Cap = 95;

    readonly Kind kind;
    readonly PetSprites.Sheet sheet;
    readonly double k, iconSize, blen;
    readonly IReadOnlyList<Rect> screens;
    readonly Rect primary;
    readonly Random rnd;

    // ---- Aufzeichnung
    readonly record struct Pose(float X, float Y, sbyte Face, byte Anim, byte Frame);
    readonly record struct Sample(float T, float X, float Y, float Sy, float A, bool Above);
    sealed class Track { public readonly List<Sample> S = []; public double Gone = double.PositiveInfinity; }
    sealed record Fx(string Kind, double T, double Life, Point P, Vector V = default, double Vz = 0, string Text = "", int Dir = 1);
    readonly List<Pose> poses = [];
    readonly Dictionary<Point, Track> tracks = [];
    readonly List<Fx> effects = [];
    readonly List<(Point C, double From, double To, double Width)> mounds = [];
    Point? bed;
    double sleepFrom = double.PositiveInfinity;

    public IReadOnlyList<(Point P, double T)> Blasts { get; }
    public double LastImpact { get; }
    public double End { get; }
    public bool Explodes => false;
    public double Reach { get; }
    public bool DrawsMovedIcons => true;
    /// <summary>Guter Zeitpunkt für das Vorschaubild (Tier mitten in der Aktion).</summary>
    public double PreviewTime { get; private set; } = 2;
    /// <summary>Für die Prüfung: Symbole, die nicht vom Tier, sondern nur durch die Zeitgrenze verschwanden.</summary>
    public int Leftovers { get; }

    // ---- Simulation
    sealed class Ico
    {
        public Point Start;
        public double X, Y, Vx, Vy, Z, Vz, Sy = 1, A = 1, Hold, Sink, Cool, Jit;
        public bool Alive = true, Carried, Above;
        public int Tries;
        public readonly Track Tr = new();
        public float LX = float.NaN, LY, LSy = 1, LA = 1;
        public bool LAbove;
    }
    readonly List<Ico> ics = [];
    double t, ax, ay, vx, vy, animT, wobble, rate = 1;
    int face = 1, anim, forced = -1;
    Ico? carry;

    public PetRaid(Kind kind, Rect primary, IReadOnlyList<Point> targets, double iconSize, IReadOnlyList<Rect>? screens)
    {
        this.kind = kind;
        this.primary = primary;
        this.iconSize = iconSize;
        this.screens = screens is { Count: > 0 } ? screens : [primary];
        sheet = kind switch { Kind.Cat => PetSprites.Cat, Kind.Pug => PetSprites.Pug, _ => PetSprites.Shepherd };
        blen = kind switch { Kind.Cat => 30, Kind.Pug => 24, _ => 46 };
        k = Math.Max(2, Math.Round(iconSize / 16));
        Reach = iconSize * 0.4;
        rnd = new Random(kind switch { Kind.Cat => 31, Kind.Pug => 47, _ => 59 });
        foreach (var p in targets.Distinct()) ics.Add(new Ico { Start = p, X = p.X, Y = p.Y });
        ax = primary.Left - 60 * k; ay = primary.Bottom - iconSize;
        var script = kind switch { Kind.Cat => CatScript(), Kind.Pug => PugScript(), _ => ShepherdScript() };
        foreach (var _ in script) { Step(); if (t > Cap) break; }
        int left = 0;
        foreach (var ic in ics.Where(i => i.Alive)) { left++; Dust(ic.X, Gy(ic)); Vanish(ic); }   // Zeitgrenze: Staubwolke
        Leftovers = left;
        Step();
        foreach (var ic in ics) tracks[ic.Start] = ic.Tr;
        Blasts = ics.Select(i => (P: i.Start, T: Math.Min(i.Tr.Gone, t))).ToList();
        LastImpact = Blasts.Count > 0 ? Blasts.Max(b => b.T) : 1;
        End = t + 0.3;
    }

    // ================================================================ Abfrage

    static Sample? At(Track tr, double time)
    {
        var s = tr.S;
        if (s.Count == 0 || time < s[0].T) return null;
        int lo = 0, hi = s.Count - 1;
        while (lo < hi) { int mid = (lo + hi + 1) / 2; if (s[mid].T <= time) lo = mid; else hi = mid - 1; }
        var a = s[lo];
        if (lo + 1 < s.Count && s[lo + 1].T - a.T < 1.6 * Dt)
        {
            var b = s[lo + 1];
            float q = (float)((time - a.T) / (b.T - a.T));
            return new Sample((float)time, a.X + (b.X - a.X) * q, a.Y + (b.Y - a.Y) * q, a.Sy + (b.Sy - a.Sy) * q, a.A + (b.A - a.A) * q, a.Above);
        }
        return a;
    }

    public Point? Move(Point start, double time) => tracks.TryGetValue(start, out var tr) && At(tr, time) is { } s ? new Point(s.X, s.Y) : null;
    public (double Sy, double Alpha) Look(Point start, double time) => tracks.TryGetValue(start, out var tr) && At(tr, time) is { } s ? (s.Sy, s.A) : (1, 1);
    public bool Above(Point start, double time) => tracks.TryGetValue(start, out var tr) && At(tr, time) is { Above: true };

    // ================================================================ Simulation

    double Gy(Ico ic) => ic.Y + iconSize * 0.5;
    IEnumerable<Ico> Free => ics.Where(i => i.Alive && !i.Carried && i.Sink <= 0);

    bool OnScreen(Point p, double margin)
    {
        foreach (var s in screens) { var r = s; r.Inflate(margin, margin); if (r.Contains(p)) return true; }
        return false;
    }

    Rect ScreenOf(Point p)
    {
        foreach (var s in screens) if (s.Contains(p)) return s;
        return screens.OrderBy(s => (new Point(s.Left + s.Width / 2, s.Top + s.Height / 2) - p).LengthSquared).First();
    }

    void Set(string name)
    {
        int i = sheet.Of(name);
        if (i != anim) { anim = i; animT = 0; }
        forced = -1;
    }

    Point Anchor(bool mouth, int a, int frame)
    {
        var p = mouth ? sheet.Anims[a].Mouth[frame] : sheet.Anims[a].Paw[frame];
        return new Point(ax + (p.X - sheet.Px) * k * face, ay + (p.Y - sheet.G) * k);
    }
    int FrameNow() { var a = sheet.Anims[anim]; return forced >= 0 ? forced : (int)(animT * a.Fps * rate) % a.Count; }

    /// <summary>Tempo (Kunstpixel/s), zu dem die Schrittfolge der Bewegung passt; schneller gelaufen wird die Folge schneller abgespielt.</summary>
    double Nominal(string name) => (kind, name) switch
    {
        (Kind.Cat, "walk") => 46, (Kind.Cat, "run") => 130, (Kind.Cat, "stalk") => 24,
        (Kind.Pug, "walk") => 40, (Kind.Pug, "carry") => 44, (Kind.Pug, "run") => 118,
        (Kind.Shepherd, "trot") => 56, (Kind.Shepherd, "carry") => 60, (Kind.Shepherd, "run") => 165,
        _ => 0,
    };
    Point Mouth() => Anchor(true, anim, FrameNow());

    void Step()
    {
        int frame = FrameNow();
        poses.Add(new Pose((float)ax, (float)ay, (sbyte)face, (byte)anim, (byte)frame));
        if (carry is not null)
        {
            var m = Anchor(true, anim, frame);
            carry.X = m.X + face * 6 * k + wobble; carry.Y = m.Y + 5 * k; carry.Z = 0;
        }
        Physics();
        foreach (var ic in ics) Record(ic);
        t += Dt; animT += Dt;
    }

    void Physics()
    {
        double g = 420 * k;
        foreach (var ic in ics)
        {
            if (!ic.Alive) continue;
            ic.Cool = Math.Max(0, ic.Cool - Dt); ic.Jit = Math.Max(0, ic.Jit - Dt);
            if (ic.Sink > 0)
            {
                ic.Sy = Math.Max(0, ic.Sy - ic.Sink * Dt); ic.A = Math.Clamp(ic.Sy * 2.2, 0, 1);
                if (ic.Sy <= 0.04) Vanish(ic);
                continue;
            }
            ic.Sy += ((ic.Hold > 0 ? ic.Hold : 1) - ic.Sy) * 0.25;
            if (ic.Carried) continue;
            ic.X += ic.Vx * Dt; ic.Y += ic.Vy * Dt;
            ic.Vz -= g * Dt; ic.Z += ic.Vz * Dt;
            if (ic.Z <= 0)
            {
                ic.Z = 0; ic.Vz = ic.Vz < -70 * k ? -ic.Vz * 0.3 : 0;
                double f = Math.Exp(-Friction * Dt);
                ic.Vx *= f; ic.Vy *= f;
                if (Math.Abs(ic.Vx) < 2 && Math.Abs(ic.Vy) < 2) ic.Vx = ic.Vy = 0;
            }
            if (kind == Kind.Shepherd)
            {   // der Hund treibt zusammen, aber nichts rutscht über den Rand: alles soll unter den Hügel
                var s = ScreenOf(new Point(ic.X, ic.Y)); s.Inflate(-iconSize * 0.5, -iconSize * 0.5);
                if (ic.X < s.Left) { ic.X = s.Left; ic.Vx = Math.Abs(ic.Vx) * 0.3; } else if (ic.X > s.Right) { ic.X = s.Right; ic.Vx = -Math.Abs(ic.Vx) * 0.3; }
                if (ic.Y < s.Top) { ic.Y = s.Top; ic.Vy = Math.Abs(ic.Vy) * 0.3; } else if (ic.Y > s.Bottom) { ic.Y = s.Bottom; ic.Vy = -Math.Abs(ic.Vy) * 0.3; }
            }
            else if (!OnScreen(new Point(ic.X, ic.Y), iconSize * 0.6)) Vanish(ic);
        }
        // Stöße zwischen Symbolen (Billard): nur wenn eines sich bewegt
        double min = iconSize * 1.05;
        var live = ics.Where(i => i.Alive && !i.Carried && i.Sink <= 0).ToList();
        for (int i = 0; i < live.Count; i++)
        {
            var a = live[i];
            for (int j = i + 1; j < live.Count; j++)
            {
                var b = live[j];
                bool still = a.Vx == 0 && a.Vy == 0 && a.Z == 0 && b.Vx == 0 && b.Vy == 0 && b.Z == 0;
                if (still || Math.Abs(a.Z - b.Z) > iconSize * 0.6) continue;
                double dx = b.X - a.X, dy = b.Y - a.Y;
                if (Math.Abs(dx) >= min || Math.Abs(dy) >= min) continue;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (d < 0.01 || d >= min) continue;
                double nx = dx / d, ny = dy / d, over = (min - d) / 2;
                a.X -= nx * over; a.Y -= ny * over; b.X += nx * over; b.Y += ny * over;
                double rv = (b.Vx - a.Vx) * nx + (b.Vy - a.Vy) * ny;
                if (rv < 0) { double imp = -rv * 0.8; a.Vx -= nx * imp; a.Vy -= ny * imp; b.Vx += nx * imp; b.Vy += ny * imp; }
            }
        }
    }

    void Record(Ico ic)
    {
        if (!ic.Alive && ic.Tr.Gone < t - 1e-6) return;
        float x = (float)(ic.X + (ic.Jit > 0 ? (rnd.Next(3) - 1) * k * 0.5 : 0)), y = (float)(ic.Y - ic.Z), sy = (float)ic.Sy, a = (float)ic.A;
        bool above = ic.Carried || ic.Above;
        float lx = float.IsNaN(ic.LX) ? (float)ic.Start.X : ic.LX, ly = float.IsNaN(ic.LX) ? (float)ic.Start.Y : ic.LY;
        if (Math.Abs(x - lx) > 0.2 || Math.Abs(y - ly) > 0.2 || Math.Abs(sy - ic.LSy) > 0.004 || Math.Abs(a - ic.LA) > 0.004 || above != ic.LAbove)
        {
            ic.Tr.S.Add(new Sample((float)t, x, y, sy, a, above));
            ic.LX = x; ic.LY = y; ic.LSy = sy; ic.LA = a; ic.LAbove = above;
        }
    }

    void Vanish(Ico ic)
    {
        if (!ic.Alive) return;
        ic.Alive = false; ic.Carried = false;
        if (carry == ic) carry = null;
        ic.Tr.Gone = t;
    }

    /// <summary>Weg in Richtung dir, bis das Symbol keinen Bildschirm mehr berührt.</summary>
    double ExitDistance(Point p, Vector dir)
    {
        double step = iconSize * 0.3;
        for (double s = 0; s < 20000; s += step)
            if (!OnScreen(p + dir * s, iconSize * 0.6)) return s;
        return 20000;
    }

    int ExitDir(Point p) => ExitDistance(p, new Vector(1, 0)) <= ExitDistance(p, new Vector(-1, 0)) ? 1 : -1;

    /// <summary>Anfangstempo, mit dem ein Symbol (Reibung) bis über den Rand gleitet.</summary>
    double ExitSpeed(Ico ic, int dir) => Friction * (ExitDistance(new Point(ic.X, ic.Y), new Vector(dir, 0)) + iconSize * 0.8) * 1.18;

    /// <summary>Punkt seitlich neben allen Bildschirmen auf Höhe von p (20.11.2026: Tiere laufen vom Rand herein, statt im Bild aufzutauchen;
    /// von unten ragte der Kopf sofort über die Taskleiste). Die ganze Figur liegt dort außerhalb.</summary>
    Point Outside(Point p)
    {
        int dir = ExitDir(p);
        double x = p.X + dir * (ExitDistance(p, new Vector(dir, 0)) + (sheet.W + 6) * k);
        for (int guard = 0; guard < 200 && SpriteOnScreen(x, p.Y); guard++) x += dir * iconSize;
        return new Point(x, p.Y);
    }

    /// <summary>Berührt die Figur (Drehpunkt x, Bodenlinie y, beide Blickrichtungen) einen Bildschirm?</summary>
    bool SpriteOnScreen(double x, double y)
    {
        var box = new Rect(x - sheet.W * k, y - sheet.G * k, 2 * sheet.W * k, sheet.H * k);
        return screens.Any(s => s.IntersectsWith(box));
    }

    /// <summary>Prüfung: Anfang und Ende der Szene liegen ganz außerhalb aller Bildschirme.</summary>
    public (bool In, bool Out) EntersAndLeavesOffscreen()
    {
        if (poses.Count == 0) return (true, true);
        var last = kind == Kind.Pug ? (Pose?)null : poses[^1];
        return (!SpriteOnScreen(poses[0].X, poses[0].Y), last is not { } l || !SpriteOnScreen(l.X, l.Y));
    }

    // ---- Drehbuch-Bausteine

    /// <summary>Gerade Strecke bleibt auf den Bildschirmen (nicht durch die Lücke zwischen zwei Bildschirmen).</summary>
    bool Clear(Point a, Point b)
    {
        int n = Math.Max(2, (int)((b - a).Length / (iconSize * 0.5)));
        for (int i = 0; i <= n; i++) if (!OnScreen(a + (b - a) * ((double)i / n), iconSize * 0.4)) return false;
        return true;
    }

    IEnumerable<int> Walk(double tx, double ty, string name, double speed, double kick = 0, Point? herd = null, int faceEnd = 0)
    {
        var from = new Point(ax, ay); var to = new Point(tx, ty);
        if (OnScreen(from, 0) && OnScreen(to, 0) && !Clear(from, to))
        {   // über eine Ecke: erst senkrecht, dann waagerecht (oder umgekehrt), je nachdem welcher Weg auf den Bildschirmen bleibt
            var c1 = new Point(ax, ty); var c2 = new Point(tx, ay);
            Point? via = Clear(from, c2) && Clear(c2, to) ? c2 : Clear(from, c1) && Clear(c1, to) ? c1 : null;
            if (via is { } v) foreach (var _ in Leg(v.X, v.Y, name, speed, kick, herd, 0)) yield return 0;
        }
        foreach (var _ in Leg(tx, ty, name, speed, kick, herd, faceEnd)) yield return 0;
    }

    IEnumerable<int> Leg(double tx, double ty, string name, double speed, double kick, Point? herd, int faceEnd)
    {
        Set(name);
        double nominal = Nominal(name);
        rate = nominal > 0 ? Math.Clamp(speed / nominal, 0.7, 2.2) : 1;
        for (int guard = 0; guard < 6000; guard++)
        {
            double dx = tx - ax, dy = ty - ay, d = Math.Sqrt(dx * dx + dy * dy), st = speed * k * Dt;
            if (Math.Abs(dx) > 0.6 * k) face = dx > 0 ? 1 : -1;
            if (d <= st)
            {
                ax = tx; ay = ty; vx = vy = 0;
                if (faceEnd != 0) face = faceEnd;
                yield return 0;
                yield break;
            }
            vx = dx / d * speed * k; vy = dy / d * speed * k;
            ax += vx * Dt; ay += vy * Dt;
            if (kick > 0) Kick(kick);
            if (herd is { } c) Herd(c);
            yield return 0;
        }
    }

    IEnumerable<int> Play(string name, double loops = 1, Dictionary<int, Action>? ev = null, double hold = 0, Action<double>? move = null, double speed = 1)
    {
        Set(name); animT = 0; rate = 1;
        var a = sheet.Anims[anim];
        double fps = a.Fps * speed;
        int n = a.Count, frames = Math.Max(1, (int)Math.Round(n * loops));
        int total = Math.Max(1, (int)Math.Ceiling(frames / fps / Dt)), last = -1;
        for (int i = 0; i < total; i++)
        {
            int idx = Math.Min((int)(i * Dt * fps), frames - 1) % n;
            forced = idx;
            if (idx != last && ev is not null && ev.TryGetValue(idx, out var act)) act();
            move?.Invoke((i + 1.0) / total);
            last = idx;
            yield return 0;
        }
        for (int i = 0; i < (int)(hold / Dt); i++) yield return 0;
        forced = -1;
    }

    IEnumerable<int> Wait(double sec) { for (int i = 0; i < (int)(sec / Dt); i++) yield return 0; }

    void Kick(double power)
    {
        foreach (var ic in Free)
        {
            if (ic.Cool > 0) continue;
            if (Math.Abs(Gy(ic) - ay) < 9 * k && Math.Abs(ic.X - ax) < blen / 2 * k + 4 * k)
            {
                double sp = Math.Sqrt(vx * vx + vy * vy); if (sp < 1) sp = 1;
                double ux = vx / sp, uy = vy / sp, side = Gy(ic) >= ay ? 1 : -1;
                ic.Vx = (ux * power * 0.8 + rnd.NextDouble() * 60 - 30) * k;
                ic.Vy = (uy * power * 0.5 + side * power * 0.45) * k;
                ic.Vz = (80 + rnd.NextDouble() * 60) * k;
                ic.Cool = 0.5;
                Star(ic.X, ic.Y - iconSize * 0.2);
            }
        }
    }

    void Herd(Point c)
    {
        double r = iconSize * 2.3;
        foreach (var ic in Free)
        {
            double dx = ic.X - ax, dy = (Gy(ic) - ay) * 1.2, d = Math.Sqrt(dx * dx + dy * dy);
            if (d >= r) continue;
            var to = c - new Point(ic.X, ic.Y);
            double len = to.Length; if (len < iconSize * 0.3) continue;
            to /= len;
            double a = 2600 * k * (1 - d / r) * Dt;
            ic.Vx += to.X * a; ic.Vy += to.Y * a;
            double sp = Math.Sqrt(ic.Vx * ic.Vx + ic.Vy * ic.Vy), cap = 260 * k;
            if (sp > cap) { ic.Vx *= cap / sp; ic.Vy *= cap / sp; }
        }
    }

    void Throw(Ico ic, int dir, double hop = 60, double spread = 0.08)
    {
        double v = ExitSpeed(ic, dir) * (1 + rnd.NextDouble() * 0.1);
        ic.Vx = dir * v; ic.Vy = (rnd.NextDouble() * 2 - 1) * spread * v; ic.Vz = hop * k; ic.Hold = 0;
        ic.Tries++;
    }

    void Pick(Ico ic) { ic.Carried = true; ic.Vx = ic.Vy = ic.Vz = 0; carry = ic; }

    void Drop(Point at)
    {
        if (carry is not { } ic) return;
        ic.Carried = false; carry = null; wobble = 0;
        ic.X = at.X; ic.Y = at.Y; ic.Z = 0; ic.Vx = ic.Vy = ic.Vz = 0;
        Dust(ic.X, Gy(ic));
    }

    // ---- Effekte (Kunstpixel)
    void Star(double x, double y) => effects.Add(new Fx("star", t, 0.3, new Point(x, y)));
    void Dust(double x, double y) => effects.Add(new Fx("dust", t, 0.4, new Point(x, y)));
    void Say(string text, double x, double y, double life = 0.9) => effects.Add(new Fx("text", t, life, new Point(x, y), Text: text));
    void Dirt(double x, double y, int dir, int n)
    {
        for (int i = 0; i < n; i++)
            effects.Add(new Fx("dirt", t, 1.1, new Point(x, y), new Vector(dir * (60 + rnd.NextDouble() * 110) * k, (rnd.NextDouble() * 50 - 25) * k), (70 + rnd.NextDouble() * 80) * k, Dir: rnd.Next(3)));
    }

    // ================================================================ Katze

    IEnumerable<int> CatScript()
    {
        if (ics.Count == 0) yield break;
        var mid = new Point(primary.Left + primary.Width / 2, primary.Top + primary.Height / 2);
        var first = ics.OrderBy(i => primary.Contains(i.Start) ? 0 : 1).ThenBy(i => (i.Start - mid).LengthSquared).First();
        var scr = ScreenOf(first.Start);
        int d = first.X - scr.Left > 44 * k ? 1 : -1;
        var stance = new Point(first.X - d * 34 * k, Gy(first) + 3 * k);
        var entry = Outside(stance);
        ax = entry.X; ay = entry.Y; face = d;
        var near = stance - new Vector(d * 3 * iconSize, 0);
        if (!OnScreen(near, 0)) near = stance;
        foreach (var _ in Walk(near.X, near.Y, "run", 190)) yield return 0;
        foreach (var _ in Walk(stance.X, stance.Y, "stalk", 34, faceEnd: d)) yield return 0;
        foreach (var _ in Play("wiggle", 2)) yield return 0;
        double x0 = ax;
        var pounce = Play("pounce", 1, new Dictionary<int, Action>
        {
            [3] = () =>
            {
                first.Hold = 0.5; Star(first.X, first.Y - iconSize * 0.2); Say("!", ax + d * 6 * k, ay - 36 * k);
                foreach (var ic in Free)
                {
                    if (ic == first) continue;
                    var v = new Point(ic.X, ic.Y) - new Point(first.X, first.Y); double dd = v.Length, r = iconSize * 2.4;
                    if (dd < 0.1 || dd > r) continue;
                    v /= dd; double p = 90 * k * (1.2 - dd / r);
                    ic.Vx += v.X * p; ic.Vy += v.Y * p; ic.Vz = (70 + 50 * (1 - dd / r)) * k;
                }
            },
        }, hold: 0.55, move: u => ax = x0 + d * 15 * k * Fly.Smooth(Math.Min(1, u * 1.7)));
        foreach (var _ in pounce) yield return 0;
        first.Hold = 0;
        PreviewTime = t + 0.25;
        foreach (var _ in Play("swat", 1, new() { [2] = () => { Throw(first, d); Star(first.X - d * iconSize * 0.4, first.Y); } }, speed: 1.5)) yield return 0;

        int round = 0; bool teased = false;
        while (Free.Any() && t < Cap - 6)
        {
            round++;
            if (round % 6 == 0 && Free.Count() > 4)
            {   // Zoomies: quer durch die Symbole, alles fliegt durcheinander
                var stops = Free.OrderBy(_ => rnd.Next()).Take(4).Select(i => new Point(i.X + (rnd.NextDouble() - 0.5) * iconSize, Gy(i) + 2 * k)).ToList();
                foreach (var s in stops) foreach (var _ in Walk(s.X, s.Y, "run", 230, kick: 190)) yield return 0;
                continue;
            }
            var here = new Point(ax, ay);
            var next = Free.OrderBy(i => (new Point(i.X, Gy(i)) - here).LengthSquared).First();
            if (next.Tries >= 3) { Dust(next.X, Gy(next)); Vanish(next); continue; }
            int dir = ExitDir(new Point(next.X, next.Y));
            var stand = new Point(next.X - dir * 19 * k, Gy(next) + 3 * k);
            double dist = (stand - here).Length;
            foreach (var _ in Walk(stand.X, stand.Y, dist > 3 * iconSize ? "run" : "walk", dist > 3 * iconSize ? 200 : 80, faceEnd: dir)) yield return 0;
            if (!next.Alive || next.Carried) continue;
            if (!teased && ExitDistance(new Point(next.X, next.Y), new Vector(dir, 0)) < 2.6 * iconSize)
            {   // erst ganz vorsichtig anstupsen, gucken – dann runter damit
                teased = true;
                foreach (var _ in Play("swat", 1, new() { [2] = () => { next.Vx = dir * 40 * k; Star(next.X - dir * iconSize * 0.4, next.Y); } }, speed: 1.2)) yield return 0;
                foreach (var _ in Play("sit", 1, speed: 1.4)) yield return 0;
                stand = new Point(next.X - dir * 19 * k, Gy(next) + 3 * k);
                foreach (var _ in Walk(stand.X, stand.Y, "walk", 50, faceEnd: dir)) yield return 0;
            }
            var target = next;
            foreach (var _ in Play("swat", 1, new() { [2] = () => { if (target.Alive) { Throw(target, dir); Star(target.X - dir * iconSize * 0.4, target.Y); } } }, speed: 1.7)) yield return 0;
        }
        foreach (var _ in Play("sit", 1)) yield return 0;
        var exit = Outside(new Point(ax, ay));
        foreach (var _ in Walk(exit.X, exit.Y, "run", 200)) yield return 0;
    }

    // ================================================================ Mops

    IEnumerable<int> PugScript()
    {
        if (ics.Count == 0) yield break;
        var mine = ics.Where(i => primary.Contains(i.Start)).ToList();
        if (mine.Count == 0) mine = ics;
        double bx = Math.Clamp(mine.Max(i => i.X) + 5 * iconSize, primary.Left + 3 * iconSize, primary.Right - 3 * iconSize);
        var b = new Point(bx, primary.Bottom - 1.2 * iconSize);
        bed = b;
        var m0 = sheet.Anims[sheet.Of("carry")].Mouth[0];
        double mdx = m0.X - sheet.Px;
        // 1. das nächste Symbol in den Korb tragen; der Mops kommt vom nächsten Rand
        var c0 = mine.OrderBy(i => (new Point(i.X, i.Y) - b).LengthSquared).First();
        var entry = Outside(new Point(c0.X, Gy(c0)));
        ax = entry.X; ay = entry.Y; face = c0.X >= ax ? 1 : -1;
        foreach (var _ in Fetch(c0, mdx)) yield return 0;
        foreach (var _ in Walk(b.X + 12 * k, b.Y + 3 * k, "carry", 80, faceEnd: -1)) yield return 0;
        var dropped = carry;
        Drop(new Point(b.X, b.Y - iconSize * 0.3));
        if (dropped is not null) { dropped.Sink = 1.4; dropped.Above = false; }
        Say("?", ax, ay - 30 * k);
        foreach (var _ in Play("sit", 1, speed: 1.5)) yield return 0;

        int round = 0;
        while (Free.Any() && t < Cap - 8)
        {
            round++;
            var here = new Point(ax, ay);
            var seed = Free.OrderBy(i => (new Point(i.X, Gy(i)) - here).LengthSquared).First();
            var group = Free.Where(i => Math.Abs(i.Y - seed.Y) < iconSize * 2.6 && Math.Abs(i.X - seed.X) < iconSize * 4).ToList();
            int all = Free.Count();
            if (all <= 4 && group.Count == all || seed.Tries >= 3)
            {   // die letzten: platt liegen
                var cx = group.Average(i => i.X); var cy = group.Average(i => Gy(i));
                var belly = sheet.Anims[sheet.Of("sploot")].Paw[0];
                double off = (belly.X - sheet.Px) * k;
                foreach (var _ in Walk(cx - face * off, cy + 3 * k, "walk", 70)) yield return 0;
                var under = Free.Where(i => Math.Abs(i.X - cx) < iconSize * 1.6 && Math.Abs(Gy(i) - cy) < iconSize * 1.2).ToList();
                if (under.Count == 0) under = [seed];
                foreach (var _ in Play("sploot", 1, new() { [0] = () => { foreach (var ic in under) { ic.Sink = 0.8; Dust(ic.X, Gy(ic)); } effects.Add(new Fx("zzz", t, 2.6, new Point(ax + 8 * k, ay - 16 * k))); } })) yield return 0;
                foreach (var _ in Play("sploot", 1)) yield return 0;
                continue;
            }
            if (round % 4 == 0 && all > 6)
            {   // Zoomies im Kreis
                var c = new Point(group.Average(i => i.X), group.Average(i => Gy(i)));
                double rx = iconSize * 3.2, ry = iconSize * 2;
                for (int s = 0; s <= 16; s++)
                {
                    double a = Math.PI * 0.5 + s * Math.Tau * 1.2 / 16;
                    var p = new Point(c.X + rx * Math.Cos(a), c.Y + ry * Math.Sin(a));
                    foreach (var _ in Walk(p.X, p.Y, "run", 190, kick: 170)) yield return 0;
                }
                continue;
            }
            // Nieser: von der Rückseite der Gruppe Richtung des näheren Rands
            var gc = new Point(group.Average(i => i.X), group.Average(i => i.Y));
            int dir = ExitDir(gc);
            var back = group.OrderBy(i => i.X * dir).First();
            double gy = group.Average(i => Gy(i));
            var stand = new Point(back.X - dir * (mdx + 9) * k, gy + 2 * k);
            if (!OnScreen(stand, -iconSize * 0.3))
            {
                dir = -dir; back = group.OrderBy(i => i.X * dir).First();
                stand = new Point(back.X - dir * (mdx + 9) * k, gy + 2 * k);
            }
            double dist = (stand - here).Length;
            foreach (var _ in Walk(stand.X, stand.Y, dist > 3 * iconSize ? "run" : "walk", dist > 3 * iconSize ? 180 : 70, faceEnd: dir)) yield return 0;
            foreach (var _ in Play("sniff", 1, speed: 1.3)) yield return 0;
            if (round == 1) PreviewTime = t + 0.3;
            int sd = dir;
            foreach (var _ in Play("idle", 1, new() { [2] = () => Sneeze(sd) }, hold: 0.25)) yield return 0;
        }
        // in den Korb und schlafen
        var home = new Point(b.X + 2 * k, b.Y + 4 * k);
        foreach (var _ in Walk(home.X, home.Y, "run", 170, faceEnd: -1)) yield return 0;
        sleepFrom = t;
        effects.Add(new Fx("zzz", t, 60, new Point(ax + 6 * k, ay - 16 * k)));
        foreach (var _ in Play("sploot", 1)) yield return 0;
    }

    IEnumerable<int> Fetch(Ico ic, double mdx)
    {
        int f = ic.X >= ax ? 1 : -1;
        var stand = new Point(ic.X - f * (mdx + 4) * k, Gy(ic) + 2 * k);
        double dist = (stand - new Point(ax, ay)).Length;
        string walk = kind == Kind.Shepherd ? "trot" : "walk";
        foreach (var _ in Walk(stand.X, stand.Y, dist > 3 * iconSize ? "run" : walk, dist > 3 * iconSize ? (kind == Kind.Shepherd ? 260 : 180) : (kind == Kind.Shepherd ? 90 : 70), faceEnd: f)) yield return 0;
        if (kind == Kind.Pug) foreach (var _ in Play("sniff", 1)) yield return 0;
        if (ic.Alive && !ic.Carried) Pick(ic);
    }

    void Sneeze(int dir)
    {
        var m = Mouth();
        effects.Add(new Fx("puff", t, 0.6, m, Dir: dir));
        Say(Loc.T("pet.sneeze"), ax, ay - 30 * k);
        foreach (var ic in Free.ToList())
        {
            double along = (ic.X - ax) * dir;
            if (along < -2 * k || along > iconSize * 9 || Math.Abs(Gy(ic) - ay) > iconSize * 2.8) continue;
            Throw(ic, dir, hop: 110 + rnd.NextDouble() * 60, spread: 0.12);
        }
        ax -= dir * 3 * k;
    }

    // ================================================================ Schäferhund

    IEnumerable<int> ShepherdScript()
    {
        if (ics.Count == 0) yield break;
        var m0 = sheet.Anims[sheet.Of("carry")].Mouth[0];
        double mdx = m0.X - sheet.Px;
        // 20.11.2026: verstreute Symbolgruppen (Ecken, Ränder, Mitte) einzeln zusammentreiben und vergraben, bis keines mehr übrig ist
        bool first = true; bool shaken = false;
        for (int pass = 0; pass < 40 && Free.Any() && t < Cap - 8; pass++)
        {
            var groups = Clusters();
            var here = new Point(ax, ay);
            var group = first
                ? groups.OrderBy(g => primary.Contains(new Point(g[0].X, g[0].Y)) ? 0 : 1).ThenByDescending(g => g.Count).First()
                : groups.OrderBy(g => g.Min(i => (new Point(i.X, Gy(i)) - here).LengthSquared)).First();
            int before = Free.Count();
            var s = ScreenOf(new Point(group.Average(i => i.X), group.Average(i => i.Y)));
            var inner = s; inner.Inflate(-2.6 * iconSize, -2.2 * iconSize);
            if (inner.Width <= 0 || inner.Height <= 0) inner = s;
            var c = new Point(Math.Clamp(group.Average(i => i.X), inner.Left, inner.Right), Math.Clamp(group.Average(i => i.Y), inner.Top, inner.Bottom));
            int n = group.Count;
            double pileR = 0.62 * iconSize * Math.Sqrt(n) + 0.6 * iconSize;
            double rx0 = group.Max(i => Math.Abs(i.X - c.X)) + 2.4 * iconSize, ry0 = group.Max(i => Math.Abs(i.Y - c.Y)) + 2.0 * iconSize;
            var path = s; path.Inflate(-iconSize * 0.45, -iconSize * 0.45);
            Point Inside(Point p) => new(Math.Clamp(p.X, path.Left, path.Right), Math.Clamp(p.Y, path.Top + sheet.G * k * 0.6, path.Bottom));
            var p0 = Inside(new Point(c.X, c.Y + iconSize * 0.5 + ry0));
            if (first)
            {
                var e = Outside(p0); ax = e.X; ay = e.Y; face = p0.X >= ax ? 1 : -1;
                foreach (var _ in Walk(p0.X, p0.Y, "run", 240)) yield return 0;
                foreach (var _ in Play("bark", 3, new() { [1] = Bark })) yield return 0;
            }
            else
            {
                foreach (var _ in Walk(p0.X, p0.Y, "run", 270)) yield return 0;
                if (n >= 3) foreach (var _ in Play("bark", 1, new() { [1] = Bark })) yield return 0;
            }
            if (n >= 3)
            {   // Zusammentreiben: Runden um die Symbole, enger werdend, immer auf dem Bildschirm
                if (first) PreviewTime = t + 1.2;
                double laps = n >= 6 ? 1.6 : 1.2, rEnd = pileR + 1.4 * iconSize;
                int pts = Math.Clamp((int)(laps * Math.Tau * (rx0 + ry0 + 2 * rEnd) / 4 / (0.8 * iconSize)), 14, 54);
                for (int i = 1; i <= pts; i++)
                {
                    double q = (double)i / pts, a = Math.PI / 2 + q * Math.Tau * laps;
                    double rx = rx0 + (rEnd - rx0) * q, ry = ry0 + (rEnd - ry0) * q;
                    var p = Inside(new Point(c.X + rx * Math.Cos(a), c.Y + iconSize * 0.5 + ry * Math.Sin(a)));
                    foreach (var _ in Walk(p.X, p.Y, "run", 300, herd: c)) yield return 0;
                }
                foreach (var _ in Wait(0.25)) yield return 0;
            }
            first = false;
            // Ausreißer dieser Gruppe holen (der erste der Szene wird geschüttelt)
            for (int guard = 0; guard < n + 3; guard++)
            {
                var stray = group.Where(i => i.Alive && !i.Carried && i.Sink <= 0 && (new Point(i.X, i.Y) - c).Length > pileR * 1.3)
                    .OrderBy(i => (new Point(i.X, i.Y) - new Point(ax, ay)).LengthSquared).FirstOrDefault();
                if (stray is null) break;
                foreach (var _ in Fetch(stray, mdx)) yield return 0;
                if (carry is null) continue;
                if (!shaken)
                {
                    shaken = true;
                    var ev = new Dictionary<int, Action>();
                    double[] sway = [-3, 3, -3, 3, -2, 0];
                    for (int f = 0; f < 6; f++) { double w = sway[f] * k; ev[f] = () => wobble = w; }
                    foreach (var _ in Play("shake", 2, ev)) yield return 0;
                    wobble = 0;
                }
                var dirIn = new Point(ax, ay) - c; if (dirIn.Length < 1) dirIn = new Vector(1, 0); dirIn.Normalize();
                var spot = Inside(c + dirIn * (pileR + iconSize * 0.9) + new Vector(0, iconSize * 0.5));
                foreach (var _ in Walk(spot.X, spot.Y, "carry", 120)) yield return 0;
                Drop(new Point(c.X + dirIn.X * pileR * 0.9, c.Y + dirIn.Y * pileR * 0.9));
            }
            // Vergraben: neben dem Haufen buddeln, Erde fliegt auf den Haufen
            int side = ax < c.X ? -1 : 1;
            var dig = new Point(c.X + side * (pileR + 24 * k), c.Y + iconSize * 0.5 + 2 * k);
            if (!path.Contains(dig)) { side = -side; dig = new Point(c.X + side * (pileR + 24 * k), dig.Y); }
            dig = Inside(dig);
            foreach (var _ in Walk(dig.X, dig.Y, "trot", 110, faceEnd: side)) yield return 0;
            var pile = Free.Where(i => group.Contains(i) && (new Point(i.X, i.Y) - c).Length <= pileR * 2.2 || (new Point(i.X, i.Y) - c).Length <= pileR * 1.6)
                .OrderByDescending(i => (new Point(i.X, i.Y) - new Point(ax, ay)).Length).ToList();
            if (pile.Count == 0) continue;
            double loops = n <= 2 ? 2 : n <= 5 ? 3 : 4;
            double span = sheet.Anims[sheet.Of("dig")].Count * loops / sheet.Anims[sheet.Of("dig")].Fps;
            mounds.Add((new Point(c.X, c.Y + iconSize * 0.55), t, t + span + 0.4, (pileR * 2 + iconSize) / k));
            int perEvent = Math.Max(1, (int)Math.Ceiling(pile.Count / (loops * 2)));
            int next = 0;
            void DigFx()
            {
                var paw = Anchor(false, anim, FrameNow());
                Dirt(paw.X, ay - 2 * k, -face, 7);
                for (int j = 0; j < perEvent && next < pile.Count; j++, next++) if (pile[next].Alive) pile[next].Sink = 1.1;
            }
            foreach (var _ in Play("dig", loops, new() { [0] = DigFx, [3] = DigFx })) yield return 0;
            foreach (var ic in pile.Where(i => i.Alive && i.Sink <= 0)) ic.Sink = 1.1;
            foreach (var _ in Wait(0.4)) yield return 0;
            if (Free.Count() >= before)
            {   // nichts geschafft (sollte nicht vorkommen): an Ort und Stelle einbuddeln, damit die Szene weitergeht
                foreach (var ic in group.Where(i => i.Alive && !i.Carried && i.Sink <= 0)) { ic.Sink = 1.1; Dust(ic.X, Gy(ic)); }
            }
        }
        // Wache halten am letzten Hügel, dann davon
        foreach (var _ in Play("lie", 2)) yield return 0;
        var exit = Outside(new Point(ax, ay));
        foreach (var _ in Walk(exit.X, exit.Y, "run", 240)) yield return 0;
    }

    /// <summary>Freie Symbole in Gruppen: benachbart (höchstens 3,6 Symbolgrößen Abstand) und auf demselben Bildschirm.</summary>
    List<List<Ico>> Clusters()
    {
        var left = Free.ToList();
        var result = new List<List<Ico>>();
        double link = 3.6 * iconSize;
        while (left.Count > 0)
        {
            var g = new List<Ico> { left[0] }; left.RemoveAt(0);
            var scr = ScreenOf(new Point(g[0].X, g[0].Y));
            for (int i = 0; i < g.Count; i++)
            {
                var a = g[i];
                for (int j = left.Count - 1; j >= 0; j--)
                {
                    var o = left[j];
                    if (Math.Abs(o.X - a.X) <= link && Math.Abs(o.Y - a.Y) <= link && ScreenOf(new Point(o.X, o.Y)) == scr) { g.Add(o); left.RemoveAt(j); }
                }
            }
            result.Add(g);
        }
        return result;
    }

    void Bark()
    {
        var m = Mouth();
        Say(Loc.T("pet.woof"), m.X + face * 8 * k, m.Y - 12 * k, 0.8);
        effects.Add(new Fx("lines", t, 0.4, m, Dir: face));
        foreach (var ic in Free) ic.Jit = 0.45;
    }

    // ================================================================ Zeichnen

    static readonly Brush ShadowBrush = Fly.B("#40000000"), StarBrush = Fly.B("#FFF0A0"), White = Fly.B("#FFFFFF"), Ink = Fly.B("#1E1E28"),
        DustBrush = Fly.B("#D2D6E0"), PuffBrush = Fly.B("#F0F4FA"), ZBrush = Fly.B("#E6ECFF");
    static readonly Brush[] DirtBrushes = [Fly.B("#7B5230"), Fly.B("#5E3E22"), Fly.B("#9A6C40")];

    Pose? PoseAt(double time)
    {
        if (poses.Count == 0) return null;
        int i = (int)(time / Dt);
        if (i < 0) return null;
        if (i >= poses.Count) return null;
        return poses[i];
    }

    double Snap(double v) => Math.Round(v);

    /// <summary>Unter den Symbolen: Schatten des Tiers, Hundekorb.</summary>
    public void Draw(DrawingContext dc, double time)
    {
        if (bed is { } b && PetSprites.Bed(false) is { } back)
        {
            dc.PushTransform(new MatrixTransform(k, 0, 0, k, Snap(b.X - PetSprites.BedW / 2.0 * k), Snap(b.Y - PetSprites.BedH * 0.62 * k)));
            dc.DrawDrawing(back);
            dc.Pop();
        }
        if (Current(time) is { } p)
            dc.DrawEllipse(ShadowBrush, null, new Point(p.X, p.Y + k), blen * 0.46 * k, 2.2 * k);
    }

    Pose? Current(double time)
    {
        if (PoseAt(time) is { } p) return p;
        if (time >= sleepFrom && bed is not null && poses.Count > 0)
        {   // Mops schläft im Korb weiter, während aufgeräumt wird
            var last = poses[^1];
            int a = sheet.Of("sploot"); var an = sheet.Anims[a];
            return new Pose(last.X, last.Y, last.Face, (byte)a, (byte)((int)(time * an.Fps * 0.6) % an.Count));
        }
        return null;
    }

    /// <summary>Über den Symbolen: Erdhügel, Tier, vorderer Korbrand, Effekte.</summary>
    public void DrawAbove(DrawingContext dc, double time)
    {
        foreach (var (c, from, to, width) in mounds)
        {
            if (time < from) continue;
            double grow = Math.Clamp((time - from) / (to - from), 0, 1);
            double fade = 1 - Math.Clamp((time - (End + 0.2)) / 0.6, 0, 1);
            if (fade <= 0) continue;
            int stage = (int)Math.Round(grow * (PetSprites.MoundStages - 1));
            if (PetSprites.Mound(stage) is not { } m) continue;
            double s = Math.Max(1, Math.Round(width / PetSprites.MoundW)) * k;
            if (fade < 1) dc.PushOpacity(fade);
            dc.PushTransform(new MatrixTransform(s, 0, 0, s, Snap(c.X - PetSprites.MoundW / 2.0 * s), Snap(c.Y - (PetSprites.MoundH - 1) * s)));
            dc.DrawDrawing(m);
            dc.Pop();
            if (fade < 1) dc.Pop();
        }
        if (Current(time) is { } p && sheet.Frame(p.Anim, p.Frame, p.Face < 0) is { } d)
        {
            double left = p.Face > 0 ? p.X - sheet.Px * k : p.X - (sheet.W - sheet.Px) * k;
            dc.PushTransform(new MatrixTransform(k, 0, 0, k, Snap(left), Snap(p.Y - sheet.G * k)));
            dc.DrawDrawing(d);
            dc.Pop();
        }
        if (bed is { } b && PetSprites.Bed(true) is { } front)
        {
            dc.PushTransform(new MatrixTransform(k, 0, 0, k, Snap(b.X - PetSprites.BedW / 2.0 * k), Snap(b.Y - PetSprites.BedH * 0.62 * k)));
            if (time < sleepFrom) dc.DrawDrawing(front);
            dc.Pop();
        }
        foreach (var f in effects)
        {
            double age = time - f.T;
            if (age < 0 || age > f.Life) continue;
            DrawFx(dc, f, age, age / f.Life);
        }
    }

    void Px(DrawingContext dc, Brush b, double x, double y, double w = 1, double h = 1) =>
        dc.DrawRectangle(b, null, new Rect(Math.Round(x / k) * k, Math.Round(y / k) * k, w * k, h * k));

    void DrawFx(DrawingContext dc, Fx f, double age, double u)
    {
        var p = f.P;
        switch (f.Kind)
        {
            case "star":
            {
                var b = Soft.Fade(StarBrush, 1 - u);
                int r = 1 + (int)(4 * u);
                for (int i = -r; i <= r; i++) { Px(dc, b, p.X + i * k, p.Y); Px(dc, b, p.X, p.Y + i * k); }
                Px(dc, Soft.Fade(White, 1 - u), p.X, p.Y);
                break;
            }
            case "dust":
            {
                var b = Soft.Fade(DustBrush, 0.7 * (1 - u));
                double r = (1.5 + 3 * u) * k;
                foreach (int s in new[] { -1, 1 }) dc.DrawEllipse(b, null, new Point(p.X + s * (3 + 7 * u) * k, p.Y - k), r, r * 0.7);
                break;
            }
            case "puff":
            {
                var b = Soft.Fade(PuffBrush, 0.8 * (1 - u));
                for (int i = 0; i < 4; i++)
                {
                    double r = (1.5 + 2.6 * u + i * 0.4) * k;
                    dc.DrawEllipse(b, null, new Point(p.X + f.Dir * (2 + 10 * u) * k + (i - 1.5) * 2.2 * k, p.Y - k + (i % 2) * 2 * k - 3 * u * k), r, r);
                }
                break;
            }
            case "dirt":
            {
                double z = Math.Max(0, f.Vz * age - 0.5 * 380 * k * age * age);
                var q = p + f.V * Math.Min(age, f.Vz / (380 * k) * 2);
                Px(dc, Soft.Fade(DirtBrushes[f.Dir], Math.Min(1, 2.5 * (1 - u))), q.X, q.Y - z);
                break;
            }
            case "text":
            {
                double a = Math.Min(1, 3 * (1 - u));
                int w = PetSprites.TextWidth(f.Text);
                double x0 = Math.Round((p.X - w * k / 2) / k) * k, y0 = Math.Round((p.Y - 10 * k - 6 * u * k) / k) * k;
                dc.DrawRectangle(Soft.Fade(Ink, a), null, new Rect(x0 - 3 * k, y0 - 3 * k, (w + 5) * k, 11 * k));
                dc.DrawRectangle(Soft.Fade(White, a), null, new Rect(x0 - 2 * k, y0 - 2 * k, (w + 3) * k, 9 * k));
                for (int i = 0; i < 3; i++) Px(dc, Soft.Fade(White, a), x0 + (1 + i * 0) * k, y0 + (7 + i) * k, 3 - i, 1);
                PetSprites.Text(dc, f.Text, new Point(x0, y0), k, Soft.Fade(Ink, a));
                break;
            }
            case "zzz":
            {
                for (int i = 0; i < 3; i++)
                {
                    double uu = (age / 1.6 + i / 3.0) % 1;
                    PetSprites.Text(dc, "Z", new Point(Math.Round((p.X + 6 * uu * k + i * k) / k) * k, Math.Round((p.Y - 14 * uu * k) / k) * k), k, Soft.Fade(ZBrush, 1 - uu));
                }
                break;
            }
            case "lines":
            {
                var b = Soft.Fade(White, 0.85 * (1 - u));
                for (int i = -1; i <= 1; i++)
                    for (int s = 0; s < 3; s++) Px(dc, b, p.X + f.Dir * (4 + s + 4 * u) * k, p.Y + i * (3 + s * 0.6) * k);
                break;
            }
        }
    }
}
