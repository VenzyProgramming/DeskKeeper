using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DeskKeeper;

/// <summary>
/// Duell F-35A gegen Su-57 (19.11.2026: „Duelle zwischen SU und F“, „soll episch aussehen“): beide jagen sich in einer
/// rollenden Schere über die Symbolgruppen, mal ist der eine vorn, mal der andere. Wer hinten liegt, feuert Luft-Luft-Raketen und
/// Kanonenstöße; der Gejagte wirft Täuschkörperfächer, die Raketen lassen sich ablenken und schlagen in die Symbole ein.
/// Lange Rauchspuren, Wirbelschleppen an den Flügelspitzen, Nachbrenner. Zum Schluss brechen beide mit Nachbrenner auseinander.
/// Keiner gewinnt.
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
    readonly double speed, spanF, spanS, uF, uS, along, side, t0, length, breakAt;
    const double Period = 5.2, Flight = 1.7, TrailLife = 3.2;

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
        along = spanS * 2.2; side = spanS * 1.7;
        speed = Math.Max(area.Width / 4.6, spanS * 5);
        Reach = iconSize * 0.5;
        t0 = 0.3;
        breakAt = double.MaxValue / 4;

        // Gruppen: je bis zu drei benachbarte Symbole
        var open = targets.ToList();
        var groups = new List<List<Point>>();
        while (open.Count > 0)
        {
            var seed = open.OrderBy(p => p.X).ThenBy(p => p.Y).First();
            var g = open.OrderBy(p => (p - seed).LengthSquared).Take(3).Where(p => (p - seed).Length < iconSize * 2.6).ToList();
            open.RemoveAll(g.Contains); groups.Add(g);
        }

        // Kampfraum: Symbolfläche großzügig erweitert, innerhalb der Bildschirme. Weite, sich kreuzende Bögen darüber
        // (Winkelschritt 2,4 rad ergibt einen Stern aus langen Kurven), Einflug vom linken Rand.
        var box = targets.Count > 0 ? new Rect(targets[0], targets[0]) : area;
        foreach (var p in targets) box.Union(p);
        box.Inflate(area.Width * 0.28, area.Height * 0.3);
        box.Intersect(area);
        if (box.IsEmpty || box.Width < area.Width * 0.4) box = area;
        box.Inflate(-spanS * 1.5, -spanS * 1.5);
        var mid = new Point(box.X + box.Width / 2, box.Y + box.Height / 2);
        double rx = box.Width / 2, ry = box.Height / 2;
        int count = Math.Max(1, groups.Count);
        double interval = Math.Clamp(7.6 / count, 0.45, 1.3), firstFire = 1.3;
        double needed = speed * (firstFire + interval * count + Flight + 4.5);
        var way = new List<Point> { new(area.Left - spanS * 10, mid.Y + ry * 0.2), new(box.Left + rx * 0.35, mid.Y - ry * 0.55) };
        double run = (way[1] - way[0]).Length;
        for (int k = 0; run < needed * 1.15; k++)
        {
            double th = 2.2 + k * 2.4;
            var p = new Point(mid.X + Math.Cos(th) * rx * (0.78 + 0.22 * Math.Sin(k * 1.7)), mid.Y + Math.Sin(th) * ry * (0.75 + 0.25 * Math.Cos(k * 1.3)));
            run += (p - way[^1]).Length * 1.1;
            way.Add(p);
        }
        const int steps = 32;
        Sample(way, steps);
        length = cum[^1];

        // Feuerplan: in festen Abständen; jeweils die Gruppe, die dem Paar in einer Sekunde am nächsten liegt. Wer hinten liegt, schießt.
        var blasts = new List<(Point, double)>();
        var left = groups.ToList();
        for (int i = 0; left.Count > 0; i++)
        {
            double fire0 = firstFire + i * interval;
            var pair = PlaneAt(true, fire0 + 1) + (PlaneAt(false, fire0 + 1) - PlaneAt(true, fire0 + 1)) * 0.5;
            var g = left.OrderBy(x => (new Point(x.Average(p => p.X), x.Average(p => p.Y)) - pair).LengthSquared).First();
            left.Remove(g);
            bool byF35 = !F35Ahead(fire0);
            fans.Add(new Fan(!byF35, fire0 + 0.25));
            if (i % 2 == 0) bursts.Add(new Burst(byF35, Math.Max(0.4, fire0 - 0.55)));
            for (int k = 0; k < g.Count; k++)
            {
                double fire = fire0 + k * 0.2;
                Shots.Add(new Shot(byF35, fire, fire + Flight, g[k]));
                blasts.Add((g[k], fire + Flight));
            }
        }
        Blasts = blasts; BlastSizes = blasts.Select(_ => 1.05).ToList();
        LastImpact = blasts.Count > 0 ? blasts.Max(b => b.Item2) : 2;
        breakAt = Math.Max(LastImpact - 0.6, 1);
        End = Math.Max(LastImpact + 0.4, breakAt + 3.2);
    }

    void Sample(List<Point> way, int steps)
    {
        for (int i = 0; i < way.Count - 1; i++)
        {
            Point p0 = way[Math.Max(i - 1, 0)], p1 = way[i], p2 = way[i + 1], p3 = way[Math.Min(i + 2, way.Count - 1)];
            for (int s = 0; s < steps; s++)
            {
                double k = s / (double)steps, k2 = k * k, k3 = k2 * k;
                double x = 0.5 * (2 * p1.X + (-p0.X + p2.X) * k + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * k2 + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * k3);
                double y = 0.5 * (2 * p1.Y + (-p0.Y + p2.Y) * k + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * k2 + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * k3);
                Add(new Point(x, y));
            }
        }
        Add(way[^1]);
        void Add(Point p) { cum.Add(line.Count == 0 ? 0 : cum[^1] + (p - line[^1]).Length); line.Add(p); }
    }

    (Point P, Vector Dir) OnLine(double d)
    {
        if (d <= 0) { var v0 = line[1] - line[0]; v0.Normalize(); return (line[0] + v0 * d, v0); }
        if (d >= length) { var v1 = line[^1] - line[^2]; v1.Normalize(); return (line[^1] + v1 * (d - length), v1); }
        int i = cum.BinarySearch(d); if (i < 0) i = ~i - 1;
        i = Math.Clamp(i, 0, line.Count - 2);
        double seg = cum[i + 1] - cum[i], k = seg > 0 ? (d - cum[i]) / seg : 0;
        var v = line[i + 1] - line[i]; if (v.Length > 0) v.Normalize();
        return (line[i] + (line[i + 1] - line[i]) * k, v);
    }

    internal double BreakAt => breakAt;

    bool F35Ahead(double t) => Math.Sin(2 * Math.PI * (t - t0) / Period) > 0;

    /// <summary>Lage eines Jets: Mitte des Paares auf dem Weg, dazu Versatz der Schere (längs und quer); nach dem Auseinanderbrechen
    /// zieht jeder in seine Richtung weg.</summary>
    internal Point PlaneAt(bool f35, double t)
    {
        double phase = 2 * Math.PI * (t - t0) / Period, sign = f35 ? 1 : -1;
        double brk = Fly.Smooth((t - breakAt) / 1.6);
        double d = (t - t0) * speed + sign * along * Math.Sin(phase) * (1 - brk) + brk * sign * speed * 0.35 * (t - breakAt);
        var (p, v) = OnLine(d);
        var n = new Vector(-v.Y, v.X);
        double lateral = sign * side * Math.Cos(phase) * (1 - brk) + sign * brk * brk * speed * 0.9 * Math.Max(0, t - breakAt);
        return p + n * lateral;
    }

    double Heading(bool f35, double t)
    {
        var a = PlaneAt(f35, t - 0.03); var b = PlaneAt(f35, t + 0.03);
        return Math.Atan2(b.Y - a.Y, b.X - a.X);
    }

    static readonly Brush Smoke = Fly.B("#E6EEF0F2"), Vapor = Fly.B("#FFFFFFFF"), Burner = Art.Glow("#F0FF9A3C"), BurnerCore = Art.Glow("#F8FFE8C0"), Muzzle = Art.Glow("#F0FFD27A");

    public void Draw(DrawingContext dc, double t)
    {
        // Rauchspuren aller Raketen: ganzer Weg, jeder Punkt verblasst mit seinem Alter
        foreach (var s in Shots)
        {
            if (t < s.Fire || t > s.Impact + TrailLife) continue;
            var (from, ctrl) = MissileFrame(s);
            double head = Math.Min(1, (t - s.Fire) / (s.Impact - s.Fire));
            var u = s.ByF35 ? uF : uS;
            Point prev = Fly.Bez(from, ctrl, s.To, 0);
            for (int i = 1; i <= 26; i++)
            {
                double k = head * i / 26;
                var p = Fly.Bez(from, ctrl, s.To, k);
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
            var (from, ctrl) = MissileFrame(s);
            double k = (t - s.Fire) / (s.Impact - s.Fire);
            var p = Fly.Bez(from, ctrl, s.To, k); var q = Fly.Bez(from, ctrl, s.To, Math.Max(0, k - 0.03));
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

    (Point From, Point Ctrl) MissileFrame(Shot s)
    {
        var from = PlaneAt(s.ByF35, s.Fire);
        var ctrl = PlaneAt(!s.ByF35, s.Fire + 0.55);   // auf den Gegner zu, dann von den Täuschkörpern abgelenkt
        return (from, ctrl);
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
