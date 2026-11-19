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
    void Draw(DrawingContext dc, double t);
}

static class Attacks
{
    public const string Tu160 = "tu160", Tu22 = "tu22m3", Su34 = "su34", Ka52 = "ka52", Tu95 = "tu95", Il76 = "il76";
    public static readonly string[] All = [Tu160, Tu22, Su34, Ka52, Tu95, Il76];

    /// <param name="clusters">dichteste Gruppen zuerst (für Marschflugkörper)</param>
    /// <param name="icons">Symbolmitten auf dem Hauptbildschirm (für Treffer je Symbol)</param>
    public static IAttack? Create(string? kind, Rect stage, IReadOnlyList<Point> clusters, IReadOnlyList<Point> icons, double iconSize)
    {
        var each = icons.Count > 0 ? icons : clusters.Take(12).ToList();
        return kind switch
        {
            Tu160 => new Bomber(stage, clusters.Take(14).ToList(), iconSize),
            Tu22 => new RowRaid(RowRaid.Kind.Carpet, stage, each, iconSize),
            Su34 => new RowRaid(RowRaid.Kind.Strike, stage, each, iconSize),
            Ka52 => new HelicopterRaid(stage, each, iconSize),
            Tu95 => new BearRaid(stage, clusters.Take(12).ToList(), iconSize),
            Il76 => new AirborneRaid(stage, each, iconSize),
            _ => null,
        };
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

static class Planes
{
    // ---------------------------------------------------------------- Ту-22М3 (Länge 42,5 m, Spannweite 34,3 m bei 20°; hier Reiseflug ~30°)
    static readonly Geometry Tu22Body = Fly.G("M50,0 C48.6,-1.5 44.5,-2.8 38,-3.4 L20,-3.8 L-42,-3.8 L-50,-2.4 L-50,2.4 L-42,3.8 L20,3.8 L38,3.4 C44.5,2.8 48.6,1.5 50,0 Z");
    static readonly Geometry Tu22Trunks = Fly.Both("M27,3.4 L23.5,7.6 L-44,7.9 L-50.5,6.4 L-50.5,3 Z");
    static readonly Geometry Tu22Glove = Fly.Both("M15,7.4 L-13,13.4 L-24,13.4 L-22,7.6 Z");
    static readonly Geometry Tu22Wing = Fly.Both("M-8,12.8 L-26,41 L-31.5,41.6 L-24,13.2 Z");
    static readonly Geometry Tu22Tail = Fly.Both("M-34,6.5 L-46,20.5 L-51,21 L-48.5,6 Z");
    static readonly Geometry Tu22Fin = Fly.G("M-20,-0.9 L-50,-1 L-53.5,0 L-50,1 L-20,0.9 Z");
    static readonly Brush Tu22Light = Fly.B("#C9CDD1"), Tu22Shade = Fly.B("#B5BBC0");
    static readonly Pen Tu22Edge = Fly.P("#6C737A", 0.35), Seam = Fly.P("#8A9197", 0.22);
    static readonly Brush Glass = Fly.B("#2C3846"), Nozzle = Fly.B("#3A3E43"), Radome = Fly.B("#E2E4E6");

    public static void Tu22(DrawingContext dc, Point at, double heading, double u, double altitude)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, Tu22Wing, Tu22Tail, Tu22Glove, Tu22Trunks, Tu22Body);
        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawGeometry(Tu22Shade, Tu22Edge, Tu22Wing);
        dc.DrawGeometry(Tu22Shade, Tu22Edge, Tu22Tail);
        dc.DrawGeometry(Tu22Light, Tu22Edge, Tu22Glove);
        dc.DrawGeometry(Tu22Light, Tu22Edge, Tu22Trunks);
        dc.DrawGeometry(Tu22Light, Tu22Edge, Tu22Body);
        dc.DrawGeometry(Tu22Shade, Tu22Edge, Tu22Fin);
        dc.DrawGeometry(Radome, Tu22Edge, Fly.G("M50,0 C49,-1 47,-2 44,-2.4 L44,2.4 C47,2 49,1 50,0 Z"));
        dc.DrawEllipse(Glass, Tu22Edge, new Point(35, 0), 4.4, 1.8);
        foreach (double s in new[] { -1.0, 1.0 })
        {
            dc.DrawLine(Seam, new Point(-21.4, 16.5 * s), new Point(-29.2, 38.6 * s));   // Klappen
            dc.DrawLine(Seam, new Point(-9, 13.8 * s), new Point(-25, 39.4 * s));        // Vorflügel
            dc.DrawLine(Seam, new Point(-24.5, 13.2 * s), new Point(-8.2, 12.9 * s));    // Schwenkfuge
            dc.DrawEllipse(Nozzle, Tu22Edge, new Point(-50.5, 4.9 * s), 1.1, 1.9);
            dc.DrawLine(Seam, new Point(22, 6.2 * s), new Point(14, 7.6 * s));           // Lufteinlauf
            Art.Star(dc, new Point(-21.5, 29 * s), 2.3, 0);
        }
        Art.Star(dc, new Point(-44, 0), 1.0, 0);
        dc.Pop();
    }

    // ---------------------------------------------------------------- Су-34 (Länge 23,3 m, Spannweite 14,7 m; Entennase, Canards, zwei Seitenleitwerke)
    static readonly Geometry Su34Body = Fly.G("M50,0 C49.6,-3.2 47,-5.2 41,-5.6 L28,-5.8 L18,-6.6 L-36,-6.6 L-48,-5 L-50.5,-3 L-50.5,3 L-48,5 L-36,6.6 L18,6.6 L28,5.8 L41,5.6 C47,5.2 49.6,3.2 50,0 Z");
    static readonly Geometry Su34Wing = Fly.Both("M27,6 C15,8.2 3,10.2 -5,12.6 L-24.5,31.5 L-31,32 L-29.5,15 L-37.5,10 L-37.5,6.6 Z");
    static readonly Geometry Su34Canard = Fly.Both("M25,6 L20.5,13.5 L16.8,13.5 L17.4,6 Z");
    static readonly Geometry Su34Tail = Fly.Both("M-36,9 L-47,21.5 L-51.5,21.5 L-50.5,8.6 Z");
    static readonly Geometry Su34Fins = Fly.Both("M-27,7.4 L-46,7.4 L-48.5,8.3 L-46,9.2 L-27,8.8 Z");
    static readonly Geometry Su34Sting = Fly.G("M-48,-1.8 L-57,-1.1 L-57,1.1 L-48,1.8 Z");
    static readonly Brush Su34Light = Fly.B("#93A3B1"), Su34Shade = Fly.B("#7E8F9E"), Su34Camo = Fly.B("#6F8191"), Su34Nose = Fly.B("#C9CED2");
    static readonly Pen Su34Edge = Fly.P("#4B5660", 0.35);

    public static void Su34(DrawingContext dc, Point at, double heading, double u, double altitude, double t)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, Su34Wing, Su34Tail, Su34Canard, Su34Body);
        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawGeometry(Su34Shade, Su34Edge, Su34Wing);
        dc.DrawGeometry(Su34Shade, Su34Edge, Su34Tail);
        dc.DrawGeometry(Su34Light, Su34Edge, Su34Body);
        dc.DrawGeometry(Su34Light, Su34Edge, Su34Canard);
        dc.DrawGeometry(Su34Camo, null, Fly.Both("M8,6.6 L-2,10.8 L-14,16.6 L-20,13 L-6,6.6 Z"));   // Tarnflecken
        dc.DrawGeometry(Su34Shade, Su34Edge, Su34Fins);
        dc.DrawGeometry(Su34Shade, Su34Edge, Su34Sting);
        dc.DrawGeometry(Su34Nose, Su34Edge, Fly.G("M50,0 C49.6,-3.2 47.5,-4.9 43,-5.4 L43,5.4 C47.5,4.9 49.6,3.2 50,0 Z"));
        dc.DrawEllipse(Glass, Su34Edge, new Point(35.5, 0), 5.4, 3.7);
        dc.DrawLine(Seam, new Point(35.5, -3.7), new Point(35.5, 3.7));
        foreach (double s in new[] { -1.0, 1.0 })
        {
            dc.DrawEllipse(Nozzle, Su34Edge, new Point(-50.5, 3.4 * s), 1.2, 1.7);
            Soft.Draw(dc, Heat, new Point(-55, 3.4 * s), 3.6 + 0.4 * Math.Sin(t * 47 + s), 1.4, 0.7);
            dc.DrawLine(Seam, new Point(-27, 15 * s), new Point(-29.5, 29 * s));
            Art.Star(dc, new Point(-17, 21 * s), 1.8, 0);
        }
        dc.Pop();
    }
    static readonly Brush Heat = Art.Glow("#C0FFB060");

    // ---------------------------------------------------------------- Ка-52 (Rumpf 13,5 m, Rotor Ø 14,5 m, zwei gegenläufige Dreiblattrotoren)
    static readonly Geometry Ka52Body = Fly.G("M50,0 C50,-5.4 46,-8.4 38,-8.6 L10,-8.6 L-6,-6.6 L-18,-3 L-48,-1.7 L-48,1.7 L-18,3 L-6,6.6 L10,8.6 L38,8.6 C46,8.4 50,5.4 50,0 Z");
    static readonly Geometry Ka52Stub = Fly.Both("M9,7.8 L5,23.5 L-4,23.5 L-2.5,7.8 Z");
    static readonly Geometry Ka52Engines = Fly.Both("M16,8.2 L13.5,12 L-13,12 L-15,8.2 Z");
    static readonly Geometry Ka52Tail = Fly.Both("M-39,1.5 L-41.5,11.2 L-47.5,11.2 L-47.5,1.5 Z");
    static readonly Geometry Ka52Fins = Fly.Both("M-36.5,10.6 L-49.5,10.6 L-49.5,12.6 L-36.5,12.6 Z");
    static readonly Brush KaLight = Fly.B("#58614F"), KaShade = Fly.B("#49513F"), KaBlade = Fly.B("#262923"), KaDisc = Fly.B("#1E202620"), Pod = Fly.B("#3A3F37");
    static readonly Pen KaEdge = Fly.P("#2A2F25", 0.4);
    const double RotorRadius = 54, RotorSpeed = 11;   // rad/s, sichtbar langsamer als echt, damit man die Blätter sieht

    public static void Ka52(DrawingContext dc, Point at, double heading, double u, double altitude, double t)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, Ka52Stub, Ka52Tail, Ka52Body, Ka52Engines);
        // Schatten der Rotorkreise
        dc.PushTransform(new TranslateTransform(altitude * 100 * u * 0.16, altitude * 100 * u * 0.26));
        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawEllipse(KaDisc, null, new Point(4, 0), RotorRadius, RotorRadius);
        dc.Pop(); dc.Pop();

        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawGeometry(KaShade, KaEdge, Ka52Stub);
        foreach (double s in new[] { -1.0, 1.0 })
        {
            dc.DrawRoundedRectangle(Pod, KaEdge, new Rect(-6, 12.8 * s - 2, 14, 4), 2, 2);    // Raketenbehälter innen
            dc.DrawRoundedRectangle(Pod, KaEdge, new Rect(-5, 20.2 * s - 1.8, 12, 3.6), 1.8, 1.8); // außen
        }
        dc.DrawGeometry(KaShade, KaEdge, Ka52Tail);
        dc.DrawGeometry(KaShade, KaEdge, Ka52Fins);
        dc.DrawGeometry(KaLight, KaEdge, Ka52Body);
        dc.DrawGeometry(KaShade, KaEdge, Ka52Engines);
        dc.DrawEllipse(Glass, KaEdge, new Point(33, -3.6), 8, 3.4);   // nebeneinander sitzende Besatzung
        dc.DrawEllipse(Glass, KaEdge, new Point(33, 3.6), 8, 3.4);
        dc.DrawEllipse(Pod, KaEdge, new Point(47, 0), 2.6, 2.6);         // Sensorkugel
        dc.DrawRectangle(Pod, KaEdge, new Rect(18, 7.8, 12, 2));          // Bordkanone rechts
        Art.Star(dc, new Point(-30, 0), 1.6, 0);
        // unterer Rotor gegen, oberer Rotor im Uhrzeigersinn
        Rotor(dc, -t * RotorSpeed + 0.5, -1, 0.55);
        Rotor(dc, t * RotorSpeed, 1, 1);
        dc.DrawEllipse(KaShade, KaEdge, new Point(4, 0), 3.4, 3.4);
        dc.DrawEllipse(KaBlade, null, new Point(4, 0), 1.6, 1.6);
        dc.Pop();
    }

    static void Rotor(DrawingContext dc, double angle, int direction, double strength)
    {
        dc.DrawEllipse(KaDisc, null, new Point(4, 0), RotorRadius, RotorRadius);
        for (int b = 0; b < 3; b++)
        {
            double a = angle + b * Math.Tau / 3;
            for (int trail = 3; trail >= 0; trail--)   // Bewegungsunschärfe: blasse Kopien hinter dem Blatt (gegen die Drehrichtung)
            {
                double at = (a - trail * 0.08 * direction) * 180 / Math.PI;
                dc.PushTransform(new RotateTransform(at, 4, 0));
                dc.PushOpacity((trail == 0 ? 0.9 : 0.22 - trail * 0.05) * strength);
                dc.DrawRoundedRectangle(KaBlade, null, new Rect(4, -1.3, RotorRadius, 2.6), 1.2, 1.2);
                dc.Pop(); dc.Pop();
            }
        }
    }

    // ---------------------------------------------------------------- Ту-95МС (Länge 46 m, Spannweite 50 m, 35° Pfeilung, 4 × NK-12 mit gegenläufigen Vierblattpropellern)
    static readonly Geometry Tu95Body = Fly.G("M50,0 C49,-2.7 46,-3.9 40,-4.2 L-40,-4.2 L-50,-2.3 L-52.5,0 L-50,2.3 L-40,4.2 L40,4.2 C46,3.9 49,2.7 50,0 Z");
    static readonly Geometry Tu95Wing = Fly.Both("M10,4 L-26,54 L-32,54.6 L-16,8 L-18,4 Z");
    static readonly Geometry Tu95Tail = Fly.Both("M-34,4 L-45.5,22 L-50,22.5 L-47.5,4 Z");
    static readonly Geometry Tu95Fin = Fly.G("M-26,-0.9 L-50,-1 L-53,0 L-50,1 L-26,0.9 Z");
    static readonly Brush BearLight = Fly.B("#C4C9CD"), BearShade = Fly.B("#B1B7BC"), Blade = Fly.B("#2E3135"), Spinner = Fly.B("#9AA1A8"), PropDisc = Fly.B("#24303030");
    static readonly Pen BearEdge = Fly.P("#727A82", 0.35), BladePen = Fly.P("#2E3135", 1.3), Probe = Fly.P("#5E656C", 0.8);
    static readonly double[] BearEngines = [17, 34];
    static double LeadingEdge(double y) => 10 - 36 * (y - 4) / 50;

    public static void Tu95(DrawingContext dc, Point at, double heading, double u, double altitude, double t)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, Tu95Wing, Tu95Tail, Tu95Body);
        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawGeometry(BearShade, BearEdge, Tu95Wing);
        dc.DrawGeometry(BearShade, BearEdge, Tu95Tail);
        dc.DrawGeometry(BearLight, BearEdge, Tu95Body);
        dc.DrawGeometry(BearShade, BearEdge, Tu95Fin);
        dc.DrawLine(Probe, new Point(50, 0), new Point(56, 0));                // Betankungssonde
        dc.DrawEllipse(Glass, BearEdge, new Point(41, 0), 3.6, 2.4);
        foreach (double s in new[] { -1.0, 1.0 })
        {
            dc.DrawLine(Seam, new Point(-17, 9 * s), new Point(-30.5, 51 * s));   // Klappen/Querruder
            foreach (double y in BearEngines)
            {
                double x = LeadingEdge(y);
                dc.DrawRoundedRectangle(BearLight, BearEdge, new Rect(x - 16, y * s - 2.4, 27, 4.8), 2.2, 2.2);   // Triebwerksgondel
                double prop = x + 11.6;
                // Draufsicht: die Propellerscheibe steht quer zur Flugrichtung, die Blätter erscheinen als Striche, deren Länge mit dem Drehwinkel schwankt
                dc.DrawRectangle(PropDisc, null, new Rect(prop - 0.8, y * s - 6.6, 3.4, 13.2));
                Propeller(dc, prop, y * s, t * 24 + y, 1);
                Propeller(dc, prop + 1.8, y * s, -t * 24 + y * 0.5, -1);
                dc.DrawEllipse(Spinner, BearEdge, new Point(prop + 3.2, y * s), 1.8, 1.3);
            }
            Art.Star(dc, new Point(-14, 44 * s), 2.2, 0);
        }
        Art.Star(dc, new Point(-46, 0), 1.0, 0);
        dc.Pop();
    }

    static void Propeller(DrawingContext dc, double x, double y, double angle, int dir)
    {
        for (int b = 0; b < 4; b++)
        {
            double a = angle + b * Math.PI / 2;
            double len = 6.6 * Math.Sin(a);
            double lean = 0.5 * Math.Cos(a) * dir;
            dc.DrawLine(BladePen, new Point(x, y), new Point(x + lean, y + len));
        }
    }

    // ---------------------------------------------------------------- Ил-76МД (Länge 46,6 m, Spannweite 50,5 m, Schulterdecker, T-Leitwerk, 4 Strahltriebwerke)
    static readonly Geometry Il76Body = Fly.G("M50,0 C50,-5 47,-6.8 40,-7 L-30,-7 L-46,-4 L-52,-1.6 L-52,1.6 L-46,4 L-30,7 L40,7 C47,6.8 50,5 50,0 Z");
    static readonly Geometry Il76Wing = Fly.Both("M10,6.6 L-14,54 L-21,54.6 L-14,20 L-18,6.6 Z");
    static readonly Geometry Il76Tail = Fly.Both("M-40,1 L-50,19.5 L-55,20 L-53,1 Z");
    static readonly Geometry Il76Gear = Fly.Both("M14,6.8 L12,10 L-14,10 L-16,6.8 Z");
    static readonly Brush IlLight = Fly.B("#CBD0D4"), IlShade = Fly.B("#BAC0C5"), Intake = Fly.B("#4A4F55");
    static readonly Pen IlEdge = Fly.P("#77808A", 0.35);
    static readonly double[] IlEngines = [17, 32];
    static double IlLeading(double y) => 10 - 24 * (y - 6.6) / 47.4;

    public static void Il76(DrawingContext dc, Point at, double heading, double u, double altitude, bool rampOpen)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, Il76Wing, Il76Tail, Il76Body, Il76Gear);
        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawGeometry(IlLight, IlEdge, Il76Gear);
        dc.DrawGeometry(IlLight, IlEdge, Il76Body);
        if (rampOpen) dc.DrawGeometry(Intake, IlEdge, Fly.G("M-44,-3.4 L-52,-1.4 L-52,1.4 L-44,3.4 Z"));   // offene Heckrampe
        dc.DrawGeometry(IlShade, IlEdge, Il76Wing);          // Schulterdecker: Flügel liegt über dem Rumpf
        foreach (double s in new[] { -1.0, 1.0 })
        {
            foreach (double y in IlEngines)
            {
                double x = IlLeading(y);
                dc.DrawRoundedRectangle(IlLight, IlEdge, new Rect(x - 9, y * s - 2.4, 16, 4.8), 2.4, 2.4);
                dc.DrawEllipse(Intake, null, new Point(x + 7, y * s), 0.8, 1.8);
            }
            dc.DrawLine(Seam, new Point(-15, 8 * s), new Point(-19.6, 52 * s));
            Art.Star(dc, new Point(-8, 40 * s), 2.2, 0);
        }
        dc.DrawGeometry(IlShade, IlEdge, Il76Tail);          // T-Leitwerk ganz oben
        dc.DrawEllipse(Glass, IlEdge, new Point(43.5, 0), 3.2, 3.6);
        dc.DrawEllipse(Glass, IlEdge, new Point(48.2, 0), 1.5, 2.6);   // verglaste Navigatorkanzel
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
        dc.PushOpacity(1 - 0.85 * flat);
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
        dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
    }

    /// <summary>Fallschirmjäger von oben, trägt das Symbol über dem Kopf weg.</summary>
    public static void Carrier(DrawingContext dc, Point at, double heading, double size, double step, double opacity)
    {
        dc.PushOpacity(opacity);
        dc.PushTransform(Fly.Frame(at, heading, size / 10));
        double swing = Math.Sin(step) * 1.2;
        dc.DrawEllipse(Trooper, null, new Point(-1 + swing, -1.8), 1.4, 0.9);      // Beine im Schritt
        dc.DrawEllipse(Trooper, null, new Point(-1 - swing, 1.8), 1.4, 0.9);
        dc.DrawEllipse(Trooper, CanopyEdge, new Point(0, 0), 2.2, 3.4);            // Schultern
        dc.DrawEllipse(Skin, null, new Point(0.4, 0), 1.3, 1.3);                   // Kopf
        dc.DrawRoundedRectangle(Box, BoxEdge, new Rect(1.2, -3.6, 7.2, 7.2), 1.6, 1.6);   // getragenes Symbol
        dc.Pop(); dc.Pop();
    }
}

// ================================================================ Angriffe

/// <summary>Zwei Flugzeuge im Verband, je eine Reihe: Tu-22M3 legt einen Bombenteppich, Su-34 wirft gelenkte Gleitbomben genau auf jedes Symbol.</summary>
sealed class RowRaid : IAttack
{
    public enum Kind { Carpet, Strike }
    readonly Kind kind;
    readonly Rect stage;
    readonly double span, speed, fall, gap, startX;
    readonly double enter = 0.3;
    readonly double[] rows;
    readonly double[] climbFrom = [double.MaxValue, double.MaxValue];
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
        rows = carpet ? [stage.Top + stage.Height * 0.3, stage.Top + stage.Height * 0.68] : [stage.Top + stage.Height * 0.24, stage.Top + stage.Height * 0.62];
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
                if (!carpet || i + 1 >= list.Count) continue;
                var d = list[i + 1] - list[i];   // Teppich: zwischen weit auseinanderliegenden Symbolen weitere Bomben
                int extra = Math.Min(3, (int)(d.Length / (iconSize * 2.2)));
                for (int e = 1; e <= extra; e++)
                    aims.Add(list[i] + d * (e / (extra + 1.0)) + new Vector((random.NextDouble() - 0.5) * iconSize, (random.NextDouble() - 0.5) * iconSize));
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
        Blasts = drops.Select(d => (d.To, d.Impact)).ToList();
        LastImpact = drops.Count > 0 ? drops.Max(d => d.Impact) : enter + 2;
        End = Math.Max(LastImpact, enter + gap + (stage.Right + span * 1.5 - startX) / speed);
    }

    Point PlaneAt(int k, double t)
    {
        double x = startX + speed * (t - enter - k * gap);
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
            if (kind == Kind.Carpet) Planes.Tu22(dc, p, HeadingAt(k, t), u, 1);
            else Planes.Su34(dc, p, HeadingAt(k, t), u, 0.45, t);
        }
    }
}

/// <summary>Ka-52: fliegt ein, schwebt, dreht sich nacheinander zu jedem Symbol und feuert eine Rakete, fliegt ab.</summary>
sealed class HelicopterRaid : IAttack
{
    readonly Rect stage;
    readonly double span, arrive, leave, gone, inHeading, outHeading;
    readonly double enter = 0.3;
    readonly Point entry, hover, exit;
    readonly List<Shot> shots = [];
    record Shot(Point Target, Point Launch, double Fire, double Impact, double Heading);
    const double FlyIn = 2.6, Step = 0.34, FlyOut = 2.6;

    public IReadOnlyList<(Point P, double T)> Blasts { get; }
    public double LastImpact { get; }
    public double End { get; }
    public bool Explodes => true;

    public HelicopterRaid(Rect stage, IReadOnlyList<Point> targets, double iconSize)
    {
        this.stage = stage;
        span = Math.Clamp(stage.Width * 0.06, iconSize * 2.0, iconSize * 3.0);
        double u = span / 100;
        var list = targets.Count > 0 ? targets.ToList() : [new Point(stage.Left + stage.Width / 2, stage.Top + stage.Height / 2)];
        hover = new Point(Math.Clamp(list.Average(p => p.X), stage.Left + stage.Width * 0.3, stage.Right - stage.Width * 0.3), stage.Top + stage.Height * 0.42);
        entry = new Point(stage.Right + span * 1.2, stage.Top + stage.Height * 0.16);
        exit = new Point(stage.Left - span * 1.8, stage.Top - span);
        inHeading = Math.Atan2(hover.Y - entry.Y, hover.X - entry.X);
        outHeading = Math.Atan2(exit.Y - hover.Y, exit.X - hover.X);
        arrive = enter + FlyIn;
        // im Kreis um den Schwebepunkt: der Hubschrauber dreht sich gleichmäßig von Ziel zu Ziel
        double Around(Point p)
        {
            double a = Math.Atan2(p.Y - hover.Y, p.X - hover.X) - inHeading;
            while (a < 0) a += Math.Tau;
            while (a >= Math.Tau) a -= Math.Tau;
            return a;
        }
        var order = list.OrderBy(Around).ToList();
        double fire = arrive + 0.6;
        for (int i = 0; i < order.Count; i++)
        {
            var d = order[i] - hover;
            double heading = Math.Atan2(d.Y, d.X);
            var launch = hover + Fly.Rot(new Vector(2, (i % 2 == 0 ? 1 : -1) * 20) * u, heading);
            double flight = 0.32 + 0.4 * Math.Clamp((order[i] - launch).Length / (stage.Width * 0.4), 0, 1.4);
            shots.Add(new Shot(order[i], launch, fire, fire + flight, heading));
            fire += Step;
        }
        leave = fire + 0.35;
        gone = leave + FlyOut;
        Blasts = shots.Select(s => (s.Target, s.Impact)).ToList();
        LastImpact = shots.Max(s => s.Impact);
        End = Math.Max(LastImpact, gone);
    }

    (Point Pos, double Heading, double Altitude) Pose(double t)
    {
        double bob = Math.Sin(t * 2.3) * span * 0.012;
        if (t < arrive)
        {
            double k = Fly.EaseOut((t - enter) / FlyIn);
            var control = new Point((entry.X + hover.X) / 2, Math.Min(entry.Y, hover.Y) - stage.Height * 0.06);
            var p = Fly.Bez(entry, control, hover, k);
            double turn = Fly.Smooth((t - (arrive - 0.6)) / 0.6);
            double h = Fly.Turn(inHeading, shots.Count > 0 ? shots[0].Heading : inHeading, turn);
            return (p + new Vector(0, bob), h, 0.5 - 0.15 * k);
        }
        if (t < leave)
        {
            int i = shots.FindIndex(s => s.Fire > t);
            double prevH = i <= 0 ? (i == 0 ? shots[0].Heading : shots[^1].Heading) : shots[i - 1].Heading;
            double prevT = i <= 0 ? (i == 0 ? arrive : shots[^1].Fire) : shots[i - 1].Fire;
            double nextH = i >= 0 ? shots[i].Heading : outHeading, nextT = i >= 0 ? shots[i].Fire : leave;
            double k = Fly.Smooth((t - prevT) / Math.Max(0.05, (nextT - prevT) * 0.7));
            return (hover + new Vector(0, bob), Fly.Turn(prevH, nextH, k), 0.35);
        }
        double q = Fly.EaseIn((t - leave) / FlyOut);
        return (hover + (exit - hover) * q + new Vector(0, bob), outHeading, 0.35 + 0.3 * q);
    }

    public void Draw(DrawingContext dc, double t)
    {
        double u = span / 100;
        foreach (var s in shots)
        {
            if (t < s.Fire || t >= s.Impact) continue;
            double k = (t - s.Fire) / (s.Impact - s.Fire);
            Planes.Rocket(dc, s.Launch, s.Launch + (s.Target - s.Launch) * k, u * 1.2, t);
        }
        if (t < enter || t > gone) return;
        var (pos, heading, altitude) = Pose(t);
        Planes.Ka52(dc, pos, heading, u, altitude, t);
        foreach (var s in shots)   // Mündungsfeuer am Behälter
        {
            double age = t - s.Fire;
            if (age >= 0 && age < 0.09) Soft.Draw(dc, MuzzleFlash, s.Launch + Fly.Rot(new Vector(8 * u, 0), s.Heading), 6 * u, 4 * u, 1 - age / 0.09);
        }
    }
    static readonly Brush MuzzleFlash = Art.Glow("#F8FFE4A0");
}

/// <summary>Tu-95MS: hoch und langsam, startet Marschflugkörper Ch-101 von den Flügelstationen; gegenläufige Propeller drehen sich.</summary>
sealed class BearRaid : IAttack
{
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
            double side = i % 2 == 0 ? 1 : -1, station = (i / 2) % 2 == 0 ? 13 : 24;
            var drop = pos + new Vector(-6 * u, side * station * u);
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
