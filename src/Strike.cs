using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DeskKeeper;

// ================================================================ Draufsichten: Shahed-136, A-10C, Su-25 (19.11.2026: „nah an der Realität“)

static partial class Planes
{
    // ---------------------------------------------------------------- Shahed-136 / Geran-2 (Länge 3,5 m, Spannweite 2,5 m)
    // Zylindrischer Rumpf mit runder Gefechtskopfspitze, Deltaflügel ab gut einem Drittel der Länge (Vorderkante etwa 58° gepfeilt),
    // gestutzte Spitzen mit senkrechten Flossen oben und unten, Zweiblatt-Druckpropeller hinter dem Kolbenmotor, GNSS-Antenne oben.
    static readonly Geometry ShahedBody = Fly.G("M50,0 C50,-3.4 48,-5.2 44,-5.2 L-38,-5.2 L-43,-4 L-46,-3.4 L-46,3.4 L-43,4 L-38,5.2 L44,5.2 C48,5.2 50,3.4 50,0 Z");
    static readonly Geometry ShahedWing = Fly.Both("M14,4.9 L-33.5,35.2 C-34.2,35.7 -35,35.8 -36,35.8 L-41.5,35.8 L-42,34.8 L-41,4.9 Z");
    static readonly Geometry ShahedFins = Fly.Both("M-27,35 L-29,34.6 L-43.6,34.6 L-44.2,35.6 L-43.6,36.6 L-29,36.6 Z");
    static readonly Geometry ShahedSeams = Fly.G("M36,-5.2 L36,5.2 M22,-5.2 L22,5.2 M-30,-5.2 L-30,5.2 M-38,-31.2 L-40,-6 M-38,31.2 L-40,6");
    // Elevons an der Hinterkante, Flügel-Rumpf-Übergang, Kühlschlitze des Zweitaktmotors
    static readonly Geometry ShahedDetail = Fly.G("M-37,-7.5 L-38.6,-31 M-37,7.5 L-38.6,31 M-37,-19 L-41,-19 M-37,19 L-41,19 M14,-5.2 L-28,-5.2 M14,5.2 L-28,5.2 " +
        "M-40,-3 L-40,-1 M-41.2,-3 L-41.2,-1 M-42.4,-3 L-42.4,-1 M-40,3 L-40,1 M-41.2,3 L-41.2,1 M-42.4,3 L-42.4,1 M44,-5.2 L44,5.2");
    static readonly Brush ShahedTop = Fly.B("#2E3134"), ShahedWingFill = Fly.B("#36393D"), ShahedNose = Fly.B("#3E4246"), ShahedDisc = Fly.B("#26383C40");
    static readonly Pen ShahedEdge = Fly.P("#1A1C1E", 0.5), ShahedSeam = Fly.P("#4A4E52", 0.35), ShahedBlade = Fly.P("#1A1C1E", 1.6);

    /// <param name="pitch">0 waagerecht … 1 steiler Sturzflug (von oben verkürzt)</param>
    public static void Shahed(DrawingContext dc, Point at, double heading, double u, double altitude, double t, double pitch = 0)
    {
        double shorten = 1 - 0.45 * Math.Clamp(pitch, 0, 1);
        Fly.ShadowOf(dc, at, heading, u, altitude, ShahedWing, ShahedBody);
        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.PushTransform(new ScaleTransform(shorten, 1));
        dc.DrawGeometry(ShahedWingFill, ShahedEdge, ShahedWing);
        dc.DrawGeometry(ShahedTop, ShahedEdge, ShahedFins);
        dc.DrawGeometry(ShahedTop, ShahedEdge, ShahedBody);
        dc.DrawGeometry(ShahedNose, null, Fly.G("M50,0 C50,-3.4 48,-5.2 44,-5.2 L36,-5.2 L36,5.2 L44,5.2 C48,5.2 50,3.4 50,0 Z"));   // Gefechtskopf
        dc.DrawGeometry(null, ShahedSeam, ShahedSeams);
        dc.DrawEllipse(ShahedNose, ShahedEdge, new Point(10, 0), 2.2, 2.2);   // Satellitennavigation
        dc.DrawRectangle(ShahedDisc, null, new Rect(-48.6, -11, 1.6, 22));   // Propellerkreis
        double a = t * 55;
        dc.DrawLine(ShahedBlade, new Point(-47.8, -10.8 * Math.Sin(a)), new Point(-47.8, 10.8 * Math.Sin(a)));
        dc.DrawGeometry(null, ShahedSeam, ShahedDetail);
        foreach (double s in new[] { -1.0, 1.0 }) dc.DrawRoundedRectangle(ShahedNose, ShahedEdge, new Rect(-37.4, 19 * s - 1, 3.6, 2), 0.6, 0.6);   // Stellmotor der Elevons
        dc.DrawRoundedRectangle(null, ShahedSeam, new Rect(24, -3, 6, 6), 0.8, 0.8);    // Wartungsdeckel Elektronik
        dc.DrawRoundedRectangle(null, ShahedSeam, new Rect(-22, -3.4, 8, 6.8), 0.8, 0.8); // Tankdeckel
        dc.DrawEllipse(ShahedNose, null, new Point(-6, 0), 1.2, 1.2);                     // Tankverschluss
        dc.DrawEllipse(ShahedTop, ShahedEdge, new Point(-47.8, 0), 1.4, 1.4);              // Propellernabe
        dc.Pop(); dc.Pop();
    }

    // ---------------------------------------------------------------- A-10C Thunderbolt II (Länge 16,26 m, Spannweite 17,53 m)
    // Gerader Tiefdecker mit breitem Mittelstück und leicht verjüngten Außenflügeln, hängende Randbögen, Fahrwerksgondeln vor der Vorderkante,
    // zwei TF34 in Gondeln hoch am Hinterrumpf, Höhenleitwerk mit Doppelseitenleitwerk an den Enden, Blasenhaube, GAU-8 in der Nase.
    static readonly Geometry WarthogBody = Fly.G("M50.5,-1 L50.5,1 L47,2.3 L42,3.5 L36,4.1 L20,4.1 L0,4.1 L-12,3.9 L-30,3 L-40,2.2 L-46.5,1.2 L-46.5,-1.2 L-40,-2.2 L-30,-3 L-12,-3.9 L0,-4.1 L20,-4.1 L36,-4.1 L42,-3.5 L47,-2.3 Z");
    static readonly Geometry WarthogWing = Fly.G(Full([(9, 0), (9, 17), (6.2, 49), (5, 51.6), (3.4, 52.6), (-6.6, 52.6), (-7.8, 51.6), (-7.8, 17), (-7.8, 0)]));
    static readonly Geometry WarthogPods = Fly.Both("M14,13.4 L0,13.4 L0,17.4 L14,17.4 C15.6,17.4 16.2,16.4 16.2,15.4 C16.2,14.4 15.6,13.4 14,13.4 Z");
    static readonly Geometry WarthogTail = Fly.Both("M-34.4,0 L-34.8,18 L-43.6,18 L-43.6,0 Z");
    static readonly Geometry WarthogFins = Fly.Both("M-30.5,17.6 C-31.5,16.6 -38,16.5 -45,16.9 L-45,18.3 C-38,18.7 -31.5,18.6 -30.5,17.6 Z");
    static readonly Geometry WarthogPylons = Fly.Both("M-12,3.8 L-24,3.2 L-24,6.4 L-12,6.6 Z");
    static readonly Geometry WarthogCanopy = Fly.G("M38,0 C37.6,-1.8 36,-2.5 33.5,-2.6 L23,-2.4 C22,-1.6 22,1.6 23,2.4 L33.5,2.6 C36,2.5 37.6,1.8 38,0 Z");
    static readonly Geometry WarthogSeams = Fly.G("M-5,-17 L-5,-50 M-5,17 L-5,50 M-40,-1.8 L-40,-18 M-40,1.8 L-40,18 M-44,0 L-46,0 M30,-4.1 L30,4.1 M14,-4.1 L14,4.1 M-20,-3.6 L-20,3.6 M9,-4.1 L9,4.1");
    // Vorflügel innen am Außenflügel, geteilte Querruder (Decelerons), Klappenfugen, Trittflächen, Höhenruder-Trimmklappen, Wartungsdeckel
    static readonly Geometry WarthogDetail = Fly.G("M8.2,-17.4 L6.8,-30 M8.2,17.4 L6.8,30 M-5,-38 L-7.8,-38 M-5,38 L-7.8,38 M-5.6,-44 L-7.6,-51 M-5.6,44 L-7.6,51 " +
        "M7,-5 L7,-12 L-6,-12 L-6,-5 M7,5 L7,12 L-6,12 L-6,5 M-41.5,-6 L-43.6,-6 M-41.5,6 L-43.6,6 M-41.5,-12 L-43.6,-12 M-41.5,12 L-43.6,12 " +
        "M44,-2 L44,2 M24,-4.1 L20,-4.1 M24,4.1 L20,4.1 M8,-1.4 L2,-1.4 L2,1.4 L8,1.4 Z M-6,-2 L-12,-2 L-12,2 L-6,2 Z");
    static readonly Brush Formation = Fly.B("#90B8F0A8");
    static readonly Pen WarthogDarkPen = Fly.P("#272C31", 0.5);
    static readonly Brush WarthogTop = Fly.B("#6F787F"), WarthogShade = Fly.B("#626A71"), WarthogDark = Fly.B("#272C31"), WarthogEngine = Fly.B("#7A838A"),
        WarthogStore = Fly.B("#5E6A52"), Ordnance = Fly.B("#E6E8E4");
    static readonly Pen WarthogEdge = Fly.P("#3E454B", 0.35), WarthogSeam = Fly.P("#586067", 0.25);

    /// <param name="mavericks">wie viele der beiden AGM-65 noch hängen</param>
    public static void A10(DrawingContext dc, Point at, double heading, double u, double altitude, double t, int mavericks = 2)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, WarthogWing, WarthogTail, WarthogBody);
        dc.PushTransform(Fly.Frame(at, heading, u));
        foreach (double s in new[] { -1.0, 1.0 })
        {
            // Außenlasten unter dem Flügel: man sieht nur, was vor bzw. hinter dem Profil hervorschaut
            foreach (double y in new[] { 23.0, 28.0 }) dc.DrawRoundedRectangle(WarthogStore, WarthogEdge, new Rect(-5, y * s - 0.9, 13.5, 1.8), 0.9, 0.9);   // Mk 82
            bool hung = s < 0 ? mavericks >= 2 : mavericks >= 1;
            if (hung)
            {
                dc.DrawRoundedRectangle(Ordnance, WarthogEdge, new Rect(-3, 35 * s - 0.95, 15.4, 1.9), 0.95, 0.95);   // AGM-65 auf LAU-117
                dc.DrawRectangle(Ordnance, null, new Rect(4, 35 * s - 2.2, 2.2, 4.4));
            }
            if (s < 0) dc.DrawRoundedRectangle(WarthogStore, WarthogEdge, new Rect(-6, -44.8, 17, 1.6), 0.8, 0.8);   // Störsender
            else { dc.DrawRoundedRectangle(Ordnance, WarthogEdge, new Rect(-2, 43.4, 11, 0.7), 0.35, 0.35); dc.DrawRoundedRectangle(Ordnance, WarthogEdge, new Rect(-2, 45, 11, 0.7), 0.35, 0.35); }   // AIM-9
        }
        dc.DrawGeometry(WarthogShade, WarthogEdge, WarthogWing);
        dc.DrawGeometry(WarthogShade, WarthogEdge, WarthogPods);
        dc.DrawGeometry(null, WarthogSeam, WarthogSeams);
        dc.DrawGeometry(WarthogShade, WarthogEdge, WarthogTail);
        dc.DrawGeometry(WarthogTop, WarthogEdge, WarthogBody);
        dc.DrawGeometry(WarthogShade, WarthogEdge, WarthogPylons);
        foreach (double s in new[] { -1.0, 1.0 })
        {
            dc.DrawRoundedRectangle(WarthogEngine, WarthogEdge, new Rect(-27, 9.4 * s - 3.9, 19, 7.8), 3.9, 3.9);   // TF34-Gondel
            dc.DrawEllipse(WarthogDark, null, new Point(-8.6, 9.4 * s), 0.9, 3.2);                              // Einlauf
            dc.DrawRectangle(WarthogDark, null, new Rect(-27.4, 9.4 * s - 2.6, 1.2, 5.2));                        // Düse
            dc.DrawLine(WarthogSeam, new Point(-12, 9.4 * s - 3.9), new Point(-12, 9.4 * s + 3.9));
        }
        dc.DrawGeometry(WarthogShade, WarthogEdge, WarthogFins);
        dc.DrawGeometry(WarthogDark, WarthogEdge, WarthogCanopy);
        dc.DrawLine(WarthogSeam, new Point(30, -2.5), new Point(30, 2.5));   // Bügel der Haube
        dc.DrawEllipse(WarthogDark, null, new Point(50.6, -0.6), 0.9, 0.9);    // GAU-8 Avenger (Mündungen, leicht links)
        dc.DrawLine(WarthogSeam, new Point(26, 0), new Point(-28, 0));          // Rumpfrücken
        Soft.Draw(dc, HogRed, new Point(1.8, -53.6), 1.3, 1.3, 1);
        Soft.Draw(dc, HogGreen, new Point(1.8, 53.6), 1.3, 1.3, 1);
        dc.DrawGeometry(null, WarthogSeam, WarthogDetail);
        dc.DrawRoundedRectangle(WarthogDark, WarthogSeam, new Rect(40.6, -0.7, 4.4, 1.4), 0.5, 0.5);   // Luftbetankungsöffnung vor der Haube
        dc.DrawRoundedRectangle(null, WarthogSeam, new Rect(40, -1.2, 5.6, 2.4), 0.8, 0.8);
        foreach (double s in new[] { -1.0, 1.0 })
        {
            dc.DrawEllipse(WarthogEngine, null, new Point(-8.2, 9.4 * s), 0.5, 1.6);   // Fan mit Spinner
            dc.DrawRectangle(Formation, null, new Rect(28, 4.0 * s - 0.2, 4, 0.4));        // Formationslichter
            dc.DrawRectangle(Formation, null, new Rect(-1, 50.6 * s - 0.2, 4, 0.4));
            dc.DrawRectangle(Formation, null, new Rect(-38, 17.6 * s - 0.2, 4, 0.4));
            dc.DrawLine(WarthogDarkPen, new Point(12, 4.4 * s), new Point(9, 4.4 * s));     // Grenzschichtzaun am Rumpf
            dc.DrawLine(WarthogDarkPen, new Point(-34, 3.0 * s), new Point(-36, 3.0 * s));  // APU-Abgas / Ventil
        }
        for (int b = 0; b < 7; b++) dc.DrawEllipse(WarthogEngine, null, new Point(50.6, -0.6) + new Vector(0, 0.55) * Math.Cos(b * Math.Tau / 7) + new Vector(0.2, 0) * Math.Sin(b * Math.Tau / 7), 0.14, 0.14);   // sieben Läufe
        dc.DrawLine(WarthogDarkPen, new Point(14, -0.8), new Point(14, 0.8));            // UHF-/IFF-Blattantennen
        dc.DrawLine(WarthogDarkPen, new Point(-4, -0.8), new Point(-4, 0.8));
        dc.DrawLine(WarthogDarkPen, new Point(-24, -0.6), new Point(-24, 0.6));
        if ((t % 1.2) < 0.1) Soft.Draw(dc, HogStrobe, new Point(-47.6, 17.6), 1.6, 1.6, 1);
        dc.Pop();
    }
    static readonly Brush HogRed = Art.Glow("#C0FF3030"), HogGreen = Art.Glow("#C040FF80"), HogStrobe = Art.Glow("#E0FFFFFF");

    // ---------------------------------------------------------------- Су-25СМ3 (Länge 15,36 m, Spannweite 14,36 m)
    // Draufsicht aus dem Dreiseitenriss „Suchoi Su-25.svg“ (Kaboldy, Wikimedia Commons, CC BY-SA 3.0; vorlage/su25-3view.png) abgetastet
    // (19.11.2026: reale Draufsichten prüfen): schlanker Rumpf (halb 3,3), Triebwerksgondeln erst ab +12,5 und nur bis −25, dahinter der
    // schmale Leitwerksträger; Flügel ab +9 mit 24° Vorderkante, Hinterkante gerade bei −16,6; Spitzenbehälter −4,5…−19,6;
    // Höhenleitwerk −30…−43,5; zwei Staurohre an der Nase, B-8M1, FAB-250 und R-60 unter dem Flügel, Täuschkörperwerfer am Heck.
    static readonly Geometry RookBody = Fly.Both("M50,0 L47,1.2 L42,2.1 L39,2.6 L36,3.0 L33,3.3 L-25,3.3 L-28,2.4 L-44,2.0 L-47,1.5 L-50.5,1.0 L-50.5,0 Z");
    static readonly Geometry RookNacelles = Fly.Both("M12.5,3.3 C12.5,6 12.7,8.3 11.6,8.7 L0,8.7 L-18,8.4 L-22,8 L-25,7.4 L-25.4,3.3 Z");
    static readonly Geometry RookWing = Fly.Both("M6.4,8.5 L-8.3,45.8 L-16.8,45.8 L-16.6,8.5 Z");   // Spitze reicht bis an den Behälter
    static readonly Geometry RookPods = Fly.Both("M-3.6,47.2 C-3.6,46.2 -4.4,45.7 -5.4,45.7 L-19,45.7 L-19.6,47.2 L-19,48.7 L-5.4,48.7 C-4.4,48.7 -3.6,48.2 -3.6,47.2 Z");   // Spitzenbehälter mit geteilter Bremsklappe hinten
    static readonly Geometry RookTail = Fly.Both("M-30,2.2 L-37.6,15.8 L-42,15.8 L-43.6,2.1 Z");
    static readonly Geometry RookFin = Fly.G("M-26,0 C-31,-0.8 -40,-1.1 -46,-0.9 L-50,-0.45 L-50,0.45 L-46,0.9 C-40,1.1 -31,0.8 -26,0 Z");
    static readonly Geometry RookCanopy = Fly.G("M32.4,0 C32.1,-1.8 30.6,-2.6 28.4,-2.7 L21.4,-2.6 C20.8,-1.6 20.8,1.6 21.4,2.6 L28.4,2.7 C30.6,2.6 32.1,1.8 32.4,0 Z");
    static readonly Geometry RookCamo = Fly.G("M3,-12 C0,-18 -6,-17 -9,-22 L-10,-30 L-4,-24 Z M3,12 C0,18 -6,17 -9,22 L-10,30 L-4,24 Z M-6,-34 L-11,-42 L-15,-42 L-13,-34 Z " +
        "M-6,34 L-11,42 L-15,42 L-13,34 Z M18,-3.3 C10,-2 4,1.5 -6,3.3 L-6,-1 C2,-2.6 10,-3.2 18,-3.3 Z M-12,-8.6 C-16,-6 -20,-5.6 -24,-7.4 L-22,-8.3 Z M-12,8.6 C-16,6 -20,5.6 -24,7.4 L-22,8.3 Z");
    // Klappen und Querruder, Vorflügel, Höhen- und Seitenruder, Haubenrahmen, Bremsklappenfuge, Spanten
    static readonly Geometry RookSeams = Fly.G("M-14.6,-10 L-14.8,-43 M-14.6,10 L-14.8,43 M4.2,-11 L-7.8,-42 M4.2,11 L-7.8,42 M-14.6,-28 L-16.6,-28 M-14.6,28 L-16.6,28 " +
        "M-40.4,-2.2 L-41,-15 M-40.4,2.2 L-41,15 M-42,-0.3 L-50,-0.3 M-14,-47.2 L-19.6,-47.2 M-14,47.2 L-19.6,47.2 M27,-2.7 L27,2.7 M14,-3.3 L14,3.3 M-8,-3.3 L-8,3.3 M-28,-2.4 L-28,2.4 " +
        "M2,-8.7 L2,-3.3 M2,8.7 L2,3.3 M-14,-8.5 L-14,-3.3 M-14,8.5 L-14,3.3");
    static readonly Geometry RookDetail = Fly.G("M-0.4,-24.6 L-1.8,-24.2 M-0.4,24.6 L-1.8,24.2 M-4.7,-36 L-6.1,-35.6 M-4.7,36 L-6.1,35.6 M38,-2.4 C34,-2.4 26,-2.6 22,-3.3 M38,2.4 C34,2.4 26,2.6 22,3.3 " +
        "M8,-0.4 L8,0.4 M-18,-0.4 L-18,0.4");
    static readonly Pen RookBoom = Fly.P("#2A2F31", 0.35);
    static readonly Brush RookTop = Fly.B("#7D8468"), RookShade = Fly.B("#717860"), RookDark = Fly.B("#252A2C"), RookPatch = Fly.B("#5A6550"), RookPod = Fly.B("#B4B8AE"), RookBomb = Fly.B("#4F5A44");
    static readonly Pen RookEdge = Fly.P("#3C4234", 0.35), RookSeam = Fly.P("#5E6552", 0.25);

    /// <param name="pods">Raketen je B-8M1 noch an Bord (0…1)</param>
    public static void Su25(DrawingContext dc, Point at, double heading, double u, double altitude, double t, double pods = 1)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, RookWing, RookNacelles, RookTail, RookBody);
        dc.PushTransform(Fly.Frame(at, heading, u));
        foreach (double s in new[] { -1.0, 1.0 })
        {
            foreach (double y in new[] { 13.5, 30.0 }) dc.DrawRoundedRectangle(RookBomb, RookEdge, new Rect(-9.5, y * s - 1.05, 14.3, 2.1), 1.05, 1.05);   // FAB-250
            foreach (double y in new[] { 19.0, 24.5 })
            {
                dc.DrawRoundedRectangle(RookPod, RookEdge, new Rect(-8, y * s - 1.7, 17.9, 3.4), 1.2, 1.2);   // B-8M1, 20 S-8 je Behälter
                if (pods > 0.02) dc.DrawEllipse(RookDark, null, new Point(9.8, y * s), 0.5, 1.3 * Math.Clamp(pods, 0.3, 1));
            }
            dc.DrawRoundedRectangle(Ordnance, RookEdge, new Rect(-10, 37.5 * s - 0.4, 13.7, 0.8), 0.4, 0.4);   // R-60
        }
        dc.DrawGeometry(RookShade, RookEdge, RookWing);
        dc.DrawGeometry(RookShade, RookEdge, RookTail);
        dc.DrawGeometry(RookTop, RookEdge, RookBody);
        dc.DrawGeometry(RookTop, RookEdge, RookNacelles);
        dc.DrawGeometry(RookPatch, null, RookCamo);
        dc.DrawGeometry(RookShade, RookEdge, RookPods);
        dc.DrawGeometry(null, RookSeam, RookSeams);
        foreach (double s in new[] { -1.0, 1.0 })
        {
            dc.DrawRectangle(RookDark, null, new Rect(11.8, s > 0 ? 3.6 : -8.4, 0.9, 4.8));   // Lufteinlauf
            dc.DrawRectangle(RookDark, null, new Rect(-26, s > 0 ? 4.0 : -6.8, 0.9, 2.8));   // Düse
            Art.Star(dc, new Point(-9, 32 * s), 2.0, 0);
        }
        dc.DrawGeometry(RookShade, RookEdge, RookFin);
        dc.DrawRoundedRectangle(RookDark, null, new Rect(-36, -1.9, 6, 0.9), 0.4, 0.4);   // ASO-2V Täuschkörperwerfer
        dc.DrawRoundedRectangle(RookDark, null, new Rect(-36, 1.0, 6, 0.9), 0.4, 0.4);
        dc.DrawGeometry(RookDark, RookEdge, RookCanopy);
        dc.DrawEllipse(RookDark, null, new Point(49.2, 0), 0.8, 0.6);   // Klen-PS Laserfenster
        dc.DrawGeometry(null, RookSeam, RookDetail);
        dc.DrawLine(RookBoom, new Point(44, -1.5), new Point(53, -1.5));   // zwei Staurohre wie im Riss
        dc.DrawLine(RookBoom, new Point(44, 1.5), new Point(53, 1.5));
        dc.DrawLine(RookBoom, new Point(49, -2.1), new Point(49, -0.9));
        dc.DrawLine(RookBoom, new Point(49, 0.9), new Point(49, 2.1));
        dc.DrawLine(RookSeam, new Point(25, -2.6), new Point(25, 2.6));   // Haubenrahmen
        dc.DrawEllipse(RookDark, null, new Point(-50.4, 0), 0.7, 0.7);        // Bremsschirmbehälter
        foreach (double s in new[] { -1.0, 1.0 })
        {
            dc.DrawEllipse(RookPod, null, new Point(-4.6, 47.2 * s), 0.6, 0.6);   // Landescheinwerfer im Spitzenbehälter
            dc.DrawLine(RookBoom, new Point(12.6, 3.6 * s), new Point(12.6, 8.4 * s));   // Grenzschichtplatte am Einlauf
        }
        Soft.Draw(dc, HogRed, new Point(-6, -48.6), 1.2, 1.2, 1);
        Soft.Draw(dc, HogGreen, new Point(-6, 48.6), 1.2, 1.2, 1);
        dc.Pop();
    }

    /// <summary>AGM-65 Maverick im Flug: weißer Körper, Kreuzflügel, Flamme und Rauch.</summary>
    public static void Maverick(DrawingContext dc, Point at, double heading, double u)
    {
        dc.PushTransform(Fly.Frame(at, heading, u));
        Soft.Draw(dc, Flame, new Point(-9.5, 0), 4, 2.2, 0.95);
        dc.DrawRoundedRectangle(Ordnance, WarthogEdge, new Rect(-7.7, -0.95, 15.4, 1.9), 0.95, 0.95);
        dc.DrawRectangle(Ordnance, null, new Rect(-3, -2.2, 2.2, 4.4));
        dc.DrawEllipse(Seeker, null, new Point(7.6, 0), 0.8, 0.8);
        dc.Pop();
    }

    /// <summary>Täuschkörper (Infrarot): heller Glutpunkt mit Rauchschweif.</summary>
    public static void Flare(DrawingContext dc, Point from, Point at, double u, double age, double life)
    {
        double k = Math.Clamp(age / life, 0, 1);
        dc.DrawLine(Soft.FadePen(FlareSmoke.Brush, FlareSmoke.Thickness * Math.Max(0.6, u), 0.7 * (1 - k)), from, at);
        Soft.Draw(dc, FlareGlow, at, (5 - 2 * k) * u, (5 - 2 * k) * u, 1 - k * k);
    }
    static readonly Brush FlareGlow = Art.Glow("#F8FFF2C8");
    static readonly Pen FlareSmoke = Fly.P("#90E6E6E6", 2.2);
}

/// <summary>
/// Shahed-136-Schwarm: Dutzende Kamikazedrohnen kommen langsam und tief vom Bildschirmrand, jede nimmt Kurs auf ein Symbol, schwenkt
/// leicht ein und stürzt sich im letzten Abschnitt steil darauf. Die Einschläge folgen dicht nacheinander, nicht alle auf einmal.
/// </summary>
sealed class ShahedRaid : IAttack
{
    internal record Drone(Point From, Point Bend, Point To, double Start, double Impact);
    internal readonly List<Drone> Drones = [];
    readonly double u;
    const double Dive = 0.7;
    public IReadOnlyList<(Point P, double T)> Blasts { get; }
    public IReadOnlyList<double>? BlastSizes { get; }
    public double LastImpact { get; }
    public double End { get; }
    public bool Explodes => true;
    public double Reach { get; }

    public ShahedRaid(Rect stage, IReadOnlyList<Point> targets, double iconSize)
    {
        double span = Scale.PerMeter(iconSize) * 2.5;   // Spannweite 2,5 m
        u = span / 71.6;
        // höchstens 36 Drohnen: bei mehr Symbolen trifft jede eine Gruppe
        List<Point> aims; double radius;
        if (targets.Count <= 36) { aims = targets.ToList(); radius = 0; }
        else (aims, radius) = Attacks.Groups(targets, iconSize, 36);
        Reach = radius > 0 ? radius + iconSize * 0.4 : iconSize * 0.8;
        var random = new Random(136);
        double speed = Math.Max(stage.Width * 0.16, iconSize * 5.5);
        // Anflug von rechts (Osten), die Reihenfolge der Einschläge läuft von rechts nach links durch, mit Zufall
        var order = aims.OrderByDescending(a => a.X + (random.NextDouble() - 0.5) * stage.Width * 0.25).ToList();
        var blasts = new List<(Point, double)>();
        double first = 2.6;
        for (int i = 0; i < order.Count; i++)
        {
            var to = order[i];
            double impact = first + i * 0.26 + random.NextDouble() * 0.14;
            var from = new Point(stage.Right + span * (1.5 + random.NextDouble() * 2),
                Math.Clamp(to.Y + (random.NextDouble() - 0.5) * stage.Height * 0.5, stage.Top - span, stage.Bottom + span));
            var line = to - from; double length = line.Length;
            var normal = new Vector(-line.Y, line.X); normal.Normalize();
            var bend = from + line * 0.55 + normal * (random.NextDouble() - 0.5) * length * 0.3;
            double flight = length * 1.08 / speed;
            Drones.Add(new Drone(from, bend, to, impact - flight, impact));
            blasts.Add((to, impact));
        }
        // früheste Drohne startet bei 0,2 s
        double shift = Drones.Count > 0 ? Math.Max(0, 0.2 - Drones.Min(d => d.Start)) : 0;
        if (shift > 0)
        {
            for (int i = 0; i < Drones.Count; i++) Drones[i] = Drones[i] with { Start = Drones[i].Start + shift, Impact = Drones[i].Impact + shift };
            blasts = blasts.Select(b => (b.Item1, b.Item2 + shift)).ToList();
        }
        Blasts = blasts;
        BlastSizes = blasts.Select(_ => 1.1).ToList();
        LastImpact = blasts.Count > 0 ? blasts.Max(b => b.Item2) : 2;
        End = LastImpact + 0.3;
    }

    internal static Point PathAt(Drone d, double t)
    {
        double k = Math.Clamp((t - d.Start) / Math.Max(1e-6, d.Impact - d.Start), 0, 1);
        return Fly.Bez(d.From, d.Bend, d.To, k);
    }

    public void Draw(DrawingContext dc, double t)
    {
        foreach (var d in Drones)
        {
            if (t < d.Start || t >= d.Impact) continue;
            var p = PathAt(d, t);
            var a = PathAt(d, t - 0.03); var b = PathAt(d, t + 0.03);
            double heading = Math.Atan2(b.Y - a.Y, b.X - a.X);
            double left = d.Impact - t;
            double dive = left < Dive ? 1 - left / Dive : 0;
            double altitude = 0.55 * (1 - Fly.Smooth(dive));
            Planes.Shahed(dc, p, heading, u, altitude, t + d.Start * 7, Fly.Smooth(dive));
        }
    }
}

/// <summary>
/// Tiefangriff wie im Einsatz (A-10C oder Su-25): je Symbolgruppe ein Anflug entlang der Gruppe, der Feuerstoß wandert von Symbol zu Symbol,
/// danach Abfangen mit Täuschkörpern, Kurve und nächster Anflug. A-10: GAU-8 und je eine AGM-65 auf das erste Symbol größerer Gruppen.
/// Su-25: abwechselnd S-8-Raketensalven aus den B-8M1 und Feuerstöße der GSch-30-2.
/// </summary>
sealed class StrafeRaid : IAttack
{
    public enum Kind { A10, Su25 }
    public enum Weapon { Gun, Rocket, Missile }
    internal record Shot(Weapon Weapon, double Fire, double Impact, Point To, bool Kill);
    record Segment(double From, double To, Func<double, Point> At, double Altitude0, double Altitude1);
    readonly Kind kind;
    readonly List<Segment> path = [];
    internal readonly List<Shot> Shots = [];
    readonly List<(double T, Vector Side)> flares = [];
    readonly double span, u;
    internal int Passes { get; }
    public IReadOnlyList<(Point P, double T)> Blasts { get; }
    public IReadOnlyList<double>? BlastSizes { get; }
    public double LastImpact { get; }
    public double End { get; }
    public bool Explodes => true;
    public double Reach { get; }

    public StrafeRaid(Kind kind, Rect stage, IReadOnlyList<Point> targets, double iconSize)
    {
        this.kind = kind;
        double meters = kind == Kind.A10 ? 17.53 : 14.36;
        span = Scale.PerMeter(iconSize) * meters;   // Spannweite im gemeinsamen Maßstab
        u = span / (kind == Kind.A10 ? 107.8 : 93.4);
        Reach = iconSize * 0.5;
        double speed = span * (kind == Kind.A10 ? 3.8 : 4.4);
        double lead = span * 1.15;   // Schrägentfernung beim Feuern: der Einschlag liegt so weit vor der Nase
        var random = new Random(kind == Kind.A10 ? 10 : 25);
        // Gruppen: höchstens acht Anflüge
        double near = iconSize * 2.4; List<List<Point>> groups;
        while (true) { groups = Gather(targets, near); if (groups.Count <= 6 || near > iconSize * 20) break; near *= 1.3; }
        var pos = new Point(stage.Right + span * 2, stage.Top + stage.Height * 0.3);
        var dir = new Vector(-1, 0.2); dir.Normalize();
        double t = 0.3, alt = 0.9;
        var blasts = new List<(Point, double)>(); var sizes = new List<double>();
        var pending = groups.ToList();
        int pass = 0;
        while (pending.Count > 0)
        {
            var here = pos;
            var g = pending.OrderBy(m => (Mid(m) - here).LengthSquared).First();
            pending.Remove(g);
            var c = Mid(g);
            // Anflugrichtung: Hauptachse der Gruppe (Reihe/Spalte wird der Länge nach bestrichen), vom Flugzeug weg orientiert
            var axis = Axis(g, c - pos);
            var members = g.OrderBy(m => Vector.Multiply(m - c, axis)).ToList();
            double s0 = Vector.Multiply(members[0] - c, axis), s1 = Vector.Multiply(members[^1] - c, axis);
            var runStart = c + axis * (s0 - lead - span * 1.6);
            var fireStart = c + axis * (s0 - lead - iconSize * 0.8);
            var runEnd = c + axis * (s1 - lead + span * 2.2);
            // Kurve zum Anflugbeginn
            double gap = (runStart - pos).Length;
            double turn = Math.Abs(Vector.AngleBetween(dir, axis)) * Math.PI / 180;
            double hop = Math.Max(1.1, (gap * 1.15 + turn * span * 1.2) / speed);
            path.Add(Hermite(t, t + hop, pos, dir * Math.Max(gap, span * 3), runStart, axis * Math.Max(gap, span * 3), alt, 0.32));
            t += hop;
            // Anflug geradeaus
            double run = (runEnd - runStart).Length / speed;
            double t0 = t; var start = runStart;
            path.Add(new Segment(t0, t0 + run, q => start + axis * ((q - t0) * speed), 0.32, 0.32));
            double TimeAt(Point onTrack) => t0 + Vector.Multiply(onTrack - start, axis) / speed;
            bool rockets = kind == Kind.Su25 && pass % 2 == 0 && members.Count >= 2;
            int k0 = 0;
            if (kind == Kind.A10 && members.Count >= 3 && pass < 2)
            {   // AGM-65 auf das erste Symbol: früher abgefeuert, schlägt kurz vor dem Feuerstoß ein
                var target = members[0];
                double fire = TimeAt(c + axis * (s0 - lead * 2.6));
                Shots.Add(new Shot(Weapon.Missile, fire, fire + 1.0, target, true));
                blasts.Add((target, fire + 1.0)); sizes.Add(1.25);
                k0 = 1;
            }
            double flight = rockets ? 0.5 : 0.22;
            double fireFrom = TimeAt(fireStart);
            for (int k = k0; k < members.Count; k++)
            {
                var target = members[k];
                double hit = TimeAt(c + axis * (Vector.Multiply(target - c, axis) - lead)) + flight;
                if (rockets)
                {
                    // je Symbol eine Salve aus vier S-8: drei schlagen ringsum ein, die letzte trifft
                    for (int i = 0; i < 4; i++)
                    {
                        double fire = hit - flight - (3 - i) * 0.05;
                        var to = i == 3 ? target : target + Fly.Rot(new Vector(iconSize * (0.55 + 0.35 * random.NextDouble()), 0), random.NextDouble() * Math.Tau);
                        Shots.Add(new Shot(Weapon.Rocket, fire, fire + flight, to, i == 3));
                    }
                    blasts.Add((target, hit)); sizes.Add(0.9);
                }
                else
                {
                    blasts.Add((target, hit)); sizes.Add(kind == Kind.A10 ? 0.8 : 0.7);
                }
            }
            if (!rockets)
            {   // Feuerstoß: Leuchtspur alle 0,05 s, der Einschlag wandert entlang der Symbole
                var walk = new List<Point> { members[k0] - axis * iconSize * 0.9 };
                walk.AddRange(members.Skip(k0)); walk.Add(members[^1] + axis * iconSize * 0.7);
                double fireEnd = TimeAt(c + axis * (s1 - lead + iconSize * 0.7)) + 0.02;
                fireFrom = TimeAt(c + axis * (Vector.Multiply(walk[0] - c, axis) - lead));
                double step = kind == Kind.A10 ? 0.045 : 0.06;
                for (double f = fireFrom; f <= fireEnd; f += step)
                {
                    var onTrack = start + axis * ((f - t0) * speed) + axis * lead;   // Punkt auf der Spur, auf den die Garbe gerade zeigt
                    var aim = Walk(walk, axis, Vector.Multiply(onTrack - c, axis), c);
                    var to = aim + new Vector((random.NextDouble() - 0.5) * iconSize * 0.5, (random.NextDouble() - 0.5) * iconSize * 0.5);
                    Shots.Add(new Shot(Weapon.Gun, f, f + flight, to, false));
                }
            }
            t = t0 + run;
            pos = runEnd; dir = axis; alt = 0.32;
            // Abfangen: drei Paar Täuschkörper
            for (int i = 0; i < 3; i++) flares.Add((t - 0.3 + i * 0.22, new Vector(-axis.Y, axis.X)));
            // Steigen auf Kurvenhöhe
            var climbEnd = pos + axis * span * 2.4;
            var p0 = pos; double tc = t;
            path.Add(new Segment(tc, tc + span * 2.4 / speed, q => p0 + axis * ((q - tc) * speed), 0.32, 0.85));
            t += span * 2.4 / speed; pos = climbEnd; alt = 0.85;
            pass++;
        }
        Passes = pass;
        var exit = new Point(stage.Left - span * 2, stage.Top - span);
        double outGap = (exit - pos).Length;
        double outHop = Math.Max(1.2, outGap * 1.15 / speed);
        path.Add(Hermite(t, t + outHop, pos, dir * outGap, exit, new Vector(-1, -0.4) * outGap, alt, 1));
        End = t + outHop;
        Blasts = blasts; BlastSizes = sizes;
        LastImpact = blasts.Count > 0 ? blasts.Max(b => b.Item2) : 2;
    }

    static Point Mid(List<Point> g) => new(g.Average(p => p.X), g.Average(p => p.Y));

    /// <summary>Hauptachse der Gruppe; bei einem Symbol die Richtung, aus der das Flugzeug kommt.</summary>
    static Vector Axis(List<Point> g, Vector from)
    {
        var c = Mid(g);
        double xx = g.Sum(p => (p.X - c.X) * (p.X - c.X)), yy = g.Sum(p => (p.Y - c.Y) * (p.Y - c.Y)), xy = g.Sum(p => (p.X - c.X) * (p.Y - c.Y));
        Vector axis;
        if (g.Count < 2 || xx + yy < 1) axis = from.LengthSquared > 1 ? from : new Vector(-1, 0);
        else { double angle = 0.5 * Math.Atan2(2 * xy, xx - yy); axis = new Vector(Math.Cos(angle), Math.Sin(angle)); }
        axis.Normalize();
        if (Vector.Multiply(axis, from) < 0) axis = -axis;
        return axis;
    }

    /// <summary>Punkt der Garbe: entlang des Polygonzugs durch die Symbole, nach der Spurposition s.</summary>
    static Point Walk(List<Point> walk, Vector axis, double s, Point c)
    {
        double S(Point p) => Vector.Multiply(p - c, axis);
        if (s <= S(walk[0])) return walk[0];
        for (int i = 1; i < walk.Count; i++)
        {
            double a = S(walk[i - 1]), b = S(walk[i]);
            if (s <= b) return walk[i - 1] + (walk[i] - walk[i - 1]) * (b - a > 1e-6 ? (s - a) / (b - a) : 1);
        }
        return walk[^1];
    }

    static List<List<Point>> Gather(IReadOnlyList<Point> icons, double r)
    {
        var open = icons.ToList(); var result = new List<List<Point>>();
        while (open.Count > 0)
        {
            var seed = open.OrderByDescending(c => open.Count(o => (o - c).Length <= r)).First();
            var members = open.Where(o => (o - seed).Length <= r).ToList();
            result.Add(members);
            open.RemoveAll(members.Contains);
        }
        return result;
    }

    static Segment Hermite(double from, double to, Point a, Vector da, Point b, Vector db, double alt0, double alt1) => new(from, to, q =>
    {
        double k = Math.Clamp((q - from) / Math.Max(1e-6, to - from), 0, 1), k2 = k * k, k3 = k2 * k;
        return (Point)((Vector)a * (2 * k3 - 3 * k2 + 1) + da * (k3 - 2 * k2 + k) + (Vector)b * (-2 * k3 + 3 * k2) + db * (k3 - k2));
    }, alt0, alt1);

    (Point P, double Alt) State(double t)
    {
        foreach (var s in path)
            if (t <= s.To)
            {
                double k = Fly.Smooth((Math.Max(t, s.From) - s.From) / Math.Max(1e-6, s.To - s.From));
                return (s.At(Math.Max(t, s.From)), s.Altitude0 + (s.Altitude1 - s.Altitude0) * k);
            }
        return (path[^1].At(path[^1].To), path[^1].Altitude1);
    }
    internal Point PlaneAt(double t) => State(t).P;
    double HeadingAt(double t) { var a = State(t - 0.02).P; var b = State(t + 0.02).P; return Math.Atan2(b.Y - a.Y, b.X - a.X); }

    Point Muzzle(Weapon w, double t, int index)
    {
        var at = State(t).P; double h = HeadingAt(t);
        Vector offset = w switch
        {
            Weapon.Gun => kind == Kind.A10 ? new Vector(51, -0.6) : new Vector(36, -3.2),
            Weapon.Missile => new Vector(12, -35),
            _ => new Vector(10, (index % 2 == 0 ? 19 : 24.5) * (index % 4 < 2 ? -1 : 1)),
        };
        return at + Fly.Rot(offset * u, h);
    }

    public void Draw(DrawingContext dc, double t)
    {
        // Einschläge der Kanone und Fehlschüsse am Boden
        foreach (var s in Shots)
        {
            if (t < s.Fire) continue;
            if (t >= s.Impact)
            {
                if (s.Weapon == Weapon.Gun && t - s.Impact < 0.5) Planes.GunHit(dc, s.To, t - s.Impact, u * 1.6);
                else if (s.Weapon == Weapon.Rocket && !s.Kill && t - s.Impact < 0.5) Planes.GunHit(dc, s.To, t - s.Impact, u * 2.6);
            }
        }
        int n = 0;
        foreach (var s in Shots)
        {
            n++;
            if (t < s.Fire || t >= s.Impact) continue;
            double k = (t - s.Fire) / (s.Impact - s.Fire);
            var from = Muzzle(s.Weapon, s.Fire, n);
            var head = from + (s.To - from) * k;
            switch (s.Weapon)
            {
                case Weapon.Gun: Planes.Tracer(dc, from + (s.To - from) * Math.Max(0, k - 0.4), head, u * 1.3); break;
                case Weapon.Rocket: Planes.Rocket(dc, from, head, u * 1.4, t); break;
                case Weapon.Missile:
                    var d = s.To - from;
                    Planes.Rocket(dc, from, head, u * 1.2, t);
                    Planes.Maverick(dc, head, Math.Atan2(d.Y, d.X), u);
                    break;
            }
        }
        var (p, alt) = State(t);
        if (t >= 0.3 && t <= End)
        {
            double heading = HeadingAt(t);
            if (kind == Kind.A10)
            {
                int fired = Shots.Count(s => s.Weapon == Weapon.Missile && s.Fire <= t);
                Planes.A10(dc, p, heading, u, alt, t, 2 - fired);
            }
            else
            {
                int total = Math.Max(1, Shots.Count(s => s.Weapon == Weapon.Rocket));
                Planes.Su25(dc, p, heading, u, alt, t, 1 - Shots.Count(s => s.Weapon == Weapon.Rocket && s.Fire <= t) / (double)total);
            }
            // Mündungsfeuer der Kanone
            if (Shots.Any(s => s.Weapon == Weapon.Gun && t >= s.Fire && t - s.Fire < 0.04))
                Soft.Draw(dc, Flash, Muzzle(Weapon.Gun, t, 0) + Fly.Rot(new Vector(2.5, 0) * u, heading), 7 * u, 5 * u, 0.95);
        }
        // Täuschkörper: paarweise nach links und rechts, fallen zurück und verglühen
        foreach (var (when, side) in flares)
        {
            double age = t - when, life = 1.5;
            if (age < 0 || age > life) continue;
            var origin = State(when).P; var back = origin - State(when - 0.05).P; back.Normalize();
            foreach (double s in new[] { -1.0, 1.0 })
            {
                var at = origin + side * s * (span * 0.5 * Fly.EaseOut(age / 0.8)) + back * (span * 0.6 * age);
                Planes.Flare(dc, origin, at, u * 1.2, age, life);
            }
        }
    }
    static readonly Brush Flash = Art.Glow("#F8FFE6A8");
}
