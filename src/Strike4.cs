using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DeskKeeper;

// ================================================================ Su-35S, MQ-9, SR-71, Х-29Т, Hellfire (20.11.2026; F-22A, J-20 und Su-27 wieder entfernt)

static partial class Planes
{
    // ---------------------------------------------------------------- Су-35С (Länge 21,9 m, Spannweite 15,3 m mit Störsenderbehältern)
    // Neu nach der Dreiseitenansicht „Sukhoi Su-35S“ von the-blueprints.com (Vorschau, 20.11.2026: Vorlagen aus dem ganzen Netz):
    // Draufsicht spaltenweise vermessen, 6,72 px je Einheit. Lange Nase mit dunklem Radom, ogivale Flügelwurzel (LERX), Flügel 45° mit
    // Störsenderbehältern „Хибины“ an den Spitzen, weit auseinanderliegende Triebwerksgondeln mit hellen Hitzeblechen, Pendelleitwerk an
    // den Leitwerksträgern, Seitenleitwerke darüber, langer Heckstachel zwischen den Schubvektordüsen. Dunkles Graublau wie die Vorlage.
    // 100 Einheiten = Länge, Spannweite 69,9.
    static readonly Geometry FlankerBody = Fly.Both("M50,0 L50,0.15 C49.2,0.6 48.6,0.85 47.9,0.9 C46.9,1.3 46.6,1.5 46.1,1.64 C44.8,2 44,2.15 43.2,2.23 C41.5,2.5 40.8,2.6 40.2,2.68 L36,2.98 L31.25,3.27 C28,3.8 26.5,4.1 25.3,4.32 C22,4.8 20.6,5 19.35,5.21 C16,5.7 14.6,5.95 13.4,6.25 C12,6.6 11.2,6.8 10.4,7 C9,7.5 8.2,7.75 7.4,8.04 C6,8.6 5.2,8.9 4.5,9.08 C3.2,9.5 2.2,9.9 1.5,10.1 C0.9,10.6 0.5,11.1 0,12.1 L-1.5,10.3 L-21.6,10.3 L-22.6,8.1 L-41.6,8 L-41.6,2.8 L-42.6,1.6 L-48.8,1.5 C-49.5,1.3 -49.9,0.8 -50.2,0 Z");
    static readonly Geometry FlankerWing = Fly.Both("M0,12.1 L-19.8,33.4 L-28.4,33.3 L-22.2,10.3 L-1.5,10.3 Z");
    static readonly Geometry FlankerPods = Fly.Both("M-11.5,33.6 L-14.5,33.05 L-28.6,33.05 L-28.9,33.6 L-28.6,34.15 L-14.5,34.15 Z");
    static readonly Geometry FlankerTail = Fly.Both("M-23.2,8.04 L-37.5,22.2 L-38.4,22.3 L-42.6,19.8 L-41.4,8.6 Z");
    static readonly Geometry FlankerFins = Fly.Both("M-21.5,8.7 L-24,9.9 L-39.6,9.9 L-40.6,9.3 L-39.6,8.7 Z");
    static readonly Geometry FlankerHeat = Fly.Both("M-23,3.1 L-36.6,3 L-36.6,7.9 L-23,7.9 Z");
    static readonly Geometry FlankerNozzles = Fly.Both("M-36.6,2.9 L-41.6,3.1 L-41.6,7.8 L-36.6,8 Z");
    static readonly Geometry FlankerCanopy = Fly.Both("M33.6,0 C33,1.2 31.5,1.8 29,1.9 L22,1.9 C20.8,1.5 20.1,0.8 19.9,0 Z");
    static readonly Geometry FlankerSeams = Fly.G("M37.5,2.9 L37.5,-2.9 M-1.4,12.9 L-20.9,33.2 M-1.4,-12.9 L-20.9,-33.2 M-19.8,10.4 L-25.9,33.2 M-19.8,-10.4 L-25.9,-33.2 " +
        "M12,2 L-21,2 M12,-2 L-21,-2 M-10,10.3 L-10,6 M-10,-10.3 L-10,-6 M-30,3 L-30,7.9 M-30,-3 L-30,-7.9 M-38.1,3 L-38.1,7.9 M-38.1,-3 L-38.1,-7.9 M-40,3 L-40,7.9 M-40,-3 L-40,-7.9 " +
        "M27,1.9 L27,-1.9 M-6,1 L-9,1 L-9,-1 L-6,-1 Z");
    static readonly Brush FlankerTop = Fly.B("#4A505E"), FlankerShade = Fly.B("#444A57"), FlankerHeatPanel = Fly.B("#6F8B96"), FlankerRadome = Fly.B("#393F4D"),
        FlankerNozzle = Fly.B("#6A6C70"), FlankerDark = Fly.B("#2A2E36"), FlankerGlass = Fly.B("#A9B7C0"), FlankerPod = Fly.B("#555C69");
    static readonly Pen FlankerEdge = Fly.P("#383D48", 0.3), FlankerSeam = Fly.P("#5A6170", 0.2);

    public static void Su35(DrawingContext dc, Point at, double heading, double u, double altitude, double t, double burner = 0.3)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, FlankerWing, FlankerTail, FlankerBody, FlankerPods);
        dc.PushTransform(Fly.Frame(at, heading, u));
        if (burner > 0.02)
            foreach (double y in new[] { -5.45, 5.45 })
            {
                Soft.Draw(dc, Burner, new Point(-46 - 4 * burner, y), 6 * burner + 2, 2.4, 0.8 * burner);
                Soft.Draw(dc, BurnerCore, new Point(-43, y), 2 * burner + 1, 1.5, burner);
            }
        dc.DrawGeometry(FlankerShade, FlankerEdge, FlankerWing);
        dc.DrawGeometry(FlankerTop, FlankerEdge, FlankerBody);
        dc.DrawGeometry(FlankerHeatPanel, null, FlankerHeat);
        dc.DrawGeometry(FlankerNozzle, FlankerEdge, FlankerNozzles);
        dc.DrawGeometry(FlankerShade, FlankerEdge, FlankerTail);
        dc.DrawGeometry(null, FlankerSeam, FlankerSeams);
        dc.DrawGeometry(FlankerTop, FlankerEdge, FlankerFins);
        dc.DrawGeometry(FlankerPod, FlankerEdge, FlankerPods);
        dc.PushClip(new RectangleGeometry(new Rect(37.5, -5, 14, 10)));
        dc.DrawGeometry(FlankerRadome, null, FlankerBody);
        dc.Pop();
        dc.DrawGeometry(FlankerGlass, FlankerEdge, FlankerCanopy);
        foreach (double s in new[] { -1.0, 1.0 }) { Star(dc, new Point(-22.5, s * 27.5), 2.6, StarWhite); Star(dc, new Point(-22.5, s * 27.5), 2.1); }
        dc.Pop();
    }

    // ---------------------------------------------------------------- MQ-9 Reaper (Länge 11 m, Spannweite 20,1 m)
    // Neu nach der Dreiseitenansicht von the-blueprints.com (Vorschau, Maßstab 1:250): Rumpf mit runder Nase und Satellitenkuppel,
    // gerader Flügel großer Streckung (Wurzel 1,1 m, Spitze 0,5 m tief), V-Leitwerk (von oben gesehen), Dreiblatt-Druckpropeller.
    // Hellfire unter den Flügeln ragen vorn über die Vorderkante. 100 Einheiten = Länge, Spannweite 183.
    static readonly Geometry ReaperBody = Fly.Both("M50,0 C50,2.8 48.6,4.6 46,5.3 C44,5.6 41,5.7 38,5.7 L4,5.6 C-8,5.4 -20,4.4 -32,3 C-37,2.4 -40,2 -41.5,1.8 L-41.5,0 Z");
    static readonly Geometry ReaperWing = Fly.Both("M2.3,5.6 L0.2,90.6 C0,91.4 -0.6,91.6 -1.3,91.6 L-3.5,91.6 C-4,91.5 -4.1,91 -4.1,90.6 L-7.4,5.6 Z");
    static readonly Geometry ReaperTail = Fly.Both("M-26.3,3.4 L-28,23.2 C-28.1,23.8 -28.6,24 -29.2,24 L-34.9,24 C-35.5,24 -35.8,23.6 -35.8,23.2 L-36.7,2.8 Z");
    static readonly Geometry ReaperSeams = Fly.G("M-5.4,5.6 L-3.2,88 M-5.4,-5.6 L-3.2,-88 M0.4,40 L-6,40 M0.4,-40 L-6,-40 M-33.6,3 L-34.4,23 M-33.6,-3 L-34.4,-23 M28,5.6 L28,-5.6 M-10,5.4 L-10,-5.4");
    static readonly Brush ReaperTop = Fly.B("#B9BDC1"), ReaperShade = Fly.B("#AAAFB4"), ReaperDome = Fly.B("#C9CDD1"), ReaperDark = Fly.B("#55595E"),
        HellfireBlack = Fly.B("#1E1F1E"), HellfireYellow = Fly.B("#E6C21A"), HellfireGlass = Fly.B("#3B4A5A");
    static readonly Pen ReaperEdge = Fly.P("#7C8186", 0.5), ReaperSeam = Fly.P("#9A9FA5", 0.35), ReaperBlade = Fly.P("#3E4246", 1.1);
    static readonly Brush PropBlur = Fly.B("#38303438");

    /// <param name="hellfires">noch hängende Hellfire (0 … 4): innen links, innen rechts, außen links, außen rechts</param>
    public static void MQ9(DrawingContext dc, Point at, double heading, double u, double altitude, double t, int hellfires = 0)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, ReaperWing, ReaperBody, ReaperTail);
        dc.PushTransform(Fly.Frame(at, heading, u));
        double[] stations = [-19, 19, -27, 27];
        for (int i = 0; i < Math.Min(4, hellfires); i++)
        {   // vorderes Ende der Hellfire vor der Flügelvorderkante
            double y = stations[i];
            dc.DrawRoundedRectangle(HellfireBlack, null, new Rect(0, y - 0.8, 7.6, 1.6), 0.8, 0.8);
            dc.DrawRectangle(HellfireYellow, null, new Rect(4.4, y - 0.8, 0.7, 1.6));
            dc.DrawEllipse(HellfireGlass, null, new Point(7.4, y), 0.6, 0.65);
        }
        dc.DrawGeometry(ReaperShade, ReaperEdge, ReaperTail);
        dc.DrawGeometry(ReaperShade, ReaperEdge, ReaperWing);
        dc.DrawGeometry(ReaperTop, ReaperEdge, ReaperBody);
        dc.DrawGeometry(null, ReaperSeam, ReaperSeams);
        dc.DrawEllipse(ReaperDome, ReaperSeam, new Point(39.5, 0), 7.5, 4.2);   // Satellitenkuppel
        dc.DrawRectangle(ReaperDark, null, new Rect(14, -0.35, 4, 0.7));       // Antenne
        dc.DrawEllipse(ReaperDark, null, new Point(-42.3, 0), 1.9, 1.5);        // Propellernabe
        dc.DrawEllipse(PropBlur, null, new Point(-43, 0), 1.1, 13.5);           // Propellerkreis
        for (int i = 0; i < 3; i++)
        {   // drei Blätter, von oben auf die Drehebene gesehen
            double a = t * 40 + i * Math.Tau / 3;
            dc.DrawLine(ReaperBlade, new Point(-43, 1.4 * Math.Sign(Math.Cos(a))), new Point(-43, 13.5 * Math.Cos(a)));
        }
        dc.Pop();
    }

    // ---------------------------------------------------------------- SR-71A Blackbird (Länge 32,74 m mit Staurohr, Spannweite 16,94 m)
    // Neu ausgemessen : „Lockheed SR-71A 3view.svg“ (Wikimedia Commons, gemeinfrei) als Silhouette geflutet, 15,35 px je
    // Einheit. Rumpf mit Chines bis 5,8 breit, Innenflügel zur Gondel, Gondeln mit Einlaufkegeln (Spitze bei −1,6), Außenflügel mit
    // gerundeter, schräg abgeschnittener Spitze, Heckkegel. 100 Einheiten = Länge, Spannweite 51,8.
    static readonly Geometry BirdBody = Fly.Both("M47.3,0 C47,0.35 46.6,0.6 46,0.8 L44.7,1.5 L43.4,2 L42.1,2.4 L40.7,2.9 L39.4,3.2 L38.1,3.6 L36.8,3.9 L35.5,4.1 L34.2,4.4 L32.9,4.6 L31.6,4.8 L30.3,5 L29,5.2 L26.4,5.4 L23.8,5.6 L21.2,5.7 L0.4,5.8 L-0.9,6.1 L-7.5,9.5 L-9,10 L-41.3,10.5 L-42.6,9.2 L-43.9,8.6 L-44.6,8.2 L-45.2,5.7 L-46.5,1.6 L-47.9,1.3 L-49.2,0.8 L-50,0 Z");
    static readonly Geometry BirdNacelle = Fly.Both("M-7.8,9.8 C-8,9.7 -10,9.6 -12,9.6 L-39,9.7 L-41.2,10.6 L-41.9,11.8 L-41.9,13.4 L-41.2,14.6 L-39,15.6 L-12,15.8 C-10,15.8 -8,15.7 -7.8,15.6 Z");
    static readonly Geometry BirdWing = Fly.Both("M-7.8,15.6 L-8.8,16.1 L-10.1,16.6 L-11.4,17 L-12.7,17.4 L-14,17.7 L-16.6,18.1 L-19.2,18.5 L-21.8,18.7 L-24.4,18.9 L-25.7,19.5 L-27,20.2 L-29.6,21.7 L-32.2,23 L-34.8,24.5 L-37.4,25.8 C-38.2,26 -38.9,26 -39.4,25.7 L-40,25.2 L-41.3,22.5 L-42.6,17.2 L-42.7,17 L-41.3,16.3 L-40,15.7 L-39.4,15.5 Z");
    static readonly Geometry BirdSpike = Fly.Both("M-1.6,12.7 L-7.8,10 L-7.8,12.7 Z");
    static readonly Geometry BirdFins = Fly.Both("M-27,12.5 L-41,12.2 L-41.5,12.7 L-27,12.9 Z");
    static readonly Geometry BirdSeams = Fly.G("M30,2.4 L0,2.4 M30,-2.4 L0,-2.4 M0,5.8 L-40,6.2 M0,-5.8 L-40,-6.2 M-20,18.6 L-41.8,19.5 M-20,-18.6 L-41.8,-19.5 M-28,21 L-41.4,21.6 M-28,-21 L-41.4,-21.6 " +
        "M-34,24 L-40.6,24 M-34,-24 L-40.6,-24 M-12,12.7 L-39,12.7 M-12,-12.7 L-39,-12.7 M-43.9,8.6 L-43.9,-8.6");
    static readonly Brush BirdTop = Fly.B("#1C1F23"), BirdShade = Fly.B("#23272C"), BirdNacelleTone = Fly.B("#282C32"), BirdCone = Fly.B("#3E434A"), BirdGlass = Fly.B("#3A4652");
    static readonly Pen BirdEdge = Fly.P("#34383F", 0.3), BirdSeam = Fly.P("#2E3238", 0.25), BirdProbe = Fly.P("#2A2E33", 0.3);

    public static void SR71(DrawingContext dc, Point at, double heading, double u, double altitude, double t, double burner = 0.6)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, BirdBody, BirdWing, BirdNacelle);
        dc.PushTransform(Fly.Frame(at, heading, u));
        foreach (double y in new[] { -12.7, 12.7 })
        {   // Nachbrenner mit Stoßdiamanten
            Soft.Draw(dc, Burner, new Point(-47 - 5 * burner, y), 8 * burner + 2, 2.2, 0.8 * burner);
            for (int i = 0; i < 3; i++) Soft.Draw(dc, BurnerCore, new Point(-43.5 - i * 3.2, y), 1.1, 0.9, 0.6 * burner);
        }
        dc.DrawGeometry(BirdShade, BirdEdge, BirdWing);
        dc.DrawGeometry(BirdTop, BirdEdge, BirdBody);
        dc.DrawGeometry(BirdNacelleTone, BirdEdge, BirdNacelle);
        dc.DrawGeometry(BirdCone, BirdEdge, BirdSpike);
        dc.DrawGeometry(null, BirdSeam, BirdSeams);
        dc.DrawGeometry(BirdShade, BirdEdge, BirdFins);
        dc.DrawLine(BirdProbe, new Point(47.3, 0), new Point(50, 0));   // Staurohr
        dc.DrawEllipse(BirdGlass, BirdEdge, new Point(32.6, 0), 2.1, 1.2);   // Kabinen: Pilot und Aufklärungsoffizier
        dc.DrawEllipse(BirdGlass, BirdEdge, new Point(28.6, 0), 1.0, 0.95);
        dc.Pop();
    }

    // ---------------------------------------------------------------- Х-29Т (Länge 3,9 m, Ø 0,38 m, Spannweite 1,1 m)
    // Rund schattiert: Fernsehsuchkopf unter Glaskuppel, vier Entenruder vorn mit Ruderfugen, Kabelkanal, Gefechtskopf- und Triebwerksnähte,
    // vier große Trapezflügel hinten mit Querrudern, Düse. 100 Einheiten = Länge.
    static readonly Geometry Kh29Hull = Solid(Fly.Both("M49,0 C49,3.1 47.2,4.9 43.5,4.9 L-48,4.9 L-50,4.2 L-50,0 Z"));
    static readonly Geometry Kh29Fins = Fly.Both("M38,4.9 L33,10 L27.5,10 L28,4.9 Z M-14,4.9 L-36,14.1 L-46,14.1 L-46,4.9 Z");
    static readonly Geometry Kh29Flaps = Fly.Both("M31,5.2 L30,10 L27.5,10 L28,5.2 Z M-40,11.6 L-41.5,14.1 L-46,14.1 L-46,11.6 Z");
    static readonly Geometry Kh29Seams = Fly.G("M44.2,4.9 L44.2,-4.9 M25,4.9 L25,-4.9 M5,4.9 L5,-4.9 M-12,4.9 L-12,-4.9 M-47.5,4.9 L-47.5,-4.9");
    static readonly Geometry Kh29Raceway = Fly.G("M24,-3.9 L-40,-3.9 L-40,-3 L24,-3 Z");
    static readonly Brush Kh29Skin = Round("#5B6655", "#D2DACB", "#A5B09C", 4.9), Kh29Fin = Fly.B("#94A08B"), Kh29Flap = Fly.B("#7E8A76"),
        Kh29Glass = Dome("#141A20", "#A8BED0"), Kh29Race = Fly.B("#B4BEAA"), Kh29Nozzle = Fly.B("#33382F"), Kh29Ink = Fly.B("#2E3530");
    static readonly Pen Kh29Edge = Fly.P("#56614F", 0.55), Kh29Seam = Fly.P("#76826D", 0.35), Kh29Lead = Fly.P("#DDE4D6", 0.45);
    public static void Kh29(DrawingContext dc, Point at, double heading, double u, bool burning = true)
    {
        dc.PushTransform(Fly.Frame(at, heading, u));
        if (burning) { Soft.Draw(dc, Flame, new Point(-62, 0), 18, 6.5, 0.95); Soft.Draw(dc, BurnerCore, new Point(-53, 0), 5, 3, 1); }
        dc.DrawGeometry(Kh29Fin, Kh29Edge, Kh29Fins);
        dc.DrawGeometry(Kh29Flap, null, Kh29Flaps);
        foreach (double s in new[] { -1.0, 1.0 }) dc.DrawLine(Kh29Lead, new Point(-14.5, 5.3 * s), new Point(-35.6, 13.8 * s));
        dc.DrawRectangle(Kh29Nozzle, Kh29Edge, new Rect(-52, -3.4, 2.4, 6.8));
        dc.DrawGeometry(Kh29Skin, Kh29Edge, Kh29Hull);
        dc.DrawGeometry(Kh29Race, Kh29Seam, Kh29Raceway);
        dc.DrawGeometry(null, Kh29Seam, Kh29Seams);
        dc.PushClip(new RectangleGeometry(new Rect(44.2, -6, 6, 12)));
        dc.DrawGeometry(Kh29Glass, Kh29Edge, Kh29Hull);   // Glas folgt der Nase
        dc.Pop();
        dc.DrawEllipse(GlassGlint, null, new Point(46.4, -1.9), 0.8, 1.3);
        dc.Pop();
    }

    // ---------------------------------------------------------------- AGM-114 Hellfire (Länge 1,63 m, Ø 0,178 m, Spannweite 0,33 m)
    // Nach dem Foto der US Air Force (gemeinfrei): rund schattierter schwarzer Körper, drei gelbe Bänder, Glaskuppel mit Reflex,
    // vier kleine Flügel vorn, vier Steuerflossen hinten, Nähte. 100 Einheiten = Länge.
    static readonly Geometry HellfireHull = Solid(Fly.Both("M44,0 C44,4 41.5,5.45 37,5.45 L-50,5.45 L-50,0 Z"));
    static readonly Geometry HellfireFins = Fly.Both("M20,5.45 L17,10.1 L11,10.1 L10,5.45 Z M-36,5.45 L-46,10.1 L-53,10.1 L-50,5.45 Z");
    static readonly Geometry HellfireSeams = Fly.G("M33,5.45 L33,-5.45 M8,5.45 L8,-5.45 M6.8,5.45 L6.8,-5.45 M-20,5.45 L-20,-5.45");
    static readonly Brush HellfireBody = Round("#050606", "#5A5D5A", "#2A2C2A", 5.45), HellfireBand = Round("#7E6A08", "#FFE45A", "#E0BE1E", 5.45),
        HellfireFin = Fly.B("#262826"), HellfireDome = Dome("#18222C", "#B4C8DA");
    static readonly Pen HellfireEdge = Fly.P("#3A3B3A", 0.6), HellfireSeam = Fly.P("#454845", 0.35), HellfireLead = Fly.P("#6A6E6A", 0.45);
    public static void Hellfire(DrawingContext dc, Point at, double heading, double u, bool burning = true)
    {
        dc.PushTransform(Fly.Frame(at, heading, u));
        if (burning) { Soft.Draw(dc, Flame, new Point(-64, 0), 18, 6.5, 0.95); Soft.Draw(dc, BurnerCore, new Point(-55, 0), 5, 3, 1); }
        dc.DrawGeometry(HellfireFin, HellfireEdge, HellfireFins);
        foreach (double s in new[] { -1.0, 1.0 }) { dc.DrawLine(HellfireLead, new Point(19.6, 5.9 * s), new Point(17, 10 * s)); dc.DrawLine(HellfireLead, new Point(-36.5, 5.9 * s), new Point(-46, 10 * s)); }
        dc.DrawGeometry(HellfireBody, HellfireEdge, HellfireHull);
        foreach (double x in new[] { 26.0, -2.0, -36.0 }) dc.DrawRectangle(HellfireBand, null, new Rect(x - 5, -5.45, 5, 10.9));
        dc.DrawGeometry(null, HellfireSeam, HellfireSeams);
        dc.PushClip(new RectangleGeometry(new Rect(37.4, -6, 8, 12)));
        dc.DrawGeometry(HellfireDome, HellfireEdge, HellfireHull);   // Glaskuppel ist die Nase selbst
        dc.Pop();
        dc.DrawEllipse(GlassGlint, null, new Point(40.6, -2.3), 1.0, 1.6);
        dc.DrawLine(HellfireSeam, new Point(37.4, 5.4), new Point(37.4, -5.4));
        dc.Pop();
    }
}

/// <summary>
/// SR-71A Blackbird : fliegt mit Mach 3 quer über alle Bildschirme. Der Machsche Kegel (halber Öffnungswinkel 19,5°)
/// zieht hinter ihm her; wo er ein Symbol erreicht, reißt die Druckwelle es mit und schleudert es aus dem Bild. Kein Feuer.
/// </summary>
sealed class SonicRaid : IAttack
{
    record Shove(Point Start, double Hit, Vector Away, double Spin, double Exit);
    readonly Dictionary<Point, Shove> shoves = [];
    readonly double span, u, speed, enter, lane, x0;
    readonly Rect area;
    readonly int dir;
    // Sichtbare Druckwelle: steiler Keil dicht hinter der Maschine. Der echte Machkegel (Mach 3, 19,5°) erreicht weit seitlich liegende Symbole
    // erst lange nach dem Überflug – 20.11.2026: „когда он вылетел из сцены, потом они все улетают … задержка“. Jetzt fliegen die Symbole
    // der Reihe nach weg, während die Maschine an ihnen vorbeizieht.
    static readonly double Cone = 66 * Math.PI / 180;
    public IReadOnlyList<(Point P, double T)> Blasts { get; }
    public double LastImpact { get; }
    public double End { get; }
    public bool Explodes => false;
    public double Reach { get; }

    public SonicRaid(Rect area, IReadOnlyList<Point> targets, double iconSize, IReadOnlyList<Rect>? screens)
    {
        this.area = area;
        span = Scale.PerMeter(iconSize) * 32.74; u = span / 100;
        Reach = iconSize * 0.3;
        enter = 0.5;
        double wide = screens is { Count: > 0 } ? screens.Max(r => r.Width) : area.Width;
        speed = wide / 1.4;   // eine Bildschirmbreite in 1,4 s
        // Bahn durch die Mitte der Symbole, von der Seite mit mehr Platz
        double cy = targets.Count > 0 ? targets.Average(p => p.Y) : area.Top + area.Height / 2;
        double cx = targets.Count > 0 ? targets.Average(p => p.X) : area.Left + area.Width / 2;
        lane = Math.Clamp(cy, area.Top + span * 0.4, area.Bottom - span * 0.4);
        dir = cx < area.Left + area.Width / 2 ? -1 : 1;
        x0 = dir > 0 ? area.Left - span * 1.5 : area.Right + span * 1.5;
        var blasts = new List<(Point, double)>();
        var rnd = new Random(71);
        double tanCone = Math.Tan(Cone);
        foreach (var p in targets)
        {
            double d = Math.Abs(p.Y - lane);
            // die Nase ist d / tan(Kegel) vor dem Symbol, wenn der Kegel es erreicht
            double hit = Passes(p) + d / tanCone / speed;
            var away = new Vector(dir * 0.55, p.Y >= lane ? 1 : -1); away.Normalize();
            var screen = screens?.FirstOrDefault(r => r.Contains(p)) ?? area;
            if (screen.IsEmpty) screen = area;
            var grown = screen; grown.Inflate(iconSize, iconSize);
            var s = new Shove(p, hit, away, (rnd.NextDouble() - 0.5) * 2, 0);
            double exit = hit;
            while (exit < hit + 6 && grown.Contains(Fling(s, exit))) exit += 0.02;
            shoves[p] = s with { Exit = exit };
            blasts.Add((p, exit));
        }
        Blasts = blasts;
        LastImpact = blasts.Count > 0 ? blasts.Max(b => b.Item2) : enter + 2;
        End = Math.Max(LastImpact + 0.3, enter + (area.Width + span * 3) / speed);
    }

    Point Plane(double t) => new(x0 + dir * speed * (t - enter), lane);
    /// <summary>Zeitpunkt, zu dem die Nase auf Höhe des Punkts ist.</summary>
    internal double Passes(Point p) => enter + (dir * (p.X - x0) - span * 0.5) / speed;
    internal double HitAt(Point p) => shoves.TryGetValue(p, out var s) ? s.Hit : double.NaN;
    internal IEnumerable<Point> Targets => shoves.Keys;
    public IEnumerable<(Func<double, Point> At, double From, double To)> Flights => [(Plane, enter, End)];

    /// <summary>Weg des Symbols nach dem Durchgang der Druckwelle: kurzer Ruck, dann immer schneller vom Kurs weg.</summary>
    static Point Fling(Shove s, double t)
    {
        double q = t - s.Hit;
        if (q <= 0) return s.Start;
        double jolt = Math.Sin(Math.Min(q, 0.18) / 0.18 * Math.PI) * 6;   // Zittern beim Aufprall der Welle
        return s.Start + s.Away * (q * 260 + q * q * 1400) + new Vector(jolt, -jolt * 0.5);
    }

    public Point? Move(Point start, double t) => shoves.TryGetValue(start, out var s) && t > s.Hit ? Fling(s, t) : null;
    public bool DrawsMovedIcons => true;

    static readonly Pen[] ShockWideFade = Enumerable.Range(0, 6).Select(i => Fly.P($"#{(int)(0x30 * (1 - i / 6.5)):X2}E8F0FF", 10)).ToArray(),
        ShockCoreFade = Enumerable.Range(0, 6).Select(i => Fly.P($"#{(int)(0x90 * (1 - i / 6.5)):X2}FFFFFF", 1.6)).ToArray();
    static readonly Brush Vapor = Art.Glow("#80F4F8FF");

    public void Draw(DrawingContext dc, double t)
    {
        if (t < enter || t > End) return;
        var nose = Plane(t) + new Vector(dir * span * 0.5, 0);
        // Machscher Kegel: zwei Linien von der Nase schräg nach hinten bis an den Rand
        double reach = area.Width + area.Height;
        foreach (double s in new[] { -1.0, 1.0 })
        {   // in Abschnitten nach außen schwächer
            var step = new Vector(-dir * Math.Cos(Cone), s * Math.Sin(Cone)) * (reach / 6);
            for (int i = 0; i < 6; i++)
            {   // vorbereitete Stifte statt Deckkraft-Ebenen (schneller)
                dc.DrawLine(ShockWideFade[i], nose + step * i, nose + step * (i + 1));
                dc.DrawLine(ShockCoreFade[i], nose + step * i, nose + step * (i + 1));
            }
        }
        Soft.Draw(dc, Vapor, nose - new Vector(dir * span * 0.15, 0), span * 0.35, span * 0.22, 0.6);   // Dampfkegel um die Nase
        Planes.SR71(dc, Plane(t), dir > 0 ? 0 : Math.PI, u, 1.6, t, 1);
    }
}

/// <summary>
/// MQ-9 Reaper (20.11.2026: eigener Ablauf statt desselben Tiefangriffs): fliegt langsam und hoch ein, kreist über jeder Symbolgruppe,
/// markiert das Ziel mit dem Laser (pulsierender Punkt) und schießt eine Hellfire; die Druckwelle erfasst die Nachbarn der Gruppe.
/// Vier Hellfire an vier Stationen, nach der letzten Abflug.
/// </summary>
sealed class ReaperRaid : IAttack
{
    record Launch(double Fire, double Impact, Point To, int Station, Point From);
    readonly List<Launch> launches = [];
    readonly FlightTrack track;
    readonly double span, u, hu, enter;
    const double Altitude = 1.2;
    public IReadOnlyList<(Point P, double T)> Blasts { get; }
    public IReadOnlyList<double>? BlastSizes { get; }
    public double LastImpact { get; }
    public double End { get; }
    public bool Explodes => true;
    public double Reach { get; }
    internal int Orbits { get; }

    public ReaperRaid(Rect area, IReadOnlyList<Point> targets, double iconSize, IReadOnlyList<Rect>? screens)
    {
        span = Scale.PerMeter(iconSize) * 20.1; u = span / 183;
        hu = Scale.PerMeter(iconSize) * 1.63 / 100 * 2.6;   // Hellfire etwas größer als maßstäblich, sonst kaum sichtbar
        var (aims, radius) = targets.Count > 0 ? Attacks.Groups(targets, iconSize, 4) : ([], 0.0);
        Reach = radius + iconSize * 0.6;
        double speed = span * 1.8;
        double maxTurn = 55 * Math.PI / 180;
        double R = Math.Max(Math.Max(span * 0.9, iconSize * 3.2), speed / (maxTurn * 0.85));   // Kreis mit Reserve zur Drehrate
        const double dt = 1 / 120.0;
        var pts = new List<Point>();
        enter = 0.3;
        var pos = new Point(area.Right + span * 0.8, area.Top + area.Height * 0.3);
        double hd = Math.PI * 0.97, t = enter;
        pts.Add(pos);
        void Step(Vector want)
        {
            double delta = Math.IEEERemainder(Math.Atan2(want.Y, want.X) - hd, Math.Tau);
            hd += Math.Clamp(delta * 1.8, -maxTurn, maxTurn) * dt;
            pos += new Vector(Math.Cos(hd), Math.Sin(hd)) * speed * dt;
            pts.Add(pos); t += dt;
        }
        var open = aims.ToList();
        var blasts = new List<(Point, double)>(); var sizes = new List<double>();
        int station = 0, orbits = 0;
        while (open.Count > 0)
        {
            var here = pos;
            var aim = open.OrderBy(a => (a - here).LengthSquared).First(); open.Remove(aim);
            // Kreismitte so weit ins Bild gerückt, dass der ganze Kreis auf dem Bildschirm des Ziels liegt (Ziele am Rand)
            var scr = screens?.FirstOrDefault(r => r.Contains(aim)) ?? area;
            if (scr.IsEmpty) scr = area;
            var c = new Point(scr.Width > 2.6 * R ? Math.Clamp(aim.X, scr.Left + R * 1.3, scr.Right - R * 1.3) : scr.Left + scr.Width / 2,
                              scr.Height > 2.6 * R ? Math.Clamp(aim.Y, scr.Top + R * 1.3, scr.Bottom - R * 1.3) : scr.Top + scr.Height / 2);
            double swept = 0, last = double.NaN; bool fired = false; double firedAt = 0;
            for (int guard = 0; guard < 120 * 30; guard++)
            {
                var r = pos - c; double d = Math.Max(1e-6, r.Length); var rn = r / d;
                var tangent = new Vector(-rn.Y, rn.X);   // immer gleicher Drehsinn
                Step(tangent + rn * Math.Clamp((R - d) / R * 1.6, -1.6, 1.6));
                if (d < R * 1.35)
                {
                    double a = Math.Atan2(r.Y, r.X);
                    if (!double.IsNaN(last)) swept += Math.Abs(Math.IEEERemainder(a - last, Math.Tau));
                    last = a;
                }
                if (!fired && swept > Math.PI * 0.4)
                {   // Laser liegt seit einer Sekunde auf dem Ziel: Hellfire los
                    fired = true; firedAt = t;
                    double flight = 1.5 + (aim - pos).Length / (span * 8);
                    launches.Add(new Launch(t, t + flight, aim, station, pos));
                    blasts.Add((aim, t + flight)); sizes.Add(1.0);
                    station++;
                }
                if (fired && swept > Math.PI * 0.9 && t - firedAt > 0.6) break;
            }
            orbits++;
        }
        Orbits = orbits;
        // Druckwelle: Nachbarn der Gruppe kurz danach
        foreach (var icon in targets)
        {
            if (blasts.Count == 0) break;
            var nearest = blasts.OrderBy(b => (b.Item1 - icon).LengthSquared).First();
            double d = (nearest.Item1 - icon).Length;
            if (d < iconSize * 0.3) continue;
            blasts.Add((icon, nearest.Item2 + 0.1 + d / (iconSize * 25))); sizes.Add(0.6);
        }
        // Abflug: zur nächsten Kante hinaus
        var outside = area; outside.Inflate(span * 0.9, span * 0.9);
        var exit = new Point(area.Left - span * 3, area.Top - span * 2);
        for (int guard = 0; guard < 120 * 30 && outside.Contains(pos); guard++) Step(exit - pos);
        Blasts = blasts; BlastSizes = sizes;
        LastImpact = blasts.Count > 0 ? blasts.Max(b => b.Item2) : enter + 2;
        End = Math.Max(t, LastImpact + 0.3);
        var list = pts.ToArray(); double t0 = enter;
        Point Plan(double q)
        {
            double f = Math.Clamp((q - t0) / dt, 0, list.Length - 1.001);
            int i = (int)f; return list[i] + (list[i + 1] - list[i]) * (f - i);
        }
        track = new FlightTrack(Plan, enter, End, 60, 60, 0.4);
        // Startpunkte der Hellfire an der Station auf der geglätteten Bahn
        for (int i = 0; i < launches.Count; i++)
        {
            var l = launches[i];
            double y = new[] { -19.0, 19.0, -27.0, 27.0 }[l.Station % 4];
            launches[i] = l with { From = track.At(l.Fire) + Fly.Rot(new Vector(2, y) * u, track.Heading(l.Fire)) };
        }
    }

    internal Point PlaneAt(double t) => track.At(t);
    public IEnumerable<(Func<double, Point> At, double From, double To)> Flights => [(PlaneAt, enter, End)];

    static readonly Brush LaserDot = Art.Glow("#E0FF3020"), LaserCore = Art.Glow("#F0FFE0D0");

    public void Draw(DrawingContext dc, double t)
    {
        foreach (var l in launches)
        {   // Laserpunkt auf dem Ziel: eine Sekunde vor dem Start bis zum Einschlag
            if (t < l.Fire - 1.0 || t > l.Impact) continue;
            double pulse = 0.65 + 0.35 * Math.Sin(t * 18);
            Soft.Draw(dc, LaserDot, l.To, span * 0.05 * pulse + 2, span * 0.05 * pulse + 2, 0.9);
            Soft.Draw(dc, LaserCore, l.To, 1.6, 1.6, pulse);
        }
        foreach (var l in launches)
        {
            if (t < l.Fire || t >= l.Impact) continue;
            double k = (t - l.Fire) / (l.Impact - l.Fire);
            // kurz fallen, dann zünden und mit leichtem Bogen auf das Ziel
            var mid = l.From + (l.To - l.From) * 0.35 + new Vector(0, -span * 0.12);
            double e = Fly.Smooth(k);
            var p = Fly.Bez(l.From, mid, l.To, e); var q = Fly.Bez(l.From, mid, l.To, Math.Max(0, e - 0.02));
            Planes.Rocket(dc, l.From, p, u * 0.6, t);
            Planes.Hellfire(dc, p, Math.Atan2(p.Y - q.Y, p.X - q.X), hu * (1.25 - 0.35 * k), k > 0.06);
        }
        if (t >= enter && t <= End)
        {
            int left = launches.Count(l => l.Fire > t);
            Planes.MQ9(dc, track.At(t), track.Heading(t), u, Altitude, t, 4 - (launches.Count - left));
        }
    }
}
