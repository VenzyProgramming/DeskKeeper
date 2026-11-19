using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DeskKeeper;

/// <summary>Ein Luftangriff einer Szene (19.11.2026: Tu-22M3, Su-34, Ka-52, Tu-95, Il-76 neben der Tu-160M):
/// Einschläge (Ort, Zeit), Ende und Zeichnung. Die Szene zeichnet Explosionen und Asche, der Angriff nur Flugzeuge und Geschosse.</summary>
interface IAttack
{
    IReadOnlyList<(Point P, double T)> Blasts { get; }
    double LastImpact { get; }
    double End { get; }
    /// <summary>false: kein Feuer, keine Asche (Il-76: Fallschirmjäger tragen die Symbole weg).</summary>
    bool Explodes { get; }
    /// <summary>Bis wohin ein Einschlag Symbole in Brand setzt; unendlich: Druckwelle wie bisher (der erste Treffer zündet die Nachbarn).</summary>
    double Reach => double.PositiveInfinity;
    /// <summary>Größe je Einschlag (1 = normal), null: alle gleich.</summary>
    IReadOnlyList<double>? BlastSizes => null;
    void Draw(DrawingContext dc, double t);
}

static class Attacks
{
    public const string Tu160 = "tu160", Tu22 = "tu22m3", Su34 = "su34", Ka52 = "ka52", Tu95 = "tu95", Il76 = "il76", Ac130 = "ac130", Shahed = "shahed", A10 = "a10", Su25 = "su25";
    public static readonly string[] All = [Tu160, Tu22, Su34, Ka52, Tu95, Il76, Ac130, Shahed, A10, Su25];

    /// <param name="clusters">dichteste Gruppen zuerst (für Marschflugkörper)</param>
    /// <param name="icons">Symbolmitten auf dem Hauptbildschirm (für Treffer je Symbol)</param>
    /// <param name="screens">alle Bildschirme (Ka-52 fliegt sie an)</param>
    /// <param name="everywhere">alle Symbolmitten auf allen Bildschirmen</param>
    public static IAttack? Create(string? kind, Rect stage, IReadOnlyList<Point> clusters, IReadOnlyList<Point> icons, double iconSize, IReadOnlyList<Rect>? screens = null, IReadOnlyList<Point>? everywhere = null)
    {
        var each = icons.Count > 0 ? icons : clusters.Take(12).ToList();
        // Marschflugkörper auf Symbolgruppen aller Bildschirme statt auf ein Raster (19.11.2026: „Bomben landen auf leeren Flächen“)
        var all = everywhere is { Count: > 0 } ? everywhere : icons;
        var area = screens is { Count: > 0 } ? screens.Aggregate(Rect.Union) : stage;   // Flugbahnen über alle Bildschirme
        var (aims14, radius14) = all.Count > 0 ? Groups(all, iconSize, 14) : (clusters.Take(14).ToList(), double.PositiveInfinity);
        var (aims12, radius12) = all.Count > 0 ? Groups(all, iconSize, 12) : (clusters.Take(12).ToList(), double.PositiveInfinity);
        return kind switch
        {
            Tu160 => new Bomber(stage, aims14, iconSize) { Reach = radius14 + iconSize * 0.4 },
            Tu22 => new RowRaid(RowRaid.Kind.Carpet, area, all.Count > 0 ? all : each, iconSize) { Reach = iconSize * 1.3 },
            Su34 => new RowRaid(RowRaid.Kind.Strike, area, all.Count > 0 ? all : each, iconSize) { Reach = iconSize * 1.3 },
            Ka52 => new HelicopterRaid(stage, everywhere is { Count: > 0 } ? everywhere : each, iconSize, screens),
            Tu95 => new BearRaid(stage, aims12, iconSize) { Reach = radius12 + iconSize * 0.4 },
            Il76 => new AirborneRaid(stage, each, iconSize),
            Ac130 => new GunshipRaid(area, all.Count > 0 ? all : each, iconSize, screens),
            Shahed => new ShahedRaid(area, all.Count > 0 ? all : each, iconSize),
            A10 => new StrafeRaid(StrafeRaid.Kind.A10, area, all.Count > 0 ? all : each, iconSize),
            Su25 => new StrafeRaid(StrafeRaid.Kind.Su25, area, all.Count > 0 ? all : each, iconSize),
            _ => null,
        };
    }
    /// <summary>
    /// Einschlagpunkte für Marschflugkörper: benachbarte Symbole (Radius 1,6 Symbolgrößen) bilden eine Gruppe, jede Gruppe bekommt einen
    /// Einschlag genau auf das Symbol nächst ihrer Mitte. Gibt es mehr Gruppen als Flugkörper, wächst der Radius. Geht vom tatsächlichen
    /// Desktop jedes Nutzers aus, nicht von einem festen Raster.
    /// </summary>
    internal static (List<Point> Aims, double Radius) Groups(IReadOnlyList<Point> icons, double iconSize, int max)
    {
        double r = iconSize * 1.6;
        while (true)
        {
            var open = icons.ToList(); var aims = new List<Point>();
            while (open.Count > 0)
            {
                var seed = open.OrderByDescending(c => open.Count(o => (o - c).Length <= r)).First();
                var group = open.Where(o => (o - seed).Length <= r).ToList();
                var mid = new Point(group.Average(c => c.X), group.Average(c => c.Y));
                aims.Add(group.OrderBy(c => (c - mid).LengthSquared).First());
                open.RemoveAll(group.Contains);
            }
            if (aims.Count <= max || r > iconSize * 14) return (aims, r);
            r *= 1.25;
        }
    }
}

static class Fly
{
    public static double Smooth(double v) { v = Math.Clamp(v, 0, 1); return v * v * (3 - 2 * v); }
    public static double EaseOut(double v) { v = Math.Clamp(v, 0, 1); return 1 - Math.Pow(1 - v, 3); }
    public static double EaseIn(double v) { v = Math.Clamp(v, 0, 1); return v * v * v; }
    public static double Turn(double from, double to, double k)
    {
        double d = to - from; while (d > Math.PI) d -= Math.Tau; while (d < -Math.PI) d += Math.Tau;
        return from + d * k;
    }
    public static Point Bez(Point a, Point c, Point b, double k) => new((1 - k) * (1 - k) * a.X + 2 * (1 - k) * k * c.X + k * k * b.X, (1 - k) * (1 - k) * a.Y + 2 * (1 - k) * k * c.Y + k * k * b.Y);
    public static Vector Rot(Vector v, double a) => new(v.X * Math.Cos(a) - v.Y * Math.Sin(a), v.X * Math.Sin(a) + v.Y * Math.Cos(a));

    static readonly Dictionary<string, Geometry> cache = [];
    /// <summary>SVG-Pfad (Draufsicht, 100 Einheiten lang, Nase bei +x) einmal einlesen.</summary>
    public static Geometry G(string svg)
    {
        lock (cache)
        {
            if (cache.TryGetValue(svg, out var g)) return g;
            g = Geometry.Parse(svg); g.Freeze(); return cache[svg] = g;
        }
    }
    /// <summary>Halbe Kontur (y ≥ 0) auf beide Seiten gespiegelt.</summary>
    public static Geometry Both(string half)
    {
        string key = "beide:" + half;
        lock (cache)
        {
            if (cache.TryGetValue(key, out var g)) return g;
            var a = Geometry.Parse(half);
            var b = Geometry.Parse(half).Clone(); b.Transform = new ScaleTransform(1, -1);   // Parse liefert eingefroren: Kopie spiegeln
            var group = new GeometryGroup { FillRule = FillRule.Nonzero };
            group.Children.Add(a); group.Children.Add(b); group.Freeze();
            return cache[key] = group;
        }
    }
    public static Brush B(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }
    public static Pen P(string hex, double w) { var p = new Pen(B(hex), w) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; p.Freeze(); return p; }
    public static MatrixTransform Frame(Point at, double heading, double u) =>
        new(Math.Cos(heading) * u, Math.Sin(heading) * u, -Math.Sin(heading) * u, Math.Cos(heading) * u, at.X, at.Y);

    public static readonly Brush Shadow = B("#48000000");
    /// <summary>Schatten auf dem Desktop, je höher, desto weiter versetzt.</summary>
    public static void ShadowOf(DrawingContext dc, Point at, double heading, double u, double altitude, params Geometry[] parts)
    {
        double lift = Math.Max(0, altitude) * 100 * u;
        dc.PushTransform(new TranslateTransform(lift * 0.16, lift * 0.26));
        dc.PushTransform(Frame(at, heading, u));
        foreach (var g in parts) dc.DrawGeometry(Shadow, null, g);
        dc.Pop(); dc.Pop();
    }
}

// ================================================================ Draufsichten (SVG-Pfade, 1 Einheit = Länge/100)

static partial class Planes
{
    // ---------------------------------------------------------------- Ту-22М3 (Länge 42,5 m, Spannweite 34,3 m bei 20°, 23,3 m bei 65°)
    // Maße aus einer Dreiseitenansicht ausgemessen : spitze Nase mit Sonde, seitliche Keileinläufe ab +17,6, Handschuh
    // 57°, Schwenkflügel um das Gelenk am Handschuhende (20° bis 65°), schmales Höhenleitwerk über das Heck hinaus, zwei Düsen.
    static readonly Geometry Tu22Body = Fly.G("M50,0 C49.2,-0.9 47,-1.7 44,-2.2 L38,-2.8 C30,-3.1 24,-3.3 18,-3.5 L-40,-3.5 L-50.6,-2.4 L-50.6,2.4 L-40,3.5 L18,3.5 C24,3.3 30,3.1 38,2.8 L44,2.2 C47,1.7 49.2,0.9 50,0 Z");
    static readonly Geometry Tu22Trunks = Fly.Both("M18.2,3.5 L17.2,7.1 L0,7.1 L-26,6.6 L-38,6.1 L-46,5.4 L-50.6,4.6 L-50.6,3 Z");
    static readonly Geometry Tu22Mouth = Fly.Both("M18.2,3.7 L17.3,6.9 L16.5,6.9 L17.4,3.7 Z");
    static readonly Geometry Tu22Glove = Fly.Both("M0.5,7.1 L-15.9,17.1 L-22.4,17.1 L-26.6,6.9 Z");
    static readonly Geometry Tu22Tail = Fly.Both("M-37.1,5.4 L-51.8,13.9 C-52.8,14.5 -55.6,14.6 -56.6,13.6 L-57.4,12 L-52.2,5.4 Z");
    static readonly Geometry Tu22Fin = Fly.G("M-18,0 C-26,-0.9 -38,-1.3 -48,-1.1 L-53,-0.4 L-53,0.4 L-48,1.1 C-38,1.3 -26,0.9 -18,0 Z");
    static readonly Geometry Tu22Canopy = Fly.G("M35.8,0 C35.4,-1.6 34,-2.3 31.5,-2.4 L28.2,-2.3 L28.2,2.3 L31.5,2.4 C34,2.3 35.4,1.6 35.8,0 Z");
    static readonly Geometry Tu22Nose = Fly.G("M50,0 C49.2,-0.9 47,-1.7 44,-2.2 L44,2.2 C47,1.7 49.2,0.9 50,0 Z");
    // Schwenkflügel wie bei der Tu-160 (19.11.2026: „dieselben Fehler wie anfangs bei der Tu-160“): nicht eine Stellung starr um einen
    // geschätzten Punkt drehen, sondern beide Stellungen aus dem Riss nehmen (20° oben, 65° unten), den Drehpunkt daraus errechnen
    // (Vorder- und Hinterkante beider Stellungen, kleinste Fehler: (−17,5; ±13,9), Schwenkwinkel 42,4°) und jeden Konturpunkt in Winkel
    // und Abstand um den Drehpunkt von der 20°- zur 65°-Kontur laufen lassen. Die Wurzel liegt in beiden Stellungen im Handschuh.
    static readonly Point Tu22Pivot = new(-17.5, 13.9);
    // Punkt für Punkt einander zugeordnet: Wurzel innen vorn, Vorderkante am Handschuh, Vorderkante Spitze, Randbogen, Hinterkante Spitze,
    // Hinterkante Wurzel, Wurzel innen hinten; dazu Klappenfuge (2 Punkte) und Stern
    static readonly Point[] Tu22Wing20 = [new(-15, 12), new(-13.75, 17.1), new(-23.8, 42.6), new(-25.8, 43.6), new(-27.6, 42.8), new(-24.3, 10), new(-20, 10)];
    static readonly Point[] Tu22Wing65 = [new(-14.4, 14.2), new(-14.6, 17.6), new(-37.5, 29), new(-41, 30.7), new(-43.6, 28.7), new(-24.2, 10), new(-16.7, 9.3)];
    static readonly Point[] Tu22Marks20 = [new(-24.4, 18), new(-26.9, 41), new(-24.4, 33)];
    static readonly Point[] Tu22Marks65 = [new(-30.4, 18), new(-40.3, 27), new(-33.5, 24)];
    static Point Tu22Blend(Point a20, Point a65, double k, double side)
    {
        var c = Tu22Pivot;
        var v1 = a20 - c; var v2 = a65 - c;
        double t1 = Math.Atan2(v1.Y, v1.X), t2 = Math.Atan2(v2.Y, v2.X);
        double d = t2 - t1; while (d > Math.PI) d -= Math.Tau; while (d < -Math.PI) d += Math.Tau;
        double r = v1.Length + (v2.Length - v1.Length) * k, t = t1 + d * k;
        return new Point(c.X + Math.Cos(t) * r, side * (c.Y + Math.Sin(t) * r));
    }
    static readonly Dictionary<int, Geometry> tu22Wings = [];
    /// <summary>Beide Außenflügel bei dieser Pfeilung (auf 1° gerundet, zwischengespeichert).</summary>
    static Geometry Tu22Outer(double sweep)
    {
        int key = (int)Math.Round(Math.Clamp(sweep, 20, 65));
        lock (tu22Wings)
        {
            if (tu22Wings.TryGetValue(key, out var g)) return g;
            double k = (key - 20) / 45.0;
            var geometry = new StreamGeometry();
            using (var c = geometry.Open())
                foreach (double side in new[] { 1.0, -1.0 })
                {
                    c.BeginFigure(Tu22Blend(Tu22Wing20[0], Tu22Wing65[0], k, side), true, true);
                    for (int n = 1; n < Tu22Wing20.Length; n++) c.LineTo(Tu22Blend(Tu22Wing20[n], Tu22Wing65[n], k, side), true, true);
                }
            geometry.Freeze();
            return tu22Wings[key] = geometry;
        }
    }
    static readonly Brush Tu22Light = Fly.B("#C9CDD1"), Tu22Shade = Fly.B("#B5BBC0");
    static readonly Pen Tu22Edge = Fly.P("#6C737A", 0.35), Seam = Fly.P("#8A9197", 0.22), Pitot = Fly.P("#5E656C", 0.5);
    static readonly Brush Glass = Fly.B("#2C3846"), Nozzle = Fly.B("#3A3E43"), Radome = Fly.B("#E2E4E6");
    // Rumpfrücken, Spanten, Höhenruder, Seitenruder, Haubenrahmen, Antennen, Tankklappen
    static readonly Geometry Tu22Detail = Fly.G("M26,-0.6 L-18,-0.6 M26,0.6 L-18,0.6 M38,-2.8 L38,2.8 M18,-3.5 L18,3.5 M0,-3.5 L0,3.5 M-20,-3.5 L-20,3.5 M-34,-3.5 L-34,3.5 " +
        "M-44.8,-6 L-54,-12.6 M-44.8,6 L-54,12.6 M-42,-0.3 L-52,-0.3 M32,-2.4 L32,2.4 M10,-0.3 L10,0.3 M-8,-0.3 L-8,0.3 M24,-1.2 L21,-1.2 L21,1.2 L24,1.2 Z");
    static readonly Brush NavRedSoft = Art.Glow("#D0FF3030"), NavGreenSoft = Art.Glow("#D040FF80");
    static readonly Brush Tu22Halo = Art.Glow("#70FF7A2A"), Tu22Flame = Art.Glow("#F0FFE8B0");

    /// <param name="sweep">Pfeilung der äußeren Flügel in Grad (20 Start/Landung, 30 Reiseflug, 65 Hochgeschwindigkeit)</param>
    /// <param name="afterburner">0…1 Nachbrenner der beiden NK-25</param>
    public static void Tu22(DrawingContext dc, Point at, double heading, double u, double altitude, double sweep = 30, double afterburner = 0, double t = 0)
    {
        var wing = Tu22Outer(sweep);
        Fly.ShadowOf(dc, at, heading, u, altitude, wing, Tu22Tail, Tu22Glove, Tu22Trunks, Tu22Body);
        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawGeometry(Tu22Shade, Tu22Edge, wing);
        double k = (Math.Round(Math.Clamp(sweep, 20, 65)) - 20) / 45;
        foreach (double s in new[] { -1.0, 1.0 })
        {   // Klappenfuge und Stern laufen mit dem Flügel
            dc.DrawLine(Seam, Tu22Blend(Tu22Marks20[0], Tu22Marks65[0], k, s), Tu22Blend(Tu22Marks20[1], Tu22Marks65[1], k, s));
            Art.Star(dc, Tu22Blend(Tu22Marks20[2], Tu22Marks65[2], k, s), 2.0, 0);
        }
        dc.DrawGeometry(Tu22Shade, Tu22Edge, Tu22Tail);
        dc.DrawGeometry(Tu22Light, Tu22Edge, Tu22Glove);
        dc.DrawGeometry(Tu22Light, Tu22Edge, Tu22Trunks);
        dc.DrawGeometry(Tu22Light, Tu22Edge, Tu22Body);
        dc.DrawGeometry(Nozzle, null, Tu22Mouth);                               // Einlaufschlitze
        dc.DrawGeometry(Tu22Shade, Tu22Edge, Tu22Fin);
        dc.DrawGeometry(Radome, Tu22Edge, Tu22Nose);
        dc.DrawLine(Pitot, new Point(50, 0), new Point(52.2, 0));
        dc.DrawGeometry(Glass, Tu22Edge, Tu22Canopy);
        dc.DrawLine(Seam, new Point(28.2, 0), new Point(35.8, 0));
        foreach (double s in new[] { -1.0, 1.0 })
        {
            dc.DrawEllipse(Nozzle, Tu22Edge, new Point(-50.8, 2.6 * s), 1.0, 1.9);
            if (afterburner > 0.01)
            {
                double flick = 0.85 + 0.15 * Math.Sin(t * 53 + s * 2);
                Soft.Draw(dc, Tu22Halo, new Point(-57, 2.6 * s), 7.5 * afterburner * flick, 2.2, afterburner);
                Soft.Draw(dc, Tu22Flame, new Point(-54, 2.6 * s), 3.6 * afterburner * flick, 1.2, 1);
            }
            dc.DrawLine(Seam, new Point(-1, 6.4 * s), new Point(-15.4, 15.6 * s));   // Vorflügel am Handschuh
            dc.DrawLine(Seam, new Point(-26.6, 6.6 * s), new Point(-46, 5.2 * s));   // Triebwerkstunnel
        }
        Art.Star(dc, new Point(-44, 0), 1.0, 0);
        dc.DrawGeometry(null, Seam, Tu22Detail);
        foreach (double s in new[] { -1.0, 1.0 })
        {
            Soft.Draw(dc, s < 0 ? NavRedSoft : NavGreenSoft, Tu22Blend(Tu22Wing20[3], Tu22Wing65[3], k, s), 1.4, 1.4, 1);   // Positionslichter
            dc.DrawRoundedRectangle(null, Seam, new Rect(9, 6.9 * s - 0.5, 3.2, 1.0), 0.3, 0.3);   // Zusatzeinlassklappen
            dc.DrawRoundedRectangle(null, Seam, new Rect(4.6, 6.9 * s - 0.5, 3.2, 1.0), 0.3, 0.3);
        }
        dc.DrawLine(Pitot, new Point(-53, -0.35), new Point(-56.4, -0.35));   // Heckstand UK-9A-802 mit GSh-23
        dc.DrawLine(Pitot, new Point(-53, 0.35), new Point(-56.4, 0.35));
        dc.DrawEllipse(Nozzle, null, new Point(-52.6, 0), 0.8, 0.6);
        dc.DrawRoundedRectangle(Nozzle, null, new Rect(-50.4, -0.5, 2.4, 1.0), 0.4, 0.4);   // Feuerleitradar am Seitenleitwerk
        dc.Pop();
    }

    // ---------------------------------------------------------------- Су-34 (Länge 23,3 m, Spannweite 14,7 m)
    // Maße ausgemessen : schmale Nase mit großem Radom, Nebeneinander-Cockpit, lange Vorderkantenwurzel, Canards
    // an der Wurzel (+9,5…−5), Flügel 43° mit Startschienen an den Spitzen, schräg stehende Seitenleitwerke, Höhenleitwerk 45°, Heckstachel.
    static readonly Geometry Su34Body = Fly.G("M50,0 C50,-1.2 49.4,-2.2 48,-2.6 C46,-3.1 42,-3.6 38,-3.95 L28,-4.5 C24,-4.8 21,-5.4 18,-6.2 C14,-7 9,-8.3 4,-9.7 L-2,-10.6 L-30,-10.8 L-46,-10.6 L-49.6,-9.4 L-50.2,-3 " +
        "L-50.2,3 L-49.6,9.4 L-46,10.6 L-30,10.8 L-2,10.6 L4,9.7 C9,8.3 14,7 18,6.2 C21,5.4 24,4.8 28,4.5 L38,3.95 C42,3.6 46,3.1 48,2.6 C49.4,2.2 50,1.2 50,0 Z");
    static readonly Geometry Su34Wing = Fly.Both("M1,9.6 L-25.9,35.2 L-26.8,35.7 L-35.4,35.6 L-34,30 L-28.8,10 Z");
    static readonly Geometry Su34Rail = Fly.Both("M-20,35.5 L-35.8,35.5 L-35.8,36.4 L-20,36.4 Z");
    static readonly Geometry Su34Canard = Fly.Both("M9.5,8.4 L-0.2,16.3 L-2.6,16.4 L-5.2,8.6 Z");
    static readonly Geometry Su34Tail = Fly.Both("M-33.6,10.4 L-46.4,23.6 L-47.8,24 L-50.2,23.4 L-50.6,10.4 Z");
    static readonly Geometry Su34Fins = Fly.Both("M-30.5,10.8 L-40.5,13.4 L-48.2,13.6 L-48.6,11.2 L-46,10.8 Z");
    static readonly Geometry Su34Sting = Fly.G("M-49.5,-2.4 L-63,-2.2 L-66.2,-1.2 L-66.2,1.2 L-63,2.2 L-49.5,2.4 Z");
    static readonly Geometry Su34Canopy = Fly.G("M38.3,0 C38,-2.6 36,-3.4 33,-3.4 L28.6,-3.3 C28.2,-2 28.2,2 28.6,3.3 L33,3.4 C36,3.4 38,2.6 38.3,0 Z");
    static readonly Geometry Su34Radome = Fly.G("M50,0 C50,-1.2 49.4,-2.2 48,-2.6 C46.6,-2.9 45.2,-3.1 44,-3.2 L44,3.2 C45.2,3.1 46.6,2.9 48,2.6 C49.4,2.2 50,1.2 50,0 Z");
    static readonly Geometry Su34Patches = Fly.Both("M-6,14.6 L-16.5,24.8 L-24,24 L-21.5,15.4 Z M-38,12 L-44,19.6 L-48.4,19.2 L-47.6,12 Z");
    static readonly Brush Su34Light = Fly.B("#93A3B1"), Su34Shade = Fly.B("#7E8F9E"), Su34Camo = Fly.B("#6F8191"), Su34Nose = Fly.B("#C9CED2");
    static readonly Pen Su34Edge = Fly.P("#4B5660", 0.35);

    public static void Su34(DrawingContext dc, Point at, double heading, double u, double altitude, double t)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, Su34Wing, Su34Tail, Su34Canard, Su34Body, Su34Sting);
        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawGeometry(Su34Shade, Su34Edge, Su34Wing);
        dc.DrawGeometry(Nozzle, null, Su34Rail);                               // Startschienen an den Flügelspitzen
        dc.DrawGeometry(Su34Shade, Su34Edge, Su34Tail);
        dc.DrawGeometry(Su34Light, Su34Edge, Su34Body);
        dc.DrawGeometry(Su34Camo, null, Su34Patches);                          // Tarnflecken
        dc.DrawGeometry(Su34Light, Su34Edge, Su34Canard);
        dc.DrawGeometry(Su34Shade, Su34Edge, Su34Fins);
        dc.DrawGeometry(Su34Shade, Su34Edge, Su34Sting);
        dc.DrawGeometry(Su34Nose, Su34Edge, Su34Radome);
        dc.DrawGeometry(Glass, Su34Edge, Su34Canopy);
        dc.DrawLine(Seam, new Point(28.4, 0), new Point(38.3, 0));
        foreach (double s in new[] { -1.0, 1.0 })
        {
            dc.DrawRoundedRectangle(Nozzle, Su34Edge, new Rect(-50, 6 * s - 2.8, 6.2, 5.6), 1.2, 1.2);
            Soft.Draw(dc, Heat, new Point(-54, 6 * s), 3.6 + 0.4 * Math.Sin(t * 47 + s), 1.6, 0.7);
            dc.DrawLine(Seam, new Point(-27.2, 12 * s), new Point(-30.8, 33 * s));   // Flaperon
            dc.DrawLine(Seam, new Point(-10.5, 17 * s), new Point(-23.5, 33 * s));   // Vorflügel
            Art.Star(dc, new Point(-25, 24 * s), 1.6, 0);
            Soft.Draw(dc, s < 0 ? NavRedSoft : NavGreenSoft, new Point(-27, 35.9 * s), 1.2, 1.2, 1);
            dc.DrawRoundedRectangle(Su34Shade, Su34Edge, new Rect(-36.4, 36 * s - 1.1, 14, 2.2), 1.1, 1.1);   // Chibiny-Störbehälter an den Spitzen
        }
        dc.DrawGeometry(null, Seam, Su34Detail);
        dc.DrawLine(Probe, new Point(42, -3.2), new Point(44.6, -3.2));     // Tankstutzen links vor der Haube (eingefahren)
        dc.DrawRoundedRectangle(null, Seam, new Rect(-26, -2.6, 8, 5.2), 1, 1);   // Luftbremse auf dem Rücken
        dc.DrawEllipse(Nozzle, null, new Point(-66, 0), 0.6, 0.9);            // Heckradar im Stachel
        dc.DrawLine(Seam, new Point(33.6, -3.4), new Point(33.6, 3.4));        // Haubenrahmen
        dc.Pop();
    }
    // Vorderkantenwurzel, Rumpfrücken, Canard-Achse, Höhen- und Seitenruder, Wartungsdeckel, Antennen
    static readonly Geometry Su34Detail = Fly.G("M26,-4.6 L4,-9.6 M26,4.6 L4,9.6 M22,-0.6 L-40,-0.6 M22,0.6 L-40,0.6 M2,-8.6 L2,8.6 M-12,-10.7 L-12,10.7 M-30,-10.8 L-30,10.8 " +
        "M-42,-14 L-49,-21 M-42,14 L-49,21 M-41,-11.6 L-47.6,-12.4 M-41,11.6 L-47.6,12.4 M14,-0.4 L14,0.4 M-4,-0.4 L-4,0.4 M8,-3 L5,-3 L5,3 L8,3 Z");
    static readonly Brush Heat = Art.Glow("#C0FFB060");

    // ---------------------------------------------------------------- Ка-52 (Rumpf 13,5 m, Rotor Ø 14,5 m, zwei gegenläufige Dreiblattrotoren)
    // Maße aus einer Dreiseitenansicht ausgemessen (19.11.2026: „sieht von oben nicht aus wie das Original“): schmaler Rumpf
    // (Cockpit nebeneinander 1,7 m breit), Rotormast 5,5 m hinter der Nase, Triebwerke außen am Rumpfrücken mit schräg nach außen
    // gerichteten Abgasrohren, Stummelflügel mit zwei Stationen und Täuschkörperbehältern an den Spitzen, langer Heckausleger,
    // Höhenleitwerk mit Endscheiben. 1 Einheit = 0,138 m.
    static readonly Geometry Ka52Body = Fly.G("M50,0 L49.3,-0.75 L47.6,-1.85 L45.1,-2.95 L42.1,-3.85 L38.2,-4.65 L35.4,-5.1 L32.6,-5.45 L29.8,-5.95 L22,-6.05 " +
        "C16,-5.95 6,-5.85 -3,-5.75 C-10,-5.5 -18,-4.6 -24,-3.9 C-32,-3.1 -38,-2.4 -42.4,-1.5 C-45,-1 -47.2,-0.6 -47.2,0 " +
        "C-47.2,0.6 -45,1 -42.4,1.5 C-38,2.4 -32,3.1 -24,3.9 C-18,4.6 -10,5.5 -3,5.75 C6,5.85 16,5.95 22,6.05 " +
        "L29.8,5.95 L32.6,5.45 L35.4,5.1 L38.2,4.65 L42.1,3.85 L45.1,2.95 L47.6,1.85 L49.3,0.75 Z");
    static readonly Geometry Ka52Stub = Fly.Both("M12.1,5.6 L11.6,24.1 L5.2,24.1 L0.3,5.6 Z");
    static readonly Geometry Ka52Pods = Fly.Both("M14.2,25.7 C13.6,24.4 12.2,24.1 10.5,24.1 L3.4,24.2 C2.8,24.4 2.6,25 2.6,25.7 C2.6,26.4 2.8,27 3.4,27.2 L10.5,27.3 C12.2,27.3 13.6,27 14.2,25.7 Z");
    static readonly Geometry Ka52Engines = Fly.Both("M19.6,5.4 C19.9,7.2 19.4,8.9 18,9.5 L4.6,9.9 C2.8,10 0.8,9.6 -1.2,8.7 L-2.6,5.4 Z");
    static readonly Geometry Ka52Exhaust = Fly.Both("M3.4,9.1 L0.4,12.9 L-1.8,12.1 L0.2,8.4 Z");
    static readonly Geometry Ka52Intake = Fly.Both("M20.4,7.4 C20.4,6.3 19.6,5.6 18.6,5.6 C17.4,5.6 16.8,6.4 16.8,7.4 C16.8,8.4 17.4,9.2 18.6,9.2 C19.6,9.2 20.4,8.5 20.4,7.4 Z");
    static readonly Geometry Ka52Tail = Fly.Both("M-25.4,3.4 L-25.9,10.8 L-34.4,10.8 L-35.6,3.4 Z");
    static readonly Geometry Ka52Endplates = Fly.Both("M-24.2,10.7 L-35.9,10.7 L-36.4,11.3 L-35.9,11.9 L-24.2,11.9 L-23.6,11.3 Z");
    static readonly Geometry Ka52Fin = Fly.G("M-33.5,0 L-38,-1 L-46.4,-0.8 L-47.6,0 L-46.4,0.8 L-38,1 Z");
    static readonly Geometry Ka52Glass = Fly.Both("M41.4,0.45 L41.8,3.3 C39.4,4.5 34.4,5.05 27.6,4.95 L26.2,0.45 Z");
    static readonly Geometry KaBladeShape = Fly.G("M2.6,-1.55 L48.6,-1.7 L52.5,-0.35 L52.5,1.15 L2.6,1.65 Z");
    static readonly Brush KaLight = Fly.B("#58614F"), KaShade = Fly.B("#49513F"), KaDark = Fly.B("#3C4334"), KaBlade = Fly.B("#262923"), KaDisc = Fly.B("#1E202620"), Pod = Fly.B("#3A3F37"),
        Soot = Fly.B("#2B2D2A"), KaDust = Fly.B("#6B705E");
    // Haubenrahmen zwischen den Sitzen, Wartungsklappen der Triebwerke, Stöße am Heckausleger, Seiten- und Höhenruder, Endscheibenruder
    static readonly Geometry Ka52Detail = Fly.G("M41.4,0 L26.2,0 M34,-0.45 L34,-4.9 M34,0.45 L34,4.9 M16,-5.4 L16,-9.6 M16,5.4 L16,9.6 M8,-5.6 L8,-9.8 M8,5.6 L8,9.8 " +
        "M-12,-5 L-12,5 M-24,-3.9 L-24,3.9 M-40,-0.9 L-46.4,-0.5 M-32,-4 L-33.6,-10.6 M-32,4 L-33.6,10.6 M-32,-11.3 L-36.2,-11.3 M-32,11.3 L-36.2,11.3");
    static readonly Pen KaEdge = Fly.P("#2A2F25", 0.4), KaSeam = Fly.P("#22261F", 0.3), Barrel = Fly.P("#202320", 0.9);
    /// <summary>Bewegungsunschärfe der Blätter: vorab gemischte Farben statt PushOpacity (jede Deckkraftgruppe kostete eine eigene Zwischenfläche).</summary>
    static readonly Brush[][] KaTrail = [Trails(0.55), Trails(1.0)];
    static Brush[] Trails(double strength) => [.. new[] { 0.9, 0.17, 0.12, 0.07 }.Select(a => (Brush)Fly.B($"#{(byte)Math.Round(a * strength * 255):X2}262923"))];
    internal static readonly Point KaHub = new(10.4, 0);
    const double RotorRadius = 52.5, RotorSpeed = 11;   // rad/s, sichtbar langsamer als echt, damit man die Blätter sieht
    /// <summary>Kanonenmündung (2A42 rechts am Rumpf), innere Station (B-8V20), äußere Station (Vikhr), Sensor in der Nase.</summary>
    internal static readonly Vector KaMuzzle = new(25.6, 6.4), KaRockets = new(15.8, 14.2), KaVikhr = new(16.4, 19.7), KaSensor = new(49.2, 0);

    public static void Ka52(DrawingContext dc, Point at, double heading, double u, double altitude, double t, bool rotors = true)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, Ka52Stub, Ka52Pods, Ka52Tail, Ka52Body, Ka52Engines);
        if (rotors)
        {   // Schatten der Rotorkreise
            dc.PushTransform(new TranslateTransform(altitude * 100 * u * 0.16, altitude * 100 * u * 0.26));
            dc.PushTransform(Fly.Frame(at, heading, u));
            dc.DrawEllipse(KaDisc, null, KaHub, RotorRadius, RotorRadius);
            dc.Pop(); dc.Pop();
        }

        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawGeometry(KaShade, KaEdge, Ka52Stub);
        foreach (double s in new[] { -1.0, 1.0 })
        {
            // innen B-8V20 mit 20 S-8, außen APU-6 mit sechs Vikhr (von oben drei Rohre)
            dc.DrawRoundedRectangle(Pod, KaEdge, new Rect(-1.8, KaRockets.Y * s - 1.9, 17.6, 3.8), 1.9, 1.9);
            dc.DrawEllipse(KaDark, null, new Point(KaRockets.X - 0.4, KaRockets.Y * s), 0.7, 1.5);
            dc.DrawRoundedRectangle(Pod, KaEdge, new Rect(-4.4, KaVikhr.Y * s - 1.5, 20.8, 3.0), 0.8, 0.8);
            dc.DrawLine(KaSeam, new Point(-4.4, KaVikhr.Y * s - 0.5), new Point(16.4, KaVikhr.Y * s - 0.5));
            dc.DrawLine(KaSeam, new Point(-4.4, KaVikhr.Y * s + 0.5), new Point(16.4, KaVikhr.Y * s + 0.5));
        }
        dc.DrawGeometry(KaShade, KaEdge, Ka52Pods);
        dc.DrawGeometry(KaShade, KaEdge, Ka52Tail);
        dc.DrawGeometry(KaDark, KaEdge, Ka52Endplates);
        dc.DrawGeometry(KaLight, KaEdge, Ka52Body);
        dc.DrawGeometry(KaShade, KaEdge, Ka52Fin);
        dc.DrawRectangle(KaDark, KaEdge, new Rect(6, 5.4, 7.5, 1.8));          // Lafette der 2A42 rechts
        dc.DrawLine(Barrel, new Point(12, KaMuzzle.Y), (Point)KaMuzzle);
        dc.DrawGeometry(Soot, KaEdge, Ka52Exhaust);
        dc.DrawGeometry(KaShade, KaEdge, Ka52Engines);
        dc.DrawGeometry(KaDust, KaEdge, Ka52Intake);                             // Staubschutz vor den Einläufen
        dc.DrawGeometry(Glass, KaEdge, Ka52Glass);                             // nebeneinander sitzende Besatzung
        dc.DrawLine(KaSeam, new Point(-3, -5.6), new Point(-3, 5.6));
        dc.DrawLine(KaSeam, new Point(-20, 0), new Point(-44, 0));
        if (rotors)
        {   // unterer Rotor gegen, oberer Rotor im Uhrzeigersinn
            Rotor(dc, -t * RotorSpeed + 0.5, -1, 0);
            Rotor(dc, t * RotorSpeed, 1, 1);
        }
        dc.DrawEllipse(KaShade, KaEdge, KaHub, 3.6, 3.6);
        dc.DrawEllipse(KaBlade, null, KaHub, 1.7, 1.7);
        dc.DrawGeometry(null, KaSeam, Ka52Detail);
        foreach (double s in new[] { -1.0, 1.0 })
        {
            dc.DrawEllipse(KaDark, KaEdge, new Point(40, 3.4 * s), 0.9, 0.9);     // L370 Witebsk: Raketenwarner an der Nase
            dc.DrawEllipse(KaDark, KaEdge, new Point(-30, 2.2 * s), 0.8, 0.8);    // … und am Heckausleger
            dc.DrawRoundedRectangle(KaDark, null, new Rect(-6, 5.8 * s - 0.6, 5, 1.2), 0.4, 0.4);   // UV-26 Täuschkörperwerfer
            Soft.Draw(dc, s < 0 ? NavRedSoft : NavGreenSoft, new Point(8.4, 27.3 * s), 1.1, 1.1, 1);
        }
        dc.Pop();
    }

    static void Rotor(DrawingContext dc, double angle, int direction, int level)
    {
        dc.DrawEllipse(KaDisc, null, KaHub, RotorRadius, RotorRadius);
        var trail = KaTrail[level];
        for (int b = 0; b < 3; b++)
        {
            double a = angle + b * Math.Tau / 3;
            for (int k = 3; k >= 0; k--)   // Bewegungsunschärfe: blasse Kopien hinter dem Blatt (gegen die Drehrichtung)
            {
                var turn = new RotateTransform((a - k * 0.08 * direction) * 180 / Math.PI, KaHub.X, KaHub.Y); turn.Freeze();
                dc.PushTransform(turn);
                dc.PushTransform(Shift);
                dc.DrawGeometry(trail[k], null, KaBladeShape);
                dc.Pop(); dc.Pop();
            }
        }
    }
    static readonly TranslateTransform Shift = Frozen(new TranslateTransform(KaHub.X, KaHub.Y));
    static T Frozen<T>(T f) where T : Freezable { f.Freeze(); return f; }

    // ---------------------------------------------------------------- Ту-95МС (Länge 46,2 m, Spannweite 50 m, 4 × NK-12 mit gegenläufigen Vierblattpropellern Ø 5,6 m)
    // Maße aus einer Dreiseitenansicht ausgemessen : Flügel weit vorn (Wurzelvorderkante bei +24), Vorderkante 33°,
    // Hinterkante 25° gepfeilt; Triebwerke bei 26 % und 50 % der Halbspannweite, Propeller weit vor der Vorderkante; innen lange
    // Fahrwerksgondeln, die weit hinter die Hinterkante reichen; schmales, stark gepfeiltes Höhenleitwerk; Heckstand mit Zwillingskanone.
    static readonly Geometry Tu95Body = Fly.G("M50.2,0 C50.2,-1.4 49.2,-2.4 47.6,-2.8 C45.5,-3.25 43,-3.45 40,-3.45 L-18,-3.45 C-26,-3.3 -38,-2.4 -46,-1.7 L-51.6,-1.25 " +
        "C-52.4,-1.2 -52.8,-0.7 -52.8,0 C-52.8,0.7 -52.4,1.2 -51.6,1.25 L-46,1.7 C-38,2.4 -26,3.3 -18,3.45 L40,3.45 C43,3.45 45.5,3.25 47.6,2.8 C49.2,2.4 50.2,1.4 50.2,0 Z");
    static readonly Geometry Tu95Wing = Fly.Both("M23.6,2.6 L-9.3,53 C-10.4,55 -12.3,56.1 -14.1,56 C-15.4,55.9 -16,55 -15.9,53.2 L7.3,2.6 Z");
    static readonly Geometry Tu95Tail = Fly.Both("M-36.4,2 L-48.6,16 C-49.4,16.7 -51.2,16.9 -52.3,16.2 L-52.9,14.6 L-46.4,2 Z");
    static readonly Geometry Tu95Fin = Fly.G("M-24,0 C-30,-0.9 -40,-1.2 -50,-1 L-53,-0.4 L-53,0.4 L-50,1 C-40,1.2 -30,0.9 -24,0 Z");
    static readonly Geometry Tu95Gear = Fly.Both("M9.6,14.4 C9.6,13.3 6,12.55 1.5,12.55 C-3,12.55 -6,13.4 -7.4,14.4 C-6,15.4 -3,16.25 1.5,16.25 C6,16.25 9.6,15.5 9.6,14.4 Z");
    static readonly Geometry Tu95Inner = Fly.Both("M26,12.7 L26,16.1 C22,16.4 14,16.4 11,16.1 C9.6,15.9 9.2,15.1 9.2,14.4 C9.2,13.7 9.6,12.9 11,12.7 C14,12.4 22,12.4 26,12.7 Z");
    static readonly Geometry Tu95Outer = Fly.Both("M18.2,26.2 L18.2,29.2 C15,29.4 8,29.4 5.5,29.1 C4,28.9 3.4,28.3 3.4,27.7 C3.4,27.1 4,26.5 5.5,26.3 C8,26 15,26 18.2,26.2 Z");
    static readonly Geometry Tu95Glass = Fly.G("M46.6,0 C46.4,-1.5 45.6,-2.3 44.2,-2.5 L41.6,-2.6 L41.2,0 L41.6,2.6 L44.2,2.5 C45.6,2.3 46.4,1.5 46.6,0 Z");
    // Grenzschichtzäune (drei je Flügel), Rumpfrücken, Spanten, Höhen- und Seitenruder, Ausstiegsluken, Antennen
    static readonly Geometry Tu95Detail = Fly.G("M17.8,-11.4 L4.6,-11.6 M17.8,11.4 L4.6,11.6 M10.6,-22.6 L-0.4,-22.6 M10.6,22.6 L-0.4,22.6 M2.4,-35 L-5.8,-35 M2.4,35 L-5.8,35 " +
        "M36,-0.6 L-30,-0.6 M36,0.6 L-30,0.6 M30,-3.4 L30,3.4 M-10,-3.4 L-10,3.4 M-30,-3.2 L-30,3.2 M-44,-4.8 L-51,-13.8 M-44,4.8 L-51,13.8 M-42,-0.3 L-52,-0.3 " +
        "M40,-1.4 L37.6,-1.4 L37.6,1.4 L40,1.4 Z M-14,-1.2 L-16.4,-1.2 L-16.4,1.2 L-14,1.2 Z M20,-0.4 L20,0.4 M0,-0.4 L0,0.4 M-22,-0.4 L-22,0.4");
    static readonly Brush BearLight = Fly.B("#C4C9CD"), BearShade = Fly.B("#B1B7BC"), Blade = Fly.B("#2E3135"), Spinner = Fly.B("#9AA1A8"), PropDisc = Fly.B("#24303030");
    static readonly Pen BearEdge = Fly.P("#727A82", 0.35), BladePen = Fly.P("#2E3135", 1.3), Probe = Fly.P("#5E656C", 0.8), Guns = Fly.P("#4A5056", 0.45);
    /// <summary>Triebwerke: Lage quer zur Flugrichtung, vordere Propellerebene, Spinnerspitze.</summary>
    static readonly (double Y, double Prop, double Tip)[] BearEngines = [(14.4, 26.6, 31.1), (27.7, 18.6, 22.9)];
    /// <summary>Flügelstationen für die Ch-101: zwischen Rumpf und innerem Triebwerk, zwischen den Triebwerken.</summary>
    internal static readonly Vector[] BearStations = [new(10, 8), new(4, 21)];
    static readonly Geometry Tu95Spinners = Fly.Both(string.Join(" ", BearEngines.Select(e => FormattableString.Invariant(
        $"M{e.Prop + 1.4},{e.Y - 1.3} C{e.Tip - 1.2},{e.Y - 1.1} {e.Tip},{e.Y - 0.3} {e.Tip},{e.Y} C{e.Tip},{e.Y + 0.3} {e.Tip - 1.2},{e.Y + 1.1} {e.Prop + 1.4},{e.Y + 1.3} Z"))));

    public static void Tu95(DrawingContext dc, Point at, double heading, double u, double altitude, double t)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, Tu95Wing, Tu95Tail, Tu95Body, Tu95Inner, Tu95Outer, Tu95Gear);
        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawGeometry(BearShade, BearEdge, Tu95Wing);
        foreach (double s in new[] { -1.0, 1.0 })
        {
            dc.DrawLine(Seam, new Point(9.6, 6 * s), new Point(-12.8, 50 * s));     // Klappen und Querruder
            dc.DrawLine(Seam, new Point(-4.6, 32 * s), new Point(-2.2, 32 * s));
            Art.Star(dc, new Point(-8.5, 44 * s), 2.2, 0);
        }
        dc.DrawGeometry(BearLight, BearEdge, Tu95Gear);
        dc.DrawGeometry(BearShade, BearEdge, Tu95Tail);
        dc.DrawGeometry(BearLight, BearEdge, Tu95Body);
        dc.DrawGeometry(BearShade, BearEdge, Tu95Fin);
        dc.DrawGeometry(Glass, BearEdge, Tu95Glass);
        dc.DrawLine(Probe, new Point(47.6, 0), new Point(54.2, 0));            // Betankungssonde
        dc.DrawLine(Guns, new Point(-52.6, -0.45), new Point(-55, -0.45));     // Heckstand GSh-23
        dc.DrawLine(Guns, new Point(-52.6, 0.45), new Point(-55, 0.45));
        dc.DrawGeometry(BearLight, BearEdge, Tu95Inner);
        dc.DrawGeometry(BearLight, BearEdge, Tu95Outer);
        foreach (double s in new[] { -1.0, 1.0 })
            foreach (var (y, prop, tip) in BearEngines)
            {
                // Draufsicht: die Propellerscheiben stehen quer zur Flugrichtung, die Blätter erscheinen als Striche, deren Länge mit dem Drehwinkel schwankt
                dc.DrawRectangle(PropDisc, null, new Rect(prop - 0.8, y * s - 6.2, 3.4, 12.4));
                Propeller(dc, prop, y * s, t * 24 + y, 1);
                Propeller(dc, prop + 1.6, y * s, -t * 24 + y * 0.5, -1);
            }
        dc.DrawGeometry(Spinner, BearEdge, Tu95Spinners);
        Art.Star(dc, new Point(-46, 0), 1.0, 0);
        dc.DrawGeometry(null, Seam, Tu95Detail);
        dc.DrawGeometry(Glass, BearEdge, Fly.G("M-50.4,-0.9 L-52.4,-0.6 L-52.4,0.6 L-50.4,0.9 Z"));   // Heckstand verglast
        foreach (double s in new[] { -1.0, 1.0 }) Soft.Draw(dc, s < 0 ? NavRedSoft : NavGreenSoft, new Point(-13.2, 55.6 * s), 1.4, 1.4, 1);
        dc.Pop();
    }

    static void Propeller(DrawingContext dc, double x, double y, double angle, int dir) => Propeller(dc, x, y, angle, dir, 6.2);

    /// <summary>Granate der 105-mm-Haubitze im Flug: dunkler Körper mit glühendem Leuchtsatz.</summary>
    public static void Shell(DrawingContext dc, Point at, double u)
    {
        Soft.Draw(dc, ShellGlow, at, 2.6 * Math.Max(0.6, u), 2.6 * Math.Max(0.6, u), 0.8);
        dc.DrawEllipse(BombBody, null, at, 1.1 * Math.Max(0.6, u), 1.1 * Math.Max(0.6, u));
    }
    static readonly Brush ShellGlow = Art.Glow("#C0FFC870");

    static void Propeller(DrawingContext dc, double x, double y, double angle, int dir, double radius)
    {
        for (int b = 0; b < 4; b++)
        {
            double a = angle + b * Math.PI / 2;
            double len = radius * Math.Sin(a);
            double lean = 0.5 * Math.Cos(a) * dir;
            dc.DrawLine(BladePen, new Point(x, y), new Point(x + lean, y + len));
        }
    }

    // ---------------------------------------------------------------- Ил-76МД (Länge 46,6 m, Spannweite 50,5 m)
    // Maße ausgemessen : Rumpf halb 5,2, Schulterflügel über dem Rumpf (Wurzelvorderkante +19, 27° gepfeilt), Triebwerke
    // bei 13,6 und 23,2 weit vor der Vorderkante, seitliche Fahrwerksgondeln, T-Leitwerk 17,4 m breit, verglaste Navigatornase.
    static readonly Geometry Il76Body = Fly.G("M50,0 C50,-1.4 49.2,-2.5 47.6,-3.1 C45.6,-3.7 43,-4.2 40,-4.6 C37,-5 35,-5.2 32,-5.2 L-22,-5.2 C-28,-4.6 -34,-3.6 -40,-2.8 L-50,-1.6 C-51.4,-1.3 -52,-0.8 -52,0 " +
        "C-52,0.8 -51.4,1.3 -50,1.6 L-40,2.8 C-34,3.6 -28,4.6 -22,5.2 L32,5.2 C35,5.2 37,5 40,4.6 C43,4.2 45.6,3.7 47.6,3.1 C49.2,2.5 50,1.4 50,0 Z");
    static readonly Geometry Il76Wing = Fly.G("M19,0 L19,5.2 L11.6,20 L-4.6,52 L-5.8,54 L-6.6,55.4 L-7.6,56.3 L-13.4,56.3 L-12.9,54 L-12.3,52 L-3.9,20 L-2.7,10 L-2.7,0 L-2.7,-10 L-3.9,-20 L-12.3,-52 L-12.9,-54 L-13.4,-56.3 L-7.6,-56.3 L-6.6,-55.4 L-5.8,-54 L-4.6,-52 L11.6,-20 L19,-5.2 Z");
    static readonly Geometry Il76Tail = Fly.Both("M-36.2,1 L-46.9,18.2 C-47.4,18.9 -48.6,19.2 -49.6,19 L-51.2,18.4 L-46,1 Z");
    static readonly Geometry Il76Fin = Fly.G("M-24.9,0 L-36,-1.2 L-46.7,-1 L-48.2,0 L-46.7,1 L-36,1.2 Z");
    static readonly Geometry Il76Gear = Fly.Both("M1,5 C-1,6.4 -4,6.8 -6,6.8 C-9,6.8 -11.5,6.2 -13,5 Z");
    static readonly Geometry Il76Fairing = Fly.G("M-2.7,-3.4 C-6,-3.4 -9.4,-2.8 -11,-1.2 L-11,1.2 C-9.4,2.8 -6,3.4 -2.7,3.4 Z");
    static readonly Geometry Il76Cockpit = Fly.G("M45.6,0 C45.6,-1.9 45,-2.7 43.6,-2.9 L42.6,-2.9 L42.6,2.9 L43.6,2.9 C45,2.7 45.6,1.9 45.6,0 Z");
    // Vorflügel, dreiteilige Klappen, Spoiler, Querruder, Rumpfrücken, Spanten, Höhen- und Seitenruder, Notausstiege, Antennen
    static readonly Geometry Il76Detail = Fly.G("M16.6,-6 L-2,-50 M16.6,6 L-2,50 M-2.5,-20 L-0.6,-20 M-2.5,20 L-0.6,20 M-3.4,-34 L-1.6,-34 M-3.4,34 L-1.6,34 " +
        "M1.6,-12 L-0.2,-30 M1.6,12 L-0.2,30 M-7.8,-40 L-10.4,-40 M-7.8,40 L-10.4,40 M30,-0.6 L-30,-0.6 M30,0.6 L-30,0.6 M36,-5 L36,5 M24,-5.2 L24,5.2 " +
        "M-16,-5.2 L-16,5.2 M-30,-4 L-30,4 M-44,-5 L-49.6,-15.6 M-44,5 L-49.6,15.6 M-37,-0.4 L-46.6,-0.4 M40,-1.4 L37.6,-1.4 L37.6,1.4 L40,1.4 Z M10,-0.4 L10,0.4 M-8,-0.4 L-8,0.4");
    static readonly Brush IlLight = Fly.B("#CBD0D4"), IlShade = Fly.B("#BAC0C5"), Intake = Fly.B("#4A4F55");
    static readonly Pen IlEdge = Fly.P("#77808A", 0.35);
    /// <summary>Triebwerke: Lage quer zur Flugrichtung, Einlauf vorn, Ende hinten, halbe Breite.</summary>
    static readonly (double Y, double Front, double Back, double Half)[] IlEngines = [(13.6, 24.4, 10.6, 2.2), (23.2, 19.4, 5.6, 1.9)];

    public static void Il76(DrawingContext dc, Point at, double heading, double u, double altitude, bool rampOpen)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, Il76Wing, Il76Tail, Il76Body, Il76Gear);
        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawGeometry(IlLight, IlEdge, Il76Gear);
        dc.DrawGeometry(IlLight, IlEdge, Il76Body);
        if (rampOpen) dc.DrawGeometry(Intake, IlEdge, Fly.G("M-40,-2.6 L-49,-1.6 L-49,1.6 L-40,2.6 Z"));   // offene Heckrampe
        foreach (double s in new[] { -1.0, 1.0 })
            foreach (var (y, front, back, half) in IlEngines)
            {   // Triebwerke hängen unter dem Flügel: von oben sieht man nur den Teil vor der Vorderkante
                dc.DrawRoundedRectangle(IlLight, IlEdge, new Rect(back, y * s - half, front - back, half * 2), half, half);
                dc.DrawEllipse(Intake, null, new Point(front - 0.5, y * s), 0.7, half - 0.5);
            }
        dc.DrawGeometry(IlShade, IlEdge, Il76Wing);          // Schulterdecker: Flügel liegt über dem Rumpf
        dc.DrawGeometry(IlLight, IlEdge, Il76Fairing);
        foreach (double s in new[] { -1.0, 1.0 })
        {
            dc.DrawLine(Seam, new Point(-1.1, 10 * s), new Point(-10.7, 52 * s));   // Klappen und Querruder
            Art.Star(dc, new Point(-3.6, 40 * s), 2.2, 0);
        }
        dc.DrawGeometry(IlShade, IlEdge, Il76Fin);
        dc.DrawGeometry(IlShade, IlEdge, Il76Tail);          // T-Leitwerk ganz oben
        dc.DrawGeometry(Glass, IlEdge, Il76Cockpit);
        dc.DrawEllipse(Glass, IlEdge, new Point(48.3, 0), 1.3, 1.2);   // verglaste Navigatorkanzel
        dc.DrawGeometry(null, Seam, Il76Detail);
        dc.DrawGeometry(Glass, IlEdge, Fly.G("M-50,-1.4 L-51.4,-1 L-51.4,1 L-50,1.4 Z"));   // Heckstand mit GSh-23
        dc.DrawLine(Guns, new Point(-51.6, -0.4), new Point(-54, -0.4)); dc.DrawLine(Guns, new Point(-51.6, 0.4), new Point(-54, 0.4));
        foreach (double s in new[] { -1.0, 1.0 }) Soft.Draw(dc, s < 0 ? NavRedSoft : NavGreenSoft, new Point(-10, 56 * s), 1.4, 1.4, 1);
        dc.Pop();
    }

    // ---------------------------------------------------------------- Bomben, Gleitbomben, Raketen, Fallschirme

    static readonly Brush BombBody = Fly.B("#3D4145"), KabBody = Fly.B("#5D636A"), Seeker = Fly.B("#A9BCCD"), RocketBody = Fly.B("#D9DDE0");
    static readonly Pen BombEdge = Fly.P("#1E2124", 0.5), Smoke = Fly.P("#66D6D6D6", 2.4), SmokeCore = Fly.P("#A0F2F2F2", 0.9);
    static readonly Brush Flame = Art.Glow("#F0FFC070");

    /// <summary>Freifallbombe (FAB-250), kleiner, je tiefer sie fällt.</summary>
    public static void Bomb(DrawingContext dc, Point at, double heading, double u, double altitude)
    {
        double s = u * (0.5 + 0.5 * altitude);
        dc.PushTransform(Fly.Frame(at, heading, s));
        dc.DrawEllipse(BombBody, BombEdge, new Point(0, 0), 4.2, 1.4);
        dc.DrawGeometry(BombBody, BombEdge, Fly.G("M-3,0 L-6,-2.2 L-6.4,-2.2 L-5.4,0 L-6.4,2.2 L-6,2.2 Z"));
        dc.Pop();
    }

    /// <summary>Gelenkte Gleitbombe (KAB-500) mit Flügeln und Suchkopf.</summary>
    public static void GuidedBomb(DrawingContext dc, Point at, double heading, double u, double altitude)
    {
        double s = u * (0.55 + 0.45 * altitude);
        dc.PushTransform(Fly.Frame(at, heading, s));
        dc.DrawGeometry(KabBody, BombEdge, Fly.Both("M1,1.3 L-2.5,4.2 L-4,4.2 L-3,1.3 Z"));
        dc.DrawGeometry(KabBody, BombEdge, Fly.Both("M-4.5,1.2 L-6.8,3.2 L-7.6,3.2 L-7,1.2 Z"));
        dc.DrawEllipse(KabBody, BombEdge, new Point(0, 0), 5.2, 1.35);
        dc.DrawEllipse(Seeker, null, new Point(4.6, 0), 0.8, 0.8);
        dc.Pop();
    }

    /// <summary>Ungelenkte Rakete (S-8) mit Rauchspur.</summary>
    public static void Rocket(DrawingContext dc, Point from, Point at, double u, double t)
    {
        var dir = at - from; if (dir.LengthSquared < 1e-6) return;
        double len = dir.Length; dir /= len;
        var tail = at - dir * Math.Min(len, 22 * u);
        dc.DrawLine(Scaled(Smoke, u), tail, at);
        dc.DrawLine(Scaled(SmokeCore, u), tail + (at - tail) * 0.5, at);
        Soft.Draw(dc, Flame, at - dir * 1.6 * u, 2.6 * u, 1.6 * u, 0.9);
        dc.PushTransform(Fly.Frame(at, Math.Atan2(dir.Y, dir.X), u * 0.6));
        dc.DrawEllipse(RocketBody, BombEdge, new Point(0, 0), 2.6, 0.7);
        dc.Pop();
    }
    static readonly Pen TracerGlow = Fly.P("#60FFB050", 2.6), TracerCore = Fly.P("#FFFFF0C0", 0.9), VikhrSmoke = Fly.P("#70E8E8E8", 1.6), LaserBeam = Fly.P("#70FF4A3A", 0.5);
    static readonly Brush Dust = Art.Glow("#B0786A58"), Spark = Art.Glow("#F0FFE7A0"), VikhrBody = Fly.B("#C8CEC2");

    /// <summary>30-mm-Leuchtspur: kurzer glühender Strich.</summary>
    public static void Tracer(DrawingContext dc, Point from, Point at, double u)
    {
        var dir = at - from; if (dir.LengthSquared < 1e-6) return;
        double len = dir.Length; dir /= len;
        var tail = at - dir * Math.Min(len, 16 * u);
        dc.DrawLine(Scaled(TracerGlow, u), tail, at);
        dc.DrawLine(Scaled(TracerCore, u), tail + (at - tail) * 0.35, at);
    }

    /// <summary>Einschlag eines Kanonengeschosses: Funke, dann Staubwölkchen.</summary>
    public static void GunHit(DrawingContext dc, Point at, double age, double u)
    {
        if (age < 0.07) Soft.Draw(dc, Spark, at, 4.5 * u, 4.5 * u, 1 - age / 0.07);
        double k = age / 0.5;
        Soft.Draw(dc, Dust, at, (3 + 7 * k) * u, (2.6 + 6 * k) * u, 0.85 * (1 - k));
    }

    /// <summary>Vikhr-Lenkflugkörper mit Kreuzflügeln.</summary>
    public static void Vikhr(DrawingContext dc, Point at, double heading, double u)
    {
        dc.PushTransform(Fly.Frame(at, heading, u * 0.6));
        Soft.Draw(dc, Flame, new Point(-6.4, 0), 3.4, 2, 0.95);
        dc.DrawGeometry(VikhrBody, BombEdge, Fly.Both("M-3,0.8 L-5.2,2.6 L-6,2.6 L-5,0.8 Z"));
        dc.DrawEllipse(VikhrBody, BombEdge, new Point(0, 0), 5, 0.85);
        dc.DrawGeometry(VikhrBody, BombEdge, Fly.Both("M3.4,0.7 L2.4,1.7 L1.8,1.7 L2.2,0.7 Z"));
        dc.Pop();
    }

    /// <summary>Rauchspur der Vikhr, bleibt nach dem Einschlag kurz stehen.</summary>
    public static void VikhrTrail(DrawingContext dc, Point from, Point at, double u, double opacity)
    {
        dc.DrawLine(Soft.FadePen(VikhrSmoke.Brush, VikhrSmoke.Thickness * Math.Max(0.6, u), Math.Clamp(opacity, 0, 1)), from, at);
    }

    /// <summary>Leitstrahl vom Visier zum Ziel (gedämpft, flackernd).</summary>
    public static void Laser(DrawingContext dc, Point from, Point to, double u, double opacity)
    {
        dc.DrawLine(Soft.FadePen(LaserBeam.Brush, LaserBeam.Thickness * Math.Max(0.6, u), Math.Clamp(opacity, 0, 1)), from, to);
    }

    static Pen Scaled(Pen p, double u) { var c = p.Clone(); c.Thickness = p.Thickness * Math.Max(0.6, u); c.Freeze(); return c; }

    static readonly Brush CanopyA = Fly.B("#E4E0CA"), CanopyB = Fly.B("#98A06C"), CanopyVent = Fly.B("#5D6146"), Trooper = Fly.B("#5A683D"),
        Skin = Fly.B("#D9B48C"), Box = Fly.B("#E9ECEF");
    static readonly Pen CanopyEdge = Fly.P("#6C7150", 0.25), Lines = Fly.P("#806C7150", 0.12), BoxEdge = Fly.P("#9AA0A6", 0.4);

    /// <summary>Rundkappenfallschirm von oben: 8 Bahnen, Öffnung in der Mitte; „open“ 0..1 entfaltet, „flat“ 0..1 fällt zusammen.</summary>
    public static void Parachute(DrawingContext dc, Point at, double radius, double open, double flat, double turn)
    {
        if (open <= 0.01) { dc.DrawEllipse(Trooper, null, at, radius * 0.12, radius * 0.12); return; }
        double r = radius * open;
        dc.PushTransform(new TranslateTransform(at.X, at.Y));
        dc.PushTransform(new RotateTransform(turn * 180 / Math.PI));
        dc.PushTransform(new ScaleTransform(1, 1 - 0.8 * flat));
        bool fading = flat > 0.001;
        if (fading) dc.PushOpacity(1 - 0.85 * flat);
        for (int i = 0; i < 8; i++)
        {
            double a0 = i * Math.Tau / 8, a1 = (i + 1) * Math.Tau / 8;
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(new Point(0, 0), true, true);
                c.LineTo(new Point(Math.Cos(a0) * r, Math.Sin(a0) * r), true, false);
                c.ArcTo(new Point(Math.Cos(a1) * r, Math.Sin(a1) * r), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
            }
            g.Freeze();
            dc.DrawGeometry(i % 2 == 0 ? CanopyA : CanopyB, CanopyEdge, g);
        }
        dc.DrawEllipse(CanopyVent, null, new Point(0, 0), r * 0.12, r * 0.12);
        if (fading) dc.Pop();
        dc.Pop(); dc.Pop(); dc.Pop();
    }

    /// <summary>Fallschirmjäger von oben, trägt das Symbol über dem Kopf weg.</summary>
    public static void Carrier(DrawingContext dc, Point at, double heading, double size, double step, double opacity)
    {
        bool fading = opacity < 0.999;
        if (fading) dc.PushOpacity(opacity);
        dc.PushTransform(Fly.Frame(at, heading, size / 10));
        double swing = Math.Sin(step) * 1.2;
        dc.DrawEllipse(Trooper, null, new Point(-1 + swing, -1.8), 1.4, 0.9);      // Beine im Schritt
        dc.DrawEllipse(Trooper, null, new Point(-1 - swing, 1.8), 1.4, 0.9);
        dc.DrawEllipse(Trooper, CanopyEdge, new Point(0, 0), 2.2, 3.4);            // Schultern
        dc.DrawEllipse(Skin, null, new Point(0.4, 0), 1.3, 1.3);                   // Kopf
        dc.DrawRoundedRectangle(Box, BoxEdge, new Rect(1.2, -3.6, 7.2, 7.2), 1.6, 1.6);   // getragenes Symbol
        dc.Pop();
        if (fading) dc.Pop();
    }
}

// ================================================================ Angriffe

/// <summary>Zwei Flugzeuge im Verband, je eine Reihe: Tu-22M3 legt einen Bombenteppich, Su-34 wirft gelenkte Gleitbomben genau auf jedes Symbol.</summary>
sealed class RowRaid : IAttack
{
    public double Reach { get; init; } = double.PositiveInfinity;
    public enum Kind { Carpet, Strike }
    readonly Kind kind;
    readonly Rect stage;
    readonly double span, speed, fall, gap, startX;
    readonly double enter = 0.3;
    readonly double[] rows;
    readonly double[] climbFrom = [double.MaxValue, double.MaxValue];
    readonly double[] sweepFrom = [double.MaxValue, double.MaxValue];
    readonly List<Drop> drops = [];
    record Drop(int Plane, Point From, Point To, double Release, double Impact);

    public IReadOnlyList<(Point P, double T)> Blasts { get; }
    public double LastImpact { get; }
    public double End { get; }
    public bool Explodes => true;

    public RowRaid(Kind kind, Rect stage, IReadOnlyList<Point> targets, double iconSize)
    {
        this.kind = kind; this.stage = stage;
        bool carpet = kind == Kind.Carpet;
        span = carpet ? Math.Clamp(stage.Width * 0.075, iconSize * 2.0, iconSize * 3.6) : Math.Clamp(stage.Width * 0.045, iconSize * 1.3, iconSize * 2.2);
        speed = stage.Width * (carpet ? 0.2 : 0.4);
        fall = carpet ? 1.25 : 0.9;
        gap = carpet ? 0.45 : 0.7;
        startX = stage.Left - span;
        // Zwei Bahnen dort, wo die Symbole liegen (obere und untere Hälfte nach Höhe), nicht auf festen Höhen des Hauptbildschirms
        var heights = targets.Select(p => p.Y).OrderBy(y => y).ToList();
        if (heights.Count >= 2)
        {
            double upper = heights.Take(heights.Count / 2).Average(), lower = heights.Skip(heights.Count / 2).Average();
            if (lower - upper < iconSize * 2.5) { double mid = (upper + lower) / 2; upper = mid - iconSize * 1.25; lower = mid + iconSize * 1.25; }
            rows = [upper - iconSize * 0.6, lower - iconSize * 0.6];
        }
        else rows = carpet ? [stage.Top + stage.Height * 0.3, stage.Top + stage.Height * 0.68] : [stage.Top + stage.Height * 0.24, stage.Top + stage.Height * 0.62];
        var random = new Random(carpet ? 2203 : 3400);
        var byRow = new[] { new List<Point>(), new List<Point>() };
        foreach (var p in targets) byRow[Math.Abs(p.Y - rows[0]) <= Math.Abs(p.Y - rows[1]) ? 0 : 1].Add(p);
        var lastRelease = new double[2];
        for (int k = 0; k < 2; k++)
        {
            var list = byRow[k].OrderBy(p => p.X).ToList();
            var aims = new List<Point>();
            for (int i = 0; i < list.Count; i++)
            {
                aims.Add(list[i]);
                // Teppich: eine zweite Bombe dicht am selben Symbol statt Bomben in die leeren Lücken 
                if (carpet) aims.Add(list[i] + new Vector((random.NextDouble() - 0.5) * iconSize * 0.7, (random.NextDouble() - 0.5) * iconSize * 0.7));
            }
            foreach (var p in aims.OrderBy(p => p.X))
            {
                double first = enter + k * gap + 0.15;
                double rt = Math.Max(first, enter + k * gap + (p.X - speed * fall * 0.5 - startX) / speed);
                var from = PlaneAt(k, rt) + new Vector(-span * 0.12, (random.NextDouble() - 0.5) * span * 0.14);
                drops.Add(new Drop(k, from, p, rt, rt + fall));
                lastRelease[k] = Math.Max(lastRelease[k], rt);
            }
        }
        if (!carpet) for (int k = 0; k < 2; k++) climbFrom[k] = lastRelease[k] + 0.25;   // Su-34 zieht nach dem letzten Abwurf hoch
        else for (int k = 0; k < 2; k++) sweepFrom[k] = lastRelease[k] + 0.3;            // Tu-22M3 schwenkt die Flügel zurück
        Blasts = drops.Select(d => (d.To, d.Impact)).ToList();
        LastImpact = drops.Count > 0 ? drops.Max(d => d.Impact) : enter + 2;
        End = Math.Max(LastImpact, enter + gap + (stage.Right + span * 1.5 - startX) / speed);
    }

    Point PlaneAt(int k, double t)
    {
        double x = startX + speed * (t - enter - k * gap);
        // Tu-22M3: nach dem Zurückschwenken Nachbrenner und Beschleunigung auf gut das Dreifache 
        double a = t - sweepFrom[k] - 0.9;
        if (a > 0) x += speed * (a * a * 0.9 / (1 + a * 0.45));
        double y = rows[k] + Math.Sin(x / stage.Width * 3 + k * 1.7) * stage.Height * 0.012;
        double c = t - climbFrom[k];
        if (c > 0) y -= c * c * stage.Height * 0.12;
        return new Point(x, y);
    }
    double HeadingAt(int k, double t) { var a = PlaneAt(k, t - 0.02); var b = PlaneAt(k, t + 0.02); return Math.Atan2(b.Y - a.Y, b.X - a.X); }

    public void Draw(DrawingContext dc, double t)
    {
        double u = span / 100;
        foreach (var d in drops)
        {
            if (t < d.Release || t >= d.Impact) continue;
            double k = (t - d.Release) / fall;
            var pos = d.From + (d.To - d.From) * k;
            double heading = Math.Atan2(d.To.Y - d.From.Y, d.To.X - d.From.X);
            if (kind == Kind.Carpet) Planes.Bomb(dc, pos, heading, u * 1.4, 1 - k);
            else Planes.GuidedBomb(dc, pos, heading, u * 1.6, 1 - k);
        }
        for (int k = 0; k < 2; k++)
        {
            if (t < enter + k * gap) continue;
            var p = PlaneAt(k, t);
            if (p.X > stage.Right + span * 1.5 || p.Y < stage.Top - span * 2) continue;
            if (kind == Kind.Carpet) Planes.Tu22(dc, p, HeadingAt(k, t), u, 1, 30 + 35 * Fly.Smooth((t - sweepFrom[k]) / 1.6), Fly.Smooth((t - sweepFrom[k] - 0.7) / 0.5), t);
            else Planes.Su34(dc, p, HeadingAt(k, t), u, 0.45, t);
        }
    }
}

/// <summary>Ka-52 (19.11.2026: „alle Verknüpfungen einzeln angreifen, je nach Bewaffnung“): fliegt ein, schwebt an einer oder mehreren
/// Stellen, dreht sich zu jedem Symbol einzeln und wählt die Waffe nach Entfernung und Vorrat:
/// nah die 30-mm-Kanone 2A42 (Feuerstoß mit Leuchtspur, die Treffer wandern auf das Symbol), mittel eine S-8-Zweiersalve aus den inneren
/// Behältern B-8V20, weit eine Vikhr aus den äußeren Startern (Leitstrahl: der Hubschrauber hält das Ziel bis zum Einschlag).
/// Jedes Symbol brennt erst, wenn es selbst getroffen ist (Reach), nicht schon durch die Druckwelle des ersten Treffers.</summary>
sealed class HelicopterRaid : IAttack
{
    public enum Weapon { Gun, Rockets, Vikhr }
    internal record Engage(Point Target, Weapon Weapon, double Aim, double Start, double Done, double Hit, Point Hover);
    record Leg(Point Hover, double From, double To);
    record Round(Point From, Point To, double Fire, double Impact, Weapon Weapon);

    const double Enter = 0.3, FlyIn = 2.4, FlyOut = 2.4;
    const int Vikhrs = 12, Salvos = 20;                // 2 × 6 Vikhr, 2 × 20 S-8 (je Ziel zwei)
    readonly Rect stage;
    readonly double span, u, arrive, leave, gone, inHeading, outHeading;
    readonly Point entry, exit;
    readonly List<Leg> legs = [];
    readonly List<Engage> engages = [];
    readonly List<Round> rounds = [];
    readonly List<(double T, double H)> keys = [];     // Blickrichtung über die Zeit
    readonly List<(Point P, double T)> blasts = [];
    readonly List<double> sizes = [];

    public IReadOnlyList<(Point P, double T)> Blasts => blasts;
    public IReadOnlyList<double>? BlastSizes => sizes;
    public double Reach { get; }
    public double LastImpact { get; }
    public double End { get; }
    public bool Explodes => true;
    internal IReadOnlyList<Engage> Plan => engages;

    record Group(Point Hover, int Screen, List<Point> Targets);

    /// <param name="stage">Hauptbildschirm (Einflug von rechts)</param>
    /// <param name="screens">alle Bildschirme: Symbole auf den anderen werden ebenfalls einzeln angeflogen</param>
    public HelicopterRaid(Rect stage, IReadOnlyList<Point> targets, double iconSize, IReadOnlyList<Rect>? screens = null)
    {
        this.stage = stage;
        span = Math.Clamp(stage.Width * 0.06, iconSize * 2.0, iconSize * 3.0);
        u = span / 100;
        Reach = iconSize * 0.45;
        double h = stage.Height;
        var list = targets.Count > 0 ? targets.ToList() : [new Point(stage.Left + stage.Width / 2, stage.Top + h / 2)];
        var areas = new List<Rect> { stage };
        foreach (var a in screens ?? []) if (a != stage && a.Width > 0) areas.Add(a);
        int ScreenOf(Point p)
        {
            int i = areas.FindIndex(a => a.Contains(p));
            return i >= 0 ? i : Enumerable.Range(0, areas.Count).MinBy(j => (new Point(areas[j].Left + areas[j].Width / 2, areas[j].Top + areas[j].Height / 2) - p).LengthSquared);
        }

        // Schwebepunkte je Bildschirm: auf breiten zwei oder drei
        var spots = new List<(Point P, int Screen)>();
        for (int s = 0; s < areas.Count; s++)
        {
            var a = areas[s];
            int k = Math.Clamp((int)Math.Round(a.Width / (a.Height * 1.25)), 1, 3);
            for (int j = 0; j < k; j++) spots.Add((new Point(a.Left + a.Width * (k - j - 0.5) / k, a.Top + a.Height * 0.45), s));
        }
        var pending = list.GroupBy(p => { int s = ScreenOf(p); return Enumerable.Range(0, spots.Count).Where(i => spots[i].Screen == s).MinBy(i => Math.Abs(spots[i].P.X - p.X)); })
            .Select(g => new Group(new Point((spots[g.Key].P.X + g.Average(p => p.X)) / 2, spots[g.Key].P.Y), spots[g.Key].Screen, g.ToList())).ToList();
        // Reihenfolge: Hauptbildschirm von rechts nach links, dann jeweils der nächste Schwebepunkt (eigener Bildschirm zuerst)
        var route = new List<Group>();
        var onMain = pending.Where(g => g.Screen == 0).ToList();
        var current = (onMain.Count > 0 ? onMain : pending).MaxBy(g => g.Hover.X)!;
        while (true)
        {
            route.Add(current); pending.Remove(current);
            if (pending.Count == 0) break;
            var here = current;
            var same = pending.Where(g => g.Screen == here.Screen).ToList();
            current = (same.Count > 0 ? same : pending).MinBy(g => (g.Hover - here.Hover).LengthSquared)!;
        }

        entry = new Point(stage.Right + span * 1.2, stage.Top + h * 0.16);
        var lastArea = areas[route[^1].Screen];
        var lastHover = route[^1].Hover;
        exit = new Point(lastHover.X - lastArea.Left < lastArea.Right - lastHover.X ? lastArea.Left - span * 1.8 : lastArea.Right + span * 1.8, lastArea.Top - span);
        inHeading = Math.Atan2(route[0].Hover.Y - entry.Y, route[0].Hover.X - entry.X);
        arrive = Enter + FlyIn;
        Key(Enter, inHeading); Key(arrive - 0.4, inHeading);

        int vikhr = Vikhrs, salvos = Salvos, n = 0;
        double t = arrive, heading = inHeading;
        Point? previous = null;
        foreach (var group in route)
        {
            var hover = group.Hover;
            if (previous is { } from)
            {   // Umsetzen zum nächsten Schwebepunkt (auch auf einen anderen Bildschirm), Nase in Flugrichtung
                double travel = Math.Atan2(hover.Y - from.Y, hover.X - from.X);
                double move = Math.Clamp(0.9 + (hover - from).Length / (h * 1.1), 1.2, 3.2);
                Key(t + 0.35, travel); Key(t + move - 0.35, travel);
                t += move; heading = travel;
            }
            double legFrom = t;
            double Around(Point p)
            {
                double a = Math.Atan2(p.Y - hover.Y, p.X - hover.X) - heading;
                while (a < 0) a += Math.Tau;
                while (a >= Math.Tau) a -= Math.Tau;
                return a;
            }
            // Waffe nach Entfernung vom Schwebepunkt: nächstes Drittel Kanone, mittleres S-8, fernstes Vikhr (solange Vorrat da ist)
            var ranked = group.Targets.OrderBy(p => (p - hover).LengthSquared).ToList();
            foreach (var target in group.Targets.OrderBy(Around))
            {
                var d = target - hover;
                double aim = Math.Atan2(d.Y, d.X);
                double rank = ranked.Count == 1 ? 0.5 : ranked.IndexOf(target) / (double)(ranked.Count - 1);
                double turn = Math.Abs(Fly.Turn(heading, aim, 1) - heading);
                double start = t + 0.1 + 0.2 * turn / Math.PI;    // auf das Ziel drehen
                var weapon = rank > 0.67 && vikhr > 0 ? Weapon.Vikhr : rank >= 0.34 && salvos > 0 ? Weapon.Rockets : Weapon.Gun;
                var side = new Vector(-Math.Sin(aim), Math.Cos(aim));   // rechts der Blickrichtung
                double hit, done;
                switch (weapon)
                {
                    case Weapon.Gun:
                    {   // Feuerstoß: 6 Schuss, Treffer wandern von vorn auf das Symbol
                        var muzzle = hover + Fly.Rot(Planes.KaMuzzle * u, aim);
                        var back = d / d.Length;
                        double speed = h * 3.0;
                        for (int i = 0; i < 6; i++)
                        {
                            double jitter = Math.Sin(n * 12.9898 + i * 78.233) * 0.18 * iconSize;
                            var to = target - back * (iconSize * (0.8 - 0.16 * i)) + side * jitter;
                            double fire = start + i * 0.055;
                            rounds.Add(new Round(muzzle, to, fire, fire + (to - muzzle).Length / speed, weapon));
                        }
                        hit = rounds[^3].Impact;
                        blasts.Add((target, hit)); sizes.Add(0.6);
                        done = start + 6 * 0.055 + 0.04;
                        break;
                    }
                    case Weapon.Rockets:
                    {   // Zweiersalve aus den inneren Behältern, rechts zuerst
                        salvos--;
                        hit = double.MaxValue;
                        for (int i = 0; i < 2; i++)
                        {
                            double s = i == 0 ? 1 : -1;
                            var launch = hover + Fly.Rot(new Vector(Planes.KaRockets.X, Planes.KaRockets.Y * s) * u, aim);
                            var to = target + side * (i == 0 ? 0.15 * iconSize : 0);
                            double fire = start + i * 0.12, impact = fire + 0.22 + (to - launch).Length / (h * 1.7);
                            rounds.Add(new Round(launch, to, fire, impact, weapon));
                            blasts.Add((to, impact)); sizes.Add(i == 0 ? 0.8 : 0.9);
                            hit = Math.Min(hit, impact);
                        }
                        done = start + 0.22;
                        break;
                    }
                    default:
                    {   // Vikhr vom äußeren Starter, abwechselnd links und rechts; Leitstrahl bis zum Einschlag
                        double s = vikhr % 2 == 0 ? 1 : -1;
                        vikhr--;
                        var launch = hover + Fly.Rot(new Vector(Planes.KaVikhr.X, Planes.KaVikhr.Y * s) * u, aim);
                        double impact = start + 0.35 + (target - launch).Length / (h * 1.6);
                        rounds.Add(new Round(launch, target, start, impact, weapon));
                        blasts.Add((target, impact)); sizes.Add(1.15);
                        hit = impact; done = impact + 0.05;
                        break;
                    }
                }
                engages.Add(new Engage(target, weapon, aim, start, done, hit, hover));
                Key(start, aim); Key(done, aim);
                heading = aim; t = done; n++;
            }
            legs.Add(new Leg(hover, legFrom, t));
            previous = hover;
        }
        leave = t + 0.3;
        outHeading = Math.Atan2(exit.Y - lastHover.Y, exit.X - lastHover.X);
        gone = leave + FlyOut;
        Key(leave + 0.5, outHeading); Key(gone, outHeading);
        LastImpact = blasts.Max(b => b.T);
        End = Math.Max(LastImpact + 0.5, gone);
    }

    void Key(double t, double heading)
    {
        if (keys.Count > 0 && t <= keys[^1].T) t = keys[^1].T + 0.01;
        keys.Add((t, heading));
    }

    double HeadingAt(double t)
    {
        if (t <= keys[0].T) return keys[0].H;
        for (int i = 1; i < keys.Count; i++)
            if (t < keys[i].T) return Fly.Turn(keys[i - 1].H, keys[i].H, Fly.Smooth((t - keys[i - 1].T) / (keys[i].T - keys[i - 1].T)));
        return keys[^1].H;
    }

    (Point Pos, double Heading, double Altitude) Pose(double t)
    {
        var bob = new Vector(0, Math.Sin(t * 2.3) * span * 0.012);
        double heading = HeadingAt(t);
        if (t < arrive)
        {
            double k = Fly.EaseOut((t - Enter) / FlyIn);
            var first = legs[0].Hover;
            var control = new Point((entry.X + first.X) / 2, Math.Min(entry.Y, first.Y) - stage.Height * 0.06);
            return (Fly.Bez(entry, control, first, k) + bob, heading, 0.5 - 0.15 * k);
        }
        if (t >= leave)
        {
            double q = Fly.EaseIn((t - leave) / FlyOut);
            var last = legs[^1].Hover;
            return (last + (exit - last) * q + bob, heading, 0.35 + 0.3 * q);
        }
        for (int i = 1; i < legs.Count; i++)
            if (t >= legs[i - 1].To && t < legs[i].From)
            {
                double k = Fly.Smooth((t - legs[i - 1].To) / (legs[i].From - legs[i - 1].To));
                return (legs[i - 1].Hover + (legs[i].Hover - legs[i - 1].Hover) * k + bob, heading, 0.35 + 0.08 * Math.Sin(k * Math.PI));
            }
        var leg = legs.LastOrDefault(l => t >= l.From) ?? legs[0];
        return (leg.Hover + bob, heading, 0.35);
    }

    public void Draw(DrawingContext dc, double t)
    {
        var (pos, heading, altitude) = Pose(t);
        foreach (var r in rounds)
        {
            double age = t - r.Fire;
            if (age < 0) continue;
            double flight = r.Impact - r.Fire, k = Math.Clamp(age / flight, 0, 1);
            switch (r.Weapon)
            {
                case Weapon.Gun:
                    if (k < 1) Planes.Tracer(dc, r.From, r.From + (r.To - r.From) * k, u);
                    else if (t - r.Impact < 0.5) Planes.GunHit(dc, r.To, t - r.Impact, u);
                    break;
                case Weapon.Rockets:
                    if (k < 1) Planes.Rocket(dc, r.From, r.From + (r.To - r.From) * k, u * 1.2, t);
                    break;
                default:
                    double eased = Math.Pow(k, 1.35);   // Startmotor: erst langsam, dann schnell
                    var at = r.From + (r.To - r.From) * eased;
                    double fade = k < 1 ? 1 : 1 - (t - r.Impact) / 0.9;
                    if (fade > 0) Planes.VikhrTrail(dc, r.From, at, u, fade);
                    if (k < 1)
                    {
                        Planes.Laser(dc, pos + Fly.Rot(Planes.KaSensor * u, heading), r.To, u, 0.55 + 0.3 * Math.Sin(t * 41));
                        Planes.Vikhr(dc, at, Math.Atan2(r.To.Y - r.From.Y, r.To.X - r.From.X), u);
                    }
                    break;
            }
        }
        if (t < Enter || t > gone) return;
        Planes.Ka52(dc, pos, heading, u, altitude, t);
        foreach (var r in rounds)   // Mündungsfeuer: Kanone kurz, Raketen und Vikhr größer
        {
            double age = t - r.Fire, life = r.Weapon == Weapon.Gun ? 0.045 : r.Weapon == Weapon.Rockets ? 0.09 : 0.15;
            if (age < 0 || age >= life) continue;
            double size = r.Weapon == Weapon.Gun ? 3.2 : r.Weapon == Weapon.Rockets ? 6 : 8;
            var dir = r.To - r.From; dir.Normalize();
            Soft.Draw(dc, MuzzleFlash, r.From + dir * 4 * u, size * u, size * 0.7 * u, 1 - age / life);
        }
    }
    static readonly Brush MuzzleFlash = Art.Glow("#F8FFE4A0");
}

/// <summary>Tu-95MS: hoch und langsam, startet Marschflugkörper Ch-101 von den Flügelstationen; gegenläufige Propeller drehen sich.</summary>
sealed class BearRaid : IAttack
{
    public double Reach { get; init; } = double.PositiveInfinity;
    readonly Rect stage;
    readonly double span, speed, startX;
    readonly double enter = 0.3;
    readonly Missile[] missiles;
    record Missile(Point Drop, Vector V0, Point Target, double Release, double Ignite, double Impact, double Seed);

    public IReadOnlyList<(Point P, double T)> Blasts { get; }
    public double LastImpact { get; }
    public double End { get; }
    public bool Explodes => true;

    public BearRaid(Rect stage, IReadOnlyList<Point> targets, double iconSize)
    {
        this.stage = stage;
        span = Math.Clamp(stage.Width * 0.07, iconSize * 2.0, iconSize * 3.4);
        speed = stage.Width * 0.12;
        startX = stage.Left - span;
        double u = span / 100;
        var random = new Random(95);
        missiles = new Missile[targets.Count];
        for (int i = 0; i < missiles.Length; i++)
        {
            double rt = enter + 0.9 + i * 0.32;
            var pos = PlaneAt(rt);
            double side = i % 2 == 0 ? 1 : -1;
            var station = Planes.BearStations[(i / 2) % 2];
            var drop = pos + new Vector(station.X * u, side * station.Y * u);
            double ignite = rt + 0.5;
            double flight = 1.3 + (targets[i] - drop).Length / Math.Max(1, stage.Width * 0.5);
            missiles[i] = new Missile(drop, new Vector(speed, 0), targets[i], rt, ignite, ignite + flight, random.NextDouble());
        }
        Blasts = missiles.Select(m => (m.Target, m.Impact)).ToList();
        LastImpact = missiles.Length > 0 ? missiles.Max(m => m.Impact) : enter + 3;
        End = Math.Max(LastImpact, enter + (stage.Right + span * 1.3 - startX) / speed);
    }

    Point PlaneAt(double t) { double x = startX + speed * (t - enter); return new Point(x, stage.Top + stage.Height * 0.15 + Math.Sin(x / stage.Width * 2) * stage.Height * 0.015); }

    // wie bei der Tu-160: kurz fallen, zünden, weiter Bogen ins Ziel
    (Point Pos, Vector Dir, double Fold, bool Burning, double Altitude) MissileAt(Missile m, double t)
    {
        if (t < m.Ignite)
        {
            double k = t - m.Release;
            var dir0 = m.V0; dir0.Normalize();
            return (m.Drop + m.V0 * k * 0.85, dir0, 0, false, 1 - 0.35 * k);
        }
        double d = m.Impact - m.Ignite, u = Math.Clamp((t - m.Ignite) / d, 0, 1);
        var start = m.Drop + m.V0 * (m.Ignite - m.Release) * 0.85;
        var v0 = m.V0 * 0.3 * d;
        var aim = m.Target - start; aim.Normalize();
        var v1 = (aim * 0.7 + new Vector(0, 0.3)) * (m.Target - start).Length * 1.2;
        double u2 = u * u, u3 = u2 * u;
        var pos = (Vector)start * (2 * u3 - 3 * u2 + 1) + v0 * (u3 - 2 * u2 + u) + (Vector)m.Target * (-2 * u3 + 3 * u2) + v1 * (u3 - u2);
        var vel = (Vector)start * (6 * u2 - 6 * u) + v0 * (3 * u2 - 4 * u + 1) + (Vector)m.Target * (-6 * u2 + 6 * u) + v1 * (3 * u2 - 2 * u);
        if (vel.LengthSquared < 1e-6) vel = aim;
        vel.Normalize();
        return ((Point)pos, vel, Fly.Smooth((t - m.Ignite) / 0.35), true, 0.65 * (1 - u));
    }

    public void Draw(DrawingContext dc, double t)
    {
        double u = span / 100;
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
        foreach (var m in missiles)
        {
            if (t < m.Release || t >= m.Impact) continue;
            var (pos, dir, fold, burning, alt) = MissileAt(m, t);
            Art.Missile(dc, pos, dir, fold, burning, alt, u * 0.9, t + m.Seed);
        }
        var plane = PlaneAt(t);
        if (t >= enter && plane.X < stage.Right + span * 1.3)
        {
            var a = PlaneAt(t - 0.02); var b = PlaneAt(t + 0.02);
            Planes.Tu95(dc, plane, Math.Atan2(b.Y - a.Y, b.X - a.X), u, 1, t);
        }
    }
    static readonly Pen TrailWide = Fly.P("#28E6E6EB", 6), TrailCore = Fly.P("#6EF5F5FA", 1.6);
}

/// <summary>Il-76: setzt über den Symbolen Fallschirmjäger ab; jeder landet auf einem Symbol und trägt es zum Bildschirmrand weg.</summary>
sealed class AirborneRaid : IAttack
{
    readonly Rect stage;
    readonly double span, speed, startX, iconSize;
    readonly double enter = 0.3;
    readonly List<Jumper> jumpers = [];
    record Jumper(Point Exit, Point Land, double Jump, double Touch, Vector Away, double Turn);
    const double Freefall = 0.35, Descent = 2.4, Carry = 2.2;

    public IReadOnlyList<(Point P, double T)> Blasts { get; }
    public double LastImpact { get; }
    public double End { get; }
    public bool Explodes => false;

    public AirborneRaid(Rect stage, IReadOnlyList<Point> targets, double iconSize)
    {
        this.stage = stage; this.iconSize = iconSize;
        span = Math.Clamp(stage.Width * 0.075, iconSize * 2.0, iconSize * 3.6);
        speed = stage.Width * 0.14;
        startX = stage.Left - span;
        var random = new Random(76);
        double last = 0;
        foreach (var p in targets.OrderBy(p => p.X))
        {
            double drift = speed * Descent * 0.3;
            double jump = Math.Max(Math.Max(enter + 0.5, last + 0.12), enter + (p.X - drift + span * 0.45 - startX) / speed);
            last = jump;
            var exit = PlaneAt(jump) + new Vector(-span * 0.46, (random.NextDouble() - 0.5) * span * 0.05);
            // weg zum nächsten Rand (links, rechts oder unten)
            double dl = p.X - stage.Left, dr = stage.Right - p.X, db = stage.Bottom - p.Y;
            var away = dl <= dr && dl <= db ? new Vector(-1, 0) : dr <= db ? new Vector(1, 0) : new Vector(0, 1);
            jumpers.Add(new Jumper(exit, p, jump, jump + Freefall + Descent, away * iconSize * 2.4, random.NextDouble() * Math.Tau));
        }
        Blasts = jumpers.Select(j => (j.Land, j.Touch)).ToList();
        LastImpact = jumpers.Count > 0 ? jumpers.Max(j => j.Touch) : enter + 3;
        End = Math.Max(LastImpact + Carry, enter + (stage.Right + span * 1.3 - startX) / speed);
    }

    Point PlaneAt(double t) { double x = startX + speed * (t - enter); return new Point(x, stage.Top + stage.Height * 0.2 + Math.Sin(x / stage.Width * 2.4) * stage.Height * 0.012); }

    public void Draw(DrawingContext dc, double t)
    {
        double u = span / 100, radius = Math.Max(span * 0.06, iconSize * 0.45);   // sichtbar, nicht maßstabsgetreu winzig
        foreach (var j in jumpers)
        {
            if (t < j.Jump) continue;
            if (t < j.Touch)
            {
                double fallen = t - j.Jump;
                double s = Math.Clamp((fallen - Freefall) / Descent, 0, 1);
                var start = j.Exit + new Vector(speed * 0.25 * Math.Min(fallen, Freefall), 0);
                var pos = start + (j.Land - start) * Fly.EaseOut(s);
                double open = Fly.Smooth((fallen - Freefall) / 0.35), alt = 1 - s;
                double r = radius * (0.6 + 0.4 * alt);
                dc.PushTransform(new TranslateTransform(alt * 100 * u * 0.16, alt * 100 * u * 0.26));
                dc.DrawEllipse(Fly.Shadow, null, pos, r * open, r * open);   // Schatten des Schirms
                dc.Pop();
                Planes.Parachute(dc, pos, r, open, 0, j.Turn + t * 0.4);
                continue;
            }
            double after = t - j.Touch;
            if (after < 0.7) Planes.Parachute(dc, j.Land + new Vector(after * iconSize * 0.4, 0), radius * 0.6, 1, Math.Clamp(after / 0.7, 0, 1), j.Turn);
            if (after < 0.25 || after > Carry) continue;
            double walk = after - 0.25;
            var at = j.Land + j.Away * walk;
            double fade = Math.Clamp((Carry - after) / 0.4, 0, 1);
            Planes.Carrier(dc, at, Math.Atan2(j.Away.Y, j.Away.X), iconSize * 0.7, walk * 9, fade);
        }
        var plane = PlaneAt(t);
        if (t >= enter && plane.X < stage.Right + span * 1.3)
        {
            bool ramp = jumpers.Any(j => t > j.Jump - 0.8 && t < j.Jump + 0.6);
            Planes.Il76(dc, plane, 0, u, 1, ramp);
        }
    }
}

// ================================================================ AC-130U „Spooky“

static partial class Planes
{
    // ---------------------------------------------------------------- AC-130U (C-130H-Zelle: Länge 29,8 m, Spannweite 40,4 m, 4 × T56 mit Vierblattpropellern)
    // Maße aus einer Dreiseitenansicht ausgemessen (19.11.2026: „1:1 wie in der Realität“): Schulterflügel mit gerader Vorderkante bei +12,
    // Triebwerke bei 17,4 und 35,1, Außentanks bei 26,5, Höhenleitwerk 16 m. Bewaffnung wie beim Original auf der linken Rumpfseite:
    // 25-mm-GAU-12/U (fünf Läufe) vorn, 40-mm-Bofors L/60 und 105-mm-Haubitze M102 hinter dem Flügel; Sensortürme links an der Nase.
    static readonly Geometry HercBody = Fly.G("M50,0 C50,-0.9 49.6,-1.6 49,-1.9 L48,-2.4 L46,-3.3 L44,-4.4 L40,-6.1 L35,-7.3 L30,-7.5 L-22,-7.5 L-25,-7.3 L-30,-6.4 L-35,-5.6 L-40,-4.4 L-45,-2.8 L-48,-1.6 L-50,-0.7 L-50,0.7 " +
        "L-48,1.6 L-45,2.8 L-40,4.4 L-35,5.6 L-30,6.4 L-25,7.3 L-22,7.5 L30,7.5 L35,7.3 L40,6.1 L44,4.4 L46,3.3 L48,2.4 L49,1.9 C49.6,1.6 50,0.9 50,0 Z");
    static readonly Geometry HercWing = Fly.G(Full([(12, 0), (12, 7.5), (11.7, 22), (11.2, 38), (10.9, 54), (10.6, 62), (10.5, 66), (9.7, 68.2), (6.9, 68.7), (3, 67.8), (1.9, 65), (1.3, 62),
        (0.4, 54), (-0.6, 46), (-1.6, 38), (-2.5, 30), (-3.5, 22), (-3.8, 7.5), (-3.8, 0)]));
    static readonly Geometry HercTail = Fly.Both("M-30,0 L-31.5,7 L-36.5,26.4 C-36.8,27.2 -37.6,27.4 -38.4,27.4 L-41.2,27.4 C-42,27.4 -42.6,27 -42.6,26.2 L-44.4,7 L-45,0 Z");
    static readonly Geometry HercSponsons = Fly.Both("M6,7.3 C4,8.7 -9,8.9 -14,7.3 Z");   // Fahrwerksgondeln seitlich am Unterrumpf
    static readonly Geometry HercFin = Fly.G("M-18,0 C-26,-0.8 -36,-1.3 -42,-1.2 L-49,-0.6 L-49,0.6 L-42,1.2 C-36,1.3 -26,0.8 -18,0 Z");
    static readonly Geometry HercGlass = Fly.G("M46.9,-0.3 L46.6,-2.2 L45.6,-2.4 L45.8,-0.3 Z M46.9,0.3 L46.6,2.2 L45.6,2.4 L45.8,0.3 Z " +   // Frontscheiben
        "M45.4,-2.6 L44.2,-3.9 L43.2,-3.9 L44.6,-2.6 Z M45.4,2.6 L44.2,3.9 L43.2,3.9 L44.6,2.6 Z " +                                           // Seitenfenster
        "M44.8,-1.9 L43.4,-1.9 L43.4,-0.5 L44.8,-0.5 Z M44.8,1.9 L43.4,1.9 L43.4,0.5 L44.8,0.5 Z");                                         // Dachfenster
    static readonly Geometry HercRadome = Fly.G("M50,0 C50,-0.9 49.6,-1.6 49,-1.9 L48,-2.4 L47.4,-2.7 L47.4,2.7 L48,2.4 L49,1.9 C49.6,1.6 50,0.9 50,0 Z");
    static readonly Geometry HercFrames = Fly.G("M40,-6.1 L40,6.1 M30,-7.5 L30,7.5 M20,-7.5 L20,7.5 M-12,-7.5 L-12,7.5 M-22,-7.5 L-22,7.5 M-30,-6.4 L-30,6.4 " +
        "M12,-7.5 L-3.8,-7.5 M12,7.5 L-3.8,7.5 M-26,-6.6 L-46,-2.4 M-26,6.6 L-46,2.4");   // Spanten, Flügelwurzel, Laderampe
    static readonly Geometry HercHatches = Fly.G("M38,-1.6 L35,-1.6 L35,1.6 L38,1.6 Z M16,-1.4 L13.4,-1.4 L13.4,1.4 L16,1.4 Z M-8,-1.4 L-10.6,-1.4 L-10.6,1.4 L-8,1.4 Z");
    static readonly Geometry HercControls = Fly.G("M-1.6,9 L1.3,60 M-1.6,-9 L1.3,-60 M-38.6,6 L-40.2,26 M-38.6,-6 L-40.2,-26 M-44,0 L-49,0");   // Klappen/Querruder, Höhenruder, Seitenruder
    static readonly Brush NavRed = Art.Glow("#E0FF3030"), NavGreen = Art.Glow("#E040FF80"), Beacon = Art.Glow("#D0FF3020"), Exhaust = Fly.B("#24282C");
    static readonly Pen HfWire = Fly.P("#40262A2E", 0.2), Antenna = Fly.P("#262A2E", 0.6);
    static readonly Brush GunLight = Fly.B("#646C74"), GunShade = Fly.B("#565E66"), GunDark = Fly.B("#2E3338"), GunMetal = Fly.B("#1E2226");
    static readonly Pen GunEdge = Fly.P("#33393F", 0.35), GunSeam = Fly.P("#4A5158", 0.25), GunBarrel = Fly.P("#1E2226", 0.9), GunBarrelThin = Fly.P("#1E2226", 0.45);
    static readonly double[] HercEngines = [17.4, 35.1];
    /// <summary>Mündungen auf der linken Seite (v negativ): 25 mm vorn, 40 mm und 105 mm hinter dem Flügel.</summary>
    internal static readonly Vector Gun25 = new(23, -10.4), Gun40 = new(-10, -12.8), Gun105 = new(-17, -13.9);

    /// <summary>Waffe in ihrem Lafettensystem zeichnen: Ursprung an der Pforte, +y zeigt quer nach links hinaus, gedreht um aim.</summary>
    static void Turned(DrawingContext dc, Point mount, double aim, Action draw)
    {
        dc.PushTransform(new TranslateTransform(mount.X, mount.Y));
        dc.PushTransform(new RotateTransform(180 + aim * 180 / Math.PI));   // +y lokal → −v (links)
        draw();
        dc.Pop(); dc.Pop();
    }

    static string Full((double U, double V)[] half)
    {
        var pts = half.Concat(half.Reverse().Where(p => p.V != 0).Select(p => (U: p.U, V: -p.V)));
        return "M" + string.Join(" L", pts.Select(p => FormattableString.Invariant($"{p.U},{p.V}"))) + " Z";
    }

    /// <param name="aim">Schwenk der drei Waffen gegenüber quer links (Bogenmaß): 25 mm, 40 mm, 105 mm</param>
    /// <param name="recoil">Rücklauf der Haubitze 0…1</param>
    public static void AC130(DrawingContext dc, Point at, double heading, double u, double altitude, double t, (double G25, double G40, double G105) aim = default, double recoil = 0)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, HercWing, HercTail, HercBody);
        dc.PushTransform(Fly.Frame(at, heading, u));
        foreach (double s in new[] { -1.0, 1.0 })
        {
            // Außentanks und Triebwerksgondeln hängen unter dem Schulterflügel: von oben sieht man den Teil vor der Vorderkante
            dc.DrawRoundedRectangle(GunShade, GunEdge, new Rect(-6, 26.5 * s - 1.8, 23, 3.6), 1.8, 1.8);
            foreach (double y in HercEngines) dc.DrawRoundedRectangle(GunLight, GunEdge, new Rect(4, y * s - 2.1, 17.6, 4.2), 2.1, 2.1);
        }
        dc.DrawGeometry(GunShade, GunEdge, HercTail);
        dc.DrawGeometry(GunShade, GunEdge, HercSponsons);
        dc.DrawGeometry(GunLight, GunEdge, HercBody);
        // Waffen links: Rohre ragen quer aus dem Rumpf (auf das Ziel in der Kreismitte)
        // Geschützpforten links (Verkleidungen der Lafetten)
        dc.DrawRoundedRectangle(GunDark, GunEdge, new Rect(Gun25.X - 2, -8.3, 4, 1.4), 0.5, 0.5);
        dc.DrawRoundedRectangle(GunDark, GunEdge, new Rect(Gun40.X - 1.7, -8.4, 3.4, 1.6), 0.5, 0.5);
        dc.DrawRoundedRectangle(GunDark, GunEdge, new Rect(Gun105.X - 2.3, -8.6, 4.6, 2.0), 0.6, 0.6);
        // GAU-12/U: fünf Läufe in einer Trommel, Verkleidung bis zur Mündung
        Turned(dc, new Point(Gun25.X, -7.9), aim.G25, () =>
        {
            dc.DrawRoundedRectangle(GunMetal, null, new Rect(-0.75, 0, 1.5, 1.2), 0.4, 0.4);
            for (int b = -2; b <= 2; b++) dc.DrawLine(GunBarrelThin, new Point(b * 0.26, 1.0), new Point(b * 0.26, 2.4));
            dc.DrawRoundedRectangle(null, GunBarrelThin, new Rect(-0.7, 2.2, 1.4, 0.35), 0.15, 0.15);   // Laufhalter vorn
        });
        // 40 mm Bofors L/60: langes Rohr mit kegelförmigem Feuerdämpfer
        Turned(dc, new Point(Gun40.X, -7.9), aim.G40, () =>
        {
            dc.DrawRectangle(GunMetal, null, new Rect(-0.6, 0, 1.2, 1.3));
            dc.DrawLine(GunBarrel, new Point(0, 1.2), new Point(0, 4.0));
            dc.DrawGeometry(GunMetal, null, Fly.G("M-0.35,4 L0.35,4 L0.6,4.9 L-0.6,4.9 Z"));
        });
        // 105 mm M102: kurzes, dickes Rohr mit Doppel-Mündungsbremse, läuft beim Schuss zurück
        Turned(dc, new Point(Gun105.X, -8.0), aim.G105, () =>
        {
            double back = -1.6 * recoil;
            dc.DrawRectangle(GunMetal, null, new Rect(-1.0, 0, 2.0, 1.5));   // Wiege
            dc.DrawLine(Fly.P("#1E2226", 1.3), new Point(0, 1.3 + back), new Point(0, 5.4 + back));
            dc.DrawRectangle(GunMetal, null, new Rect(-1.0, 4.6 + back, 2.0, 0.55));
            dc.DrawRectangle(GunMetal, null, new Rect(-1.0, 5.35 + back, 2.0, 0.5));
        });
        dc.DrawGeometry(GunShade, GunEdge, HercWing);
        foreach (double s in new[] { -1.0, 1.0 })
        {
            foreach (double y in HercEngines) dc.DrawRoundedRectangle(GunLight, GunEdge, new Rect(-5, y * s - 1.3, 17, 2.6), 1.3, 1.3);   // Gondel über dem Flügel
            dc.DrawLine(GunSeam, new Point(-2.4, 9 * s), new Point(1.3, 60 * s));   // Klappen und Querruder
        }
        dc.DrawGeometry(GunShade, GunEdge, HercFin);
        dc.DrawGeometry(GunLight, GunEdge, HercRadome);
        dc.DrawGeometry(null, GunSeam, HercFrames);
        dc.DrawGeometry(null, GunSeam, HercHatches);
        dc.DrawGeometry(null, GunSeam, HercControls);
        dc.DrawGeometry(GunDark, GunEdge, HercGlass);
        dc.DrawLine(HfWire, new Point(40, 0), new Point(-30, 0));                      // HF-Antennendraht zum Seitenleitwerk
        dc.DrawLine(Antenna, new Point(28, -0.8), new Point(26.4, -0.8));               // SATCOM- und UHF-Antennen
        dc.DrawLine(Antenna, new Point(6, 0.9), new Point(4.6, 0.9));
        dc.DrawEllipse(GunMetal, null, new Point(18, 0), 0.9, 0.9);                      // Antennenkuppel
        foreach (double s in new[] { -1.0, 1.0 })
        {
            dc.DrawEllipse(GunMetal, GunEdge, new Point(-33, 6.6 * s), 0.9, 0.9);      // LAIRCM-Infrarotstörer
            dc.DrawRoundedRectangle(GunDark, null, new Rect(-27, 7.2 * s - 0.4, 3, 0.8), 0.3, 0.3);   // Täuschkörperwerfer
            foreach (double y in HercEngines) dc.DrawRoundedRectangle(Exhaust, null, new Rect(-3.2, y * s - 0.55, 4.2, 1.1), 0.5, 0.5);   // Abgas oben auf der Gondel
        }
        dc.DrawEllipse(GunMetal, GunEdge, new Point(40, -6.6), 1.5, 1.5);   // Infrarot-Sensorturm links an der Nase
        dc.DrawEllipse(GunMetal, GunEdge, new Point(36, -7.2), 1.1, 1.1);   // Fernsehkamera (ALLTV)
        Soft.Draw(dc, NavRed, new Point(8.2, -68.4), 1.6, 1.6, 1);       // Positionslichter an den Flügelspitzen: links rot, rechts grün
        Soft.Draw(dc, NavGreen, new Point(8.2, 68.4), 1.6, 1.6, 1);
        Soft.Draw(dc, Beacon, new Point(10, 0), (t % 1.1) < 0.12 ? 2.4 : 0.9, (t % 1.1) < 0.12 ? 2.4 : 0.9, 1);   // Blinklicht
        foreach (double s in new[] { -1.0, 1.0 })
            foreach (double y in HercEngines)
            {
                dc.DrawRectangle(PropDisc, null, new Rect(21, y * s - 6.9, 1.6, 13.8));
                Propeller(dc, 21.6, y * s, t * 22 + y, 1, 6.9);
                dc.DrawEllipse(GunLight, GunEdge, new Point(22.4, y * s), 1.4, 1.3);   // Spinner
            }
        dc.Pop();
    }
}

/// <summary>
/// AC-130U: kreist in einer Linkskurve um eine Symbolgruppe – die Waffen zeigen nach links in die Kreismitte. Die Gruppe hält erst ein paar
/// Sekunden durch (Feuerstöße schlagen daneben ein, 19.11.2026: „man möchte ja die Show sehen“), dann wird ein Symbol nach dem anderen
/// zerstört (40 mm, 105 mm auf das mittlere, ab und zu ein 25-mm-Feuerstoß), danach fliegt die Maschine zur nächsten Gruppe.
/// </summary>
sealed class GunshipRaid : IAttack
{
    public enum Gun { G25, G40, G105 }
    internal record Shot(Gun Gun, double Fire, double Impact, Point To, bool Kill);
    record Segment(double From, double To, Func<double, Point> At);

    readonly List<Segment> path = [];
    internal readonly List<Shot> Shots = [];
    readonly double span, u;
    const double Warm = 2.8, Per = 0.62;
    public IReadOnlyList<(Point P, double T)> Blasts { get; }
    public IReadOnlyList<double>? BlastSizes { get; }
    public double LastImpact { get; }
    public double End { get; }
    public bool Explodes => true;
    public double Reach { get; }
    internal int Orbits { get; }

    public GunshipRaid(Rect stage, IReadOnlyList<Point> targets, double iconSize, IReadOnlyList<Rect>? screens = null)
    {
        span = Math.Clamp(stage.Width * 0.085, iconSize * 2.8, iconSize * 4.2);   // Spannweite in Pixeln
        u = span / 137;
        Reach = iconSize * 0.5;
        double speed = Math.Max(stage.Height * 0.36, span * 1.6);
        var random = new Random(130);
        var groups = Gather(targets, iconSize * 2.4);
        var pos = new Point(stage.Right + span * 1.2, stage.Top + stage.Height * 0.2);
        var dir = new Vector(-1, 0.15); dir.Normalize();
        double t = 0.3;
        var blasts = new List<(Point, double)>(); var sizes = new List<double>();
        // Reihenfolge: jeweils die nächste Gruppe
        var pending = groups.ToList();
        int orbits = 0;
        while (pending.Count > 0)
        {
            var here = pos;
            var g = pending.OrderBy(x => (x.Center - here).LengthSquared).First();
            pending.Remove(g);
            orbits++;
            double radius = Math.Max(g.Radius + iconSize * 1.8, span * 0.85);
            // Kreis ganz auf dem Bildschirm der Gruppe: am Rand die Mitte nach innen schieben (die Waffen zielen trotzdem auf die Symbole)
            var screen = screens?.FirstOrDefault(r => r.Contains(g.Center)) ?? stage;
            if (screen.IsEmpty) screen = stage;
            radius = Math.Min(radius, Math.Max(span * 0.6, Math.Min(screen.Width, screen.Height) / 2 - span * 0.55));
            double inset = radius + span * 0.55;
            var orbit = new Point(screen.Width > 2 * inset ? Math.Clamp(g.Center.X, screen.Left + inset, screen.Right - inset) : screen.Left + screen.Width / 2,
                screen.Height > 2 * inset ? Math.Clamp(g.Center.Y, screen.Top + inset, screen.Bottom - inset) : screen.Top + screen.Height / 2);
            // Einflugpunkt: Kreis so treffen, dass die linke Seite zur Mitte zeigt
            var away = pos - orbit; if (away.LengthSquared < 1) away = new Vector(1, 0); away.Normalize();
            var p0 = orbit + Fly.Rot(away, -1.0) * radius;
            var n = orbit - p0; n.Normalize();
            var tangent = new Vector(-n.Y, n.X);   // links der Flugrichtung liegt die Mitte
            double length = (p0 - pos).Length;
            double hop = Math.Max(0.9, length * 1.15 / speed);
            path.Add(Hermite(t, t + hop, pos, dir * length, p0, tangent * length));
            t += hop;
            // Kreisen
            double theta0 = Math.Atan2(p0.Y - orbit.Y, p0.X - orbit.X);
            double sign = Vector.CrossProduct(p0 - orbit, tangent) > 0 ? 1 : -1;
            double omega = speed / radius * sign;
            var members = g.Members.OrderBy(m => (m - g.Center).LengthSquared).ToList();
            double stay = Warm + members.Count * Per + 0.6;
            double t0 = t, c0 = theta0; var center = orbit;
            path.Add(new Segment(t0, t0 + stay, q => center + new Vector(Math.Cos(c0 + omega * (q - t0)), Math.Sin(c0 + omega * (q - t0))) * radius));
            // Show: erst daneben (25-mm-Feuerstöße, ein 40-mm-Schuss), dann ein Symbol nach dem anderen
            for (double b = t0 + 0.4; b < t0 + Warm - 0.2; b += 0.42)
            {
                var near = members[random.Next(members.Count)];
                var miss = near + Fly.Rot(new Vector(iconSize * (0.75 + 0.6 * random.NextDouble()), 0), random.NextDouble() * Math.Tau);
                for (int i = 0; i < 5; i++)
                {
                    double fire = b + i * 0.045;
                    var to = miss + new Vector((random.NextDouble() - 0.5) * iconSize * 0.4, (random.NextDouble() - 0.5) * iconSize * 0.4);
                    Shots.Add(new Shot(Gun.G25, fire, fire + 0.18, to, false));
                }
            }
            var close = members[0] + Fly.Rot(new Vector(iconSize * 0.9, 0), random.NextDouble() * Math.Tau);
            Shots.Add(new Shot(Gun.G40, t0 + Warm * 0.7, t0 + Warm * 0.7 + 0.26, close, false));
            for (int k = 0; k < members.Count; k++)
            {
                double kill = t0 + Warm + k * Per;
                var target = members[k];
                var gun = k == 0 && members.Count >= 3 ? Gun.G105 : k % 3 == 2 ? Gun.G25 : Gun.G40;
                double flight = gun == Gun.G105 ? 0.42 : gun == Gun.G40 ? 0.26 : 0.18;
                if (gun == Gun.G25)
                    for (int i = 0; i < 6; i++)
                    {   // Feuerstoß wandert auf das Symbol
                        double fire = kill - flight - (5 - i) * 0.045;
                        var to = target + new Vector(iconSize * (0.7 - 0.14 * i), 0) * (i % 2 == 0 ? 1 : -1);
                        Shots.Add(new Shot(Gun.G25, fire, fire + flight, i == 5 ? target : to, i == 5));
                    }
                else
                {
                    if (gun == Gun.G40) Shots.Add(new Shot(Gun.G40, kill - flight - 0.22, kill - 0.22, target + Fly.Rot(new Vector(iconSize * 0.55, 0), random.NextDouble() * Math.Tau), false));
                    Shots.Add(new Shot(gun, kill - flight, kill, target, true));
                }
                blasts.Add((target, kill)); sizes.Add(gun == Gun.G105 ? 1.35 : gun == Gun.G40 ? 0.85 : 0.6);
            }
            t = t0 + stay;
            pos = path[^1].At(t);
            var after = pos - path[^1].At(t - 0.02); after.Normalize();
            dir = after;
        }
        Orbits = orbits;
        // Abflug nach links oben aus dem Bild
        var exit = new Point(stage.Left - span * 2, stage.Top - span);
        double outLength = (exit - pos).Length;
        double outHop = Math.Max(1.2, outLength * 1.1 / speed);
        path.Add(Hermite(t, t + outHop, pos, dir * outLength, exit, new Vector(-1, -0.3) * outLength));
        End = t + outHop;
        Blasts = blasts; BlastSizes = sizes;
        LastImpact = blasts.Count > 0 ? blasts.Max(b => b.Item2) : 2;
    }

    record Group(Point Center, double Radius, List<Point> Members);

    /// <summary>Benachbarte Symbole (Radius r) zu Gruppen zusammenfassen; jede Gruppe wird einmal umkreist.</summary>
    static List<Group> Gather(IReadOnlyList<Point> icons, double r)
    {
        var open = icons.ToList(); var result = new List<Group>();
        while (open.Count > 0)
        {
            var seed = open.OrderByDescending(c => open.Count(o => (o - c).Length <= r)).First();
            var members = open.Where(o => (o - seed).Length <= r).ToList();
            var center = new Point(members.Average(c => c.X), members.Average(c => c.Y));
            result.Add(new Group(center, members.Max(m => (m - center).Length), members));
            open.RemoveAll(members.Contains);
        }
        return result;
    }

    static Segment Hermite(double from, double to, Point a, Vector da, Point b, Vector db) => new(from, to, q =>
    {
        double k = Math.Clamp((q - from) / Math.Max(1e-6, to - from), 0, 1), k2 = k * k, k3 = k2 * k;
        return (Point)((Vector)a * (2 * k3 - 3 * k2 + 1) + da * (k3 - 2 * k2 + k) + (Vector)b * (-2 * k3 + 3 * k2) + db * (k3 - k2));
    });

    Point At(double t)
    {
        foreach (var s in path) if (t <= s.To) return s.At(Math.Max(t, s.From));
        return path[^1].At(path[^1].To);
    }
    double HeadingAt(double t) { var a = At(t - 0.02); var b = At(t + 0.02); return Math.Atan2(b.Y - a.Y, b.X - a.X); }
    internal Point PlaneAt(double t) => At(t);

    Point Muzzle(Gun gun, double t)
    {
        var offset = gun == Gun.G25 ? Planes.Gun25 : gun == Gun.G40 ? Planes.Gun40 : Planes.Gun105;
        return At(t) + Fly.Rot(offset * u, HeadingAt(t));
    }

    public void Draw(DrawingContext dc, double t)
    {
        foreach (var s in Shots)
        {
            if (t < s.Fire) continue;
            double k = (t - s.Fire) / (s.Impact - s.Fire);
            var from = Muzzle(s.Gun, s.Fire);
            if (k < 1)
            {
                var head = from + (s.To - from) * k;
                if (s.Gun == Gun.G105) Planes.Shell(dc, head, u);
                else Planes.Tracer(dc, from + (s.To - from) * Math.Max(0, k - 0.35), head, s.Gun == Gun.G40 ? u * 1.5 : u);
            }
            else if (!s.Kill && t - s.Impact < 0.5) Planes.GunHit(dc, s.To, t - s.Impact, s.Gun == Gun.G40 ? u * 1.8 : u);
        }
        if (t >= 0.3 && t <= End)
        {
            double heading = HeadingAt(t);
            double Aim(Gun gun)
            {   // Lafette folgt dem nächsten bzw. letzten Ziel dieser Waffe, höchstens ±35° aus der Querlage
                var shot = Shots.Where(s => s.Gun == gun && s.Fire >= t - 0.4).OrderBy(s => Math.Abs(s.Fire - t)).FirstOrDefault();
                if (shot is null) return 0;
                var mount = At(t) + Fly.Rot((gun == Gun.G25 ? new Vector(23, -7.9) : gun == Gun.G40 ? new Vector(-10, -7.9) : new Vector(-17, -8)) * u, heading);
                var d = shot.To - mount;
                double rel = Math.Atan2(d.Y, d.X) - heading + Math.PI / 2;   // 0 = quer links
                while (rel > Math.PI) rel -= Math.Tau; while (rel < -Math.PI) rel += Math.Tau;
                return Math.Clamp(rel, -0.6, 0.6);
            }
            var last105 = Shots.Where(s => s.Gun == Gun.G105 && s.Fire <= t).Select(s => t - s.Fire).DefaultIfEmpty(9).Min();
            double recoil = last105 < 0.08 ? last105 / 0.08 : Math.Max(0, 1 - (last105 - 0.08) / 0.55);
            Planes.AC130(dc, At(t), heading, u, 0.9, t, (Aim(Gun.G25), Aim(Gun.G40), Aim(Gun.G105)), recoil);
        }
        foreach (var s in Shots)   // Mündungsfeuer links am Rumpf
        {
            double age = t - s.Fire, life = s.Gun == Gun.G25 ? 0.04 : s.Gun == Gun.G40 ? 0.09 : 0.16;
            if (age < 0 || age >= life) continue;
            double size = s.Gun == Gun.G25 ? 3 : s.Gun == Gun.G40 ? 5.5 : 9;
            var muzzle = Muzzle(s.Gun, s.Fire);
            var outward = s.To - muzzle; outward.Normalize();
            Soft.Draw(dc, Flash, muzzle + outward * 2 * u, size * u, size * 0.8 * u, 1 - age / life);
        }
        foreach (var s in Shots.Where(s => s.Gun != Gun.G25 && t >= s.Fire && t - s.Fire < 0.9))   // Pulverrauch zieht nach hinten ab
        {
            double age = t - s.Fire;
            var drift = At(s.Fire) - At(Math.Max(0, s.Fire - 0.05)); drift.Normalize();
            var puff = Muzzle(s.Gun, s.Fire) - drift * (age * span * 0.3);
            double size = (s.Gun == Gun.G105 ? 7 : 4) * u * (1 + age * 1.6);
            Soft.Draw(dc, Smoke, puff, size, size, 0.55 * (1 - age / 0.9));
        }
    }
    static readonly Brush Flash = Art.Glow("#F8FFE6A8"), Smoke = Art.Glow("#80B8B4AE");
}
