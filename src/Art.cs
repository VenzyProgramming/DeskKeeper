using System;
using System.Windows;
using System.Windows.Media;

namespace DeskKeeper;

/// <summary>
/// Flache Seitenansicht mit Volumen nur durch Licht : weiche Verläufe, feine Fugen und Glanzkanten,
/// keine dicken Konturen, kein 3D. Licht von oben links. Vorlagen: SMs Иерихон-Bild und Industrie-Manipulator.
/// Einheiten: Иерихон Länge 100 (Nase +50), Дубина Fahrwerk 100 breit, Boden y = 0, Blick nach rechts.
/// </summary>
static class Art
{
    // ================================================================ Maße Дубина (Einheiten)
    internal const double BaseWidth = 100;
    internal static readonly Vector ShoulderFromGround = new(2, -74);
    internal const double UpperLength = 54, ForeLength = 60, GripLength = 24;
    internal static Vector HitchFromGround(int frame = 0) => new(-58 * Squash(frame), -10);
    internal static readonly Vector CargoFromHitch = new(-46, -40);
    internal const int TurnFrames = 9;
    static double Squash(int frame) => Math.Cos(Math.PI * Math.Clamp(frame, 0, TurnFrames - 1) / (TurnFrames - 1));

    // ================================================================ Palette
    static readonly Brush HullTube = V(("#4A5774", 0), ("#6E7D9C", 0.14), ("#46536E", 0.3), ("#2C3549", 0.68), ("#1A2030", 1));
    static readonly Brush HullFin = V(("#3C4862", 0), ("#2E3850", 0.5), ("#222A3C", 1));
    static readonly Brush HullFinLow = V(("#2A3346", 0), ("#222A3C", 0.5), ("#181E2C", 1));
    static readonly Brush NoseSteel = V(("#D6D5D0", 0), ("#A9A8A3", 0.3), ("#77766F", 0.7), ("#4C4B47", 1));
    static readonly Brush Vent = V(("#05070B", 0), ("#121722", 1));
    static readonly Pen HullSeam = P("#141A26", 0.35), HullGlint = P("#8794B0", 0.25), FinGlint = P("#6E7B98", 0.3);

    static readonly Brush Iron = V(("#5B626C", 0), ("#434951", 0.22), ("#30353C", 0.6), ("#1D2126", 1));        // waagerechte Flächen
    static readonly Brush IronDark = V(("#3E444C", 0), ("#2A2E34", 0.5), ("#16181C", 1));
    static readonly Brush Column = H(("#15181C", 0), ("#3F454E", 0.18), ("#59606A", 0.3), ("#3A4048", 0.5), ("#24282E", 0.82), ("#121418", 1));   // senkrechter Zylinder
    static readonly Brush Tube = V(("#2A2F36", 0), ("#646C77", 0.22), ("#3A4048", 0.45), ("#22262C", 0.8), ("#121418", 1));   // liegender Zylinder (lokal)
    static readonly Brush Chrome = V(("#F2F4F7", 0), ("#B9BFC7", 0.3), ("#7E858E", 0.65), ("#C3C8CF", 0.85), ("#646A72", 1));
    static readonly Brush SteelDisc = R(("#B7BDC5", 0), ("#8C939C", 0.55), ("#4E545C", 0.9), ("#2C3036", 1));
    static readonly Brush Brass = V(("#FFE27A", 0), ("#F0C02A", 0.35), ("#C8960F", 0.8), ("#8E6A08", 1));
    static readonly Brush Rubber = V(("#2A2C30", 0), ("#141518", 0.5), ("#08090A", 1));
    static readonly Brush HubYellow = R(("#FFE680", 0), ("#E8B81C", 0.6), ("#9C7608", 1));
    static readonly Brush AmberLamp = R(("#FFF4D0", 0), ("#FFB240", 0.45), ("#C86A10", 1));
    static readonly Brush AmberGlow = Glow("#80FFA640");
    static readonly Pen Edge = P("#0B0D10", 0.45), Glint = P("#858D98", 0.35), Rivet = P("#6E7680", 0.9), CablePen = P("#0A0B0D", 1.3), CableGlint = P("#3A3E44", 0.4);
    static readonly Brush RivetFill = S("#7A828C"), Shadow = Glow("#66000000");

    // ================================================================ Иерихон
    static readonly Geometry TailCone = G("M -50,-2.6 L -44,-3.6 L -44,3.6 L -50,2.6 Z");
    static readonly Geometry Body = G("M -44,-3.6 L 38,-3.6 L 38,3.6 L -44,3.6 Z");
    static readonly Geometry NoseShape = G("M 37.8,-3.6 C 43,-3.5 48,-2.4 51.5,0 C 48,2.4 43,3.5 37.8,3.6 Z");
    static readonly Geometry TailFinTop = G("M -45,-3.4 L -49.5,-13.2 L -38.5,-13.2 L -27.5,-3.4 Z");
    static readonly Geometry TailFinBottom = G("M -45,3.4 L -49.5,13.2 L -38.5,13.2 L -27.5,3.4 Z");
    static readonly Geometry StrakeTop = G("M -34,-3.5 L -31,-5 L -19,-5 L -16,-3.5 Z");
    static readonly Geometry CanardTop = G("M 14,-3.4 L 17,-8.6 L 25,-8.6 L 31,-3.4 Z");
    static readonly Geometry CanardBottom = G("M 14,3.4 L 17,8.6 L 25,8.6 L 31,3.4 Z");
    static readonly Geometry TailPanel = G("M -44,-2.6 L -38,-3.6 M -44,2.6 L -38,3.6 M -38,-3.6 L -38,-2.2 L -24,-2.2 M -38,3.6 L -38,2.2 L -21,2.2 L -18,3.6");

    public static void Rocket(DrawingContext dc, bool bayOpen, double flame)
    {
        Exhaust(dc, -50, 2.3, flame);
        dc.DrawGeometry(HullFinLow, null, TailFinBottom);
        dc.DrawGeometry(HullFinLow, null, CanardBottom);
        dc.DrawGeometry(HullTube, HullSeam, TailCone);
        dc.DrawGeometry(HullTube, null, Body);
        dc.DrawGeometry(null, HullSeam, TailPanel);
        dc.DrawLine(HullSeam, new Point(1, -3.6), new Point(1, 3.6));                                   // Rumpfnaht
        dc.DrawLine(HullGlint, new Point(1.4, -3.4), new Point(1.4, 3.4));
        dc.DrawLine(HullGlint, new Point(-42, -2.7), new Point(37, -2.7));                              // Glanzband
        dc.DrawGeometry(HullFin, null, StrakeTop);
        dc.DrawGeometry(HullFin, null, TailFinTop);
        dc.DrawLine(FinGlint, new Point(-45, -3.4), new Point(-49.5, -13.2));
        dc.DrawGeometry(HullFin, null, CanardTop);
        dc.DrawLine(FinGlint, new Point(14, -3.4), new Point(17, -8.6));
        dc.DrawGeometry(NoseSteel, null, NoseShape);
        dc.DrawRoundedRectangle(Vent, null, new Rect(29.5, -2.5, 6, 1.4), 0.4, 0.4);                    // Lüftungsschlitze
        dc.DrawRoundedRectangle(Vent, null, new Rect(29.5, -0.3, 6, 1.4), 0.4, 0.4);
        dc.DrawEllipse(AmberGlow, null, new Point(-28, -0.6), 1.8, 1.2);
        dc.DrawRoundedRectangle(AmberLamp, null, new Rect(-28.8, -1, 1.6, 0.8), 0.4, 0.4);
        if (bayOpen) dc.DrawRoundedRectangle(Vent, null, new Rect(-14, -3.6, 28, 3.4), 0.6, 0.6);
        dc.DrawLine(HullSeam, new Point(-44, 3.6), new Point(38, 3.6));
    }

    public static void Panel(DrawingContext dc)
    {
        dc.DrawRoundedRectangle(HullTube, HullSeam, new Rect(-14, -1.7, 28, 3.4), 0.5, 0.5);
        dc.DrawLine(HullGlint, new Point(-13, -1.1), new Point(13, -1.1));
    }

    // Submunition: kurzer Stahlkörper, stumpfe Spitze, orange Kennbinde, kreuzförmiges Leitwerk – lokal Länge 20.
    static readonly Brush DartBody = V(("#C9CDD3", 0), ("#9AA0A8", 0.3), ("#5E646C", 0.75), ("#3A3E44", 1));
    static readonly Brush DartTip = V(("#E8E4DA", 0), ("#A8A398", 0.45), ("#5C584F", 1));
    static readonly Brush DartBand = V(("#FFB060", 0), ("#E8702A", 0.5), ("#9A4210", 1));
    static readonly Brush DartFin = V(("#7A8088", 0), ("#4A4F56", 1));
    static readonly Geometry DartHull = G("M -7,-1.5 L 5,-1.5 L 5,1.5 L -7,1.5 Z");
    static readonly Geometry DartNose = G("M 5,-1.5 C 7.5,-1.4 9.4,-0.8 10,0 C 9.4,0.8 7.5,1.4 5,1.5 Z");
    static readonly Geometry DartFins = G("M -9.6,-4.2 L -7.6,-4.2 L -4.4,-1.4 L -8.6,-1.4 Z M -9.6,4.2 L -7.6,4.2 L -4.4,1.4 L -8.6,1.4 Z");
    static readonly Pen DartEdge = P("#22262C", 0.18);

    public static void Dart(DrawingContext dc, double flame)
    {
        Exhaust(dc, -9.4, 0.9, flame * 0.7);
        dc.DrawGeometry(DartFin, DartEdge, DartFins);
        dc.DrawRoundedRectangle(DartFin, null, new Rect(-9.6, -0.5, 3.2, 1.0), 0.3, 0.3);   // Flosse quer (von vorn gesehen)
        dc.DrawGeometry(DartBody, DartEdge, DartHull);
        dc.DrawRectangle(DartBand, null, new Rect(1.2, -1.5, 1.4, 3));
        dc.DrawGeometry(DartTip, DartEdge, DartNose);
        dc.DrawLine(P("#E8ECF0", 0.15), new Point(-6.6, -1.05), new Point(4.8, -1.05));
    }

    static void Exhaust(DrawingContext dc, double x, double h, double length)
    {
        double l = h * 5.5 * length;
        dc.DrawEllipse(PlumeGlow, null, new Point(x - l * 0.55, 0), l * 1.0, h * 2.4);
        dc.DrawEllipse(Plume, null, new Point(x - l * 0.42, 0), l * 0.55, h * 0.95);
        dc.DrawEllipse(Core, null, new Point(x - l * 0.12, 0), l * 0.2, h * 0.5);
    }
    static readonly Brush PlumeGlow = Glow("#70FF9A3C"), Plume = Glow2("#F0FFD27A", "#90FF8A2A"), Core = Glow2("#FFFFFFFF", "#E0BFE4FF");

    // ================================================================ Дубина

    /// <summary>Gelenkkette in Weltkoordinaten (nach rechts gewandt). s = Pixel je Einheit, head = Kopfwinkel (y nach unten).</summary>
    public static void Robot(DrawingContext dc, Point ground, Point shoulder, Point elbow, Point wrist, double head, double jaw, double wheel, double s)
    {
        dc.DrawEllipse(Shadow, null, ground + new Vector(0, -1 * s), 62 * s, 6 * s);
        dc.PushTransform(new MatrixTransform(s, 0, 0, s, ground.X, ground.Y));
        DrawBase(dc, wheel);
        dc.Pop();
        // Arm: Oberarm, Unterarm, Kopf; Kabel; Schulterkappe obenauf
        Local(dc, shoulder, elbow, s, l => DrawUpper(dc, l));
        Local(dc, elbow, wrist, s, l => DrawFore(dc, l));
        var dir = new Vector(Math.Cos(head), Math.Sin(head));
        Local(dc, wrist, wrist + dir * GripLength * s, s, _ => DrawHand(dc, jaw));
        DrawCable(dc, shoulder + (elbow - shoulder) * 0.2 + new Vector(0, 6 * s), elbow + new Vector(0, 7 * s), s, 7);
        dc.PushTransform(new MatrixTransform(s, 0, 0, s, shoulder.X, shoulder.Y));
        dc.DrawEllipse(SteelDisc, Edge, new Point(), 11.5, 11.5);
        dc.DrawEllipse(null, Glint, new Point(), 9.2, 9.2);
        Octagon(dc, new Point(), 5.6, HubYellow);
        dc.Pop();
        dc.PushTransform(new MatrixTransform(s, 0, 0, s, elbow.X, elbow.Y));
        dc.DrawEllipse(IronDark, Edge, new Point(), 7.5, 7.5);
        dc.DrawEllipse(SteelDisc, null, new Point(), 3.6, 3.6);
        dc.Pop();
    }

    static void DrawBase(DrawingContext dc, double wheel)
    {
        // Ausleger-Holm über die ganze Breite, Wanne, Lager
        dc.DrawRoundedRectangle(Iron, Edge, new Rect(-50, -16, 100, 8), 2, 2);
        dc.DrawLine(Glint, new Point(-48.5, -15.3), new Point(48.5, -15.3));
        dc.DrawRoundedRectangle(IronDark, null, new Rect(-30, -10, 46, 3), 1, 1);
        foreach (double x in new[] { -38.0, -20, 22, 40 }) dc.DrawEllipse(RivetFill, null, new Point(x, -12), 0.8, 0.8);
        Caster(dc, new Point(-44, 0), wheel, false);
        Caster(dc, new Point(44, 0), wheel, false);
        // Drehteller (zwei Stufen)
        dc.DrawRoundedRectangle(IronDark, Edge, new Rect(-27, -22, 54, 6.5), 2.5, 2.5);
        dc.DrawRoundedRectangle(Iron, Edge, new Rect(-22, -26.5, 44, 5), 2, 2);
        dc.DrawLine(Glint, new Point(-21, -26), new Point(21, -26));
        // Gegengewicht-Flügel hinten (wie bei den Vorlagen)
        dc.DrawGeometry(Iron, Edge, G("M -16,-58 L -38,-58 L -38,-48 L -30,-44 L -16,-44 Z"));
        dc.DrawLine(Glint, new Point(-37.5, -57.4), new Point(-16, -57.4));
        // Säule (senkrechter Zylinder) mit Klemmkasten
        dc.DrawRectangle(Column, null, new Rect(-13, -64, 26, 38));
        dc.DrawLine(Edge, new Point(-13, -64), new Point(-13, -26)); dc.DrawLine(Edge, new Point(13, -64), new Point(13, -26));
        dc.DrawRoundedRectangle(IronDark, Edge, new Rect(4, -50, 9, 13), 1, 1);
        foreach (double y in new[] { -48.0, -39 }) { dc.DrawEllipse(RivetFill, null, new Point(5.6, y), 0.55, 0.55); dc.DrawEllipse(RivetFill, null, new Point(11.4, y), 0.55, 0.55); }
        // Schultergabel
        dc.DrawGeometry(Iron, Edge, G("M -15,-64 L -15,-80 L -11,-86 L 15,-86 L 19,-80 L 19,-64 Z"));
        dc.DrawLine(Glint, new Point(-11, -85.4), new Point(15, -85.4));
        for (int k = 0; k < 7; k++) dc.DrawEllipse(RivetFill, null, new Point(-11 + k * 4.2, -82.5), 0.55, 0.55);
        // Hydraulikzylinder an der Säule (stützen die Schulter)
        dc.DrawRoundedRectangle(Tube, Edge, new Rect(-20, -62, 4, 20), 1.5, 1.5);
        dc.DrawRectangle(Chrome, null, new Rect(-19, -70, 2, 9));
        DrawCable(dc, new Point(-16, -40), new Point(-8, -72), 1, -6);
    }

    static void Caster(DrawingContext dc, Point at, double wheel, bool back)
    {
        double r = back ? 4.2 : 5;
        var c = at + new Vector(0, -r);
        dc.PushOpacity(back ? 0.75 : 1);
        dc.DrawRoundedRectangle(IronDark, Edge, new Rect(c.X - 2.6, c.Y - r - 3, 5.2, r + 2), 1, 1);   // Gabel
        dc.DrawEllipse(Rubber, Edge, c, r, r);
        dc.DrawEllipse(HubYellow, null, c, r * 0.34, r * 0.34);
        var spoke = new Vector(Math.Cos(wheel), Math.Sin(wheel)) * r * 0.3;
        dc.DrawLine(P("#6E560A", 0.5), c - spoke, c + spoke);
        dc.Pop();
    }

    static void DrawUpper(DrawingContext dc, double l)
    {
        // kräftiger Kastenträger, Fasen an beiden Enden, Nietleiste; ein Hydraulikzylinder darunter
        dc.DrawGeometry(Iron, Edge, G($"M -4,-8 L {l - 4},-7 L {l + 2},0 L {l - 4},7 L -4,8 L -10,0 Z"));
        dc.DrawLine(Glint, new Point(-3, -7.3), new Point(l - 5, -6.4));
        dc.DrawLine(P("#15181C", 0.5), new Point(-2, 3.8), new Point(l - 6, 3.4));
        for (int k = 0; k < 5; k++) dc.DrawEllipse(RivetFill, null, new Point(5 + k * (l - 16) / 4, -4.2), 0.65, 0.65);
        dc.DrawRoundedRectangle(Tube, Edge, new Rect(4, 8, l * 0.48, 4.2), 2, 2);
        dc.DrawRoundedRectangle(Chrome, null, new Rect(4 + l * 0.48, 9.2, l * 0.3, 1.9), 0.9, 0.9);
        dc.DrawEllipse(IronDark, Edge, new Point(4, 10.1), 2.2, 2.2);
    }

    static void DrawFore(DrawingContext dc, double l)
    {
        // ein durchgehendes Rohr mit kurzem Antriebsgehäuse am Ellbogen und einem Bundring
        dc.DrawRoundedRectangle(Tube, Edge, new Rect(-10, -6, 18, 12), 3, 3);
        dc.DrawLine(Glint, new Point(-8.5, -5.2), new Point(6.5, -5.2));
        dc.DrawRoundedRectangle(Tube, Edge, new Rect(6, -4, l - 6, 8), 4, 4);
        dc.DrawLine(Glint, new Point(8, -3), new Point(l - 3, -3));
        dc.DrawRoundedRectangle(IronDark, Edge, new Rect(l * 0.62 - 1.6, -5, 3.2, 10), 1, 1);
    }

    static void DrawHand(DrawingContext dc, double jaw)
    {
        // Handgelenk, Flansch, zwei klar getrennte Greifbacken
        double open = 0.1 + 0.42 * jaw;
        Finger(dc, new Point(11, -2.6), -open, -1);
        Finger(dc, new Point(11, 2.6), open, 1);
        dc.DrawRoundedRectangle(Tube, Edge, new Rect(-4, -5.5, 11, 11), 2.5, 2.5);
        dc.DrawRoundedRectangle(Chrome, Edge, new Rect(7, -7, 2.6, 14), 0.8, 0.8);
        dc.DrawRoundedRectangle(IronDark, Edge, new Rect(9.4, -4.6, 3.4, 9.2), 1, 1);
        dc.DrawEllipse(AmberGlow, null, new Point(1.5, -5.5), 2, 1.2);
        dc.DrawRoundedRectangle(AmberLamp, null, new Rect(0.5, -5.9, 2, 0.8), 0.3, 0.3);
    }

    static void Finger(DrawingContext dc, Point at, double angle, double side)
    {
        // Backe: breiter Grundkörper, schmaler Haken nach innen (zur Mitte)
        dc.PushTransform(new MatrixTransform(Math.Cos(angle), Math.Sin(angle), -Math.Sin(angle), Math.Cos(angle), at.X, at.Y));
        double i = -side;   // Richtung zur Mitte
        dc.DrawGeometry(Chrome, Edge, G($"M 0,{side * 1.6} L 9,{side * 1.4} L 13,{i * 0.4} L 12.4,{i * 1.8} L 10.6,{i * 1.5} L 10,{i * 0.2} L 7,{i * 0.9} L 0,{i * 1.0} Z"));
        dc.DrawLine(Glint, new Point(1, side * 1.1), new Point(8.5, side * 1.0));
        dc.Pop();
    }

    static void DrawCable(DrawingContext dc, Point a, Point b, double s, double sag)
    {
        var mid = a + (b - a) / 2 + new Vector(0, sag * s);
        var g = new StreamGeometry();
        using (var ctx = g.Open()) { ctx.BeginFigure(a, false, false); ctx.QuadraticBezierTo(mid, b, true, false); }
        dc.DrawGeometry(null, new Pen(CablePen.Brush, 1.3 * s), g);
        dc.DrawGeometry(null, new Pen(CableGlint.Brush, 0.4 * s), g);
    }

    /// <summary>Zeichnet ein Glied in seinem lokalen System: Ursprung am Gelenk a, x zeigt auf b, Länge in Einheiten.</summary>
    static void Local(DrawingContext dc, Point a, Point b, double s, Action<double> draw)
    {
        var d = b - a;
        double length = d.Length / s;
        if (length < 0.01) return;
        d.Normalize();
        dc.PushTransform(new MatrixTransform(d.X * s, d.Y * s, -d.Y * s, d.X * s, a.X, a.Y));
        draw(length);
        dc.Pop();
    }

    static void Octagon(DrawingContext dc, Point c, double radius, Brush fill)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
            for (int k = 0; k < 8; k++)
            {
                double a = Math.PI / 8 + k * Math.PI / 4;
                var p = c + new Vector(Math.Cos(a), Math.Sin(a)) * radius;
                if (k == 0) ctx.BeginFigure(p, true, true); else ctx.LineTo(p, true, true);
            }
        g.Freeze();
        dc.DrawGeometry(fill, P("#7A5C06", 0.35), g);
        dc.DrawEllipse(Glow("#90FFFFFF"), null, c + new Vector(-1.6, -1.8), 2.2, 1.5);
    }

    /// <summary>Ganzer Roboter in Ruhehaltung beim Umdrehen: Bild 0 (rechts) … 8 (links) als flaches Drehen um die Hochachse.</summary>
    public static void TurnFrame(DrawingContext dc, int frame, Point ground, double s)
    {
        double q = Squash(frame);
        if (Math.Abs(q) < 0.32) q = Math.Sign(q == 0 ? 1 : q) * 0.32;   // beim Wenden nie dünner als ein Drittel
        dc.PushTransform(new MatrixTransform(q, 0, 0, 1, (1 - q) * ground.X, 0));
        var shoulder = ground + ShoulderFromGround * s;
        var elbow = shoulder + new Vector(Math.Cos(-96 * Math.PI / 180), Math.Sin(-96 * Math.PI / 180)) * UpperLength * s;
        var wrist = elbow + new Vector(Math.Cos(-8 * Math.PI / 180), Math.Sin(-8 * Math.PI / 180)) * ForeLength * s;
        Robot(dc, ground, shoulder, elbow, wrist, 72 * Math.PI / 180, 0.4, 0, s);
        dc.Pop();
    }

    // ================================================================ Anhänger (Öse am Boden = hitchGround, Wagen nach links)

    public static void Trailer(DrawingContext dc, Point hitchGround, int frame, double tilt, double s)
    {
        double q = Squash(frame);
        if (Math.Abs(q) < 0.32) q = Math.Sign(q == 0 ? 1 : q) * 0.32;   // beim Wenden nie dünner als ein Drittel
        dc.PushTransform(new MatrixTransform(q, 0, 0, 1, (1 - q) * hitchGround.X, 0));
        dc.PushTransform(new RotateTransform(tilt, hitchGround.X, hitchGround.Y));
        dc.DrawEllipse(Shadow, null, hitchGround + new Vector(-52 * s, -1 * s), 44 * s, 5 * s);
        dc.PushTransform(new MatrixTransform(s, 0, 0, s, hitchGround.X, hitchGround.Y));
        // Deichsel mit Öse
        dc.DrawRoundedRectangle(Chrome, Edge, new Rect(-18, -11.6, 17, 2.6), 1.2, 1.2);
        dc.DrawEllipse(IronDark, Edge, new Point(-1, -10.3), 2.6, 2.6);
        // Pritsche
        dc.DrawRoundedRectangle(Iron, Edge, new Rect(-96, -16, 80, 6), 1.5, 1.5);
        dc.DrawLine(Glint, new Point(-95, -15.4), new Point(-17, -15.4));
        Caster(dc, new Point(-86, 0), 0, false);
        Caster(dc, new Point(-26, 0), 0, false);
        // Ladekiste mit Rippen und Deckel
        dc.DrawRectangle(IronDark, Edge, new Rect(-90, -46, 68, 30));
        for (int k = 0; k < 7; k++)
        {
            double x = -88 + k * 10.6;
            dc.DrawRectangle(Iron, null, new Rect(x, -46, 3.2, 30));
            dc.DrawLine(Glint, new Point(x + 0.4, -45.5), new Point(x + 0.4, -16.5));
        }
        dc.DrawRoundedRectangle(Iron, Edge, new Rect(-92, -49, 72, 4), 1, 1);
        dc.DrawLine(Glint, new Point(-91, -48.4), new Point(-21, -48.4));
        dc.DrawEllipse(AmberGlow, null, new Point(-95.5, -13), 2.4, 1.8);
        dc.DrawRoundedRectangle(AmberLamp, null, new Rect(-96.6, -14, 2, 2), 0.5, 0.5);
        dc.Pop(); dc.Pop(); dc.Pop();
    }

    // ================================================================ Atomrakete (lokal Länge 100, Nase +50)
    static readonly Brush NukeHull = V(("#F4F5F6", 0), ("#D9DCE0", 0.25), ("#A9AEB5", 0.7), ("#7C828A", 1));
    static readonly Brush NukeNose = V(("#3A3E44", 0), ("#1C1F23", 0.5), ("#0B0C0E", 1));
    static readonly Brush NukeFin = V(("#4A5058", 0), ("#2A2E34", 1));
    static readonly Brush Hazard = Stripes();
    static Brush Stripes()
    {
        var g = new DrawingGroup();
        g.Children.Add(new GeometryDrawing(S("#F2C21B"), null, new RectangleGeometry(new Rect(0, 0, 4, 4))));
        g.Children.Add(new GeometryDrawing(S("#141414"), null, Geometry.Parse("M 0,4 L 2,4 L 4,2 L 4,0 Z")));
        var b = new DrawingBrush(g) { TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 2.2, 2.2), ViewportUnits = BrushMappingMode.Absolute };
        b.Freeze();
        return b;
    }

    public static void NukeMissile(DrawingContext dc, double flame)
    {
        Exhaust(dc, -48, 3.6, flame);
        dc.DrawGeometry(NukeFin, Edge, G("M -46,-6 L -52,-16 L -40,-16 L -30,-6 Z"));
        dc.DrawGeometry(NukeFin, Edge, G("M -46,6 L -52,16 L -40,16 L -30,6 Z"));
        dc.DrawRoundedRectangle(NukeFin, Edge, new Rect(-48, -5, 6, 10), 1.5, 1.5);
        dc.DrawRoundedRectangle(NukeHull, Edge, new Rect(-43, -6.4, 70, 12.8), 3, 3);
        dc.DrawLine(P("#FFFFFF", 0.4), new Point(-41, -5.2), new Point(25, -5.2));
        dc.DrawRectangle(Hazard, Edge, new Rect(4, -6.4, 9, 12.8));
        dc.DrawEllipse(S("#F2C21B"), Edge, new Point(-16, 0), 3.4, 3.4);   // Kennzeichen
        dc.DrawEllipse(S("#141414"), null, new Point(-16, 0), 1.1, 1.1);
        dc.DrawGeometry(NukeNose, Edge, G("M 26.5,-6.4 C 38,-6 46,-3.6 50,0 C 46,3.6 38,6 26.5,6.4 Z"));
    }

    // ================================================================ Skins (Farben echter Industrie-Roboter, nur Schriftzug, kein Logo)
    internal sealed record Skin(string Id, string Name, Brush Plate, Brush Arm, Brush Housing, Brush Turntable, Brush Cap, string Label, Brush LabelInk);
    internal static readonly Skin[] Skins =
    [
        new("graphit", "Графит", V(("#5C646E", 0), ("#3E444C", 0.5), ("#262A30", 1)), V(("#5B626C", 0), ("#434951", 0.22), ("#30353C", 0.6), ("#1D2126", 1)),
            R(("#6A727D", 0), ("#4A515A", 0.45), ("#2C3036", 0.85), ("#1C1F23", 1)), R(("#474E57", 0), ("#30353B", 0.5), ("#17191D", 1)),
            R(("#FFE680", 0), ("#E8B81C", 0.6), ("#9C7608", 1)), "", S("#00000000")),
        new("kuka", "KUKA", V(("#FFA040", 0), ("#F07800", 0.45), ("#B85600", 1)), V(("#FFA850", 0), ("#F27C00", 0.3), ("#D46800", 0.65), ("#9C4A00", 1)),
            R(("#FFAE5A", 0), ("#F07800", 0.5), ("#A04C00", 1)), R(("#4A4A4A", 0), ("#2A2A2A", 0.6), ("#121212", 1)),
            R(("#5A5A5A", 0), ("#262626", 0.6), ("#0E0E0E", 1)), "KUKA", S("#141414")),
        new("fanuc", "FANUC", V(("#FFE45A", 0), ("#F5C800", 0.45), ("#B89200", 1)), V(("#FFEA70", 0), ("#F7CC00", 0.3), ("#DDB000", 0.65), ("#9C7C00", 1)),
            R(("#FFEA70", 0), ("#F5C800", 0.5), ("#A88600", 1)), R(("#505050", 0), ("#2E2E2E", 0.6), ("#141414", 1)),
            R(("#E04040", 0), ("#B81818", 0.6), ("#6E0A0A", 1)), "FANUC", S("#C81414")),
        new("abb", "ABB", V(("#FFFFFF", 0), ("#E6E8EB", 0.45), ("#AEB4BC", 1)), V(("#FFFFFF", 0), ("#EDEFF2", 0.3), ("#CDD2D8", 0.65), ("#8E959E", 1)),
            R(("#FFFFFF", 0), ("#E2E5E9", 0.5), ("#9CA3AC", 1)), R(("#7A8089", 0), ("#525860", 0.6), ("#2A2E33", 1)),
            R(("#FF5A5A", 0), ("#E2001A", 0.6), ("#80000E", 1)), "ABB", S("#E2001A")),
        new("ur", "Universal Robots", V(("#F2F4F6", 0), ("#D3D8DE", 0.45), ("#9AA2AC", 1)), V(("#E8ECF0", 0), ("#C9CFD6", 0.35), ("#9EA6B0", 0.7), ("#6E7680", 1)),
            R(("#F4F6F8", 0), ("#CDD3DA", 0.5), ("#8C949E", 1)), R(("#9DBFE0", 0), ("#6E9CCB", 0.6), ("#3E6894", 1)),
            R(("#B6D2EE", 0), ("#76A5D6", 0.6), ("#3A6A9C", 1)), "UR", S("#2E5C8A")),
    ];
    internal static Skin Current = Skins[0];
    static readonly Typeface LabelFace = new(new FontFamily("Arial Black, Segoe UI"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal);

    // ================================================================ Дубина von oben (Einheiten: Fahrwerk ≈ 100, u = Pixel je Einheit)
    // Licht von oben links: Scheiben mit Radialverlauf, Flächen mit festem Schatten nach unten rechts.
    static readonly Brush TopIron = R(("#6A727D", 0), ("#4A515A", 0.45), ("#2C3036", 0.85), ("#1C1F23", 1));
    static readonly Brush TopIronDark = R(("#474E57", 0), ("#30353B", 0.5), ("#17191D", 1));
    static readonly Brush TopPlate = V(("#5C646E", 0), ("#3E444C", 0.5), ("#262A30", 1));
    static readonly Brush TopShadow = S("#55000000");
    static readonly Brush TopShadowSoft = Glow("#70000000");
    static readonly Brush ArmShadow = S("#2E000000");

    // ================================================================ Industrie-Roboter von oben (Skins KUKA, FANUC, ABB, UR)
    static readonly Brush Platform = V(("#5A6068", 0), ("#3C4148", 0.5), ("#24282D", 1));
    static readonly Brush PlatformLight = V(("#F4F5F7", 0), ("#D6DADF", 0.5), ("#A9AFB7", 1));
    static readonly Brush Scanner = R(("#4A4F56", 0), ("#1A1C20", 0.6), ("#08090A", 1));
    static readonly Brush ScanGlass = R(("#6FD0FF", 0), ("#1E6A99", 0.6), ("#0B2A40", 1));
    static readonly Brush Estop = R(("#FF7070", 0), ("#D81818", 0.6), ("#700808", 1));
    static readonly Brush LedCyan = S("#5FE3FF"), MotorGrey = V(("#6A7079", 0), ("#3E434A", 0.5), ("#24272C", 1));
    static readonly Brush UrBlue = R(("#BFD9F2", 0), ("#76A5D6", 0.55), ("#3A6A9C", 1));
    static bool Pro => Current.Id != "graphit";

    /// <summary>Mobile Plattform (wie KMR/MiR): rechteckig, Mecanum-Räder, Laserscanner an zwei Ecken, Not-Halt, LED-Band.</summary>
    static void TopPlatform(DrawingContext dc, double travel)
    {
        bool light = Current.Id is "abb";
        dc.DrawRoundedRectangle(TopShadow, null, new Rect(-50, -36, 104, 76), 8, 8);
        foreach (var (x, y) in new[] { (-34.0, -38.0), (34.0, -38.0), (-34.0, 38.0), (34.0, 38.0) })
        {
            dc.DrawRoundedRectangle(Rubber, Edge, new Rect(x - 11, y - 5, 22, 10), 2.5, 2.5);
            double spin = travel * 30 % 5;
            for (int k = -2; k <= 2; k++) dc.DrawLine(P("#4A4D52", 0.9), new Point(x + k * 4 + spin - 2.5, y - 4.5), new Point(x + k * 4 + spin + 2.5, y + 4.5));   // Rollen schräg
        }
        dc.DrawRoundedRectangle(light ? PlatformLight : Platform, Edge, new Rect(-52, -38, 104, 76), 8, 8);
        dc.DrawLine(Glint, new Point(-46, -36.6), new Point(46, -36.6));
        dc.DrawRoundedRectangle(Current.Plate, null, new Rect(-46, -32, 92, 6), 2, 2);                 // Farbband der Marke
        dc.DrawRoundedRectangle(LedCyan, null, new Rect(-46, 33.2, 92, 1.6), 0.8, 0.8);                // LED-Band
        foreach (var (x, y) in new[] { (46.0, -32.0), (-46.0, 32.0) })
        {
            dc.DrawEllipse(Scanner, Edge, new Point(x, y), 7, 7);
            dc.DrawEllipse(ScanGlass, null, new Point(x, y), 3.6, 3.6);
        }
        dc.DrawEllipse(Estop, Edge, new Point(-40, -20), 4.2, 4.2);
        dc.DrawRoundedRectangle(IronDark, Edge, new Rect(-44, 6, 14, 18), 2, 2);                       // Akku-Klappe
    }

    /// <summary>Arm eines Industrie-Roboters von oben je nach Skin; Achsen wie beim Original (Karussell, Schwinge, Arm, Hand).</summary>
    static void ProArm(DrawingContext dc, Point at, Vector dir, double e, double ez, double d, double z, double jaw, double u, Func<double, double> lift)
    {
        e = Math.Max(6, e);
        double fl = Math.Max(18, d - e);
        d = e + fl;
        var n = new Vector(-dir.Y, dir.X);
        Point W(double along, double side = 0) => at + dir * along * u + n * side * u;
        void Local(Point origin, double scale, Action draw)
        {
            dc.PushTransform(new MatrixTransform(dir.X * u * scale, dir.Y * u * scale, -dir.Y * u * scale, dir.X * u * scale, origin.X, origin.Y));
            draw();
            dc.Pop();
        }
        void Label(double x, double y, double size)
        {
            if (Current.Label.Length == 0) return;
            var text = new FormattedText(Current.Label, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFace, size, Current.LabelInk, 1);
            dc.DrawText(text, new Point(x - text.Width / 2, y - text.Height / 2));
        }
        void Gripper(Point origin, double scale)
        {
            Local(origin, scale, () =>
            {
                dc.DrawRoundedRectangle(Chrome, Edge, new Rect(-1, -6.5, 2.6, 13), 0.8, 0.8);
                double open = 1.5 + 4.6 * jaw;
                foreach (double side in new[] { -1.0, 1.0 })
                    dc.DrawGeometry(Chrome, Edge, G($"M 1.5,{side * (open + 1.4)} L 9,{side * (open + 1.0)} L 10.5,{side * (open - 1.6)} L 8.5,{side * (open - 1.8)} L 7.5,{side * (open - 0.4)} L 1.5,{side * (open - 0.8)} Z"));
            });
        }
        double sl = lift(55), el = lift(ez), wl = lift(z);
        switch (Current.Id)
        {
            case "kuka":
                // Arm (A3) mit Motorpaket hinten, schlanker Unterarm, Handgelenk
                Local(W(e), el, () =>
                {
                    dc.DrawRoundedRectangle(Current.Arm, Edge, new Rect(0, -6.5, Math.Max(1, fl - 9), 13), 4, 4);
                    dc.DrawLine(Glint, new Point(2, -5.5), new Point(fl - 11, -5.5));
                    for (int k = 0; k < 3; k++) dc.DrawRoundedRectangle(MotorGrey, Edge, new Rect(-26, -10 + k * 7, 12, 6), 2.5, 2.5);   // A4/A5/A6
                    dc.DrawRoundedRectangle(Current.Housing, Edge, new Rect(-15, -12, 24, 24), 5, 5);
                });
                Local(W(d - 9), wl, () => { dc.DrawEllipse(Current.Housing, Edge, new Point(0, 0), 6.5, 6.5); dc.DrawEllipse(MotorGrey, null, new Point(0, 0), 3, 3); });
                Gripper(W(d - 3), wl);
                // Schwinge (A2) breit, mit Gewichtsausgleich (Federzylinder) seitlich
                Local(W(0), (sl + el) / 2, () =>
                {
                    dc.DrawGeometry(Current.Arm, Edge, G($"M -6,-11 L {e},-8.5 L {e},8.5 L -6,11 Z"));
                    dc.DrawLine(Glint, new Point(-4, -10), new Point(e - 2, -7.8));
                    dc.DrawRoundedRectangle(MotorGrey, Edge, new Rect(-4, 12, Math.Max(1, e * 0.6), 5), 2.5, 2.5);
                    dc.DrawRoundedRectangle(Chrome, null, new Rect(e * 0.56, 13.2, Math.Max(1, e * 0.22), 2.6), 1.3, 1.3);
                    Label(e * 0.5, 0, 8.6);
                });
                // Karussell (A1) mit Motor
                Local(W(0), sl, () =>
                {
                    dc.DrawRoundedRectangle(MotorGrey, Edge, new Rect(-10, -30, 14, 12), 3, 3);
                    dc.DrawEllipse(Current.Housing, Edge, new Point(), 21, 21);
                    dc.DrawEllipse(null, Glint, new Point(), 17, 17);
                    dc.DrawEllipse(MotorGrey, Edge, new Point(), 7, 7);
                });
                break;

            case "fanuc":
                Local(W(e), el, () =>
                {
                    dc.DrawRoundedRectangle(Current.Arm, Edge, new Rect(0, -5.5, Math.Max(1, fl - 9), 11), 5.5, 5.5);          // Unterarm rund
                    dc.DrawLine(Glint, new Point(2, -4.5), new Point(fl - 11, -4.5));
                    dc.DrawRoundedRectangle(MotorGrey, Edge, new Rect(-24, -6, 12, 12), 3, 3);                       // J4-Motor
                    dc.DrawRoundedRectangle(Current.Housing, Edge, new Rect(-13, -11, 22, 22), 6, 6);                // J3-Gehäuse
                    dc.DrawRoundedRectangle(MotorGrey, Edge, new Rect(-6, 11, 12, 6), 2, 2);                         // J3-Motor
                    Label(-2, 0, 6.2);
                });
                Local(W(d - 9), wl, () => dc.DrawRoundedRectangle(Current.Housing, Edge, new Rect(-6, -6, 12, 12), 4, 4));
                Gripper(W(d - 3), wl);
                Local(W(0), (sl + el) / 2, () =>
                {
                    foreach (double side in new[] { -1.0, 1.0 })                                                    // J2-Schwinge: zwei Wangen
                    {
                        dc.DrawRoundedRectangle(Current.Arm, Edge, new Rect(-4, side > 0 ? 3 : -10, Math.Max(1, e + 4), 7), 3, 3);
                        dc.DrawLine(Glint, new Point(-2, side > 0 ? 3.8 : -9.2), new Point(e - 2, side > 0 ? 3.8 : -9.2));
                    }
                    dc.DrawRectangle(IronDark, null, new Rect(2, -3, Math.Max(1, e - 6), 6));
                });
                Local(W(0), sl, () =>
                {
                    dc.DrawEllipse(Current.Housing, Edge, new Point(), 20, 20);
                    dc.DrawRoundedRectangle(MotorGrey, Edge, new Rect(-8, 18, 16, 10), 3, 3);                        // J2-Motor
                    dc.DrawEllipse(MotorGrey, Edge, new Point(), 6, 6);
                });
                break;

            case "abb":
                Local(W(e), el, () =>
                {
                    dc.DrawRoundedRectangle(MotorGrey, Edge, new Rect(-30, -8, 20, 16), 8, 8);                       // Gegengewicht hinten
                    dc.DrawRoundedRectangle(Current.Arm, Edge, new Rect(-12, -10, Math.Max(1, fl + 3), 20), 10, 10);            // glatter Arm, läuft zur Hand hin aus
                    dc.DrawRoundedRectangle(Current.Arm, Edge, new Rect(0, -5.8, Math.Max(1, fl - 6), 11.6), 5.8, 5.8);
                    dc.DrawLine(Glint, new Point(-8, -8.6), new Point(fl - 10, -4.6));
                });
                Gripper(W(d - 3), wl);
                Local(W(0), (sl + el) / 2, () =>
                {
                    dc.DrawRoundedRectangle(Current.Arm, Edge, new Rect(-9, -9.5, Math.Max(1, e + 18), 19), 9.5, 9.5);
                    dc.DrawRoundedRectangle(MotorGrey, null, new Rect(4, 6.5, Math.Max(1, e - 8), 2), 1, 1);                       // graue Fuge
                    dc.DrawLine(Glint, new Point(-5, -8.4), new Point(e + 4, -8.4));
                    Label(e * 0.5, -1.5, 9);
                });
                Local(W(0), sl, () =>
                {
                    dc.DrawRoundedRectangle(MotorGrey, Edge, new Rect(-26, -11, 14, 22), 6, 6);                       // Ausgleich hinten
                    dc.DrawEllipse(Current.Housing, Edge, new Point(), 19, 19);
                    dc.DrawEllipse(null, P("#9AA2AC", 1.2), new Point(), 15, 15);
                });
                break;

            default: // "ur": Cobot – gleiche runde Gelenke mit blauen Deckeln, dünne Rohre
                void Joint(Point p, double r, double scale)
                {
                    Local(p, scale, () =>
                    {
                        dc.DrawEllipse(Current.Arm, Edge, new Point(), r, r);
                        dc.DrawEllipse(UrBlue, null, new Point(), r * 0.72, r * 0.72);
                        dc.DrawEllipse(Glow("#80FFFFFF"), null, new Point(-r * 0.25, -r * 0.3), r * 0.35, r * 0.25);
                    });
                }
                Local(W(e), el, () => { dc.DrawRoundedRectangle(Current.Arm, Edge, new Rect(0, -3.6, Math.Max(1, fl - 10), 7.2), 3.6, 3.6); dc.DrawLine(Glint, new Point(2, -2.8), new Point(fl - 12, -2.8)); });
                Joint(W(d - 16, 0), 6, wl); Joint(W(d - 9, 3), 5.6, wl);
                Gripper(W(d - 3), wl);
                Local(W(0, 9), (sl + el) / 2, () => { dc.DrawRoundedRectangle(Current.Arm, Edge, new Rect(0, -4.2, Math.Max(1, e), 8.4), 4.2, 4.2); dc.DrawLine(Glint, new Point(2, -3.4), new Point(e - 2, -3.4)); });
                Joint(W(e, 4.5), 8.5, el);
                Joint(W(0, 9), 10, sl);
                Joint(W(0), 12.5, sl);
                Local(W(0), sl, () => Label(0, 17, 5.8));
                break;
        }
    }

    /// <summary>Fahrwerk, Lenkrollen, Drehteller, Säulenkopf.</summary>
    public static void TopBase(DrawingContext dc, Point at, double heading, double travel, double u)
    {
        dc.DrawEllipse(TopShadowSoft, null, at + new Vector(6 * u, 8 * u), 66 * u, 62 * u);
        dc.PushTransform(new MatrixTransform(Math.Cos(heading) * u, Math.Sin(heading) * u, -Math.Sin(heading) * u, Math.Cos(heading) * u, at.X, at.Y));
        if (Pro) { TopPlatform(dc, travel); dc.Pop(); return; }
        // Kreuzausleger: vier Holme, an den Enden Lenkrollen (Rad in Fahrtrichtung)
        foreach (double a in new[] { 38.0, 142, 218, 322 })
        {
            double rad = a * Math.PI / 180;
            var end = new Point(Math.Cos(rad) * 50, Math.Sin(rad) * 50);
            dc.PushTransform(new RotateTransform(a));
            dc.DrawRoundedRectangle(TopShadow, null, new Rect(4, -3.5, 48, 9), 3, 3);
            dc.DrawRoundedRectangle(Current.Plate, Edge, new Rect(2, -4.5, 48, 9), 3, 3);
            dc.DrawLine(Glint, new Point(4, -3.6), new Point(48, -3.6));
            dc.Pop();
            // Rolle
            dc.DrawRoundedRectangle(Rubber, Edge, new Rect(end.X - 6, end.Y - 3, 12, 6), 2.5, 2.5);
            double spin = travel * 40 % 12;
            dc.DrawLine(P("#3A3D42", 0.8), new Point(end.X - 6 + spin, end.Y - 3), new Point(end.X - 6 + spin, end.Y + 3));
            dc.DrawEllipse(HubYellow, Edge, end, 2.6, 2.6);
        }
        // Gegengewicht hinten, Klemmkasten seitlich
        dc.DrawRoundedRectangle(TopShadow, null, new Rect(-40, -13, 20, 30), 3, 3);
        dc.DrawRoundedRectangle(Current.Plate, Edge, new Rect(-42, -15, 20, 30), 3, 3);
        dc.DrawLine(Glint, new Point(-40, -13.6), new Point(-24, -13.6));
        // Drehteller und Säulenkopf
        dc.DrawEllipse(TopShadow, null, new Point(3, 4), 26, 26);
        dc.DrawEllipse(Current.Turntable, Edge, new Point(), 26, 26);
        for (int k = 0; k < 12; k++) { double a = k * Math.Tau / 12; dc.DrawEllipse(RivetFill, null, new Point(Math.Cos(a) * 22, Math.Sin(a) * 22), 0.9, 0.9); }
        dc.DrawEllipse(Current.Housing, Edge, new Point(), 18, 18);
        // Statuslicht vorn
        dc.DrawEllipse(AmberGlow, null, new Point(30, 0), 5, 3.5);
        dc.DrawRoundedRectangle(AmberLamp, null, new Rect(28, -1.4, 4, 2.8), 1, 1);
        dc.Pop();
    }

    /// <summary>
    /// Arm von oben in Richtung yaw: Schultergehäuse mit gelben Achtkantkappen, Oberarm bis Ellbogen (Abstand e, Höhe ez),
    /// Unterarm bis zur Klaue (Abstand d, Höhe z). Höhere Teile wirken größer und werfen einen weiter versetzten Schatten.
    /// </summary>
    public static void TopArm(DrawingContext dc, Point at, double yaw, double e, double ez, double d, double z, double jaw, double u)
    {
        double Lift(double h) => 1 + h / 260;                       // leichte Vergrößerung mit der Höhe
        Vector Off(double h) => new Vector(0.16 * h, 0.22 * h) * u;  // Schatten nach unten rechts
        var dir = new Vector(Math.Cos(yaw), Math.Sin(yaw));
        Point W(double along) => at + dir * along * u;
        double shoulderH = 55;
        // Schatten des Arms auf dem Schreibtisch
        var shadow = new StreamGeometry();
        using (var g = shadow.Open())
        {
            var n = new Vector(-dir.Y, dir.X);
            var p0 = W(0) + Off(shoulderH); var p1 = W(e) + Off(ez); var p2 = W(d) + Off(z);
            g.BeginFigure(p0 + n * 7 * u, true, true);
            g.LineTo(p1 + n * 6 * u, true, false); g.LineTo(p2 + n * 4 * u, true, false);
            g.LineTo(p2 - n * 4 * u, true, false); g.LineTo(p1 - n * 6 * u, true, false); g.LineTo(p0 - n * 7 * u, true, false);
        }
        dc.DrawGeometry(ArmShadow, null, shadow);
        if (Pro) { ProArm(dc, at, dir, e, ez, d, z, jaw, u, Lift); return; }

        void Segment(double from, double to, double hFrom, double hTo, double width, Brush fill, Action<double>? detail = null)
        {
            double length = (to - from);
            if (length < 0.5) return;
            double scale = Lift((hFrom + hTo) / 2);
            dc.PushTransform(new MatrixTransform(dir.X * u, dir.Y * u, -dir.Y * u * scale, dir.X * u * scale, W(from).X, W(from).Y));
            dc.DrawRoundedRectangle(fill, Edge, new Rect(-2, -width / 2, length + 4, width), width / 2.2, width / 2.2);
            dc.DrawLine(Glint, new Point(1, -width / 2 + 1), new Point(length + 1, -width / 2 + 1));
            detail?.Invoke(length);
            dc.Pop();
        }
        // Unterarm (unter dem Oberarm, weil tiefer), Klaue
        Segment(e, d - 8, ez, z, 8.5, Tube, l => dc.DrawRoundedRectangle(IronDark, Edge, new Rect(l * 0.6, -5, 3, 10), 1, 1));
        dc.PushTransform(new MatrixTransform(dir.X * u * Lift(z), dir.Y * u * Lift(z), -dir.Y * u * Lift(z), dir.X * u * Lift(z), W(d - 8).X, W(d - 8).Y));
        dc.DrawRoundedRectangle(Tube, Edge, new Rect(-2, -5.5, 8, 11), 2, 2);
        dc.DrawRoundedRectangle(Chrome, Edge, new Rect(5.5, -7, 2.4, 14), 0.8, 0.8);
        double open = 1.5 + 5 * jaw;
        foreach (double side in new[] { -1.0, 1.0 })
            dc.DrawGeometry(Chrome, Edge, G($"M 7.5,{side * (open + 1.4)} L 15,{side * (open + 1.0)} L 16.5,{side * (open - 1.6)} L 14.5,{side * (open - 1.8)} L 13.5,{side * (open - 0.4)} L 7.5,{side * (open - 0.8)} Z"));
        dc.Pop();
        // Ellbogengelenk
        dc.DrawEllipse(IronDark, Edge, W(e), 7.5 * u * Lift(ez), 7.5 * u * Lift(ez));
        dc.DrawEllipse(SteelDisc, null, W(e), 3.6 * u * Lift(ez), 3.6 * u * Lift(ez));
        // Oberarm mit Hydraulikzylinder und Nieten
        Segment(0, e, shoulderH, ez, 14, Current.Arm, l =>
        {
            if (Current.Label.Length > 0)
            {
                var text = new FormattedText(Current.Label, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFace, 8.2, Current.LabelInk, 1);
                dc.DrawText(text, new Point(Math.Max(4, (l - text.Width) / 2 + 3), -text.Height / 2 - 0.3));
                return;
            }
            for (int k = 0; k < 4; k++) dc.DrawEllipse(RivetFill, null, new Point(8 + k * (l - 16) / 3, -3.5), 0.7, 0.7);
            dc.DrawRoundedRectangle(Tube, Edge, new Rect(6, 7, l * 0.5, 3.4), 1.6, 1.6);
            dc.DrawRoundedRectangle(Chrome, null, new Rect(6 + l * 0.5, 7.7, l * 0.3, 2), 1, 1);
        });
        // Schultergehäuse über dem Drehteller, gelbe Achtkantkappen links und rechts
        dc.PushTransform(new MatrixTransform(dir.X * u * Lift(shoulderH), dir.Y * u * Lift(shoulderH), -dir.Y * u * Lift(shoulderH), dir.X * u * Lift(shoulderH), at.X, at.Y));
        dc.DrawRoundedRectangle(Current.Housing, Edge, new Rect(-12, -15, 24, 30), 4, 4);
        dc.DrawLine(Glint, new Point(-10, -14), new Point(10, -14));
        foreach (double side in new[] { -1.0, 1.0 })
        {
            dc.DrawRoundedRectangle(SteelDisc, Edge, new Rect(-8, side > 0 ? 14 : -19, 16, 5), 1.5, 1.5);
            Octagon(dc, new Point(0, side * 20.5), 4.2, Current.Cap);
        }
        dc.Pop();
    }

    /// <summary>Anhänger von oben: Pritsche mit Ladekiste (Latten), vier Räder, Deichsel zur Kupplung, Rücklichter.</summary>
    public static void TopTrailer(DrawingContext dc, Point center, double heading, Point hitch, double u)
    {
        dc.DrawEllipse(TopShadowSoft, null, center + new Vector(6 * u, 8 * u), 48 * u, 36 * u);
        // Deichsel (Weltkoordinaten zur Kupplung)
        var front = center + new Vector(Math.Cos(heading), Math.Sin(heading)) * 36 * u;
        dc.DrawLine(new Pen(Chrome, 3.2 * u) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, front, hitch);
        dc.DrawEllipse(IronDark, Edge, hitch, 3.2 * u, 3.2 * u);
        dc.PushTransform(new MatrixTransform(Math.Cos(heading) * u, Math.Sin(heading) * u, -Math.Sin(heading) * u, Math.Cos(heading) * u, center.X, center.Y));
        foreach (var (x, y) in new[] { (-22.0, -27.0), (22.0, -27.0), (-22.0, 27.0), (22.0, 27.0) })
        {
            dc.DrawRoundedRectangle(Rubber, Edge, new Rect(x - 7, y - 3.5, 14, 7), 3, 3);
            dc.DrawEllipse(HubYellow, Edge, new Point(x, y), 2.2, 2.2);
        }
        dc.DrawRoundedRectangle(TopShadow, null, new Rect(-34, -21, 70, 46), 3, 3);
        dc.DrawRoundedRectangle(Current.Plate, Edge, new Rect(-36, -23, 72, 46), 3, 3);
        // Ladekiste mit Latten (quer), Rahmen
        dc.DrawRoundedRectangle(TopIronDark, Edge, new Rect(-32, -19, 62, 38), 2, 2);
        for (int k = 0; k < 7; k++)
        {
            double x = -30 + k * 9;
            dc.DrawRectangle(Iron, null, new Rect(x, -18, 5.5, 36));
            dc.DrawLine(Glint, new Point(x + 0.5, -17.5), new Point(x + 0.5, 17.5));
        }
        foreach (double y in new[] { -21.0, 21.0 }) dc.DrawEllipse(AmberGlow, null, new Point(-36, y), 4, 3);
        foreach (double y in new[] { -21.0, 21.0 }) dc.DrawRoundedRectangle(AmberLamp, null, new Rect(-37.4, y - 1.6, 2.8, 3.2), 0.8, 0.8);
        dc.Pop();
    }

    // ================================================================ Ту-160М von oben (Länge 100, Nase +50) und Marschflugkörper
    static readonly Brush SwanWhite = V(("#FFFFFF", 0), ("#EEF0F2", 0.35), ("#C9CDD3", 0.8), ("#9EA4AC", 1));
    static readonly Brush SwanShade = V(("#E8EAED", 0), ("#C4C8CE", 0.5), ("#8E949C", 1));
    static readonly Brush Radome = V(("#5A5F66", 0), ("#2C2F34", 0.6), ("#15171A", 1));
    static readonly Brush Canopy = V(("#3C5A78", 0), ("#1A2838", 0.6), ("#0A1018", 1));
    static readonly Brush Afterburner = Glow2("#FFFFF4D8", "#C0FF9A3C"), AfterburnerHalo = Glow("#70FF7A2A");
    static readonly Brush MissileBody = V(("#9AA0A8", 0), ("#6E747C", 0.4), ("#3E434A", 1));
    static readonly Pen SwanEdge = P("#6A7079", 0.35), SwanSeam = P("#A9AEB5", 0.25);
    // Draufsicht exakt nach dem Dreiseitenriss „Tupolev Tu-160 3-view graphic.svg“ (Wikimedia Commons, Mike1979 Russia,
    // CC BY-SA 3.0), Umrisse aus den SVG-Pfaden abgetastet: Länge 100 (Nase +50, Heck −50), y quer.
    // Drehpunkt der Schwenkflügel aus beiden Flügelstellungen errechnet: (−3,3; ±15,75).
    static readonly Point WingPivot = new(-3.3, 15.75);
    // Flügelkontur (Seite y > 0) in beiden Stellungen aus dem Riss, Punkt für Punkt einander zugeordnet:
    // Wurzel innen, Vorderkante Wurzel, Vorderkante Spitze, Randbogen, Hinterkante Spitze, Knick, Hinterkante Wurzel, Wurzel innen.
    static readonly Point[] Wing20 = [new(0, 13.4), new(3.1, 16.8), new(-11.7, 51.3), new(-13.5, 52.1), new(-17.1, 52.7), new(-14.3, 37.3), new(-10.2, 15.6), new(-7, 13.2)];
    static readonly Point[] Wing65 = [new(1.6, 14.6), new(2.1, 16.4), new(-35.2, 33.6), new(-36.8, 33.1), new(-39.7, 31.0), new(-26.8, 22.3), new(-12.6, 12.7), new(-8.6, 11.6)];
    // Rumpf, Flügelwurzel, Gondeln, Heck: halbe Kontur (y ≥ 0), wird gespiegelt
    static readonly Point[] BodyHalf = [new(50.1, 0), new(45.4, 1.3), new(41.2, 2.0), new(32.1, 2.2), new(28.8, 2.9), new(26.8, 3.5), new(19.8, 6.4), new(14, 9.4), new(2.1, 16.4),
        new(-4, 14.7), new(-5, 13.8), new(-6.5, 13.1), new(-8, 12.8), new(-9.7, 12.5), new(-11.6, 11.2), new(-13.4, 8.8), new(-14.8, 6.2), new(-16.2, 4.8),
        new(-33.6, 2.8), new(-41.6, 1.8), new(-49.8, 0.3), new(-49.8, 0)];
    static readonly Point[] StabHalf = [new(-33.6, 2.8), new(-42.5, 11.9), new(-43.3, 12.2), new(-47, 12.3), new(-42.9, 1.6)];
    static readonly Geometry Fuselage = Mirror(BodyHalf);
    static readonly Geometry Stabilizer = Join(StabHalf, true);
    static readonly Geometry GloveLines = G("M 26,-2.2 L -14,-2.2 M 26,2.2 L -14,2.2 M -13.1,0 L -31.6,0.7 M 12,-4.6 L -6,-4.6 M 12,4.6 L -6,4.6 M -3,-9.5 L -10.5,-9.8 M -3,9.5 L -10.5,9.8");
    static readonly Geometry Fin = G("M -31.5,0 L -35.2,-0.45 L -43.6,-0.35 L -46.8,-0.3 L -49.9,0 L -46.8,0.3 L -43.6,0.35 L -35.2,0.45 Z");
    static readonly Geometry Cockpit = G("M 41.6,-1.1 L 40,-1.5 L 40,-0.2 L 41.6,-0.2 Z M 41.6,0.2 L 40,0.2 L 40,1.5 L 41.6,1.1 Z M 39.6,-1.55 L 37.8,-1.75 L 37.8,-0.2 L 39.6,-0.2 Z M 39.6,0.2 L 37.8,0.2 L 37.8,1.75 L 39.6,1.55 Z");
    static readonly Brush NavRedTip = Glow("#E0FF3030"), NavGreenTip = Glow("#E040FF80"), BeaconRed = Glow("#F0FF2020");
    static readonly Brush StarRed = S("#D81E1E");
    static readonly Brush NozzleMetal = V(("#C7CBD1", 0), ("#8C9199", 0.35), ("#5A5F67", 0.7), ("#3A3E44", 1));
    static readonly Brush NacelleUnder = V(("#9AA0A8", 0), ("#7A8088", 0.5), ("#5C626A", 1));
    static readonly Pen StarRim = P("#FFFFFF", 0.35);

    static Geometry Mirror(Point[] half)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(half[0], true, true);
            for (int k = 1; k < half.Length; k++) c.LineTo(half[k], true, true);
            for (int k = half.Length - 2; k >= 1; k--) c.LineTo(new Point(half[k].X, -half[k].Y), true, true);
        }
        g.Freeze();
        return g;
    }

    static Geometry Join(Point[] half, bool both)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
            foreach (double side in both ? new[] { 1.0, -1.0 } : new[] { 1.0 })
            {
                c.BeginFigure(new Point(half[0].X, side * half[0].Y), true, true);
                for (int k = 1; k < half.Length; k++) c.LineTo(new Point(half[k].X, side * half[k].Y), true, true);
            }
        g.Freeze();
        return g;
    }

    static void Star(DrawingContext dc, Point c, double r, double turn)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
            for (int k = 0; k < 10; k++)
            {
                double a = turn - Math.PI / 2 + k * Math.PI / 5;
                double rr = k % 2 == 0 ? r : r * 0.4;
                var p = c + new Vector(Math.Cos(a), Math.Sin(a)) * rr;
                if (k == 0) ctx.BeginFigure(p, true, true); else ctx.LineTo(p, true, false);
            }
        g.Freeze();
        dc.DrawGeometry(StarRed, StarRim, g);
    }

    /// <summary>Ту-160М von oben nach dem Dreiseitenriss; Flügel drehen um den Drehpunkt (Pfeilung 20°…65°).</summary>
    public static void Bomber(DrawingContext dc, Point at, double heading, double sweep, bool bayOpen, double afterburner, double u, double t)
    {
        dc.PushTransform(new MatrixTransform(Math.Cos(heading) * u, Math.Sin(heading) * u, -Math.Sin(heading) * u, Math.Cos(heading) * u, at.X, at.Y));
        // Schwenken: jeder Konturpunkt läuft in Winkel und Abstand um den Drehpunkt von der 20°- zur 65°-Kontur
        double k = Math.Clamp((sweep - 20) / 45, 0, 1);
        Point Blend(Point a20, Point a65, double side)
        {
            var c = WingPivot;
            var v1 = a20 - c; var v2 = a65 - c;
            double r1 = v1.Length, r2 = v2.Length;
            double t1 = Math.Atan2(v1.Y, v1.X), t2 = Math.Atan2(v2.Y, v2.X);
            double d = t2 - t1; while (d > Math.PI) d -= Math.Tau; while (d < -Math.PI) d += Math.Tau;
            double r = r1 + (r2 - r1) * k, t = t1 + d * k;
            return new Point(c.X + Math.Cos(t) * r, side * (c.Y + Math.Sin(t) * r));
        }
        Point W(int index, double side) => Blend(Wing20[index], Wing65[index], side);
        Point Along(int a, int b, double f, double side) { var p = W(a, side); var q = W(b, side); return p + (q - p) * f; }
        Geometry Wing(double side)
        {
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(W(0, side), true, true);
                for (int n = 1; n < Wing20.Length; n++) c.LineTo(W(n, side), true, true);
            }
            g.Freeze();
            return g;
        }
        var left = Wing(-1); var right = Wing(1);
        // Schatten (Reiseflughöhe: weit versetzt)
        dc.PushTransform(new TranslateTransform(12, 20));
        foreach (var g in new[] { left, right, Fuselage, Stabilizer }) dc.DrawGeometry(TopShadow, null, g);
        dc.Pop();
        foreach (var (wing, side) in new[] { (left, -1.0), (right, 1.0) })
        {
            dc.DrawGeometry(SwanShade, SwanEdge, wing);
            // Vorflügel entlang der Vorderkante, Klappen- und Querruderlinie parallel zur Hinterkante
            var le0 = Along(1, 2, 0.12, side); var le1 = Along(1, 2, 0.97, side);
            dc.DrawLine(SwanSeam, le0 + (Along(6, 5, 0.12, side) - le0) * 0.12, le1 + (W(4, side) - le1) * 0.1);
            var te0 = Along(6, 5, 0.15, side); var te1 = W(5, side); var te2 = Along(5, 4, 0.92, side);
            var fwd0 = (Along(1, 2, 0.15, side) - te0) * 0.24; var fwd1 = (Along(1, 2, 0.6, side) - te1) * 0.26;
            dc.DrawLine(SwanSeam, te0 + fwd0, te1 + fwd1);                 // Landeklappe
            dc.DrawLine(SwanSeam, te1 + fwd1, te2 + fwd1 * 0.8);           // Querruder
            dc.DrawLine(SwanSeam, te1, te1 + fwd1);
            Star(dc, Along(2, 5, 0.32, side), 2.0, 0);
            Soft.Draw(dc, side < 0 ? NavRedTip : NavGreenTip, W(3, side), 2.0, 2.0, 1);
        }
        // Triebwerke hängen unter der Flügelwurzel: von oben nur der Gondelrand und die Düsenrohre hinten sichtbar
        foreach (double side in new[] { -1.0, 1.0 })
        {
            dc.DrawRoundedRectangle(NacelleUnder, SwanEdge, new Rect(-19.2, side > 0 ? 5.6 : -12.8, 12, 7.2), 1.6, 1.6);
            foreach (double y in new[] { side * 7.6, side * 10.9 })
            {
                dc.DrawRoundedRectangle(NozzleMetal, SwanEdge, new Rect(-21.4, y - 1.55, 9, 3.1), 1.5, 1.5);
                for (int r = 0; r < 3; r++) dc.DrawLine(SwanSeam, new Point(-20.6 + r * 1.1, y - 1.5), new Point(-20.6 + r * 1.1, y + 1.5));   // Düsenklappen
                dc.DrawEllipse(Radome, null, new Point(-21.4, y), 0.8, 1.35);
                if (afterburner > 0.02)
                {
                    double flick = 1 + 0.15 * Math.Sin(t * 55 + y);
                    Soft.Draw(dc, AfterburnerHalo, new Point(-27, y), 7.5 * afterburner * flick, 2.6, afterburner);
                    Soft.Draw(dc, Afterburner, new Point(-24, y), 3.9 * afterburner * flick, 1.2, 1);
                }
            }
        }
        // Rumpf mit Flügelwurzel (Kontur wie im Riss), Höhenruder, Seitenleitwerk
        dc.DrawGeometry(SwanWhite, SwanEdge, Fuselage);
        dc.DrawGeometry(null, SwanSeam, GloveLines);
        dc.DrawGeometry(SwanShade, SwanEdge, Stabilizer);
        Star(dc, new Point(-44, 9.4), 1.4, 0); Star(dc, new Point(-44, -9.4), 1.4, 0);
        dc.DrawGeometry(SwanWhite, SwanEdge, Fin);
        foreach (double x in new[] { 26.0, 12.0, -24.0, -32.0 }) { double w = x > 0 ? 2.2 : 2.9; dc.DrawLine(SwanSeam, new Point(x, -w), new Point(x, w)); }
        // Radom, Tankstutzen, Cockpit
        dc.DrawGeometry(Radome, null, G("M 50.1,0 C 48.8,-0.5 47.2,-1.0 45.4,-1.3 L 45.4,1.3 C 47.2,1.0 48.8,0.5 50.1,0 Z"));
        dc.DrawLine(P("#8E949C", 0.35), new Point(45, -0.3), new Point(42.2, -0.3));
        dc.DrawGeometry(Canopy, null, Cockpit);
        _ = bayOpen;   // Schacht liegt unten, von oben nicht sichtbar
        Soft.Draw(dc, BeaconRed, new Point(14, 0), (t % 1.2) < 0.15 ? 3.2 : 1.4, (t % 1.2) < 0.15 ? 3.2 : 1.4, 1);
        Soft.Draw(dc, NavWhiteSoft, new Point(-50, 0), 1.4, 1.4, (t % 1) < 0.15 ? 1 : 0.4);
        dc.Pop();
    }
    static readonly Brush NavWhiteSoft = Glow("#E0FFFFFF");

    /// <summary>Marschflugkörper: schlanker Rumpf mit kantiger Spitze, Flügel klappen aus (fold 0…1), Heckflossen, Triebwerksglut.</summary>
    public static void Missile(DrawingContext dc, Point at, Vector dir, double fold, bool burning, double altitude, double u, double t)
    {
        double s = u * (0.85 + 0.3 * altitude);
        dc.PushTransform(new MatrixTransform(dir.X * s, dir.Y * s, -dir.Y * s, dir.X * s, at.X, at.Y));
        if (burning) { Soft.Draw(dc, AfterburnerHalo, new Point(-8.5, 0), 4.5, 1.6, 0.9); Soft.Draw(dc, Afterburner, new Point(-7.4, 0), 2.2, 0.8, 1); }
        double w = 7.5 * fold;
        dc.DrawGeometry(MissileBody, SwanEdge, G($"M 0,-0.6 L -2.2,{-0.6 - w} L -3.4,{-0.6 - w} L -2.2,-0.6 Z M 0,0.6 L -2.2,{0.6 + w} L -3.4,{0.6 + w} L -2.2,0.6 Z"));
        dc.DrawGeometry(MissileBody, SwanEdge, G("M 6.5,0 L 4.5,-0.9 L -6.6,-0.9 L -7.2,0 L -6.6,0.9 L 4.5,0.9 Z"));
        dc.DrawGeometry(MissileBody, null, G("M -5,-0.8 L -6.8,-2.4 L -7.2,-0.8 Z M -5,0.8 L -6.8,2.4 L -7.2,0.8 Z"));
        dc.DrawLine(SwanSeam, new Point(4, -0.4), new Point(-6, -0.4));
        dc.Pop();
    }

    // ================================================================ Explosion (Bildfolge aus werkzeuge/explosion.py)
    static System.Windows.Media.Imaging.BitmapSource[]? explosion;
    internal static System.Windows.Media.Imaging.BitmapSource[] Explosion => explosion ??= LoadExplosion();
    static System.Windows.Media.Imaging.BitmapSource[] LoadExplosion()
    {
        var list = new System.Windows.Media.Imaging.BitmapSource[28];
        for (int i = 0; i < list.Length; i++)
        {
            var image = new System.Windows.Media.Imaging.BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri($"pack://application:,,,/DeskKeeper;component/Assets/explosion-{i:00}.png", UriKind.Absolute);
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            list[i] = image;
        }
        return list;
    }

    // ================================================================ Hilfen
    static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    static Brush S(string hex) { var b = new SolidColorBrush(C(hex)); b.Freeze(); return b; }
    static Pen P(string hex, double w) { var p = new Pen(S(hex), w) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }; p.Freeze(); return p; }
    static Geometry G(string path) { var g = Geometry.Parse(path); g.Freeze(); return g; }
    static Brush V(params (string Color, double At)[] stops) => Grad(new Point(0, 0), new Point(0, 1), stops);
    static Brush H(params (string Color, double At)[] stops) => Grad(new Point(0, 0), new Point(1, 0), stops);
    static Brush Grad(Point a, Point b, (string Color, double At)[] stops)
    {
        var g = new LinearGradientBrush { StartPoint = a, EndPoint = b };
        foreach (var (c, at) in stops) g.GradientStops.Add(new GradientStop(C(c), at));
        g.Freeze();
        return g;
    }
    static Brush R(params (string Color, double At)[] stops)
    {
        var g = new RadialGradientBrush { GradientOrigin = new Point(0.35, 0.3), Center = new Point(0.42, 0.38), RadiusX = 0.62, RadiusY = 0.62 };
        foreach (var (c, at) in stops) g.GradientStops.Add(new GradientStop(C(c), at));
        g.Freeze();
        return g;
    }
    internal static Brush Glow(string center)
    {
        var c = C(center);
        var b = new RadialGradientBrush();
        b.GradientStops.Add(new GradientStop(c, 0));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1));
        b.Freeze();
        return b;
    }
    internal static Brush Glow2(string center, string middle)
    {
        var a = C(center); var m = C(middle);
        var b = new RadialGradientBrush();
        b.GradientStops.Add(new GradientStop(a, 0));
        b.GradientStops.Add(new GradientStop(m, 0.5));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, m.R, m.G, m.B), 1));
        b.Freeze();
        return b;
    }
}
