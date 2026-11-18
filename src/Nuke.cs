using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace DeskKeeper;

/// <summary>
/// Atomexplosion als vereinfachtes physikalisches Modell (Seitenansicht, Boden = Einschlagpunkt):
/// • Lichtblitz: Doppelpuls (kurze Spitze, Einbruch, breiter zweiter Puls) wie bei echten Kernwaffen.
/// • Feuerball: Radius nach Taylor–Sedov R ∝ t^0,4, Temperatur kühlt ab; Farbe = Schwarzkörperstrahlung.
/// • Stoßwelle: ebenfalls R ∝ t^0,4 bis über alle Bildschirme; kurz eine Kondensationswolke (Wilson-Wolke).
/// • Auftrieb: der heiße Ball steigt, bremst an der Schichtgrenze; um ihn bildet sich ein Wirbelring,
///   Teilchen rollen im Hut (Torus-Zirkulation). Nachströmung saugt Staub vom Boden in den Stiel. Staubrock am Boden.
/// Die Teilchen werden einmal vorab integriert (60 Hz), gezeichnet wird daraus nur noch.
/// </summary>
sealed class NukeCloud
{
    public const double Length = 5.2;            // Sekunden bis zum Verwehen
    const double Dt = 1 / 60.0;
    readonly Point ground;
    readonly double h;                            // Bezugshöhe (Bildschirmhöhe)
    readonly double shockMax, shockTime;
    readonly List<Particle> particles = [];
    readonly Brush[] heat, smoke, dust;

    enum Type { Cap, Stem, Skirt }
    sealed class Particle
    {
        public Type Kind;
        public double Born, Size, Seed, Phi;   // Phi: Winkel um die Hochachse (achsensymmetrisch)
        public readonly List<(Point P, double T)> Track = [];   // je 1/60 s: Ort, Temperatur (0…1)
    }

    public NukeCloud(Point ground, double screenHeight, double shockReach, int seed = 11)
    {
        this.ground = ground; h = screenHeight; shockMax = shockReach; shockTime = 1.7;
        heat = Palette(true); smoke = Palette(false); dust = DustPalette();
        var random = new Random(seed);
        // Teilchen säen: Hut (aus dem Feuerball), Stiel (Bodenstaub, später), Rock (an der Stoßfront am Boden)
        for (int i = 0; i < 260; i++) particles.Add(new Particle { Kind = Type.Cap, Born = 0.05 + random.NextDouble() * 0.5, Size = 0.018 + random.NextDouble() * 0.016, Seed = random.NextDouble() });
        for (int i = 0; i < 110; i++) particles.Add(new Particle { Kind = Type.Stem, Born = 0.45 + random.NextDouble() * 2.4, Size = 0.012 + random.NextDouble() * 0.012, Seed = random.NextDouble() });
        for (int i = 0; i < 70; i++) particles.Add(new Particle { Kind = Type.Skirt, Born = 0.15 + random.NextDouble() * 0.9, Size = 0.014 + random.NextDouble() * 0.016, Seed = random.NextDouble() });
        Integrate(random);
        // Bilder der weichen Teilchen vorab erzeugen, damit der erste Pilzmoment nicht hakt
        var warm = new DrawingVisual();
        using (var dc = warm.RenderOpen())
            foreach (var set in new[] { heat, smoke, dust })
                foreach (var b in set)
                    for (int level = 1; level <= 8; level++) Soft.Draw(dc, b, new Point(), 1, 1, level / 8.0);
    }

    // ------------------------------------------------------------ Physik (Einheiten: h, y nach oben)

    /// <summary>Feuerballradius (Taylor–Sedov, später Abflachung).</summary>
    double Fireball(double t) => 0.13 * Math.Pow(Math.Clamp(t / 0.6, 0, 1), 0.4) * (1 + 0.35 * Smooth((t - 0.6) / 1.6));
    /// <summary>Höhe des Ballzentrums: Auftrieb, Bremsen an der „Tropopause“ (0,42 h).</summary>
    double Rise(double t) => 0.42 * (1 - Math.Exp(-Math.Max(0, t - 0.15) / 1.1)) + Fireball(t) * 0.6;
    /// <summary>Temperatur des Feuerballs 0…1 (1 ≈ 9000 K, 0 ≈ 900 K).</summary>
    static double Temperature(double t) => Math.Clamp(Math.Pow(1 + Math.Max(0, t) / 0.6, -1.1), 0, 1);
    /// <summary>Stoßfront am Boden (Pixel): R ∝ t^0,4.</summary>
    public double Shock(double t) => shockMax * Math.Pow(Math.Clamp(t / shockTime, 0, 1), 0.4);
    /// <summary>Zeit, zu der die Stoßfront die Entfernung erreicht (für das Verbrennen der Symbole).</summary>
    public double ShockArrival(double distance) => shockTime * Math.Pow(Math.Clamp(distance / shockMax, 0, 1), 2.5);

    void Integrate(Random random)
    {
        foreach (var p in particles)
        {
            p.Phi = random.NextDouble() * Math.Tau;
            Vector pos; double temp;   // pos = (rho, y)
            double ang = p.Seed * Math.Tau;
            switch (p.Kind)
            {
                case Type.Cap:
                {
                    // Start im Feuerball: zufällig im Kugelvolumen (Radius, Höhe)
                    double rr = Fireball(p.Born) * Math.Cbrt(random.NextDouble());
                    double th = Math.Acos(2 * random.NextDouble() - 1);
                    pos = new Vector(Math.Abs(Math.Sin(th)) * rr, Rise(p.Born) * 0.3 + Math.Cos(th) * rr);
                    temp = 1; break;
                }
                case Type.Stem:
                    pos = new Vector(0.02 + random.NextDouble() * 0.16, 0.0); temp = 0; break;
                default:
                    pos = new Vector(Shock(p.Born) / h, 0.01); temp = 0; break;
            }
            for (double t = p.Born; t <= Length; t += Dt)
            {
                double zc = Rise(t), rf = Fireball(t);
                Vector v;
                switch (p.Kind)
                {
                    case Type.Cap:
                    {
                        // achsensymmetrischer Wirbelring: Kern bei rho = a, Höhe zc; innen aufwärts, außen abwärts
                        double a = rf * 0.8;
                        double rise = (Rise(t + Dt) - zc) / Dt;
                        double circulation = 1.0 * Math.Exp(-(t - 0.4) / 2.6);
                        var d = pos - new Vector(a, zc);
                        double dist2 = d.LengthSquared + 0.003;
                        v = new Vector(0, rise) + new Vector(d.Y, -d.X) * (circulation * 0.035 / dist2) * Math.Exp(-d.LengthSquared / (rf * rf * 3));
                        v += new Vector(pos.X * 0.45, -(pos.Y - zc) * 0.4) * Smooth((t - 1.0) / 1.8);   // Ausbreiten an der Schichtgrenze
                        temp = Math.Min(temp, Temperature(t - p.Seed * 0.35) * (0.55 + 0.45 * Math.Exp(-d.Length / rf)));
                        break;
                    }
                    case Type.Stem:
                    {
                        // Nachströmung: zur Achse, im Stiel aufwärts
                        double top = Rise(t) - Fireball(t) * 0.7;
                        double up = Math.Max(0, 0.34 * (1 - pos.Y / Math.Max(0.05, top)));
                        v = new Vector(-pos.X * 1.5, up + 0.02);
                        if (pos.Y > top) v = new Vector(pos.X * 0.35, 0.02);
                        break;
                    }
                    default:
                    {
                        double age = t - p.Born;
                        v = new Vector(0.25 * Math.Exp(-age * 1.6), 0.05 * Math.Exp(-age));
                        break;
                    }
                }
                v += new Vector(Math.Sin(t * 3.1 + p.Seed * 40), Math.Cos(t * 2.7 + p.Seed * 31)) * 0.01;
                pos += v * Dt;
                if (pos.X < 0) pos.X = -pos.X;
                p.Track.Add((new Point(pos.X, pos.Y), temp));
            }
        }
    }

    // ------------------------------------------------------------ Zeichnen

    public void Draw(DrawingContext dc, double age, Rect all)
    {
        if (age < 0 || age > Length) return;
        // 1. Doppelpuls des Lichtblitzes über alle Bildschirme
        double flash = Math.Max(Math.Exp(-age / 0.035), 0.75 * Math.Exp(-Math.Pow((age - 0.32) / 0.28, 2)));
        if (age > 0.6) flash *= Math.Exp(-(age - 0.6) / 0.35);
        if (flash > 0.01) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)(255 * Math.Min(0.94, flash)), 0xFF, 0xFB, 0xF2)), null, all);   // ohne eigene Ebene
        // 2. Stoßwelle am Boden, kurz die Kondensationswolke
        double shock = Shock(age);
        double shockFade = 1 - Smooth(age / 2.2);
        if (shockFade > 0.01)
        {
            Soft.Draw(dc, ShockBand, ground, shock, shock * 0.86, shockFade * 0.5, 256);
        }
        double wilson = Math.Exp(-Math.Pow((age - 0.45) / 0.18, 2));
        if (wilson > 0.02) Soft.Draw(dc, Condensation, ground - new Vector(0, h * 0.05), h * 0.34, h * 0.26, wilson * 0.32, 128);
        // 3. Teilchen (achsensymmetrisch, nach Tiefe sortiert: hinten zuerst; vorn heller)
        int frame = (int)(age / Dt);
        var visible = new List<(double Depth, Particle P, Point Pos, double Temp)>();
        foreach (var p in particles)
        {
            if (age < p.Born) continue;
            int i = Math.Min(p.Track.Count - 1, frame - (int)(p.Born / Dt));
            if (i < 0) continue;
            var (pos, temp) = p.Track[i];
            visible.Add((pos.X * Math.Sin(p.Phi), p, pos, temp));
        }
        visible.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        double zc0 = Rise(age), rf0 = Math.Max(0.05, Fireball(age));
        foreach (var (depth, p, pos, temp) in visible)
        {
            double life = age - p.Born;
            double size = h * p.Size * (1 + 1.15 * Math.Sqrt(life));   // Diffusion: σ ∝ √t
            double fade = Smooth(life / 0.25) * (1 - Smooth((age - 3.6) / 1.6));
            if (p.Kind == Type.Skirt) fade *= 1 - Smooth(life / 2.4);
            if (fade <= 0.01) continue;
            double x = pos.X * Math.Cos(p.Phi);
            var at = new Point(ground.X + x * h, ground.Y - pos.Y * h + depth * h * 0.18);   // leichte Aufsicht
            double light = Math.Clamp(0.45 + (pos.Y - zc0) / (rf0 * 2.4) - x * 0.5 + depth * 1.2, 0, 1);
            Brush brush = p.Kind == Type.Skirt ? dust[(int)(p.Seed * (dust.Length - 1))]
                : temp > 0.16 ? heat[(int)(Math.Clamp(temp, 0, 1) * (heat.Length - 1))]
                : smoke[(int)(light * (smoke.Length - 1))];
            Soft.Draw(dc, brush, at, size, size * 0.9, fade * (p.Kind == Type.Cap ? 0.8 : 0.65));
        }
        // 4. Feuerball (glühender Kern) solange heiß
        double t = Temperature(age);
        if (t > 0.35)
        {
            var c = new Point(ground.X, ground.Y - Rise(age) * h);
            double rf = Fireball(age) * h;
            Soft.Draw(dc, heat[(int)(t * (heat.Length - 1))], c, rf * 1.05, rf, Math.Min(1, (t - 0.35) * 2.2));
        }
    }

    // ------------------------------------------------------------ Farben

    /// <summary>Schwarzkörperfarbe (Näherung nach Tanner Helland) für 900 K … 9000 K.</summary>
    static Color Blackbody(double kelvin)
    {
        double t = kelvin / 100, r, g, b;
        r = t <= 66 ? 255 : 329.7 * Math.Pow(t - 60, -0.1332);
        g = t <= 66 ? 99.47 * Math.Log(t) - 161.12 : 288.12 * Math.Pow(t - 60, -0.0755);
        b = t >= 66 ? 255 : t <= 19 ? 0 : 138.52 * Math.Log(t - 10) - 305.04;
        return Color.FromRgb((byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255));
    }

    static Brush[] Palette(bool hot)
    {
        var list = new Brush[24];
        for (int i = 0; i < list.Length; i++)
        {
            double k = i / (double)(list.Length - 1);
            Color core, rim;
            if (hot)
            {
                // heiß: Schwarzkörper, außen von Ruß verdunkelt
                core = Blackbody(1100 + 7900 * k);
                rim = Color.FromArgb(0, (byte)(core.R * 0.5), (byte)(core.G * 0.35), (byte)(core.B * 0.3));
            }
            else
            {
                // Rauch: oben im Licht heller, unten dunkler (k = Höhe)
                byte v = (byte)(48 + 110 * k);
                core = Color.FromRgb(v, (byte)(v * 0.93), (byte)(v * 0.86));
                rim = Color.FromArgb(0, (byte)(v * 0.6), (byte)(v * 0.55), (byte)(v * 0.5));
            }
            var b = new RadialGradientBrush { GradientOrigin = new Point(0.38, 0.32), Center = new Point(0.45, 0.42), RadiusX = 0.6, RadiusY = 0.6 };
            b.GradientStops.Add(new GradientStop(core, 0));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(150, core.R, core.G, core.B), 0.4));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(60, core.R, core.G, core.B), 0.72));
            b.GradientStops.Add(new GradientStop(rim, 1));
            b.Freeze();
            list[i] = b;
        }
        return list;
    }

    static Brush[] DustPalette()
    {
        var list = new Brush[6];
        for (int i = 0; i < list.Length; i++)
        {
            byte v = (byte)(110 + i * 10);
            var core = Color.FromRgb(v, (byte)(v * 0.86), (byte)(v * 0.7));
            var b = new RadialGradientBrush();
            b.GradientStops.Add(new GradientStop(Color.FromArgb(170, core.R, core.G, core.B), 0));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0, core.R, core.G, core.B), 1));
            b.Freeze();
            list[i] = b;
        }
        return list;
    }

    static readonly Brush Flash = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xFB, 0xF2)));
    static readonly Brush ShockBand = Band(Color.FromArgb(150, 255, 246, 232), Color.FromArgb(60, 190, 170, 150));
    static readonly Brush Condensation = Band(Color.FromArgb(200, 255, 255, 255), Color.FromArgb(120, 235, 240, 245), 0.55);

    static Brush Band(Color edge, Color inner, double start = 0.82)
    {
        var b = new RadialGradientBrush();
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, inner.R, inner.G, inner.B), start));
        b.GradientStops.Add(new GradientStop(inner, (start + 1) / 2));
        b.GradientStops.Add(new GradientStop(edge, 0.97));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, edge.R, edge.G, edge.B), 1));
        b.Freeze();
        return b;
    }
    static Brush Freeze(Brush b) { b.Freeze(); return b; }
    static double Smooth(double v) { v = Math.Clamp(v, 0, 1); return v * v * (3 - 2 * v); }
}
