using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DeskKeeper;

// ================================================================ F-35A, Su-57, B-2 Spirit, Saugroboter 

static partial class Planes
{
    // ---------------------------------------------------------------- F-35A Lightning II (Länge 15,7 m, Spannweite 10,7 m)
    // Rautenflügel (Vorderkante 34°, Hinterkante leicht vorwärts gepfeilt), Rumpf mit Kanten (Chines) und seitlichen DSI-Einläufen,
    // nach außen geneigte Doppelseitenleitwerke, Höhenleitwerk, ein Triebwerk mit Düse, rahmenlose goldbedampfte Haube, innere Waffenschächte.
    // 100 Einheiten = Länge, Spannweite 68 Einheiten.
    static readonly Geometry LightningBody = Fly.Both("M50,0 L47,1.4 L42,3 L36,4.4 L28,5.8 L22,7.6 L14,8.6 L0,9 L-20,9 L-34,7.8 L-42,5.6 L-47,3.6 L-50,3 L-50,0 Z");
    static readonly Geometry LightningWing = Fly.Both("M8,8.6 L-9,34 L-14.5,34 L-27,8.6 Z");
    static readonly Geometry LightningTail = Fly.Both("M-33,7 L-43,21 L-48.5,21 L-49,5.6 Z");
    static readonly Geometry LightningFins = Fly.Both("M-26,6.4 L-40,9.4 L-46.5,10.8 L-46.5,8.6 L-33,6 Z");
    static readonly Geometry LightningIntakes = Fly.Both("M24,5.8 C22,7.6 18,8.4 14,8.6 L14,6.6 C18,6.4 21,6 24,5.8 Z");
    static readonly Geometry LightningCanopy = Fly.G("M37.5,0 C37,-1.6 35.4,-2.3 33,-2.4 L24,-2.2 C23,-1.4 23,1.4 24,2.2 L33,2.4 C35.4,2.3 37,1.6 37.5,0 Z");
    static readonly Geometry LightningSeams = Fly.G("M-24,-9 L-12.5,-30 M-24,9 L-12.5,30 M-44,-6 L-46.5,-19 M-44,6 L-46.5,19 M20,-0.4 L-36,-0.4 M20,0.4 L-36,0.4 " +
        "M42,-3 L28,-5.8 M42,3 L28,5.8 M10,-6 L-26,-6 M10,6 L-26,6 M-42,-5.6 L-47,-3.6 M-42,5.6 L-47,3.6");
    static readonly Brush LightningTop = Fly.B("#646C73"), LightningShade = Fly.B("#5A6168"), LightningGold = Fly.B("#7A6A3C"), LightningDark = Fly.B("#2A2E32");
    static readonly Pen LightningEdge = Fly.P("#3A4045", 0.35), LightningSeam = Fly.P("#525A60", 0.22);

    /// <param name="bay">0 … 1: Waffenschachtklappen offen</param>
    public static void F35(DrawingContext dc, Point at, double heading, double u, double altitude, double t, double bay = 0)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, LightningWing, LightningTail, LightningBody);
        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawGeometry(LightningShade, LightningEdge, LightningWing);
        dc.DrawGeometry(LightningShade, LightningEdge, LightningTail);
        dc.DrawGeometry(LightningTop, LightningEdge, LightningBody);
        dc.DrawGeometry(LightningDark, null, LightningIntakes);
        dc.DrawGeometry(null, LightningSeam, LightningSeams);
        if (bay > 0.01)
            foreach (double s in new[] { -1.0, 1.0 })
            {   // Klappen klappen nach außen auf, darunter der dunkle Schacht
                dc.DrawRectangle(LightningDark, null, new Rect(-24, s > 0 ? 1.6 : -5.6, 18, 4));
                dc.DrawRectangle(LightningShade, LightningEdge, new Rect(-24, s > 0 ? 5.6 : -5.6 - 3.4 * bay, 18, 3.4 * bay));
            }
        dc.DrawGeometry(LightningShade, LightningEdge, LightningFins);
        dc.DrawGeometry(LightningGold, LightningEdge, LightningCanopy);
        dc.DrawEllipse(LightningDark, LightningEdge, new Point(-50.4, 0), 1.2, 3.0);   // Düse
        dc.DrawEllipse(LightningDark, null, new Point(44, 0), 0.9, 0.6);               // EOTS-Fenster unter der Nase
        Soft.Draw(dc, HogRed, new Point(-12, -33.6), 1, 1, 1);
        Soft.Draw(dc, HogGreen, new Point(-12, 33.6), 1, 1, 1);
        dc.Pop();
    }

    // ---------------------------------------------------------------- Су-57 (Länge 20,1 m, Spannweite 14,1 m)
    // Flacher, tragender Rumpf mit weit getrennten Triebwerksgondeln, bewegliche Vorderkantenwurzeln (LEVCON), Flügel 48°,
    // ganz bewegliche, geneigte kleine Seitenleitwerke, ganz bewegliches Höhenleitwerk, Heckstachel, getrennte Düsen. 100 Einheiten = Länge.
    static readonly Geometry FelonBody = Fly.Both("M50,0 L46,1.8 L40,3.6 L30,5.4 L22,7 L14,10 L6,12 L-10,12 L-30,11 L-38,9.6 L-42,8 L-44,6.6 L-44,4 L-38,2.2 L-50,1.4 L-50,0 Z");
    static readonly Geometry FelonWing = Fly.Both("M6,12 L-17,35 L-23,35 L-25,12 Z");
    static readonly Geometry FelonTail = Fly.Both("M-30,11 L-38,24 L-44,24 L-44.5,10 Z");
    static readonly Geometry FelonFins = Fly.Both("M-22,7.6 L-36,9.8 L-42,10.8 L-42,9.2 L-28,7.2 Z");
    static readonly Geometry FelonLevcon = Fly.Both("M22,7 L14,10 L6,12 L8,10 L16,8 Z");
    static readonly Geometry FelonCanopy = Fly.G("M36.5,0 C36,-1.6 34.6,-2.2 32.4,-2.3 L25,-2.1 C24.2,-1.3 24.2,1.3 25,2.1 L32.4,2.3 C34.6,2.2 36,1.6 36.5,0 Z");
    static readonly Geometry FelonCamo = Fly.G("M18,-6 L4,-11 L-8,-11 L2,-4 Z M18,6 L4,11 L-8,11 L2,4 Z M-12,-14 L-20,-28 L-17,-33 L-8,-15 Z M-12,14 L-20,28 L-17,33 L-8,15 Z " +
        "M-30,-1 L-40,-3 L-38,1 Z M10,-2 L-20,-2 L-24,1 L6,1 Z M-34,-13 L-40,-21 L-42,-14 Z M-34,13 L-40,21 L-42,14 Z");
    static readonly Geometry FelonSeams = Fly.G("M-22,-12.4 L-21,-33 M-22,12.4 L-21,33 M-41,-11 L-43,-22 M-41,11 L-43,22 M-6,-4 L-34,-3 M-6,4 L-34,3 M30,-5.4 L30,5.4 M6,-12 L6,12");
    static readonly Brush FelonTop = Fly.B("#9AA6AE"), FelonShade = Fly.B("#8B979F"), FelonPatch = Fly.B("#6F7D88"), FelonGold = Fly.B("#8A7A48"), FelonDark = Fly.B("#2B3034");
    static readonly Pen FelonEdge = Fly.P("#56616A", 0.35), FelonSeam = Fly.P("#7C8890", 0.22);

    public static void Su57(DrawingContext dc, Point at, double heading, double u, double altitude, double t, double bay = 0)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, FelonWing, FelonTail, FelonBody);
        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawGeometry(FelonShade, FelonEdge, FelonWing);
        dc.DrawGeometry(FelonShade, FelonEdge, FelonTail);
        dc.DrawGeometry(FelonTop, FelonEdge, FelonBody);
        dc.DrawGeometry(FelonShade, FelonEdge, FelonLevcon);
        dc.DrawGeometry(FelonPatch, null, FelonCamo);
        dc.DrawGeometry(null, FelonSeam, FelonSeams);
        if (bay > 0.01)
            foreach (double s in new[] { -1.0, 1.0 })
            {   // zwei Schächte hintereinander zwischen den Gondeln
                dc.DrawRectangle(FelonDark, null, new Rect(-30, s > 0 ? 0.6 : -3.6, 26, 3));
                dc.DrawRectangle(FelonShade, FelonEdge, new Rect(-30, s > 0 ? 3.6 : -3.6 - 2.6 * bay, 26, 2.6 * bay));
            }
        dc.DrawGeometry(FelonShade, FelonEdge, FelonFins);
        dc.DrawGeometry(FelonGold, FelonEdge, FelonCanopy);
        foreach (double s in new[] { -1.0, 1.0 })
        {
            dc.DrawEllipse(FelonDark, FelonEdge, new Point(-44.5, 5.3 * s), 1.0, 2.0);   // getrennte Düsen
            Art.Star(dc, new Point(-14, 24 * s), 1.8, 0);
        }
        dc.DrawEllipse(FelonDark, null, new Point(38.5, -1.4), 0.7, 0.7);   // Infrarotsensor vor der Haube
        Soft.Draw(dc, HogRed, new Point(-19, -34.6), 1, 1, 1);
        Soft.Draw(dc, HogGreen, new Point(-19, 34.6), 1, 1, 1);
        dc.Pop();
    }

    // ---------------------------------------------------------------- B-2 Spirit (Spannweite 52,4 m, Länge 21 m)
    // Nurflügel: gerade Vorderkante 33°, Hinterkante als doppeltes W, vier eingegrabene Triebwerke mit Einläufen oben und flachen
    // Abgasrinnen, kleines Cockpit an der Spitze. 100 Einheiten = Spannweite (Länge 40 Einheiten).
    static readonly Geometry SpiritWing = Fly.Both("M20,0 L-12.5,50 L-15,50.4 L-17,49 L-9.5,35 L-19.5,21 L-13,10 L-20,0 Z");
    static readonly Geometry SpiritHump = Fly.Both("M17,0 C15,2.6 10,4.6 2,5.4 L-12,4.6 L-16,0 Z");
    static readonly Geometry SpiritIntakes = Fly.Both("M8.5,5.4 L6.6,9.4 L5.2,9 L7,5.2 Z M6,11 L4.2,14.6 L2.8,14.2 L4.6,10.6 Z");
    static readonly Geometry SpiritExhaust = Fly.Both("M-6,6 L-12,8 L-12.5,10 L-6.5,8.4 Z M-8,12 L-14,13.6 L-14.4,15.4 L-8.5,14.2 Z");
    static readonly Geometry SpiritGlass = Fly.Both("M15.6,0.3 L14.6,2.2 L13.6,2.2 L14.4,0.3 Z M13.4,0.3 L12.6,2.4 L11.8,2.4 L12.4,0.3 Z");
    static readonly Geometry SpiritSeams = Fly.G("M-9.5,-35 L-6,-43 M-9.5,35 L-6,43 M-19.5,-21 L-14,-28 M-19.5,21 L-14,28 M-13,-10 L-16,-16 M-13,10 L-16,16 M-20,0 L-16,0");
    static readonly Brush SpiritTop = Fly.B("#40464C"), SpiritShade = Fly.B("#383E44"), SpiritDark = Fly.B("#202428");
    static readonly Pen SpiritEdge = Fly.P("#2C3136", 0.25), SpiritSeam = Fly.P("#4C535A", 0.18);

    public static void B2(DrawingContext dc, Point at, double heading, double u, double altitude, double t)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, SpiritWing);
        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawGeometry(SpiritShade, SpiritEdge, SpiritWing);
        dc.DrawGeometry(SpiritTop, SpiritEdge, SpiritHump);
        dc.DrawGeometry(SpiritDark, null, SpiritIntakes);
        dc.DrawGeometry(SpiritDark, null, SpiritExhaust);
        dc.DrawGeometry(null, SpiritSeam, SpiritSeams);
        dc.DrawGeometry(SpiritDark, SpiritEdge, SpiritGlass);
        if ((t % 1.5) < 0.08) { Soft.Draw(dc, HogRed, new Point(-13.5, -50), 0.8, 0.8, 1); Soft.Draw(dc, HogGreen, new Point(-13.5, 50), 0.8, 0.8, 1); }
        dc.Pop();
    }

    // ---------------------------------------------------------------- Waffen
    static readonly Brush BombGrey = Fly.B("#7C848A"), BombOlive = Fly.B("#5B6550"), Seeker2 = Fly.B("#B9C7D2");

    /// <summary>GBU-39 Gleitbombe: schlanker Körper, ausgeklappte Gleitflügel.</summary>
    public static void Sdb(DrawingContext dc, Point at, double heading, double u)
    {
        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawGeometry(BombGrey, BombEdge, Fly.Both("M-1,0.4 L-3.4,5.5 L-4.2,5.5 L-2.6,0.4 Z"));
        dc.DrawRoundedRectangle(BombGrey, BombEdge, new Rect(-9, -0.9, 18, 1.8), 0.9, 0.9);
        dc.DrawGeometry(BombGrey, BombEdge, Fly.Both("M-8,0.8 L-9.6,2.6 L-10.2,2.6 L-9.2,0.8 Z"));
        dc.DrawEllipse(Seeker2, null, new Point(8.4, 0), 0.6, 0.6);
        dc.Pop();
    }

    /// <summary>GBU-31 JDAM: schwerer Bombenkörper mit Leitwerkssatz und Kufen.</summary>
    public static void Jdam(DrawingContext dc, Point at, double heading, double u)
    {
        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawRoundedRectangle(BombOlive, BombEdge, new Rect(-9, -1.6, 18, 3.2), 1.6, 1.6);
        dc.DrawRectangle(BombGrey, BombEdge, new Rect(-9.5, -1.4, 4.5, 2.8));
        dc.DrawGeometry(BombGrey, BombEdge, Fly.Both("M-7,1.4 L-9.8,3.8 L-10.6,3.8 L-9,1.4 Z"));
        dc.DrawGeometry(BombGrey, BombEdge, Fly.Both("M1,1.5 L-3,2.4 L-4,1.5 Z"));
        dc.Pop();
    }
}

/// <summary>
/// Tarnkappen-Angriff (F-35A mit GBU-39, Su-57 mit Lenkflugkörpern aus den inneren Schächten): ein schneller Überflug je Symbolgruppe,
/// kurz vor der Gruppe öffnen sich die Schächte, je Symbol eine Waffe, die auf ihr Ziel gleitet; danach Schächte zu und hinaus.
/// </summary>
sealed class StealthRaid : IAttack
{
    public enum Kind { F35, Su57 }
    record Pass(double Start, double End, Point From, Vector Dir, double Length, double Release);
    record Weapon(Point From, Point To, double Fire, double Impact);
    readonly Kind kind;
    readonly List<Pass> passes = [];
    readonly List<Weapon> weapons = [];
    readonly double span, u, speed;
    public IReadOnlyList<(Point P, double T)> Blasts { get; }
    public IReadOnlyList<double>? BlastSizes { get; }
    public double LastImpact { get; }
    public double End { get; }
    public bool Explodes => true;
    public double Reach { get; }

    public StealthRaid(Kind kind, Rect area, IReadOnlyList<Point> targets, double iconSize)
    {
        this.kind = kind;
        double meters = kind == Kind.F35 ? 10.7 : 14.1;
        span = Scale.PerMeter(iconSize) * meters;
        u = span / (kind == Kind.F35 ? 68 : 70);
        speed = Math.Max(span * 7, area.Width / 2.4);
        Reach = iconSize * 0.5;
        int capacity = kind == Kind.F35 ? 8 : 6;
        // Gruppen: benachbarte Symbole, höchstens capacity je Überflug
        var open = targets.OrderBy(p => p.X).ToList(); var groups = new List<List<Point>>();
        while (open.Count > 0)
        {
            var seed = open[0];
            var g = open.OrderBy(p => (p - seed).LengthSquared).Take(capacity).Where(p => (p - seed).Length < iconSize * 6).ToList();
            groups.Add(g); open.RemoveAll(g.Contains);
        }
        double diag = Math.Sqrt(area.Width * area.Width + area.Height * area.Height);
        double t = 0.4;
        var blasts = new List<(Point, double)>();
        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            var c = new Point(g.Average(p => p.X), g.Average(p => p.Y));
            double angle = (i % 2 == 0 ? 0 : Math.PI) + (i % 3 - 1) * 0.35;
            var dir = new Vector(Math.Cos(angle), Math.Sin(angle));
            double half = diag * 0.6 + span * 2;
            var from = c - dir * half;
            double length = 2 * half / speed;
            double lead = span * 2.4;
            double release = t + (half - lead) / speed;
            passes.Add(new Pass(t, t + length, from, dir, length, release));
            for (int k = 0; k < g.Count; k++)
            {
                double fire = release + k * 0.07;
                var at = from + dir * ((fire - t) * speed);
                var side = new Vector(-dir.Y, dir.X) * ((k % 2 == 0 ? 1 : -1) * span * 0.08);
                double flight = kind == Kind.F35 ? 1.25 : 0.9;
                weapons.Add(new Weapon(at + side, g[k], fire, fire + flight));
                blasts.Add((g[k], fire + flight));
            }
            t += Math.Max(length * 0.42, 1.2);
        }
        Blasts = blasts; BlastSizes = blasts.Select(_ => kind == Kind.F35 ? 0.85 : 1.0).ToList();
        LastImpact = blasts.Count > 0 ? blasts.Max(b => b.Item2) : 2;
        End = Math.Max(LastImpact + 0.3, passes.Count > 0 ? passes.Max(p => p.End) : 2);
    }

    public void Draw(DrawingContext dc, double t)
    {
        foreach (var w in weapons)
        {
            if (t < w.Fire || t >= w.Impact) continue;
            double k = Fly.Smooth((t - w.Fire) / (w.Impact - w.Fire));
            var mid = w.From + (w.To - w.From) * 0.5 + new Vector(0, -span * 0.15);
            var p = Fly.Bez(w.From, mid, w.To, k); var q = Fly.Bez(w.From, mid, w.To, Math.Max(0, k - 0.02));
            double heading = Math.Atan2(p.Y - q.Y, p.X - q.X);
            if (kind == Kind.F35) Planes.Sdb(dc, p, heading, u * 0.9);
            else { Planes.Rocket(dc, q, p, u * 1.4, t); Planes.Maverick(dc, p, heading, u * 0.9); }
        }
        foreach (var pass in passes)
        {
            if (t < pass.Start || t > pass.End) continue;
            var at = pass.From + pass.Dir * ((t - pass.Start) * speed);
            double heading = Math.Atan2(pass.Dir.Y, pass.Dir.X);
            double bay = Fly.Smooth((t - (pass.Release - 0.6)) / 0.3) * (1 - Fly.Smooth((t - (pass.Release + 0.7)) / 0.3));
            if (kind == Kind.F35) Planes.F35(dc, at, heading, u, 0.45, t, bay);
            else Planes.Su57(dc, at, heading, u, 0.45, t, bay);
        }
    }
}

/// <summary>B-2 Spirit: lange, hohe Bahnen über die Symbolreihen; auf jedes Symbol eine schwere JDAM, die schräg herabfällt.</summary>
sealed class SpiritRaid : IAttack
{
    record Pass(double Start, double End, double Y, int Dir);
    record Bomb(Point From, Point To, double Drop, double Impact);
    readonly List<Pass> passes = [];
    readonly List<Bomb> bombs = [];
    readonly Rect area;
    readonly double span, u, speed;
    public IReadOnlyList<(Point P, double T)> Blasts { get; }
    public IReadOnlyList<double>? BlastSizes { get; }
    public double LastImpact { get; }
    public double End { get; }
    public bool Explodes => true;
    public double Reach { get; }

    public SpiritRaid(Rect area, IReadOnlyList<Point> targets, double iconSize)
    {
        this.area = area;
        span = Scale.PerMeter(iconSize) * 52.4;
        u = span / 100;
        speed = Math.Max(area.Width / 4.6, span * 1.4);
        Reach = iconSize * 0.7;
        // Bänder nach Höhe, höchstens drei
        var bands = targets.OrderBy(p => p.Y).Select(p => new List<Point> { p }).ToList();
        bool merged = true;
        while (merged && bands.Count > 1)
        {
            merged = false;
            int best = -1; double gap = double.MaxValue;
            for (int i = 0; i + 1 < bands.Count; i++) { double g = bands[i + 1].Min(p => p.Y) - bands[i].Max(p => p.Y); if (g < gap) { gap = g; best = i; } }
            if (gap < iconSize * 2.5 || bands.Count > 3) { bands[best].AddRange(bands[best + 1]); bands.RemoveAt(best + 1); merged = true; }
        }
        double t = 0.4, fall = 1.7, lead = speed * fall * 0.55;
        var blasts = new List<(Point, double)>();
        for (int i = 0; i < bands.Count; i++)
        {
            int dir = i % 2 == 0 ? 1 : -1;
            double y = bands[i].Average(p => p.Y);
            double x0 = dir > 0 ? area.Left - span : area.Right + span;
            double length = (area.Width + 2 * span) / speed;
            var pass = new Pass(t, t + length, y, dir);
            passes.Add(pass);
            foreach (var target in bands[i].OrderBy(p => p.X * dir))
            {
                double xDrop = target.X - dir * lead;
                double drop = t + Math.Abs(xDrop - x0) / speed;
                var from = new Point(xDrop, y);
                bombs.Add(new Bomb(from, target, drop, drop + fall));
                blasts.Add((target, drop + fall));
            }
            t += length * 0.75 + 0.4;   // nächste Bahn beginnt, während die vorige noch abfliegt
        }
        Blasts = blasts; BlastSizes = blasts.Select(_ => 1.35).ToList();
        LastImpact = blasts.Count > 0 ? blasts.Max(b => b.Item2) : 2;
        End = Math.Max(LastImpact + 0.3, t);
    }

    public void Draw(DrawingContext dc, double t)
    {
        foreach (var b in bombs)
        {
            if (t < b.Drop || t >= b.Impact) continue;
            double k = (t - b.Drop) / (b.Impact - b.Drop);
            var p = b.From + (b.To - b.From) * (k * k * 0.4 + k * 0.6);
            var d = b.To - b.From;
            Planes.Jdam(dc, p, Math.Atan2(d.Y, d.X), u * (1.4 - 0.6 * k));   // wird kleiner, je tiefer sie fällt
        }
        foreach (var pass in passes)
        {
            if (t < pass.Start || t > pass.End) continue;
            double x0 = pass.Dir > 0 ? area.Left - span : area.Right + span;
            var at = new Point(x0 + pass.Dir * (t - pass.Start) * speed, pass.Y);
            Planes.B2(dc, at, pass.Dir > 0 ? 0 : Math.PI, u, 1.2, t);
        }
    }
}

/// <summary>
/// Saugroboter: fährt aus seiner Ladestation, steuert ein Symbol nach dem anderen an (immer das nächste), saugt es ein und fährt zurück.
/// Nichts explodiert; das Symbol gleitet in den Roboter.
/// </summary>
sealed class VacuumRaid : IAttack
{
    readonly List<(double T, Point P, double H)> keys = [];
    readonly Dictionary<Point, (double T, Func<double, Point> Path)> sucked = [];
    readonly List<double> suckTimes = [];
    readonly Point dock;
    readonly double radius;
    public IReadOnlyList<(Point P, double T)> Blasts { get; }
    public double LastImpact { get; }
    public double End { get; }
    public bool Explodes => false;
    public double Reach { get; }

    public VacuumRaid(Rect primary, IReadOnlyList<Point> targets, double iconSize)
    {
        radius = iconSize * 0.8;
        Reach = iconSize * 0.4;
        dock = new Point(primary.Right - radius * 2.6, primary.Bottom - radius * 1.6);
        double speed = iconSize * 7, turnRate = 6;
        var p = dock + new Vector(0, -radius * 1.4); double h = -Math.PI / 2, t = 0.5;
        keys.Add((0, dock, -Math.PI / 2)); keys.Add((t, p, h));
        var open = targets.ToList();
        var blasts = new List<(Point, double)>();
        void Go(Point to)
        {
            var d = to - p; if (d.Length < 1) return;
            double head = Math.Atan2(d.Y, d.X), turn = Math.Abs(Wrap(head - h));
            if (turn > 0.05) { t += turn / turnRate; keys.Add((t, p, head)); }
            h = head; t += d.Length / speed; keys.Add((t, to, head)); p = to;
        }
        while (open.Count > 0)
        {
            var here = p;
            var next = open.OrderBy(q => (q - here).LengthSquared).First(); open.Remove(next);
            Go(next);
            double at = t;
            var target = next;
            sucked[target] = (at, q => Pose(q).P);
            blasts.Add((target, at));
            suckTimes.Add(at);
            t += 0.12; keys.Add((t, p, h));   // kurz stehen bleiben, schlürfen
        }
        Go(dock + new Vector(0, -radius * 1.4));
        Go(dock);
        Blasts = blasts;
        LastImpact = blasts.Count > 0 ? blasts.Max(b => b.Item2) : 1;
        End = t + 0.3;
    }

    static double Wrap(double a) { while (a > Math.PI) a -= Math.Tau; while (a < -Math.PI) a += Math.Tau; return a; }

    (Point P, double H) Pose(double t)
    {
        if (t <= keys[0].T) return (keys[0].P, keys[0].H);
        for (int i = 1; i < keys.Count; i++)
            if (t <= keys[i].T)
            {
                double k = Fly.Smooth((t - keys[i - 1].T) / Math.Max(1e-6, keys[i].T - keys[i - 1].T));
                return (keys[i - 1].P + (keys[i].P - keys[i - 1].P) * k, Fly.Turn(keys[i - 1].H, keys[i].H, k));
            }
        return (keys[^1].P, keys[^1].H);
    }

    /// <summary>Das Symbol gleitet in den letzten 0,35 s vor dem Einsaugen in den Roboter.</summary>
    public Point? Move(Point start, double t)
    {
        if (!sucked.TryGetValue(start, out var s) || t < s.T - 0.35) return null;
        double k = Fly.Smooth((t - (s.T - 0.35)) / 0.35);
        return start + (Pose(Math.Min(t, s.T)).P - start) * k;
    }

    static readonly Brush Shell = Fly.B("#E9ECEF"), ShellShade = Fly.B("#C9CED3"), Top = Fly.B("#2C3136"), Bumper = Fly.B("#3A3F45"), Lidar = Fly.B("#1E2226"),
        BrushArm = Fly.B("#4C88D8"), DockBody = Fly.B("#2E3338"), Contact = Fly.B("#B9C0C6");
    static readonly Brush Led = Art.Glow("#E040FF80"), Suction = Art.Glow("#704CC2FF");
    static readonly Pen Edge = Fly.P("#1E2226", 0.04), Bristle = Fly.P("#2A2E33", 0.035);

    public void Draw(DrawingContext dc, double t)
    {
        // Ladestation am rechten unteren Rand
        dc.PushTransform(Fly.Frame(dock, 0, radius));
        dc.DrawRoundedRectangle(DockBody, Edge, new Rect(-0.9, 0.2, 1.8, 0.7), 0.2, 0.2);
        dc.DrawRectangle(Contact, null, new Rect(-0.5, 0.12, 0.25, 0.1)); dc.DrawRectangle(Contact, null, new Rect(0.25, 0.12, 0.25, 0.1));
        Soft.Draw(dc, Led, new Point(0, 0.55), 0.12, 0.12, (t % 1.4) < 0.7 ? 1 : 0.4);
        dc.Pop();
        if (t > End + 0.3) return;
        var (p, h) = Pose(t);
        int full = suckTimes.Count(x => x <= t);
        bool sucking = suckTimes.Any(x => t > x - 0.35 && t < x + 0.12);
        if (sucking) Soft.Draw(dc, Suction, p, radius * 1.25, radius * 1.25, 0.8);
        dc.PushTransform(new TranslateTransform(radius * 0.08, radius * 0.12));
        dc.DrawEllipse(Fly.Shadow, null, p, radius, radius);
        dc.Pop();
        dc.PushTransform(Fly.Frame(p, h, radius));
        // Seitenbürsten vorn links und rechts, drehen sich
        foreach (double s in new[] { -1.0, 1.0 })
        {
            var c = new Point(0.62, 0.62 * s);
            for (int b = 0; b < 3; b++)
            {
                double a = t * 14 * s + b * Math.Tau / 3;
                dc.DrawLine(Bristle, c, c + new Vector(Math.Cos(a), Math.Sin(a)) * 0.36);
            }
            dc.DrawEllipse(BrushArm, null, c, 0.07, 0.07);
        }
        dc.DrawEllipse(Shell, Edge, new Point(0, 0), 1, 1);
        dc.DrawGeometry(Bumper, null, Fly.G("M0.98,-0.2 A1,1 0 0 1 0.98,0.2 L0.88,0.18 A0.9,0.9 0 0 0 0.88,-0.18 Z M0.2,-0.98 A1,1 0 0 1 0.98,-0.2 L0.88,-0.18 A0.9,0.9 0 0 0 0.18,-0.88 Z M0.98,0.2 A1,1 0 0 1 0.2,0.98 L0.18,0.88 A0.9,0.9 0 0 0 0.88,0.18 Z"));
        dc.DrawEllipse(ShellShade, null, new Point(-0.05, 0), 0.72, 0.72);
        dc.DrawEllipse(Top, null, new Point(-0.05, 0), 0.62, 0.62);
        // Behälteranzeige: füllt sich mit jedem Symbol
        double fill = Math.Min(1, full / Math.Max(1.0, Blasts.Count));
        dc.DrawLine(new Pen(BrushArm, 0.07) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, new Point(-0.45, -0.3), new Point(-0.45, -0.3 + 0.6 * fill));
        dc.DrawEllipse(Lidar, Edge, new Point(0.25, 0), 0.2, 0.2);   // Laserturm
        dc.DrawEllipse(ShellShade, null, new Point(0.25, 0), 0.08, 0.08);
        Soft.Draw(dc, Led, new Point(-0.2, 0.25), 0.07, 0.07, 1);
        dc.Pop();
    }
}

/// <summary>
/// Schwerkraft aus: alle Symbole lösen sich und schweben schwerelos nach oben; nach gut zwei Sekunden kommt die Schwerkraft mit voller
/// Wucht zurück, alles stürzt nach unten und fällt aus dem unteren Bildschirmrand.
/// </summary>
sealed class GravityRaid : IAttack
{
    record Faller(Point Start, double Lift, double Drift, double Phase, double Back, double G, double Exit);
    readonly Dictionary<Point, Faller> fallers = [];
    readonly IReadOnlyList<Rect> screens;
    readonly double iconSize, back;
    public IReadOnlyList<(Point P, double T)> Blasts { get; }
    public double LastImpact { get; }
    public double End { get; }
    public bool Explodes => false;
    public double Reach { get; }

    public GravityRaid(Rect area, IReadOnlyList<Point> targets, double iconSize, IReadOnlyList<Rect>? screens)
    {
        this.iconSize = iconSize;
        this.screens = screens is { Count: > 0 } ? screens : [area];
        Reach = iconSize * 0.4;
        var rnd = new Random(981);
        back = 2.6;
        var blasts = new List<(Point, double)>();
        foreach (var p in targets)
        {
            var screen = this.screens.FirstOrDefault(r => r.Contains(p)); if (screen.IsEmpty) screen = area;
            double lift = iconSize * (0.6 + 0.9 * rnd.NextDouble()), drift = (rnd.NextDouble() - 0.5) * iconSize * 1.2, phase = rnd.NextDouble() * Math.Tau;
            double b = back + rnd.NextDouble() * 0.25;
            double g = screen.Height * 3.2;
            // Höhe zum Zeitpunkt der Rückkehr der Schwerkraft, dann freier Fall bis unter den Rand
            double y0 = p.Y - lift * (b - 0.3);
            double fallTo = screen.Bottom + iconSize * 1.2;
            double exit = b + Math.Sqrt(2 * Math.Max(1, fallTo - y0) / g);
            fallers[p] = new Faller(p, lift, drift, phase, b, g, exit);
            blasts.Add((p, exit));
        }
        Blasts = blasts;
        LastImpact = blasts.Count > 0 ? blasts.Max(b => b.Item2) : 1;
        End = LastImpact + 0.3;
    }

    public Point? Move(Point start, double t)
    {
        if (!fallers.TryGetValue(start, out var f) || t < 0.3) return null;
        double up = Math.Min(t, f.Back) - 0.3;
        var floating = new Vector(f.Drift * up / f.Back + Math.Sin(t * 1.7 + f.Phase) * iconSize * 0.08, -f.Lift * up + Math.Cos(t * 1.3 + f.Phase) * iconSize * 0.05);
        if (t <= f.Back) return start + floating;
        double fall = t - f.Back;
        return start + floating + new Vector(f.Drift * 0.2 * fall, 0.5 * f.G * fall * fall);
    }

    static readonly Brush Pulse = Art.Glow("#604CC2FF"), Slam = Art.Glow("#80FFFFFF");
    static readonly Pen Streak = Fly.P("#50FFFFFF", 2);

    public void Draw(DrawingContext dc, double t)
    {
        foreach (var screen in screens)
        {
            var c = new Point(screen.X + screen.Width / 2, screen.Y + screen.Height / 2);
            double a = t - 0.25;   // Schwerkraft aus: weiche Welle über den Bildschirm
            if (a > 0 && a < 1.2) Soft.Draw(dc, Pulse, c, screen.Width * 0.6 * a, screen.Height * 0.6 * a, 1 - a / 1.2);
            double s = t - back;   // Schwerkraft zurück: heller Schlag von oben
            if (s > 0 && s < 0.35) Soft.Draw(dc, Slam, new Point(c.X, screen.Top), screen.Width * 0.7, screen.Height * 0.4 * (1 - s / 0.35), 1 - s / 0.35);
        }
        foreach (var f in fallers.Values)
        {   // Fahrtstreifen über fallenden Symbolen
            if (t <= f.Back + 0.1 || t >= f.Exit) continue;
            var p = Move(f.Start, t)!.Value;
            double v = f.G * (t - f.Back);
            double len = Math.Min(iconSize * 2.5, v * 0.05);
            dc.DrawLine(Streak, p + new Vector(-iconSize * 0.25, -iconSize * 0.6), p + new Vector(-iconSize * 0.25, -iconSize * 0.6 - len));
            dc.DrawLine(Streak, p + new Vector(iconSize * 0.25, -iconSize * 0.6), p + new Vector(iconSize * 0.25, -iconSize * 0.6 - len));
        }
    }
}
