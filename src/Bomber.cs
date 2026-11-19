using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DeskKeeper;

/// <summary>
/// Ту-160М-Szene , Draufsicht: Der Schwenkflügel-Bomber fliegt mit ausgefahrenen Flügeln (20°) ein,
/// öffnet den Schacht und setzt nacheinander Marschflugkörper ab. Diese fallen kurz, klappen die Flügel aus, zünden
/// das Triebwerk und fliegen in weichen Bögen ihre Ziele an (Einschläge nacheinander, nicht gleichzeitig).
/// Danach Nachbrenner, Flügel schwenken auf 65°, der Bomber beschleunigt aus dem Bild.
/// </summary>
sealed class Bomber : IAttack
{
    public double Reach { get; init; } = double.PositiveInfinity;
    public bool Explodes => true;
    readonly Rect stage;
    readonly double span;                 // Länge des Bombers in Pixeln
    readonly double enter, release0, releaseStep, sweepStart;
    readonly Missile[] missiles;
    record Missile(Point Drop, Vector V0, Point Target, double Release, double Ignite, double Impact, double Seed);

    public IReadOnlyList<(Point P, double T)> Blasts => missiles.Select(m => (m.Target, m.Impact)).ToList();
    public double LastImpact => missiles.Max(m => m.Impact);
    public double End { get; }

    public Bomber(Rect stage, IReadOnlyList<Point> targets, double iconSize)
    {
        this.stage = stage;
        span = Math.Clamp(stage.Width * 0.08, iconSize * 2.0, iconSize * 4.0);   // 18.11.: noch kleiner (etwa halb so groß wie 2.5)
        // 18.11.: ab der Bildschirmmitte Flügel anlegen und mit Nachbrenner beschleunigen, damit man es sieht.
        // Reiseflug bis zur Mitte, Abwurf vorher gleichmäßig verteilt.
        enter = 0.3;
        sweepStart = enter + (stage.Width * 0.5 + span * 0.8) / (stage.Width * Cruise);
        release0 = enter + 0.6;
        releaseStep = targets.Count > 1 ? (sweepStart - 0.25 - release0) / (targets.Count - 1) : 0;
        BuildPath();   // erst nach sweepStart, sonst falsche Geschwindigkeit
        var random = new Random(160);
        missiles = new Missile[targets.Count];
        for (int i = 0; i < missiles.Length; i++)
        {
            double rt = release0 + i * releaseStep;
            var (pos, heading) = Pose(rt);
            var dir = new Vector(Math.Cos(heading), Math.Sin(heading));
            var side = new Vector(-dir.Y, dir.X) * ((i % 2 == 0 ? 1 : -1) * span * 0.02);
            var drop = pos - dir * span * 0.02 + side * 0.3;
            double ignite = rt + 0.55;
            double dist = (targets[i] - drop).Length;
            double flight = 1.2 + dist / Math.Max(1, stage.Width * 0.55);     // Marschflug, deutlich langsamer als Иерихон
            missiles[i] = new Missile(drop, dir * Speed(rt), targets[i], rt, ignite, ignite + flight, random.NextDouble());
        }
        // Ende, wenn der Bomber rechts ganz hinaus ist
        double gone = sweepStart;
        while (gone < sweepStart + 9 && Pose(gone).Pos.X < stage.Right + span) gone += 0.05;
        End = Math.Max(LastImpact, gone + 0.2);
    }

    // ------------------------------------------------------------ Flug des Bombers

    /// <summary>Geschwindigkeit (Pixel/s): Anflug langsam, ab dem Schwenken mit Nachbrenner stark beschleunigt.</summary>
    // 18.11.: langsamer Reiseflug, damit man den Bomber erkennt; danach sanfter beschleunigen
    const double Cruise = 0.16;   // Bildschirmbreiten je Sekunde im Reiseflug
    double Speed(double t) => stage.Width * (Cruise + 0.5 * Smooth((t - sweepStart) / 1.8) * Math.Max(0, t - sweepStart));

    // zurückgelegter Weg vorab je 1/100 s (vorher in jedem Bild neu aufsummiert → Ruckeln)
    double[] distance = [];
    const double PathStep = 0.01;
    void BuildPath()
    {
        int n = (int)(20 / PathStep) + 2;
        distance = new double[n];
        for (int i = 1; i < n; i++) distance[i] = distance[i - 1] + Speed(enter + (i - 1) * PathStep) * PathStep;
    }
    double Distance(double t)
    {
        double f = Math.Clamp((t - enter) / PathStep, 0, distance.Length - 1.001);
        int i = (int)f; return distance[i] + (distance[i + 1] - distance[i]) * (f - i);
    }

    (Point Pos, double Heading) Pose(double t)
    {
        // leichter Bogen von links oben nach rechts, zuletzt steigend aus dem Bild
        double s = Distance(t);
        double x = stage.Left - span * 0.8 + s;
        double y = stage.Top + stage.Height * 0.24 + Math.Sin(s / stage.Width * 2.2) * stage.Height * 0.05 - Math.Max(0, t - sweepStart) * stage.Height * 0.03;
        double slope = Math.Cos(s / stage.Width * 2.2) * stage.Height * 0.05 * 2.2 / stage.Width;
        return (new Point(x, y), Math.Atan(slope));
    }

    /// <summary>Flügelpfeilung in Grad: 20° beim Anflug, 65° nach dem Abwurf.</summary>
    double Sweep(double t) => 20 + 45 * Smooth((t - sweepStart) / 1.6);

    // ------------------------------------------------------------ Marschflugkörper

    (Point Pos, Vector Dir, double Fold, bool Burning, double Altitude) MissileAt(Missile m, double t)
    {
        if (t < m.Ignite)
        {
            // Fall: mit der Bombergeschwindigkeit weiter, sinkt, Flügel noch eingeklappt
            double k = t - m.Release;
            var p = m.Drop + m.V0 * k * 0.85;
            var dir = m.V0; dir.Normalize();
            return (p, dir, 0, false, 1 - 0.35 * k);
        }
        double d = m.Impact - m.Ignite;
        double u = Math.Clamp((t - m.Ignite) / d, 0, 1);
        var start = m.Drop + m.V0 * (m.Ignite - m.Release) * 0.85;
        var v0 = m.V0 * 0.3 * d;   // wenig Schwung vom Bomber: weite Wende statt Schleife
        var aim = m.Target - start; aim.Normalize();
        var v1 = (aim * 0.7 + new Vector(0, 0.3)) * (m.Target - start).Length * 1.2;
        double u2 = u * u, u3 = u2 * u;
        var pos = (Vector)start * (2 * u3 - 3 * u2 + 1) + v0 * (u3 - 2 * u2 + u) + (Vector)m.Target * (-2 * u3 + 3 * u2) + v1 * (u3 - u2);
        var vel = (Vector)start * (6 * u2 - 6 * u) + v0 * (3 * u2 - 4 * u + 1) + (Vector)m.Target * (-6 * u2 + 6 * u) + v1 * (3 * u2 - 2 * u);
        if (vel.LengthSquared < 1e-6) vel = aim;
        vel.Normalize();
        return ((Point)pos, vel, Smooth((t - m.Ignite) / 0.35), true, 0.65 * (1 - u));
    }

    // ------------------------------------------------------------ Zeichnen

    public void Draw(DrawingContext dc, double t)
    {
        double u = span / 100;
        // Kondensstreifen und Rauch der Flugkörper
        foreach (var m in missiles)
        {
            if (t < m.Ignite || t >= m.Impact) continue;
            var trail = new StreamGeometry();
            using (var g = trail.Open())
            {
                bool first = true;
                for (double q = Math.Max(m.Ignite, t - 0.9); q <= t; q += 0.05)
                {
                    var p = MissileAt(m, q).Pos;
                    if (first) { g.BeginFigure(p, false, false); first = false; } else g.LineTo(p, true, true);
                }
            }
            trail.Freeze();
            dc.DrawGeometry(null, TrailWide, trail);
            dc.DrawGeometry(null, TrailCore, trail);
        }
        // Der Schacht liegt unten: von oben gesehen fallen die Flugkörper immer unter dem Bomber,
        // er verdeckt sie, bis sie unter ihm hervorkommen.
        foreach (var m in missiles)
        {
            if (t < m.Release || t >= m.Impact) continue;
            var (pos, dir, fold, burning, alt) = MissileAt(m, t);
            Art.Missile(dc, pos, dir, fold, burning, alt, u, t + m.Seed);
        }
        if (t >= enter && t <= End)
        {
            var (pos, heading) = Pose(t);
            double afterburner = Smooth((t - sweepStart + 0.2) / 0.5);
            Art.Bomber(dc, pos, heading, Sweep(t), false, afterburner, u, t);
        }
    }

    static readonly Pen TrailWide = MakePen(Color.FromArgb(40, 230, 230, 235), 6), TrailCore = MakePen(Color.FromArgb(110, 245, 245, 250), 1.6);
    static Pen MakePen(Color c, double w) { var p = new Pen(new SolidColorBrush(c), w) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; p.Freeze(); return p; }
    static double Smooth(double v) { v = Math.Clamp(v, 0, 1); return v * v * (3 - 2 * v); }
}
