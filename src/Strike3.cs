using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DeskKeeper;

// ================================================================ МиГ-31К mit Х-47М2 «Кинжал» 

static partial class Planes
{
    // ---------------------------------------------------------------- МиГ-31К (Länge 22,69 m mit Staurohr, Spannweite 13,46 m)
    // Neu ausgemessen (20.11.2026: „не выглядит как МиГ-31“): Draufsicht aus „Mikoyan MiG-31 3-view.svg“ (Wikimedia Commons,
    // CC BY-SA 3.0) als Silhouette geflutet und spaltenweise vermessen, 14,26 px je Einheit. Schlanke lange Nase mit Staurohr, breite
    // Kastenlufteinläufe mit Grenzschichtspalt ab 20 % der Länge, Flügelwurzel-Vorderkante (LERX), Flügel 41°, weit hinten liegendes
    // Pendelleitwerk, das über die Düsen hinausreicht, zwei Seitenleitwerke auf den Triebwerken. 100 Einheiten = Länge, Spannweite 59,5.
    static readonly Geometry FoxhoundBody = Fly.Both("M45.8,0 C45.6,0.2 45.4,0.3 45.1,0.35 C44.6,0.6 44.2,0.78 43.7,0.84 C43.1,1.0 42.7,1.1 42.3,1.19 L40.9,1.54 L39.5,1.89 L38.1,2.1 L36.7,2.38 L35.3,2.52 L33.2,2.81 L31.8,2.95 L30.4,3.09 L20.2,3.09 L20.2,7.2 C20.1,7.6 19.6,7.78 19,7.78 L17.7,7.78 L17,7.55 L1.6,7.36 L-1.9,8.63 L-5.4,9.89 L-6.8,10.6 L-7.6,7.2 L-32.9,7.29 L-44.9,6.6 L-50,6.2 L-50,0 Z");
    static readonly Geometry FoxhoundWing = Fly.Both("M-6.8,10.6 L-23.1,29.73 L-32.9,29.73 L-33.2,29.5 L-29.7,7.08 L-7.6,7.2 Z");
    static readonly Geometry FoxhoundTail = Fly.Both("M-32.9,7.29 L-47.5,19.07 L-50.3,16.9 L-44.9,6.6 Z");
    static readonly Geometry FoxhoundFins = Fly.Both("M-19.4,4.5 L-36,3.9 L-46.8,3.9 L-48.2,4.6 L-46.8,5.6 L-36,5.6 Z");
    static readonly Geometry FoxhoundGap = Fly.Both("M20.2,3.09 L17.4,3.09 L17.4,3.5 L20.2,3.5 Z M20.2,3.5 L19.9,3.5 L19.9,7.3 L20.2,7.2 Z");
    static readonly Geometry FoxhoundNozzles = Fly.Both("M-46.1,0.5 L-50,0.5 L-50,6.2 L-46.1,6.5 Z");
    static readonly Geometry FoxhoundCanopy = Fly.Both("M29.7,0 C29.2,1.2 28.4,1.75 27,1.8 L11,1.8 C10,1.6 9.4,1 9.3,0 Z");
    // Tandemkabine: Frontscheibe, Haube des Piloten, Metallbügel, Haube des Waffensystemoffiziers, Verkleidung dahinter
    static readonly Geometry FoxhoundPanes = Fly.Both("M29.3,0 C28.9,1 28.3,1.45 27.4,1.5 L27.4,0 Z M26.8,0 L26.8,1.55 L20.6,1.55 L20.6,0 Z M18.4,0 L18.4,1.5 L12,1.5 C11.6,1.2 11.4,0.7 11.3,0 Z");
    static readonly Geometry FoxhoundSeams = Fly.G("M32,3 L32,-3 M17,5.4 L1.6,5.4 M17,-5.4 L1.6,-5.4 M9,1.2 L-44,1.2 M9,-1.2 L-44,-1.2 " +
        "M-8,10.9 L-24.2,29.6 M-8,-10.9 L-24.2,-29.6 M-26.6,7.4 L-30.4,29.4 M-26.6,-7.4 L-30.4,-29.4 M-18,7.2 L-18,3.4 M-18,-7.2 L-18,-3.4 " +
        "M-36,9.6 L-46.4,17.8 M-36,-9.6 L-46.4,-17.8 M-46.1,3.5 L-50,3.4 M-46.1,-3.5 L-50,-3.4 M22,1.8 L22,-1.8 M15,1.8 L15,-1.8");
    static readonly Brush FoxhoundTop = Fly.B("#9AAAB6"), FoxhoundShade = Fly.B("#8D9DA9"), FoxhoundRadome = Fly.B("#4D565C"), FoxhoundDark = Fly.B("#2F3438"), FoxhoundGlass = Fly.B("#5E7889"), FoxhoundGlare = Fly.B("#15181A"), FoxhoundFin = Fly.B("#8494A0");   // Farben nach der Hobby-Master-Vorlage МиГ-31К 
    static readonly Pen FoxhoundEdge = Fly.P("#5C6A75", 0.3), FoxhoundSeam = Fly.P("#7C8B97", 0.2), FoxhoundProbe = Fly.P("#3A4046", 0.35), FoxhoundShine = Fly.P("#9FB8C8", 0.35);
    static readonly Brush StarRed = Fly.B("#C8302C");

    /// <param name="kinzhal">Rakete unter dem Rumpf (von oben verdeckt, nur für die Lage)</param>
    /// <param name="burner">0 … 1 Nachbrenner</param>
    public static void MiG31(DrawingContext dc, Point at, double heading, double u, double altitude, double t, bool kinzhal = false, double burner = 0.6)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, FoxhoundWing, FoxhoundTail, FoxhoundBody);
        dc.PushTransform(Fly.Frame(at, heading, u));
        if (burner > 0.02)
            foreach (double y in new[] { -3.4, 3.4 })
            {
                Soft.Draw(dc, Burner, new Point(-54 - 4 * burner, y), 7 * burner + 2, 2.6, 0.85 * burner);
                Soft.Draw(dc, BurnerCore, new Point(-51.5, y), 3 * burner + 1, 1.6, burner);
            }
        dc.DrawGeometry(FoxhoundShade, FoxhoundEdge, FoxhoundTail);
        dc.DrawGeometry(FoxhoundShade, FoxhoundEdge, FoxhoundWing);
        dc.DrawGeometry(FoxhoundTop, FoxhoundEdge, FoxhoundBody);
        dc.DrawGeometry(FoxhoundDark, FoxhoundEdge, FoxhoundNozzles);
        dc.DrawGeometry(FoxhoundDark, null, FoxhoundGap);
        dc.DrawGeometry(null, FoxhoundSeam, FoxhoundSeams);
        dc.DrawGeometry(FoxhoundFin, FoxhoundEdge, FoxhoundFins);
        dc.PushClip(new RectangleGeometry(new Rect(32, -5, 15, 10)));
        dc.DrawGeometry(FoxhoundRadome, null, FoxhoundBody);
        dc.Pop();
        dc.DrawLine(FoxhoundProbe, new Point(45.8, 0), new Point(50, 0));   // Staurohr
        dc.DrawRectangle(FoxhoundDark, null, new Rect(46.3, -0.6, 0.4, 1.2));
        dc.DrawRectangle(FoxhoundGlare, null, new Rect(29.4, -2.9, 2.6, 5.8));   // schwarzes Blendschutzfeld vor der Kabine
        dc.DrawGeometry(FoxhoundTop, FoxhoundEdge, FoxhoundCanopy);
        dc.DrawGeometry(FoxhoundGlass, null, FoxhoundPanes);
        dc.DrawLine(FoxhoundShine, new Point(26, -0.9), new Point(21.5, -0.9));
        dc.DrawLine(FoxhoundShine, new Point(17.6, -0.9), new Point(13, -0.9));
        foreach (double s in new[] { -1.0, 1.0 }) { Star(dc, new Point(-21, s * 21), 3.2, StarWhite); Star(dc, new Point(-21, s * 21), 2.6); }
        dc.Pop();
    }

    static readonly Brush StarWhite = Fly.B("#F4F4F2");
    static void Star(DrawingContext dc, Point c, double r, Brush? fill = null)
    {
        var g = new StreamGeometry();
        using (var o = g.Open())
            for (int i = 0; i < 10; i++)
            {
                double a = -Math.PI / 2 + i * Math.PI / 5, rr = i % 2 == 0 ? r : r * 0.42;
                var p = new Point(c.X + Math.Cos(a) * rr, c.Y + Math.Sin(a) * rr);
                if (i == 0) o.BeginFigure(p, true, true); else o.LineTo(p, true, false);
            }
        g.Freeze();
        dc.DrawGeometry(fill ?? StarRed, null, g);
    }

    // ---------------------------------------------------------------- Х-47М2 «Кинжал» (Länge 7,2 m mit Heckverkleidung, Ø 1,0 m)
    // Nach dem Foto vom Forum Armija-2022 (Wikimedia Commons, CC BY-SA 3.0), 20.11.2026: „выглядит слабо“ – jetzt rund schattiert
    // (Licht von links oben), Spitzbogennase mit dunkler Spitze, Stoßringe, Kabelkanal, Beschriftungsfelder, vier große gepfeilte Flossen
    // mit Ruderklappen und heller Vorderkante, Düse mit Strahlrudern; dunklere Heckverkleidung, die beim Zünden abfällt.
    // 100 Einheiten = Länge mit Verkleidung.
    static readonly Geometry KinzhalHull = Solid(Fly.Both("M50,0 C49.2,0.9 47,2.9 43,4.7 C39,6.1 36,6.75 33,6.9 L-24,6.9 L-24,0 Z"));
    static readonly Geometry KinzhalCone = Solid(Fly.Both("M50,0 C49.2,0.9 47,2.9 43,4.7 C39,6.1 36,6.75 33,6.9 L33,0 Z"));
    static readonly Geometry KinzhalTip = Fly.Both("M50,0 C49.6,0.5 48.9,1.2 47.6,1.9 L47.6,0 Z");
    static readonly Geometry KinzhalFins = Fly.Both("M-5,6.9 L-17,17 L-21.5,17 L-24,6.9 Z");
    static readonly Geometry KinzhalFlaps = Fly.Both("M-19.6,9.2 L-20.8,16.9 L-21.5,17 L-22.4,10.6 Z");
    static readonly Geometry KinzhalFairing = Solid(Fly.Both("M-24,6.9 L-45,6.7 C-47.5,6.3 -49.3,5.2 -50,3.6 L-50,0 L-24,0 Z"));
    static readonly Geometry KinzhalNozzle = Fly.Both("M-24,4.6 L-26.6,4 L-26.6,0 L-24,0 Z");
    static readonly Geometry KinzhalRings = Fly.G("M33,6.9 L33,-6.9 M18,6.9 L18,-6.9 M17.2,6.9 L17.2,-6.9 M3,6.9 L3,-6.9 M-11,6.9 L-11,-6.9 M-23.4,6.9 L-23.4,-6.9");
    static readonly Geometry KinzhalRaceway = Fly.G("M31,-5.1 L-21,-5.1 L-21,-4.2 L31,-4.2 Z");
    static readonly Geometry KinzhalStencil = Fly.G("M12,1.6 L8,1.6 L8,2.3 L12,2.3 Z M12,3 L9.5,3 L9.5,3.6 L12,3.6 Z M-2,-1.8 L-7,-1.8 L-7,-1.2 L-2,-1.2 Z");
    static readonly Brush KinzhalBody = Round("#7C8388", "#E2E6E8", "#B9BFC3", 6.9), KinzhalNose = Round("#5D6469", "#B4BBBF", "#8E959A", 6.9),
        KinzhalTail = Round("#5F666B", "#A7AEB2", "#858C91", 6.9), KinzhalFin = Fly.B("#9AA1A6"), KinzhalFlap = Fly.B("#868D92"),
        KinzhalDark = Fly.B("#3A3F43"), KinzhalRace = Fly.B("#A3AAAE"), KinzhalInk = Fly.B("#2E3236");
    static readonly Pen KinzhalEdge = Fly.P("#5F6669", 0.5), KinzhalRing = Fly.P("#6E757A", 0.35), KinzhalLead = Fly.P("#E4E8EA", 0.45);
    static readonly Brush Plasma = Art.Glow("#C0FF9EE8"), PlasmaCore = Art.Glow("#F0FFF2FA"), Burner = Art.Glow("#B0FF8A2A"), BurnerCore = Art.Glow("#F0FFE7B0");

    /// <summary>Rund schattierter Körper (Draufsicht eines Rotationskörpers): dunkle Ränder, Glanzlicht links oben.</summary>
    static Brush Round(string edge, string light, string mid, double r)
    {
        var b = new LinearGradientBrush { MappingMode = BrushMappingMode.Absolute, StartPoint = new Point(0, -r), EndPoint = new Point(0, r) };
        b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(edge), 0));
        b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(light), 0.3));
        b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(mid), 0.62));
        b.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(edge), 1));
        b.Freeze(); return b;
    }
    /// <summary>Beide Hälften zu einer Fläche vereinigt: Umriss ohne Naht in der Mittellinie.</summary>
    static readonly Brush GlassGlint = Fly.B("#70E8F2FF");
    static Geometry Solid(Geometry g) { var r = Geometry.Combine(g, Geometry.Empty, GeometryCombineMode.Union, null); r.Freeze(); return r; }
    /// <summary>Glaskuppel eines Suchkopfs: dunkel mit Lichtreflex.</summary>
    static Brush Dome(string dark, string light)
    {
        var b = new RadialGradientBrush((Color)ColorConverter.ConvertFromString(light), (Color)ColorConverter.ConvertFromString(dark)) { GradientOrigin = new Point(0.62, 0.3), Center = new Point(0.55, 0.42), RadiusX = 0.6, RadiusY = 0.6 };
        b.Freeze(); return b;
    }

    /// <param name="plasma">0 … 1 Plasmahülle im Hyperschallflug</param>
    /// <param name="fairing">Heckverkleidung noch dran (vor dem Zünden)</param>
    public static void Kinzhal(DrawingContext dc, Point at, double heading, double u, double altitude, bool burning, double plasma, double t, bool fairing = false)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, KinzhalHull, KinzhalFins);
        dc.PushTransform(Fly.Frame(at, heading, u));
        if (burning)
        {
            double flicker = 0.85 + 0.15 * Math.Sin(t * 47);
            Soft.Draw(dc, Burner, new Point(-46, 0), 24 * flicker, 9, 0.9);
            Soft.Draw(dc, BurnerCore, new Point(-31, 0), 8, 4.6, 1);
        }
        if (plasma > 0.02)
        {
            Soft.Draw(dc, Plasma, new Point(18, 0), 70, 22, 0.7 * plasma);
            Soft.Draw(dc, PlasmaCore, new Point(48, 0), 16, 11, plasma);
        }
        dc.DrawGeometry(KinzhalFin, KinzhalEdge, KinzhalFins);
        dc.DrawGeometry(KinzhalFlap, null, KinzhalFlaps);
        foreach (double s in new[] { -1.0, 1.0 }) dc.DrawLine(KinzhalLead, new Point(-5.4, 7.3 * s), new Point(-16.8, 16.6 * s));   // helle Vorderkante
        if (fairing) dc.DrawGeometry(KinzhalTail, KinzhalEdge, KinzhalFairing);
        else dc.DrawGeometry(KinzhalDark, KinzhalEdge, KinzhalNozzle);
        dc.DrawGeometry(KinzhalBody, KinzhalEdge, KinzhalHull);
        dc.DrawGeometry(KinzhalNose, null, KinzhalCone);
        dc.DrawGeometry(KinzhalDark, null, KinzhalTip);
        dc.DrawGeometry(KinzhalRace, KinzhalRing, KinzhalRaceway);
        dc.DrawGeometry(null, KinzhalRing, KinzhalRings);
        if (fairing) dc.DrawLine(KinzhalRing, new Point(-38, 6.8), new Point(-38, -6.8));
        dc.Pop();
    }

    /// <summary>Abgeworfene Heckverkleidung: taumelt zurück und fällt.</summary>
    public static void KinzhalFairingPart(DrawingContext dc, Point at, double heading, double u, double altitude)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, KinzhalFairing);
        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawGeometry(KinzhalTail, KinzhalEdge, KinzhalFairing);
        dc.Pop();
    }
}

/// <summary>
/// МиГ-31К mit Х-47М2 «Кинжал» : je dichte Symbolgruppe ein Abfangjäger, hoch und schnell von links. Er klinkt die Rakete
/// vor der Gruppe aus, sie fällt kurz, zündet, steigt mit Feuerstrahl und wird hyperschallschnell (Plasmahülle), stürzt dann fast senkrecht
/// auf die Gruppe: ein schwerer Einschlag, die Druckwelle setzt die Nachbarn in Brand. Der МиГ-31К dreht mit Nachbrenner weich ab.
/// </summary>
sealed class KinzhalRaid : IAttack
{
    record Sortie(FlightTrack Track, double Enter, double Release, double Ignite, double Impact, Point Drop, Vector Dir, Point Target, double Seed);
    readonly List<Sortie> sorties = [];
    readonly double span, u, mu, speed;
    public IReadOnlyList<(Point P, double T)> Blasts { get; }
    public IReadOnlyList<double>? BlastSizes { get; }
    public double LastImpact { get; }
    public double End { get; }
    public bool Explodes => true;
    public double Reach { get; }

    public KinzhalRaid(Rect stage, IReadOnlyList<Point> targets, double iconSize, IReadOnlyList<Rect>? screens = null)
    {
        span = Scale.PerMeter(iconSize) * 22.69; u = span / 100;
        mu = Scale.PerMeter(iconSize) * 7.2 / 100;
        double wide = screens is { Count: > 0 } ? screens.Max(r => r.Width) : stage.Width;   // nach Bildschirm, nicht nach allen zusammen
        speed = Math.Max(wide * 0.3, span * 4);
        var (aims, radius) = targets.Count > 0 ? Attacks.Groups(targets, iconSize, 4) : ([], 0.0);
        Reach = radius + iconSize * 0.6;
        var rnd = new Random(31);
        double t = 0.3;
        var blasts = new List<(Point, double)>();
        foreach (var target in aims.OrderBy(a => a.X))
        {
            // Anflug von links in Höhe der Gruppe, leicht schräg; Abwurf ein gutes Stück vor der Gruppe
            // von der Seite mit mehr Platz anfliegen, damit der Abwurf gut sichtbar vor der Gruppe liegt; Bahn in Höhe der Gruppe
            // innerhalb des Bildschirms der Gruppe (nicht durch Flächen ohne Bildschirm)
            var screen = screens?.FirstOrDefault(r => r.Contains(target)) ?? stage;
            if (screen.IsEmpty) screen = stage;
            bool fromRight = target.X < screen.Left + screen.Width * 0.5;
            double lane = Math.Clamp(target.Y + (rnd.NextDouble() < 0.5 ? -1 : 1) * iconSize * (2 + rnd.NextDouble() * 2), screen.Top + span * 0.6, screen.Bottom - span * 0.6);
            var from = new Point(fromRight ? screen.Right + span * 2 : screen.Left - span * 2, lane);
            var dir = new Vector(fromRight ? -1 : 1, 0);
            double lead = Math.Min(screen.Width * 0.62, Math.Abs(target.X - from.X) - span * 2.5);
            double releaseX = target.X - dir.X * lead;
            double enter = t, release = enter + Math.Abs(releaseX - from.X) / speed;
            double turnSign = lane < stage.Top + stage.Height / 2 ? -1 : 1;   // zur nahen Kante abdrehen
            Point Plan(double q)
            {
                double d = (q - enter) * speed, straight = (release + 0.6 - enter) * speed;
                if (d <= straight) return from + dir * d;
                // Abdrehen: Kreisbogen (Radius aus 22°/s) und dann geradeaus hinaus
                double r = speed / (22 * Math.PI / 180), a0 = Math.Atan2(dir.Y, dir.X), arc = Math.Min(d - straight, r * 1.2);
                var start = from + dir * straight;
                var center = start + Fly.Rot(dir, turnSign * Math.PI / 2) * r;
                double a = a0 - turnSign * Math.PI / 2 + turnSign * arc / r;
                var onArc = center + new Vector(Math.Cos(a), Math.Sin(a)) * r;
                if (d - straight <= r * 1.2) return onArc;
                double h = a0 + turnSign * 1.2;
                return onArc + new Vector(Math.Cos(h), Math.Sin(h)) * (d - straight - r * 1.2);
            }
            double gone = release + 0.6 + (stage.Width + stage.Height) / speed;
            var track = new FlightTrack(Plan, enter, gone, 60, 45, 0.4);
            var drop = track.At(release);
            double ignite = release + 0.4;
            double flight = 2.4 + (target - drop).Length / Math.Max(1, stage.Width * 1.4);   // länger: Steigen, Gleiten, sichtbarer Sturz
            sorties.Add(new Sortie(track, enter, release, ignite, ignite + flight, drop, dir, target, rnd.NextDouble()));
            blasts.Add((target, ignite + flight));
            t += 2.2;
        }
        var sizes = blasts.Select(_ => 1.9).ToList();
        // Druckwelle: läuft vom Einschlag nach außen und erfasst jedes Symbol der Gruppe mit kurzer Verzögerung (kleine Folgeexplosionen)
        foreach (var icon in targets)
        {
            var nearest = blasts.OrderBy(b => (b.Item1 - icon).LengthSquared).FirstOrDefault();
            double d = (nearest.Item1 - icon).Length;
            if (blasts.Count == 0 || d < iconSize * 0.3) continue;
            blasts.Add((icon, nearest.Item2 + 0.08 + d / (iconSize * 30))); sizes.Add(0.55);
        }
        Blasts = blasts; BlastSizes = sizes;
        LastImpact = blasts.Count > 0 ? blasts.Max(b => b.Item2) : 2;
        End = Math.Max(LastImpact + 0.5, sorties.Count > 0 ? sorties.Max(s => s.Release + 0.6 + 4.5) : 2);
    }

    public IEnumerable<(Func<double, Point> At, double From, double To)> Flights => sorties.Select(s => ((Func<double, Point>)s.Track.At, s.Enter, s.Release + 6));

    /// <summary>Lage der Rakete (20.11.2026: „так себе … влетает в цель“): Fall nach dem Ausklinken mit Heckverkleidung, Zünden
    /// (Verkleidung fällt ab), steiler Steigflug, Gleitbogen in großer Höhe bis über das Ziel, dann fast senkrechter Sturz: die Lage über
    /// dem Boden bleibt auf dem Ziel, die Höhe fällt rasch, der Schatten läuft unter der Rakete zusammen, von oben wirkt sie verkürzt.</summary>
    (Point Pos, double Heading, double Alt, bool Burning, double Plasma, double Pitch) MissileAt(Sortie s, double t)
    {
        var v = s.Dir * speed;
        if (t < s.Ignite)
        {
            double k = t - s.Release;
            return (s.Drop + v * k * 0.95, Math.Atan2(s.Dir.Y, s.Dir.X), 0.9 - 0.25 * k, false, 0, 1);
        }
        var start = s.Drop + v * (s.Ignite - s.Release) * 0.95;
        double q = Math.Clamp((t - s.Ignite) / (s.Impact - s.Ignite), 0, 1);
        const double over = 0.62;   // ab hier stürzt die Rakete; über Grund läuft sie dabei immer langsamer auf das Ziel zu
        double horizontal = Fly.Smooth(Math.Min(1, q / 0.93));
        var c1 = start + s.Dir * Math.Min((s.Target - start).Length * 0.45, speed * 0.6);
        var p = Fly.Bez(start, c1, s.Target, horizontal);
        var pb = Fly.Bez(start, c1, s.Target, Math.Max(0, Math.Min(horizontal, 0.995) - 0.01));
        double heading = (p - pb).LengthSquared > 1e-6 ? Math.Atan2(p.Y - pb.Y, p.X - pb.X) : Math.Atan2(s.Target.Y - start.Y, s.Target.X - start.X);
        double alt = q < 0.4 ? 0.65 + 0.95 * Fly.Smooth(q / 0.4) : q < over ? 1.6 - 0.15 * Fly.Smooth((q - 0.4) / (over - 0.4)) : 1.45 * (1 - Fly.EaseIn((q - over) / (1 - over)));
        double plasma = Fly.Smooth((q - 0.3) / 0.25);
        double pitch = 1 - 0.7 * Fly.Smooth((q - over) / 0.25);   // Nase nach unten: Körper erscheint kürzer
        return (p, heading, alt, q < 0.62, plasma, pitch);
    }

    static readonly Pen TrailWide = Fly.P("#30F2F2F5", 7), TrailCore = Fly.P("#90FFFFFF", 1.8);
    static readonly Brush ImpactFlash = Art.Glow("#F8FFFFF4"), ImpactFire = Art.Glow("#E0FFB050");
    static readonly Brush ShockRing = Fly.B("#E8FFFFFF"), DustRing = Fly.B("#B08A7A66"), DiveShadow = Art.Glow("#58000000");

    public void Draw(DrawingContext dc, double t)
    {
        foreach (var s in sorties)
        {
            if (t >= s.Ignite && t < s.Impact)
            {   // Rauchspur (im Sturz nur noch kurz: die Rakete kommt von oben)
                var trail = new StreamGeometry();
                using (var g = trail.Open())
                {
                    bool first = true;
                    for (double q = Math.Max(s.Ignite, t - 1.1); q <= t; q += 0.03)
                    {
                        var p = MissileAt(s, q).Pos;
                        if (first) { g.BeginFigure(p, false, false); first = false; } else g.LineTo(p, true, true);
                    }
                }
                trail.Freeze();
                dc.DrawGeometry(null, TrailWide, trail); dc.DrawGeometry(null, TrailCore, trail);
            }
            if (t >= s.Ignite && t < s.Ignite + 1.4)
            {   // abgeworfene Heckverkleidung taumelt zurück und fällt
                double k = t - s.Ignite;
                var at = s.Drop + s.Dir * speed * ((s.Ignite - s.Release) * 0.95 + k * 0.45) - s.Dir * mu * 30;
                Planes.KinzhalFairingPart(dc, at, Math.Atan2(s.Dir.Y, s.Dir.X) + k * 3.2, mu * (1 - 0.3 * k), 0.55 - 0.35 * k);
            }
            if (t >= s.Release && t < s.Impact)
            {
                var (pos, heading, alt, burning, plasma, pitch) = MissileAt(s, t);
                double size = mu * 1.55 * (0.8 + 0.35 * alt);   // etwas größer als maßstäblich, sonst kaum zu sehen
                if (pitch < 0.98)
                {   // im Sturz verkürzt: Länge mit der Neigung stauchen; Schatten in Bildkoordinaten, läuft mit fallender Höhe unter die Rakete
                    double lift = alt * 100 * size;
                    Soft.Draw(dc, DiveShadow, pos + new Vector(lift * 0.16, lift * 0.26), size * 40 * pitch + size * 8, size * 9, 0.9);
                    dc.PushTransform(new TranslateTransform(pos.X, pos.Y));
                    dc.PushTransform(new RotateTransform(heading * 180 / Math.PI));
                    dc.PushTransform(new ScaleTransform(pitch, 1));
                    Planes.Kinzhal(dc, new Point(0, 0), 0, size, 0, burning, plasma, t + s.Seed, fairing: false);
                    dc.Pop(); dc.Pop(); dc.Pop();
                }
                else Planes.Kinzhal(dc, pos, heading, size, alt, burning, plasma, t + s.Seed, fairing: t < s.Ignite);
            }
            if (t >= s.Impact && t < s.Impact + 0.9)
            {   // Einschlag mit Hyperschall: greller Blitz, Feuerkern, Druckring und Staubring laufen nach außen
                double k = (t - s.Impact) / 0.9;
                Soft.Draw(dc, ImpactFlash, s.Target, Reach * (0.5 + 0.6 * k), Reach * (0.5 + 0.6 * k), Math.Max(0, 1 - k * 3));
                Soft.Draw(dc, ImpactFire, s.Target, Reach * 0.45, Reach * 0.45, Math.Max(0, 0.9 - k * 1.4));
                double ring = Reach * (0.3 + 1.1 * Fly.EaseOut(k));
                dc.DrawEllipse(null, Soft.FadePen(ShockRing, 2.4 + 3 * (1 - k), 0.9 * (1 - k)), s.Target, ring, ring);
                double dust = Reach * (0.2 + 0.8 * Fly.EaseOut(k));
                dc.DrawEllipse(null, Soft.FadePen(DustRing, Reach * 0.25 * (1 - k * 0.5), 0.6 * (1 - k)), s.Target, dust, dust);
            }
            if (t >= s.Enter && t <= s.Release + 6)
            {
                var at = s.Track.At(t); double h = s.Track.Heading(t);
                double burner = t > s.Release + 0.3 ? Fly.Smooth((t - s.Release - 0.3) / 0.4) : 0.35;
                Planes.MiG31(dc, at, h, u, 1.1, t, kinzhal: t < s.Release, burner: burner);
            }
        }
    }
}

// ================================================================ Су-57 mit С-70 «Охотник» 

static partial class Planes
{
    // ---------------------------------------------------------------- С-70 «Охотник» (Länge 14 m, Spannweite 19 m)
    // Aus SMs Dreiseitenansicht (Draufsicht, 20.11.2026) maschinell ausgemessen: Pixel des Flugzeugs vom Hintergrund getrennt, Kanten
    // zeilenweise erfasst, Geraden angepasst und als Umriss über die Vorlage gelegt (deckungsgleich). 6,08 px je Einheit, Nase (441|716).
    // Düse nach Farbproben: hinten schmaler dunkler Austritt, Schaufelring, schwarzer Ring, vorn breiteres Bronzegehäuse; links und rechts
    // ragen zwei kurze Heckspitzen weiter nach hinten. Farben aus der Vorlage gemessen. 100 Einheiten = Länge, Spannweite 140.
    static readonly Geometry HunterBody = Fly.Both("M50,0 L-41.8,68.6 L-55.8,68.6 L-61.8,64.0 L-30.4,25.3 L-49.8,5.7 L-34.5,5.5 L-34.5,0 Z");
    // Tarnung nach der Infografik : Splittertarnung in hellgrau-weiß, hellem und mittlerem Blaugrau, große Winkelbänder mit
    // Spitze hinten auf der Mittellinie und Armen nach vorn-außen, helle Ruderstreifen entlang der Hinterkante. Farben aus der Grafik gemessen.
    static readonly Geometry HunterMid = Fly.Both("M39.2,8 L20.4,22 L14,17 L28,6 Z M-12,36 L-38,60 L-50,66 L-60,63 L-44,44 L-30,32 Z");
    static readonly Geometry HunterDarkBands = Fly.Both("M-2.7,0 L16.5,40 L8,40 L-11.3,0 Z");
    static readonly Geometry HunterPaleBands = Fly.Both("M-12,6 L-24,14 L-34.5,9 L-34.5,3 L-18,3 Z M2,24 L-8,32 L-14,30 L-4,22 Z");
    static readonly Geometry HunterRudders = Fly.Both("M-34,29.7 L-58.6,60 L-56.4,61.6 L-31.6,31.3 Z M-37,9.5 L-45.6,7.1 L-44.6,6.0 L-36,8.3 Z");
    static readonly Geometry HunterSpine = Fly.Both("M30,0 L28,2.6 L-34.5,2.6 L-34.5,0 Z");
    static readonly Geometry HunterIntakeDark = Fly.G("M26.8,0 L33.4,-5.1 C34.4,-3 35.2,-1.4 35.5,0 C35.2,1.4 34.4,3 33.4,5.1 Z");
    static readonly Geometry HunterChevron = Fly.Both("M21.1,0 L33.4,7.9 L32.7,8.8 L20.3,0.9 Z");
    static readonly Geometry HunterRedBands = Fly.Both("M-49.2,68.6 L-41.8,68.6 L-35.7,64.0 L-35.7,61.6 L-41.8,66.1 L-49.2,66.1 Z M-28,24.7 L-18.4,24.7 L-18.4,27 L-28,27 Z M29.3,17.4 L21.1,11.7 L21.8,10.8 L30,16.5 Z");
    static readonly Geometry HunterLines = Fly.G("M29.3,10.4 L-18.4,10.4 M29.3,-10.4 L-18.4,-10.4 M-30.4,25.3 L-30.4,-25.3 " +
        "M-10,30 L-44,58 M-10,-30 L-44,-58");
    static readonly Brush HunterTop = Fly.B("#D3D9DF"), HunterPale = Fly.B("#E2E6EA"), HunterDeep = Fly.B("#95A3AF"), HunterMidTone = Fly.B("#B4C4CA"), HunterDark = Fly.B("#36474E"), HunterRed = Fly.B("#7A2E3A"), HunterRudder = Fly.B("#C9CDD4");
    static readonly Pen HunterEdge = Fly.P("#7D8A96", 0.35), HunterSeam = Fly.P("#A3AEB8", 0.22), HunterProbe = Fly.P("#8C99A6", 0.5);
    static readonly Brush HunterGrill = Fly.B("#293146");
    static readonly Pen HunterWave = Fly.P("#6F7E98", 0.25);
    // Düse (gemessen in 10-facher Vergrößerung): ragt frei aus dem Rumpf in den offenen Schlitz zwischen den Heckspitzen (Schlitz halb 5,5,
    // von −34,5 bis −49,8). Kegel vorn halb 4,4, hinten (−47,7) halb 2,55; dunkle Ringe bei −38,8 und −41,8, Bronze-Schaufeln, heller Rand.
    static readonly Geometry NozzleCone = Fly.Both("M-34.5,4.4 L-47.7,2.55 L-47.7,0 L-34.5,0 Z");
    static readonly Geometry NozzleBands = Fly.Both("M-39.1,3.77 L-41.9,3.38 L-41.9,0 L-39.1,0 Z");
    static readonly Geometry NozzleRear = Fly.Both("M-41.9,3.38 L-47.7,2.55 L-47.7,0 L-41.9,0 Z");
    static readonly Geometry NozzleRim = Fly.Both("M-46.9,2.66 L-47.7,2.55 L-47.7,0 L-46.9,0 Z");
    static readonly Brush NozzleBronze = Fly.B("#8C7255"), NozzleShine = Fly.B("#A08868"), NozzleBand = Fly.B("#221C18"), NozzleBack = Fly.B("#4A3424"), NozzleLip = Fly.B("#C9CED6"), NozzleExit = Fly.B("#1A1716"),
        HornVent = Fly.B("#1F2A3C");
    static readonly Pen NozzlePetal = Fly.P("#5A4230", 0.22), NozzleEdge = Fly.P("#3B342E", 0.3);

    /// <param name="bay">Schacht liegt unten (von oben nicht sichtbar)</param>
    public static void S70(DrawingContext dc, Point at, double heading, double u, double altitude, double t, double bay = 0)
    {
        Fly.ShadowOf(dc, at, heading, u, altitude, HunterBody, NozzleCone);
        dc.PushTransform(Fly.Frame(at, heading, u));
        Soft.Draw(dc, HunterExhaust, new Point(-50.5, 0), 6, 2.6, 0.5);
        foreach (double y in new[] { -14.6, 14.6 })
        {   // Staurohre nach vorn über die Nase hinaus, mit kleiner Gabel
            dc.DrawLine(HunterProbe, new Point(30.9, y), new Point(55.6, y));
            double side = y > 0 ? 1.6 : -1.6;
            dc.DrawLine(HunterProbe, new Point(44, y), new Point(44, y + side));
            dc.DrawLine(HunterProbe, new Point(44, y + side), new Point(49.5, y + side));
        }
        dc.DrawGeometry(HunterTop, null, HunterBody);
        dc.PushClip(HunterBody);   // Tarnung 1:1 aus der Infografik (Textur in Modellkoordinaten, auf die Zelle beschnitten)
        if (HunterCamo is { } camo) dc.DrawImage(camo, new Rect(-65, -71, 115, 142));
        dc.Pop();
        dc.DrawGeometry(null, HunterEdge, HunterBody);
        dc.DrawGeometry(null, HunterSeam, HunterLines);
        dc.DrawGeometry(HunterDark, null, HunterIntakeDark);
        dc.DrawGeometry(HunterRed, null, HunterChevron);
        foreach (double s in new[] { -1.0, 1.0 })
        {   // Lüftungsgitter mit Wellenlinien
            double y0 = s > 0 ? 10.9 : -16.9;
            dc.DrawRectangle(HunterGrill, null, new Rect(-18.4, y0, 6.6, 6));
            for (int i = 0; i < 4; i++)
            {
                var g = new StreamGeometry();
                using (var o = g.Open())
                {
                    o.BeginFigure(new Point(-17.9, y0 + 0.8 + i * 1.4), false, false);
                    for (int k = 1; k <= 6; k++) o.LineTo(new Point(-17.9 + k * 0.94, y0 + 0.8 + i * 1.4 + (k % 2 == 0 ? 0 : 0.6)), true, false);
                }
                g.Freeze(); dc.DrawGeometry(null, HunterWave, g);
            }
        }
        // Düse: frei im Schlitz zwischen den Heckspitzen (Kegel mit Schaufeln, zwei dunklen Ringen, hellem Rand am Austritt)
        dc.DrawGeometry(NozzleBronze, NozzleEdge, NozzleCone);
        dc.DrawGeometry(NozzleShine, null, Fly.G("M-34.5,1.6 L-47,1.0 L-47,-0.4 L-34.5,-0.6 Z"));
        for (int i = -4; i <= 4; i++) dc.DrawLine(NozzlePetal, new Point(-34.5, i * 1.0), new Point(-47.7, i * 0.58));
        dc.DrawGeometry(NozzleBack, null, NozzleRear);   // hintere Hälfte dunkelbraun
        for (int i = -3; i <= 3; i++) dc.DrawLine(NozzlePetal, new Point(-41.9, i * 0.9), new Point(-47.7, i * 0.7));
        dc.DrawGeometry(NozzleBand, null, NozzleBands);  // breiter dunkler Ring
        dc.DrawGeometry(NozzleExit, null, NozzleRim);     // dunkler Rand am Austritt
        foreach (double y in new[] { -1.0, 1.0 }) dc.DrawRoundedRectangle(HornVent, null, new Rect(-42.5, y * 7.4 - 1.1, 5, 2.2), 1.1, 1.1);   // Auslass an jeder Heckspitze
        foreach (double y in new[] { -1.0, 1.0 }) { Star(dc, new Point(-42.8, y * 56.9), 4.4, StarWhite); Star(dc, new Point(-42.8, y * 56.9), 3.5); }   // roter Stern mit weißem Rand
        dc.Pop();
    }
    static readonly Brush HunterExhaust = Art.Glow("#60C8D2E0");

    /// <summary>Tarnung der S-70, maschinell aus SMs Infografik übertragen (20.11.2026): die Grafik wurde stückweise über Dreiecke (Nase,
    /// Kerbe, Spitzen, Heck) auf den gemessenen Umriss abgebildet, Beschriftungen (Rahmen, Linien, Sterne) entfernt, weich geglättet.
    /// X −65 … 50, Y −71 … 71, 4 Pixel je Einheit.</summary>
    static ImageSource? hunterCamo;
    static bool hunterCamoTried;
    static ImageSource? HunterCamo
    {
        get
        {
            if (hunterCamoTried) return hunterCamo;
            hunterCamoTried = true;
            try
            {
                var image = new System.Windows.Media.Imaging.BitmapImage();
                image.BeginInit();
                var assembly = typeof(Planes).Assembly.GetName().Name;
                image.UriSource = new Uri($"pack://application:,,,/{assembly};component/{(assembly == "DeskKeeper" ? "" : "DeskKeeper/")}Assets/s70-tarnung.png", UriKind.Absolute);
                image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                image.EndInit(); image.Freeze();
                hunterCamo = image;
            }
            catch (Exception e) when (e is System.IO.IOException or NotSupportedException or UriFormatException) { hunterCamo = null; }
            return hunterCamo;
        }
    }

    // ---------------------------------------------------------------- КАБ-500Кр (Länge 3,05 m, Ø 0,4 m, Spannweite 0,75 m)
    // Rund schattiert: stumpfe Glaskuppel des Fernsehsuchkopfs in einem Metallkragen, vier schmale Entenruder vorn (mit Ruderfuge),
    // Nähte des Gefechtskopfs, vier Trapezflügel hinten mit Klappen, dunkles Heck. 100 Einheiten = Länge.
    static readonly Geometry KabHull = Solid(Fly.Both("M47,0 C47,3.6 44.6,6.6 40,6.6 L-44,6.6 L-48,5.2 L-50,5.2 L-50,0 Z"));
    static readonly Geometry KabFins = Fly.Both("M30,6.6 L27,10.5 L20,10.5 L19,6.6 Z M-22,6.6 L-36,12.3 L-46,12.3 L-46,6.6 Z");
    static readonly Geometry KabFlaps = Fly.Both("M23,7 L22.2,10.5 L20,10.5 L19.2,7 Z M-40,10 L-41,12.3 L-46,12.3 L-46,10 Z");
    static readonly Geometry KabSeams = Fly.G("M40.8,6.6 L40.8,-6.6 M38.6,6.6 L38.6,-6.6 M14,6.6 L14,-6.6 M-12,6.6 L-12,-6.6 M-44,6.6 L-44,-6.6");
    static readonly Brush KabSkin = Round("#434C37", "#9AA788", "#76825F", 6.6), KabFin = Fly.B("#6A7556"), KabFlap = Fly.B("#5A6449"),
        KabCollar = Round("#4A4F52", "#B6BCC0", "#868C90", 6.6), KabNose = Dome("#1C2228", "#9DB2C6"), KabTailEnd = Fly.B("#2E3428"), KabInk = Fly.B("#E8E2C8");
    static readonly Pen KabEdge = Fly.P("#3D4632", 0.6), KabSeam = Fly.P("#56604A", 0.35);
    public static void Kab500(DrawingContext dc, Point at, double heading, double u)
    {
        dc.PushTransform(Fly.Frame(at, heading, u));
        dc.DrawGeometry(KabFin, KabEdge, KabFins);
        dc.DrawGeometry(KabFlap, null, KabFlaps);
        dc.DrawGeometry(KabSkin, KabEdge, KabHull);
        dc.DrawGeometry(null, KabSeam, KabSeams);
        dc.DrawRectangle(KabTailEnd, null, new Rect(-50, -5.2, 3, 10.4));
        dc.PushClip(new RectangleGeometry(new Rect(38.6, -7, 2.2, 14)));
        dc.DrawGeometry(KabCollar, null, KabHull);   // Metallkragen
        dc.Pop();
        dc.PushClip(new RectangleGeometry(new Rect(40.8, -7, 8, 14)));
        dc.DrawGeometry(KabNose, KabEdge, KabHull);   // Glas des Suchkopfs folgt der Nase
        dc.Pop();
        dc.DrawEllipse(GlassGlint, null, new Point(43.4, -2.6), 1.1, 1.7);
        dc.Pop();
    }
}

/// <summary>
/// Су-57 mit dem Begleitdrohnen-Nurflügel С-70 «Охотник» : das Paar fliegt in Formation über jede Symbolgruppe
/// (der „Охотник“ seitlich versetzt dahinter). Der „Охотник“ öffnet den Schacht und wirft КАБ-500 auf jedes zweite Symbol,
/// die Су-57 schießt aus ihren Innenschächten Lenkflugkörper auf die übrigen. Gerade Überflüge ohne Haken.
/// </summary>
sealed class HunterRaid : IAttack
{
    record Pass(double Start, double End, Point From, Vector Dir, double Release);
    record Weapon(bool ByDrone, Point From, Point To, double Fire, double Impact);
    readonly List<Pass> passes = [];
    readonly List<Weapon> weapons = [];
    readonly double spanS, uS, spanH, uH, speed, kabU;
    public IReadOnlyList<(Point P, double T)> Blasts { get; }
    public IReadOnlyList<double>? BlastSizes { get; }
    public double LastImpact { get; }
    public double End { get; }
    public bool Explodes => true;
    public double Reach { get; }

    public HunterRaid(Rect area, IReadOnlyList<Point> targets, double iconSize)
    {
        spanS = Scale.PerMeter(iconSize) * 14.1; uS = spanS / 70;          // Su-57 wie in der Szene „Su-57“
        spanH = Scale.PerMeter(iconSize) * 14; uH = spanH / 100;            // С-70: 100 Einheiten = 14 m Länge
        kabU = Scale.PerMeter(iconSize) * 3.05 / 100;
        speed = Math.Max(spanS * 6, area.Width / 2.8);
        Reach = iconSize * 0.5;
        var open = targets.OrderBy(p => p.X).ToList(); var groups = new List<List<Point>>();
        while (open.Count > 0)
        {
            var seed = open[0];
            var g = open.OrderBy(p => (p - seed).LengthSquared).Take(8).Where(p => (p - seed).Length < iconSize * 6).ToList();
            groups.Add(g); open.RemoveAll(g.Contains);
        }
        double diag = Math.Sqrt(area.Width * area.Width + area.Height * area.Height);
        double t = 0.4;
        var blasts = new List<(Point, double)>(); var sizes = new List<double>();
        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            var c = new Point(g.Average(p => p.X), g.Average(p => p.Y));
            // von der Seite mit mehr Platz: der Abwurf liegt sichtbar vor der Gruppe
            double angle = (c.X < area.Left + area.Width / 2 ? Math.PI : 0) + (i % 3 - 1) * 0.3;
            var dir = new Vector(Math.Cos(angle), Math.Sin(angle));
            double half = diag * 0.6 + spanH * 3;
            var from = c - dir * half;
            double release = t + (half - spanS * 3) / speed;
            var pass = new Pass(t, t + 2 * half / speed, from, dir, release);
            passes.Add(pass);
            for (int k = 0; k < g.Count; k++)
            {
                bool drone = k % 2 == 0;
                double fire = release + k * 0.12;
                var at = Drone(pass, fire, drone);
                double flight = drone ? 1.35 : 0.95;
                weapons.Add(new Weapon(drone, at, g[k], fire, fire + flight));
                blasts.Add((g[k], fire + flight)); sizes.Add(drone ? 1.15 : 0.9);
            }
            t += Math.Max((pass.End - pass.Start) * 0.45, 1.4);
        }
        Blasts = blasts; BlastSizes = sizes;
        LastImpact = blasts.Count > 0 ? blasts.Max(b => b.Item2) : 2;
        End = Math.Max(LastImpact + 0.3, passes.Count > 0 ? passes.Max(p => p.End) : 2);
    }

    /// <summary>Lage eines Flugzeugs im Überflug: die Су-57 auf der Bahn, der „Охотник“ seitlich versetzt dahinter.</summary>
    Point Drone(Pass p, double t, bool drone)
    {
        var lead = p.From + p.Dir * ((t - p.Start) * speed);
        return drone ? lead - p.Dir * spanH * 1.5 + new Vector(-p.Dir.Y, p.Dir.X) * spanH * 1.6 : lead;
    }

    public IEnumerable<(Func<double, Point> At, double From, double To)> Flights =>
        passes.SelectMany(p => new[] { ((Func<double, Point>)(t => Drone(p, t, false)), p.Start, p.End), ((Func<double, Point>)(t => Drone(p, t, true)), p.Start, p.End) });

    public void Draw(DrawingContext dc, double t)
    {
        foreach (var w in weapons)
        {
            if (t < w.Fire || t >= w.Impact) continue;
            double k = (t - w.Fire) / (w.Impact - w.Fire);
            if (w.ByDrone)
            {   // Gleitbombe: segelt mit abnehmender Höhe aufs Ziel, wird dabei kleiner
                double e = k * k * 0.35 + k * 0.65;
                var p = w.From + (w.To - w.From) * e;
                var d = w.To - w.From;
                Planes.Kab500(dc, p, Math.Atan2(d.Y, d.X), kabU * (1.8 - 0.7 * k));
            }
            else
            {
                double s = Fly.Smooth(k);
                var mid = w.From + (w.To - w.From) * 0.5 + new Vector(0, -spanS * 0.2);
                var p = Fly.Bez(w.From, mid, w.To, s); var q = Fly.Bez(w.From, mid, w.To, Math.Max(0, s - 0.02));
                Planes.Rocket(dc, q, p, uS * 1.4, t); Planes.Maverick(dc, p, Math.Atan2(p.Y - q.Y, p.X - q.X), uS * 0.9);
            }
        }
        foreach (var pass in passes)
        {
            if (t < pass.Start || t > pass.End) continue;
            double heading = Math.Atan2(pass.Dir.Y, pass.Dir.X);
            double bay = Fly.Smooth((t - (pass.Release - 0.6)) / 0.3) * (1 - Fly.Smooth((t - (pass.Release + 1.1)) / 0.3));
            Planes.S70(dc, Drone(pass, t, true), heading, uH, 0.5, t, bay);
            Planes.Su57(dc, Drone(pass, t, false), heading, uS, 0.55, t, bay);
        }
    }
}
