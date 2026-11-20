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
    // Nach der gemeinfreien Dreiseitenansicht (vorlage/f35a-3view.png, Wikimedia Commons, nach jsf.mil) ausgemessen :
    // Flügel 34° mit leicht vorwärts gepfeilter Hinterkante, Einlaufwangen mit DSI-Lippe, Leitwerksträger bis hinter die Düse,
    // nach außen geneigte Seitenleitwerke, Höhenleitwerk hinter der Düse, rahmenlose Haube. 100 Einheiten = Länge, Spannweite 68,4.
    static readonly Geometry LightningBody = Fly.Both("M50,0 C49.4,0.5 48.3,1 47.5,1.3 L44.9,2.4 L42.4,3.1 L39,3.5 L34.7,4 L30.5,4.5 L25.4,5.1 L22.2,6.4 L23.2,9.4 L22.4,10.3 L19.5,10.7 L4.2,11.6 L1.6,12 L-29.7,11.7 L-34,12.6 L-41.5,8.6 L-41.5,4.2 L-35.5,3.6 L-35.5,0 Z");
    static readonly Geometry LightningWing = Fly.Both("M2.4,11.8 L-14.1,34.2 L-24,34.2 L-29.7,11.7 Z");
    static readonly Geometry LightningTail = Fly.Both("M-31,11.8 L-34.1,13.8 L-40.9,23.3 L-45.9,23.3 L-50,7.2 L-50,6.2 L-40.5,4.6 Z");
    static readonly Geometry LightningFins = Fly.Both("M-24,8.6 L-34.6,14.3 L-45,14.3 L-41.5,8.4 Z");
    static readonly Geometry LightningIntakes = Fly.Both("M22.4,6.6 L23,9.2 L21.8,9.7 C21.4,8.6 21,7.6 20.4,7 Z");
    static readonly Geometry LightningNozzle = Fly.Both("M-35.5,3.5 L-38.8,2.8 L-40.4,1.9 L-40.4,0 L-35.5,0 Z");
    static readonly Geometry LightningCanopy = Fly.Both("M37.6,0 C37,1.5 35.6,2.5 33.4,2.9 L30,3.2 L25,3.2 L22.6,2.7 C21.4,1.8 20.8,0.9 20.6,0 Z");
    static readonly Geometry LightningGlint = Fly.G("M35.6,-1.4 C34,-2.2 31,-2.5 28,-2.5");
    static readonly Geometry LightningSeams = Fly.G("M0.8,13.4 L-13.4,32.8 M0.8,-13.4 L-13.4,-32.8 M-26.6,12.2 L-21.6,32.6 M-26.6,-12.2 L-21.6,-32.6 M-6,1.6 L-24,1.6 L-24,5.6 L-6,5.6 L-6,1.6 M-6,-1.6 L-24,-1.6 L-24,-5.6 L-6,-5.6 L-6,-1.6 M20,1.1 L-30,1.1 M20,-1.1 L-30,-1.1 M42.4,3.1 L25.4,5.1 M42.4,-3.1 L25.4,-5.1 M19.5,10.7 L19.5,7.2 L23,5.8 M19.5,-10.7 L19.5,-7.2 L23,-5.8 M-29.7,11.7 L-36,8 L-41.5,8.4 M-29.7,-11.7 L-36,-8 L-41.5,-8.4 M-36,3.6 L-36,8 M-36,-3.6 L-36,-8 M-40.9,23.3 L-37,15 M-40.9,-23.3 L-37,-15 M-35.5,3.5 L-35.5,0 M-35.5,-3.5 L-35.5,-0");
    static readonly Brush LightningTop = Fly.B("#646C73"), LightningShade = Fly.B("#5A6168"), LightningGold = Fly.B("#7A6A3C"), LightningDark = Fly.B("#2A2E32");
    static readonly Pen LightningEdge = Fly.P("#3A4045", 0.35), LightningSeam = Fly.P("#525A60", 0.22), LightningShine = Fly.P("#B8A874", 0.35);

    /// <param name="bay">0 … 1: Waffenschachtklappen offen</param>
    public static void F35(DrawingContext dc, Point at, double heading, double u, double altitude, double t, double bay = 0)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, LightningWing, LightningTail, LightningBody);
        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawGeometry(LightningShade, LightningEdge, LightningTail);
        dc.DrawGeometry(LightningShade, LightningEdge, LightningWing);
        dc.DrawGeometry(LightningTop, LightningEdge, LightningBody);
        dc.DrawGeometry(LightningDark, LightningEdge, LightningNozzle);
        for (int i = 0; i < 7; i++)
        {   // Zackenrand der Düse (Radartarnung)
            double y = -3.0 + i;
            dc.DrawLine(LightningSeam, new Point(-38.6, y), new Point(-40.2, y * 0.75));
        }
        dc.DrawGeometry(LightningDark, null, LightningIntakes);
        dc.DrawGeometry(null, LightningSeam, LightningSeams);
        // Waffenschächte liegen an der Unterseite und öffnen nach unten: von oben nicht sichtbar (wie bei der Su-57, 20.11.2026)
        dc.DrawGeometry(LightningShade, LightningEdge, LightningFins);
        dc.DrawGeometry(LightningGold, LightningEdge, LightningCanopy);
        dc.DrawGeometry(null, LightningShine, LightningGlint);
        Soft.Draw(dc, HogRed, new Point(-18, -34.2), 1, 1, 1);
        Soft.Draw(dc, HogGreen, new Point(-18, 34.2), 1, 1, 1);
        dc.Pop();
    }

    // ---------------------------------------------------------------- Су-57 (Länge 20,1 m, Spannweite 14,1 m)
    // Aus der Vektor-Dreiseitenansicht (vorlage/su57-3view.svg, Wikimedia Commons, XaHyMaH, CC BY-SA 3.0) übernommen :
    // tragender Rumpf, LEVCON-Klappen, Flügel mit Vorderkantenklappe und Flaperon, Leitwerksträger außen an den Gondeln, ganz bewegliche
    // Seitenleitwerke, Höhenleitwerk, getrennte Düsen, Heckstachel, Haube mit Bügel, Infrarotsensor rechts vor der Haube. 100 Einheiten = Länge.
    static readonly Geometry FelonBody = Fly.Both("M50,0 C42,1.4 33,3.6 27.8,4.4 L20,4.9 L11.2,12.6 L-0.3,15.8 L-9.1,15 L-27.5,15 L-36.1,11.3 L-38.1,10.4 L-38.2,10.1 L-38.4,4.1 L-38.7,3.4 L-50,0 Z");
    static readonly Geometry FelonWing = Fly.Both("M-0.3,15.8 L-22.8,35.6 L-27.4,35.6 L-29.9,32.1 L-32.2,19.1 L-27.5,15 L-9.1,14 Z");
    static readonly Geometry FelonTail = Fly.Both("M-27.8,15 L-39.8,25.5 L-44,24.3 L-46.1,12.6 L-36,11.3 Z");
    static readonly Geometry FelonBoom = Fly.Both("M-19.3,12.4 L-26.4,14.1 L-46.1,12.6 L-36.1,11.3 L-19.9,11.4 Z");
    static readonly Geometry FelonNozzles = Fly.Both("M-38.4,4.1 L-38.2,10.1 L-40,9.8 L-43,9 L-43.2,5.6 L-40.2,4.5 Z");
    static readonly Geometry FelonFins = Fly.Both("M-23.4,12.7 L-32.5,16.9 L-35.7,16.9 L-38.6,12.9 L-32.8,12.4 L-29.6,12.4 Z");
    static readonly Geometry FelonLevcon = Fly.Both("M20,4.9 L11.2,12.6 L8.2,13.4 L12.6,5.9 L19.8,5 Z");
    static readonly Geometry FelonCanopy = Fly.Both("M37.5,0 L34.9,2.4 L28.9,2.8 L25.7,2.7 L21.1,0 Z");
    static readonly Geometry FelonGlass = Fly.Both("M37.1,0 L34.5,2.2 L29.8,2.6 L31,0 Z M30.5,0 L29.1,2.6 L25.5,2.6 L24.7,0 Z");
    static readonly Geometry FelonCamo = Fly.G("M26,-4.6 L12,-11.6 L5,-13.6 L11,-6 Z M14,6 L2,14.2 L-6,14.6 L4,7 Z M-4,-16.6 L-14,-27 L-19.5,-31 L-12,-17.4 Z " +
        "M-6,17 L-16,28 L-22,33.6 L-25,31 L-14,20 Z M-26,-21 L-29.4,-30 L-27.6,-33 L-24,-24 Z M-10,-3 L-28,-2 L-32,2 L-14,2 Z " +
        "M-31,13 L-39,20.6 L-43,23.2 L-44.6,15 Z M-30,-13.4 L-38,-20 L-42,-14 Z M2,-8 L-8,-12.4 L-16,-12.4 L-6,-7 Z M-18,6 L-26,9.6 L-34,9.8 L-24,5.6 Z");
    static readonly Geometry FelonSeams = Fly.G("M19.8,5 L12.6,5.9 L8.2,13.4 M19.8,-5 L12.6,-5.9 L8.2,-13.4 M-0.3,15.8 L-8.4,15.8 L-9.1,15 L-27.5,15 M-0.3,-15.8 L-8.4,-15.8 L-9.1,-15 L-27.5,-15 M-18.8,32 L-21.9,32 L-3.5,15.8 M-18.8,-32 L-21.9,-32 L-3.5,-15.8 M-31.2,25 L-26.6,25 M-31.2,-25 L-26.6,-25 M-29.9,32.1 L-27.9,32.1 L-26.6,25 L-28.3,15.7 M-29.9,-32.1 L-27.9,-32.1 L-26.6,-25 L-28.3,-15.7 M-22.8,9.1 L-28.9,9.7 L-36.1,10.2 M-22.8,-9.1 L-28.9,-9.7 L-36.1,-10.2 M-23,4.4 L-28.8,4.1 L-35.3,3.8 L-38.4,4.1 M-23,-4.4 L-28.8,-4.1 L-35.3,-3.8 L-38.4,-4.1 M-29.2,3.5 L-35.4,3.2 L-38.7,3.4 M-29.2,-3.5 L-35.4,-3.2 L-38.7,-3.4 M-43.2,5.6 L-43,9 M-43.2,-5.6 L-43,-9 M-40.3,4.5 L-40,9.8 M-40.3,-4.5 L-40,-9.8 M-35.1,12.6 L-46.1,12.6 M-35.1,-12.6 L-46.1,-12.6 M-19.3,12.4 L-21.3,12.7 L-22.8,12.5 L-23,12 L-19.9,11.4 M-19.3,-12.4 L-21.3,-12.7 L-22.8,-12.5 L-23,-12 L-19.9,-11.4 M-23,12 L-24,11.7 L-25.5,11.4 M-23,-12 L-24,-11.7 L-25.5,-11.4");
    static readonly Brush FelonTop = Fly.B("#9AA6AE"), FelonShade = Fly.B("#8B979F"), FelonPatch = Fly.B("#6F7D88"), FelonGold = Fly.B("#8A7A48"), FelonDark = Fly.B("#2B3034");
    static readonly Pen FelonEdge = Fly.P("#56616A", 0.35), FelonSeam = Fly.P("#7C8890", 0.22);
    static readonly Geometry FelonSkin = Geometry.Combine(Geometry.Combine(FelonBody, FelonWing, GeometryCombineMode.Union, null), FelonTail, GeometryCombineMode.Union, null);

    public static void Su57(DrawingContext dc, Point at, double heading, double u, double altitude, double t, double bay = 0)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, FelonWing, FelonTail, FelonBody);
        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawGeometry(FelonShade, FelonEdge, FelonTail);
        dc.DrawGeometry(FelonShade, FelonEdge, FelonWing);
        dc.DrawGeometry(FelonTop, FelonEdge, FelonBody);
        dc.DrawGeometry(FelonDark, FelonEdge, FelonNozzles);
        dc.DrawGeometry(FelonShade, FelonEdge, FelonLevcon);
        dc.PushClip(FelonSkin);
        dc.DrawGeometry(FelonPatch, null, FelonCamo);   // Splittertarnung nur auf der Haut
        dc.Pop();
        dc.DrawGeometry(FelonTop, FelonEdge, FelonBoom);
        dc.DrawGeometry(null, FelonSeam, FelonSeams);
        dc.DrawRectangle(null, FelonSeam, new Rect(-42.7, -1.1, 4.3, 2.2));   // Bremsschirmklappe
        // Waffenschächte liegen unten zwischen den Gondeln und öffnen nach unten: von oben nicht sichtbar (20.11.2026: „люки открываются сверху … неправильно“)
        dc.DrawGeometry(FelonShade, FelonEdge, FelonFins);
        dc.DrawGeometry(FelonDark, FelonEdge, FelonCanopy);
        dc.DrawGeometry(FelonGold, null, FelonGlass);
        dc.DrawEllipse(FelonDark, FelonEdge, new Point(37.8, 0.8), 1.9, 0.7);   // Infrarotsensor, rechts versetzt
        foreach (double s in new[] { -1.0, 1.0 }) Art.Star(dc, new Point(-15, 24 * s), 1.8, 0);
        Soft.Draw(dc, HogRed, new Point(-25, -35.6), 1, 1, 1);
        Soft.Draw(dc, HogGreen, new Point(-25, 35.6), 1, 1, 1);
        dc.Pop();
    }

    // ---------------------------------------------------------------- B-2 Spirit (Spannweite 52,4 m, Länge 21 m)
    // Nach dem gemeinfreien US-Air-Force-Foto von oben (vorlage/b2-planview.jpg, Andersen AFB 2006) ausgemessen und die Schrägsicht
    // des Fotos herausgerechnet : gerade Vorderkante, Hinterkante als doppeltes W mit Biberschwanz in der Mitte,
    // je Seite ein Einlauf mit Zackenlippe, helle Abgasrinnen bis zur Hinterkante, Cockpitfenster, helle Laufwegmarkierungen.
    // 100 Einheiten = Spannweite (Länge 40 Einheiten).
    static readonly Geometry SpiritWing = Fly.Both("M20,0 L-12.5,50 L-13.4,50 L-18.4,42.6 L-8.6,25.7 L-17.4,12.9 L-14.4,8 L-17.2,4.2 L-20,0 Z");
    static readonly Geometry SpiritHump = Fly.Both("M17.5,0 C15.5,2.4 11,4 7,4.3 L-9,4.4 L-17.2,4.2 L-20,0 Z");
    static readonly Geometry SpiritIntakes = Fly.Both("M8,4.4 L6.4,7 L8,9.6 L6,10 L4.8,7.2 L5.4,4.2 Z");
    static readonly Geometry SpiritCheeks = Fly.Both("M9.6,4.2 C9,6.4 9,8.6 9.8,10.4 L6,10.6 L4.4,7.2 L5.2,4.2 Z");
    static readonly Geometry SpiritExhaust = Fly.Both("M-9,4.4 L-16.4,4.6 L-14.4,8 L-16.6,11.6 L-9,11 Z");
    static readonly Geometry SpiritGlass = Fly.Both("M15.6,0.3 L14.6,2.2 L13.6,2.2 L14.4,0.3 Z M13.4,0.3 L12.6,2.4 L11.8,2.4 L12.4,0.3 Z");
    static readonly Geometry SpiritMarks = Fly.G("M-10.4,47.6 L-15.9,42.6 L-6.1,25.7 L-14.6,13.4 M-10.4,-47.6 L-15.9,-42.6 L-6.1,-25.7 L-14.6,-13.4 M14.6,3.6 L-8.6,40.6 M14.6,-3.6 L-8.6,-40.6 M-9,4.4 L-9,11 M-9,-4.4 L-9,-11 M6,4.2 L-9,4.4 M6,-4.2 L-9,-4.4");
    static readonly Brush SpiritTop = Fly.B("#4B5664"), SpiritShade = Fly.B("#434D5A"), SpiritDark = Fly.B("#1A1E23"), SpiritTiles = Fly.B("#6C7682");
    static readonly Pen SpiritEdge = Fly.P("#2C333B", 0.25), SpiritMark = Fly.P("#8C96A0", 0.22), SpiritSeam = Fly.P("#3A424C", 0.2);

    public static void B2(DrawingContext dc, Point at, double heading, double u, double altitude, double t)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, SpiritWing);
        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawGeometry(SpiritShade, SpiritEdge, SpiritWing);
        dc.DrawGeometry(SpiritTop, SpiritEdge, SpiritHump);
        dc.DrawGeometry(SpiritTiles, SpiritSeam, SpiritExhaust);
        foreach (double s in new[] { -1.0, 1.0 }) dc.DrawLine(SpiritSeam, new Point(-9, 7.6 * s), new Point(-14.4, 8 * s));   // zwei Triebwerke je Rinne
        dc.DrawGeometry(SpiritTop, SpiritEdge, SpiritCheeks);
        dc.DrawGeometry(SpiritDark, null, SpiritIntakes);
        dc.DrawGeometry(null, SpiritMark, SpiritMarks);
        dc.DrawGeometry(SpiritDark, SpiritEdge, SpiritGlass);
        if ((t % 1.5) < 0.08) { Soft.Draw(dc, HogRed, new Point(-13, -50), 0.8, 0.8, 1); Soft.Draw(dc, HogGreen, new Point(-13, 50), 0.8, 0.8, 1); }
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

    public IEnumerable<(Func<double, Point> At, double From, double To)> Flights =>
        passes.Select(p => ((Func<double, Point>)(t => p.From + p.Dir * ((t - p.Start) * speed)), p.Start, p.End));

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

    public IEnumerable<(Func<double, Point> At, double From, double To)> Flights =>
        passes.Select(p => ((Func<double, Point>)(t => new Point((p.Dir > 0 ? area.Left - span : area.Right + span) + p.Dir * (t - p.Start) * speed, p.Y)), p.Start, p.End));

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
    public bool DrawsMovedIcons => true;   // 20.11.2026: doppelte Symbole am Anfang
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
            // nacheinander statt alle auf einmal: oben zuerst, dazu etwas Zufall; sanftere Schwerkraft (20.11.2026: „funktioniert sehr schlecht“)
            double b = back + 0.9 * Math.Clamp((p.Y - screen.Top) / Math.Max(1, screen.Height), 0, 1) + rnd.NextDouble() * 0.35;
            double g = screen.Height * 1.5;
            // Höhe zum Zeitpunkt der Rückkehr der Schwerkraft, dann freier Fall bis unter den Rand
            double y0 = p.Y - lift * (b - 0.3) - lift * Coast;
            double fallTo = screen.Bottom + iconSize * 1.2;
            double exit = b + Math.Sqrt(2 * Math.Max(1, fallTo - y0) / g);
            fallers[p] = new Faller(p, lift, drift, phase, b, g, exit);
            blasts.Add((p, exit));
        }
        Blasts = blasts;
        LastImpact = blasts.Count > 0 ? blasts.Max(b => b.Item2) : 1;
        End = LastImpact + 0.3;
    }

    public bool DrawsMovedIcons => true;   // 20.11.2026: doppelte Symbole am Anfang
    public Point? Move(Point start, double t)
    {
        if (!fallers.TryGetValue(start, out var f) || t < 0.3) return null;
        double up = Math.Min(t, f.Back) - 0.3;
        var floating = new Vector(f.Drift * up / f.Back + Math.Sin(t * 1.7 + f.Phase) * iconSize * 0.08, -f.Lift * up + Math.Cos(t * 1.3 + f.Phase) * iconSize * 0.05);
        if (t <= f.Back) return start + floating;
        double fall = t - f.Back;
        // Auftrieb klingt stetig aus (kein Geschwindigkeitssprung), dann freier Fall
        double coast = Coast * (1 - Math.Exp(-fall / Coast));
        return start + floating + new Vector(f.Drift * 0.2 * fall, -f.Lift * coast + 0.5 * f.G * fall * fall);
    }
    const double Coast = 0.25;

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
