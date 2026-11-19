using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DeskKeeper;

/// <summary>
/// Duell F-35A gegen Su-57 (19.11.2026: „Duelle zwischen SU und F“, „soll episch aussehen“): beide jagen sich über die
/// Symbolgruppen, die Führung wechselt nach jeder Gruppe (der Hintere zieht seitlich vorbei, der Vordere fällt zurück – rollende Schere).
/// Wer hinten liegt, feuert nach vorn: Luft-Luft-Raketen aus der Nase heraus auf den Vorderen, dazu Kanonenstöße. Der Vordere wirft über
/// der Gruppe Täuschkörperfächer, die Raketen gehen auf die Täuschkörper und schlagen in die Symbole darunter ein.
/// Lange Rauchspuren, Wirbelschleppen an den Flügelspitzen, Nachbrenner. Zum Schluss brechen beide mit Nachbrenner auseinander. Keiner gewinnt.
/// Nachbesserung 19.11.2026 („schießen blind irgendwo hin oder teleportieren sich random“): Querversatz an einer geglätteten
/// Bahnrichtung (vorher sprang er in engen Kurven bis 10-fache Fluggeschwindigkeit), Raketen starten in Flugrichtung des Schützen
/// (vorher bis 171° daneben) und treffen nur Symbole unter dem Gegner (vorher bis 40 Symbolgrößen entfernt).
/// </summary>
sealed class DuelRaid : IAttack
{
    internal record Shot(bool ByF35, double Fire, double Impact, Point To);
    internal record Burst(bool ByF35, double Start);
    internal record Fan(bool OfF35, double Start);

    readonly List<Point> line = [];
    readonly List<double> cum = [];
    internal readonly List<Shot> Shots = [];
    readonly List<Burst> bursts = [];
    readonly List<Fan> fans = [];
    internal IReadOnlyList<Burst> Bursts => bursts;
    internal IReadOnlyList<Fan> Fans => fans;
    readonly List<double> passes = [];   // Zeitpunkte, zu denen der Vordere über einer Gruppe ist
    readonly List<double> leads = [];    // +1: F-35A vorn, −1: Su-57 vorn
    readonly double speed, spanF, spanS, uF, uS, along, side, t0, length, breakAt;
    const double Flight = 0.85, TrailLife = 3.2, Smooth = 150;

    public IReadOnlyList<(Point P, double T)> Blasts { get; }
    public IReadOnlyList<double>? BlastSizes { get; }
    public double LastImpact { get; }
    public double End { get; }
    public bool Explodes => true;
    public double Reach { get; }

    public DuelRaid(Rect area, IReadOnlyList<Point> targets, double iconSize)
    {
        spanF = Scale.PerMeter(iconSize) * 10.7; spanS = Scale.PerMeter(iconSize) * 14.1;
        uF = spanF / 68; uS = spanS / 70;
        along = spanS * 1.15; side = spanS * 1.25;
        speed = Math.Max(area.Width / 3.9, spanS * 4.5);
        Reach = iconSize * 0.5;
        breakAt = double.MaxValue / 4;

        // Gruppen: bis zu vier benachbarte Symbole, Reihenfolge als Kette vom linken Rand
        var open = targets.ToList();
        var groups = new List<List<Point>>();
        var inner = area; inner.Inflate(-spanS * 2.5, -spanS * 2.5);
        if (inner.IsEmpty) inner = area;
        var mid = new Point(inner.X + inner.Width / 2, inner.Y + inner.Height / 2);
        var start = new Point(area.Left - spanS * 10, open.Count > 0 ? open.OrderBy(p => p.X).First().Y : mid.Y);
        var way = new List<Point> { start };
        var index = new List<int>();
        Point prev = start;
        double minLeg = speed * 1.8;
        while (open.Count > 0)
        {
            var here = prev;
            var seed = open.OrderBy(p => (p - here).LengthSquared).First();
            var g = open.OrderBy(p => (p - seed).LengthSquared).Take(6).Where(p => (p - seed).Length < iconSize * 4.8).ToList();
            open.RemoveAll(g.Contains);
            var c = new Point(g.Average(p => p.X), g.Average(p => p.Y));
            if (groups.Count > 0 && (c - prev).Length < minLeg)
            {   // zu nah: erst weit ausholen (in den freien Raum), dann zurück – ergibt eine Schleife statt eines Zickzacks
                var outv = mid - prev; if (outv.Length < 1) outv = new Vector(1, 0);
                outv.Normalize();
                double turn = (groups.Count % 2 == 0 ? 1 : -1) * 0.75;
                outv = new Vector(outv.X * Math.Cos(turn) - outv.Y * Math.Sin(turn), outv.X * Math.Sin(turn) + outv.Y * Math.Cos(turn));
                var swing = prev + outv * minLeg * 0.55;
                swing = new Point(Math.Clamp(swing.X, inner.Left, inner.Right), Math.Clamp(swing.Y, inner.Top, inner.Bottom));
                way.Add(swing);
            }
            way.Add(c); index.Add(way.Count - 1); groups.Add(g);
            prev = c;
        }
        var exitDir = way.Count > 1 ? way[^1] - way[^2] : new Vector(1, 0);
        if (exitDir.Length < 1) exitDir = new Vector(1, 0);
        exitDir.Normalize();
        double diag = Math.Sqrt(area.Width * area.Width + area.Height * area.Height);
        way.Add(way[^1] + exitDir * (diag * 0.5));
        way.Add(way[^1] + exitDir * (diag + spanS * 10));
        const int steps = 32;
        Sample(way, steps);
        length = cum[^1];

        // Überflugzeiten: der Vordere (Weg + along) ist über der Gruppe; erste Gruppe frühestens nach 1,6 s
        double firstD = groups.Count > 0 ? cum[index[0] * steps] : 0;
        t0 = Math.Min(0.3, 1.6 - (firstD - along) / speed);
        for (int i = 0; i < groups.Count; i++)
        {
            passes.Add(t0 + (cum[index[i] * steps] - along) / speed);
            leads.Add(i % 2 == 0 ? 1 : -1);
        }

        var blasts = new List<(Point, double)>();
        for (int i = 0; i < groups.Count; i++)
        {
            double tp = passes[i];
            bool byF35 = leads[i] < 0;   // der Hintere schießt
            fans.Add(new Fan(!byF35, tp - 0.4));
            if (i % 2 == 1) bursts.Add(new Burst(byF35, tp - 1.05));
            var g = groups[i].OrderBy(p => (p - PlaneAt(!byF35, tp - 0.4)).Length).ToList();
            for (int k = 0; k < g.Count; k++)
            {
                double impact = tp + 0.05 + k * 0.13, best = impact - Flight, bestAngle = double.MaxValue;
                for (double fire = impact - 1.15; fire <= impact - 0.5; fire += 0.05)
                {
                    double h = Heading(byF35, fire);
                    var to = PlaneAt(!byF35, fire) - PlaneAt(byF35, fire);
                    double off = Math.Abs(Math.IEEERemainder(Math.Atan2(to.Y, to.X) - h, 2 * Math.PI));
                    if (off < bestAngle - 0.02) { bestAngle = off; best = fire; }
                }
                Shots.Add(new Shot(byF35, best, impact, g[k]));
                blasts.Add((g[k], impact));
            }
        }
        Blasts = blasts; BlastSizes = blasts.Select(_ => 1.05).ToList();
        LastImpact = blasts.Count > 0 ? blasts.Max(b => b.Item2) : 2;
        breakAt = (passes.Count > 0 ? passes[^1] : 1.5) + 0.7;
        if (Environment.GetEnvironmentVariable("DK_DUEL_DEBUG") is "1") Console.WriteLine($"gruppen {groups.Count}, ueberfluege {string.Join(" ", passes.Select(p => p.ToString("0.0")))}");
        End = Math.Max(LastImpact + 0.4, breakAt + 3.2);
    }

    /// <summary>Zentripetale Catmull-Rom-Kurve durch die Wegpunkte: keine Schlaufen und Spitzen an dicht liegenden Punkten.</summary>
    void Sample(List<Point> way, int steps)
    {
        for (int i = 0; i < way.Count - 1; i++)
        {
            Point p0 = way[Math.Max(i - 1, 0)], p1 = way[i], p2 = way[i + 1], p3 = way[Math.Min(i + 2, way.Count - 1)];
            if (i == 0) p0 = p1 - (p2 - p1);
            if (i + 2 > way.Count - 1) p3 = p2 + (p2 - p1);
            double k0 = 0, k1 = k0 + Knot(p0, p1), k2 = k1 + Knot(p1, p2), k3 = k2 + Knot(p2, p3);
            for (int s = 0; s < steps; s++)
            {
                double t = k1 + (k2 - k1) * s / steps;
                var a1 = Mix(p0, p1, k0, k1, t); var a2 = Mix(p1, p2, k1, k2, t); var a3 = Mix(p2, p3, k2, k3, t);
                var b1 = Mix(a1, a2, k0, k2, t); var b2 = Mix(a2, a3, k1, k3, t);
                Add(Mix(b1, b2, k1, k2, t));
            }
        }
        Add(way[^1]);
        void Add(Point p) { cum.Add(line.Count == 0 ? 0 : cum[^1] + (p - line[^1]).Length); line.Add(p); }
        static double Knot(Point a, Point b) => Math.Max(1e-3, Math.Sqrt((b - a).Length));
        static Point Mix(Point a, Point b, double ka, double kb, double t) { double w = (t - ka) / (kb - ka); return a + (b - a) * w; }
    }

    Point OnLine(double d)
    {
        if (d <= 0) { var v0 = line[1] - line[0]; v0.Normalize(); return line[0] + v0 * d; }
        if (d >= length) { var v1 = line[^1] - line[^2]; v1.Normalize(); return line[^1] + v1 * (d - length); }
        int i = cum.BinarySearch(d); if (i < 0) i = ~i - 1;
        i = Math.Clamp(i, 0, line.Count - 2);
        double seg = cum[i + 1] - cum[i], k = seg > 0 ? (d - cum[i]) / seg : 0;
        return line[i] + (line[i + 1] - line[i]) * k;
    }

    internal double BreakAt => breakAt;

    /// <summary>Führung der F-35A: +1 vorn, −1 hinten. Wechsel in den ersten 55 % zwischen zwei Überflügen (weich),
    /// damit beim Schießen und Täuschen die Rollen klar sind.</summary>
    double Lead(double t)
    {
        if (passes.Count == 0) return 1;
        if (t <= passes[0]) return leads[0];
        for (int i = 0; i < passes.Count - 1; i++)
        {
            if (t > passes[i + 1]) continue;
            double gap = passes[i + 1] - passes[i];
            double k = Math.Clamp((t - passes[i] - 0.2) / (gap * 0.55), 0, 1);
            return leads[i] + (leads[i + 1] - leads[i]) * Fly.Smooth(k);
        }
        return leads[^1];
    }

    bool F35Ahead(double t) => Lead(t) > 0;

    /// <summary>Lage eines Jets: Mitte des Paares auf dem Weg, Längsversatz nach Führung, Querversatz beim Überholen (an der
    /// über ±150 px geglätteten Bahnrichtung). Nach dem Auseinanderbrechen zieht jeder zu seiner Seite weg.</summary>
    internal Point PlaneAt(bool f35, double t)
    {
        double sign = f35 ? 1 : -1, lead = Lead(t);
        double brk = Fly.Smooth((t - breakAt) / 1.6);
        double d = (t - t0) * speed + sign * along * lead + brk * sign * speed * 0.3 * Math.Max(0, t - breakAt);
        var p = OnLine(d);
        var (n, straight) = Across(d);
        double lateral = sign * side * (1 - lead * lead) * straight + sign * brk * brk * speed * 0.9 * Math.Max(0, t - breakAt);
        return p + n * lateral;
    }

    /// <summary>Querrichtung, gemittelt über ±0,6 s Flugweg, und wie gerade die Bahn dort ist (1 gerade … 0 Haarnadel).
    /// Ohne Mittelung kippte die Querrichtung in engen Kurven und der Jet sprang seitlich.</summary>
    (Vector N, double Straight) Across(double d)
    {
        var sum = new Vector(); const int n = 12; double reach = speed * 0.7;
        for (int j = -n; j <= n; j++)
        {
            double x = d + reach * j / n;
            var v = OnLine(x + Smooth * 0.5) - OnLine(x - Smooth * 0.5);
            if (v.Length > 0) { v.Normalize(); sum += v; }
        }
        double straight = sum.Length / (2 * n + 1);
        if (sum.Length < 1e-6) return (new Vector(0, 0), 0);
        sum.Normalize();
        return (new Vector(-sum.Y, sum.X), Fly.Smooth(Math.Clamp((straight - 0.6) / 0.35, 0, 1)));
    }

    double Heading(bool f35, double t)
    {
        var a = PlaneAt(f35, t - 0.04); var b = PlaneAt(f35, t + 0.04);
        return Math.Atan2(b.Y - a.Y, b.X - a.X);
    }

    /// <summary>Rakete als kubische Kurve: aus der Nase in Flugrichtung des Schützen, auf den Gegner zu, dann auf die Täuschkörper
    /// über dem Symbol.</summary>
    Point MissileAt(Shot s, double k)
    {
        double h = Heading(s.ByF35, s.Fire);
        var dir = new Vector(Math.Cos(h), Math.Sin(h));
        var p0 = PlaneAt(s.ByF35, s.Fire) + dir * (s.ByF35 ? spanF : spanS) * 0.55;
        var p1 = p0 + dir * (speed * 0.5 + spanS * 1.2);
        var target = PlaneAt(!s.ByF35, s.Impact - 0.3);
        var p2 = target + (s.To - target) * 0.45;   // auf den Gegner zu, dann zu den Täuschkörpern über dem Symbol
        var p3 = s.To;
        double m = 1 - k;
        return new Point(m * m * m * p0.X + 3 * m * m * k * p1.X + 3 * m * k * k * p2.X + k * k * k * p3.X,
                         m * m * m * p0.Y + 3 * m * m * k * p1.Y + 3 * m * k * k * p2.Y + k * k * k * p3.Y);
    }

    /// <summary>Messwerte für die Prüfung: größte Geschwindigkeit eines Jets (Vielfaches der Fluggeschwindigkeit; Sprünge zeigen sich
    /// als großer Wert), größter Winkel zwischen Raketenstart und Flugrichtung des Schützen (Grad), größter Abstand Gegner–Einschlag
    /// (in Symbolgrößen), kleinster Längsabstand Schütze hinter Gegner beim Abschuss (in Spannweiten; negativ = davor).</summary>
    internal (double SpeedRatio, double LaunchAngle, double TargetMiss, double Behind) Metrics(double iconSize)
    {
        double maxV = 0;
        for (double t = 0.5; t < breakAt; t += 1 / 60.0)
            foreach (bool f in new[] { true, false })
            {
                double v = (PlaneAt(f, t + 1 / 60.0) - PlaneAt(f, t)).Length * 60 / speed;
                if (v > 2.2 && Environment.GetEnvironmentVariable("DK_DUEL_DEBUG") is "1") Console.WriteLine($"spitze t={t:0.00} {(f ? "F35" : "Su57")} {v:0.0}x lead={Lead(t):0.00} d={(t - t0) * speed:0}");
                maxV = Math.Max(maxV, v);
            }
        double angle = 0, miss = 0, behind = double.MaxValue;
        foreach (var s in Shots)
        {
            var launch = MissileAt(s, 0.02) - MissileAt(s, 0);
            double h = Heading(s.ByF35, s.Fire);
            angle = Math.Max(angle, Math.Abs(Math.IEEERemainder(Math.Atan2(launch.Y, launch.X) - h, 2 * Math.PI)) * 180 / Math.PI);
            miss = Math.Max(miss, (PlaneAt(!s.ByF35, s.Impact - 0.3) - s.To).Length / iconSize);
            var to = PlaneAt(!s.ByF35, s.Fire) - PlaneAt(s.ByF35, s.Fire);
            behind = Math.Min(behind, 180 - Math.Abs(Math.IEEERemainder(Math.Atan2(to.Y, to.X) - h, 2 * Math.PI)) * 180 / Math.PI);
        }
        return (maxV, angle, miss, behind == double.MaxValue ? 0 : behind);
    }

    static readonly Brush Smoke = Fly.B("#E6EEF0F2"), Vapor = Fly.B("#FFFFFFFF"), Burner = Art.Glow("#F0FF9A3C"), BurnerCore = Art.Glow("#F8FFE8C0"), Muzzle = Art.Glow("#F0FFD27A");

    public void Draw(DrawingContext dc, double t)
    {
        // Rauchspuren aller Raketen: ganzer Weg, jeder Punkt verblasst mit seinem Alter
        foreach (var s in Shots)
        {
            if (t < s.Fire || t > s.Impact + TrailLife) continue;
            double head = Math.Min(1, (t - s.Fire) / (s.Impact - s.Fire));
            var u = s.ByF35 ? uF : uS;
            Point prev = MissileAt(s, 0);
            for (int i = 1; i <= 26; i++)
            {
                double k = head * i / 26;
                var p = MissileAt(s, k);
                double age = t - (s.Fire + k * (s.Impact - s.Fire));
                double fade = 1 - Math.Clamp(age / TrailLife, 0, 1);
                if (fade > 0.02)
                {
                    dc.DrawLine(Soft.FadePen(Smoke, (4 + 9 * (1 - fade)) * u * 1.3, 0.28 * fade * fade), prev, p);
                    dc.DrawLine(Soft.FadePen(Smoke, (1.6 + 2 * (1 - fade)) * u * 1.3, 0.75 * fade * fade), prev, p);
                }
                prev = p;
            }
        }
        // Täuschkörperfächer („Engel“): je Seite drei Glutpunkte, die nach außen und hinten abfallen
        foreach (var f in fans)
        {
            double age = t - f.Start;
            if (age < 0 || age > 1.8) continue;
            var origin = PlaneAt(f.OfF35, f.Start);
            double h = Heading(f.OfF35, f.Start);
            var fwd = new Vector(Math.Cos(h), Math.Sin(h)); var n = new Vector(-fwd.Y, fwd.X);
            double span = f.OfF35 ? spanF : spanS;
            for (int k = 0; k < 6; k++)
            {
                double sgn = k % 2 == 0 ? 1 : -1, lane = 1 + k / 2;
                double a = Math.Max(0, age - k * 0.06);
                Point FlareAt(double x) => origin + n * (sgn * span * (0.9 + 0.55 * lane) * (1 - Math.Exp(-x * 2.2))) - fwd * (span * 1.6 * x + span * 0.5 * x * x * lane);
                Planes.Flare(dc, FlareAt(Math.Max(0, a - 0.22)), FlareAt(a), (f.OfF35 ? uF : uS) * 1.3, a, 1.8);
            }
        }
        // Raketen im Flug
        foreach (var s in Shots)
        {
            if (t < s.Fire || t >= s.Impact) continue;
            double k = (t - s.Fire) / (s.Impact - s.Fire);
            var p = MissileAt(s, k); var q = MissileAt(s, Math.Max(0, k - 0.03));
            var u = s.ByF35 ? uF : uS;
            Planes.Rocket(dc, q, p, u * 1.3, t);
            Planes.Maverick(dc, p, Math.Atan2(p.Y - q.Y, p.X - q.X), u * 0.75);
        }
        // Kanonenstöße: Leuchtspur nach vorn, Mündungsfeuer
        foreach (var b in bursts)
        {
            if (t < b.Start || t > b.Start + 1.1) continue;
            var u = b.ByF35 ? uF : uS;
            for (int i = 0; i < 9; i++)
            {
                double shot = b.Start + i * 0.07, age = t - shot;
                if (age < 0 || age > 0.42) continue;
                var gun = PlaneAt(b.ByF35, shot); double h = Heading(b.ByF35, shot);
                var fwd = new Vector(Math.Cos(h), Math.Sin(h));
                var at = gun + fwd * ((b.ByF35 ? spanF : spanS) * 0.6 + speed * 2.4 * age) + new Vector(-fwd.Y, fwd.X) * ((i % 3 - 1) * u * 2);
                Planes.Tracer(dc, at - fwd * u * 10, at, u * 1.2);
            }
            if (t < b.Start + 0.6 && (int)(t * 30) % 2 == 0)
            {
                var gun = PlaneAt(b.ByF35, t); double h = Heading(b.ByF35, t);
                Soft.Draw(dc, Muzzle, gun + new Vector(Math.Cos(h), Math.Sin(h)) * (b.ByF35 ? spanF : spanS) * 0.55, u * 6, u * 6, 0.9);
            }
        }
        // Jets: wer hinten ist, zuerst
        bool fFirst = F35Ahead(t);
        foreach (bool f in fFirst ? new[] { false, true } : new[] { true, false }) Jet(dc, f, t);
    }

    void Jet(DrawingContext dc, bool f35, double t)
    {
        var at = PlaneAt(f35, t); double h = Heading(f35, t);
        double span = f35 ? spanF : spanS, u = f35 ? uF : uS;
        var fwd = new Vector(Math.Cos(h), Math.Sin(h)); var n = new Vector(-fwd.Y, fwd.X);
        // Kurvenlast aus der Richtungsänderung: Wirbelschleppen werden dichter
        double turn = Math.Abs(Math.IEEERemainder(Heading(f35, t + 0.1) - Heading(f35, t - 0.1), 2 * Math.PI));
        double g = Math.Clamp(turn * 4, 0.15, 1) + Fly.Smooth((t - breakAt) / 0.6) * 0.6;
        double tip = span * 0.48;
        for (int sgn = -1; sgn <= 1; sgn += 2)
        {
            Point? prev = null;
            for (int i = 0; i <= 18; i++)
            {
                double ti = t - i * 0.045;
                var p = PlaneAt(f35, ti); double hi = Heading(f35, ti);
                var ni = new Vector(-Math.Sin(hi), Math.Cos(hi));
                var w = p + ni * (sgn * tip) - new Vector(Math.Cos(hi), Math.Sin(hi)) * span * 0.3;
                if (prev is { } q && (w - q).Length < span * 0.9) dc.DrawLine(Soft.FadePen(Vapor, u * (1.4 + i * 0.16), 0.32 * g * (1 - i / 19.0)), q, w);
                prev = w;
            }
        }
        // Nachbrenner: flackernde Glut hinter den Düsen, beim Auseinanderbrechen voll
        double ab = 0.55 + 0.25 * Math.Sin(t * 47 + (f35 ? 0 : 1.3)) + Fly.Smooth((t - breakAt) / 0.5) * 0.5;
        double back = span * (f35 ? 0.6 : 0.62);
        var nozzles = f35 ? new[] { 0.0 } : new[] { -0.1, 0.1 };
        foreach (double o in nozzles)
        {
            var c = at - fwd * back + n * (o * span);
            dc.PushTransform(new RotateTransform(h * 180 / Math.PI, c.X, c.Y));
            Soft.Draw(dc, Burner, new Point(c.X - span * 0.12 * ab, c.Y), span * 0.22 * ab, span * 0.09 * ab, 0.85);
            Soft.Draw(dc, BurnerCore, c, span * 0.08 * ab, span * 0.045 * ab, 0.95);
            dc.Pop();
        }
        if (f35) Planes.F35(dc, at, h, u, 0.45, t);
        else Planes.Su57(dc, at, h, u, 0.45, t);
    }
}
