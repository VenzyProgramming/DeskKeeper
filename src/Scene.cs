using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DeskKeeper;

/// <summary>
/// Szene als reine Funktion der Zeit (2.0):
/// Иерихон fliegt an, 18 kleine Raketen verteilen sich über alle Bildschirme, 19 Blitze im selben Bild,
/// Druckwellen laufen über den Schreibtisch und verbrennen die echten Symbole zu Asche (sie verschwinden).
/// Дубина fährt mit dem Anhänger ein, holt jedes Symbol einzeln aus dem Anhänger und stellt es an seinen Platz.
/// Wiederherstellen ohne Rakete: Дубина greift jedes verschobene Symbol und trägt es heim.
/// </summary>
sealed class Scene
{
    internal const double AscentEnd = 1.6, Linger = 1.0, ReturnStart = 3.3, Turn = 4.6;
    internal const double PanelsOpen = 4.35, FirstRelease = 4.62, ReleaseSpan = 0.24, Impact = 6.1;
    internal const double BurstTime = 1.2, BurnTime = 0.8, HideAfter = 0.09;
    internal const double PauseSample = (AscentEnd + Linger + ReturnStart) / 2;

    readonly IReadOnlyList<IconInfo> icons;
    readonly Dictionary<string, Point> start, home;
    readonly Dictionary<string, double> burnAt = new(StringComparer.OrdinalIgnoreCase);
    readonly Rect[] screens;
    readonly Rect stage;
    readonly bool jericho, nuke;
    readonly Rect all;
    readonly NukeCloud? cloud;
    readonly Bomber? bomber;
    readonly List<(Point P, double T, double Seed)> raid = [];
    readonly double iconSize, length, u, r, waveSpeed;
    readonly Point finalFlash;
    readonly Mote[] motes;
    readonly IHelper robot;
    readonly Pen[] mainTrail, moteTrail;
    readonly double robotStart;

    public double Duration { get; }
    public double BodyLength => length;
    public double RobotSize => r;
    public IEnumerable<Point> FlashCenters => bomber is not null ? raid.Select(b => b.P) : motes.Select(m => m.End).Append(finalFlash);
    public bool HasBomber => bomber is not null;
    internal IReadOnlyList<(Point P, double T, double Seed)> Raid => raid;
    public bool Nuke => nuke;
    double Burst => nuke ? NukeBurst : BurstTime;
    internal const double NukeBurst = NukeCloud.Length, NukeLaunch = 2.6;
    public IReadOnlyDictionary<string, Point> Homes => home;
    public int CarriedCount => robot.Carried;
    internal Robot Dubina => (Robot)robot;
    public double RobotStart => robotStart;

    record Mote(Point Start, Vector V0, Point End, Vector V1, double Release, double Seed);
    /// <param name="robotSize">Größe Дубина in Bildschirmpixeln (wie der Saugroboter: 76 DIP × DPI-Faktor).</param>
    public Scene(IReadOnlyList<IconInfo> icons, IReadOnlyDictionary<string, Point> targets, double iconSize, Rect[] screens, Rect primary, bool withJericho, double robotSize, bool nuclear = false, bool tu160 = false)
    {
        this.icons = icons;
        this.screens = screens.Length > 0 ? screens : [primary];
        stage = primary;
        jericho = withJericho;
        nuke = withJericho && nuclear;
        all = this.screens.Aggregate(Rect.Union);
        this.iconSize = Math.Clamp(iconSize, 16, 160);
        length = this.iconSize * 3;          // Rumpf = 3 Symbolbreiten
        u = length / 100;
        r = robotSize;
        mainTrail = TrailPens(3.4 * u, 0.55);
        moteTrail = TrailPens(Math.Max(1.4, 1.1 * u), 0.45);
        start = icons.ToDictionary(i => i.Id, i => i.Center, StringComparer.OrdinalIgnoreCase);
        home = icons.ToDictionary(i => i.Id, i => targets.TryGetValue(i.Id, out var t) ? t : i.Center, StringComparer.OrdinalIgnoreCase);
        var random = new Random(376);

        // Ziele über die ganze Fläche aller Bildschirme; Иерихон trifft die Mitte des Hauptbildschirms.
        var points = SpreadTargets(random);
        finalFlash = nuke ? new Point(primary.Left + primary.Width / 2, primary.Top + primary.Height * 0.52) : points[0];
        motes = new Mote[nuke || (withJericho && tu160) ? 0 : 18];
        for (int index = 0; index < motes.Length; index++)
        {
            var end = points[index + 1];
            double release = FirstRelease + ((index * 7) % motes.Length) * (ReleaseSpan / motes.Length);
            var forward = CarrierHeading(release);
            var across = new Vector(-forward.Y, forward.X);
            double side = index % 2 == 0 ? 1 : -1;
            var from = CarrierAt(release) + forward * (2 * u) + across * (4 * side * u);
            double distance = (end - from).Length;
            // Abtrennen mit der Geschwindigkeit des Trägers plus seitlichem Ausstoß, dann Anflug im Sturz auf das Ziel.
            var carrierV = (CarrierAt(release + 0.01) - CarrierAt(release - 0.01)) / 0.02;
            var v0 = carrierV * 0.9 + across * (side * carrierV.Length * (0.45 + 0.3 * random.NextDouble()));
            double flight = Impact - release;
            var aim = end - from; aim.Normalize();
            var dive = aim * 0.75 + new Vector(0, 0.45); dive.Normalize();
            var v1 = dive * (distance / flight * 1.35);
            motes[index] = new Mote(from, v0, end, v1, release, random.NextDouble());
        }

        // Druckwelle: jedes Symbol fängt Feuer, wenn die Welle des nächsten Blitzes es erreicht.
        double diagonal = Math.Sqrt(primary.Width * primary.Width + primary.Height * primary.Height);
        waveSpeed = nuke ? Math.Sqrt(all.Width * all.Width + all.Height * all.Height) / 1.5 : diagonal / 1.1;
        if (nuke)
        {
            // Stoßfront erreicht die äußerste Bildschirmecke kurz vor dem Ende ihrer Laufzeit
            double reach = new[] { all.TopLeft, all.TopRight, all.BottomLeft, all.BottomRight }.Max(c => (c - finalFlash).Length) * 1.03;
            cloud = new NukeCloud(finalFlash, primary.Height, reach);
        }
        if (withJericho && tu160)
        {
            // Ту-160М: 14 Marschflugkörper auf die Ziele (dichteste Gruppe zuerst), Einschläge nacheinander
            bomber = new Bomber(primary, points.Take(14).ToList(), this.iconSize);
            foreach (var (p, when) in bomber.Blasts) raid.Add((p, when, random.NextDouble()));
            foreach (var icon in icons)
                burnAt[icon.Id] = raid.Min(b => b.T + Math.Min(0.85, (b.P - icon.Center).Length / waveSpeed)) + 0.04 * random.NextDouble();
        }
        else if (jericho)
            foreach (var icon in icons)
            {
                double near = FlashCenters.Min(p => (p - icon.Center).Length);
                burnAt[icon.Id] = Impact + (cloud is not null ? 0.05 + cloud.ShockArrival(near) : Math.Min(0.85, near / waveSpeed)) + 0.04 * random.NextDouble();
            }

        // Дубина von oben mit Anhänger; jedes Symbol einzeln.
        robotStart = jericho ? Math.Max((burnAt.Count > 0 ? burnAt.Values.Max() : Impact) + BurnTime + 0.25, nuke ? Impact + 3.9 : bomber is not null ? bomber.LastImpact + 0.9 : 0) : 0.2;
        var order = jericho ? HomeRoute() : MovedRoute(new Point(primary.Left, primary.Bottom));
        robot = new Robot(icons, id => start[id], home, jericho, robotStart, r, this.screens, primary, order);
        Duration = robot.End;
    }

    List<string> HomeRoute()
    {
        // Spaltenweise von oben nach unten, Spalten von links nach rechts: so fährt Дубина eine Spalte ab.
        double column = iconSize * 1.4;
        return icons.Select(i => i.Id).OrderBy(id => ScreenIndex(home[id])).ThenBy(id => Math.Round(home[id].X / column)).ThenBy(id => home[id].Y).ToList();
    }

    List<string> MovedRoute(Point from)
    {
        var pending = icons.Where(i => (start[i.Id] - home[i.Id]).Length > 2).Select(i => i.Id).ToList();
        var route = new List<string>();
        var at = from;
        while (pending.Count > 0)
        {
            var next = pending.OrderBy(id => (start[id] - at).LengthSquared).First();
            pending.Remove(next); route.Add(next); at = home[next];
        }
        return route;
    }

    int ScreenIndex(Point p) { for (int i = 0; i < screens.Length; i++) if (screens[i].Contains(p)) return i; return 0; }

    // ------------------------------------------------------------ Symbole

    /// <summary>Mittelpunkte aller Symbole zur Zeit t; null = verbrannt bzw. im Anhänger (unsichtbar).</summary>
    public Dictionary<string, Point?> IconCenters(double t)
    {
        var result = new Dictionary<string, Point?>(StringComparer.OrdinalIgnoreCase);
        foreach (var icon in icons) result[icon.Id] = IconAt(icon.Id, t);
        return result;
    }

    Point? IconAt(string id, double t)
    {
        Point? current = start[id];
        if (jericho && burnAt.TryGetValue(id, out var burn) && t >= burn + HideAfter) current = null;
        var (handled, at) = robot.IconAt(id, t);
        return handled ? at : current;
    }

    // ------------------------------------------------------------ Zeichnen

    public void Draw(DrawingContext dc, double t)
    {
        if (bomber is not null)
        {
            bomber.Draw(dc, t);
            foreach (var (p, when, seed) in raid)
            {
                double age = t - when;
                if (age >= 0 && age <= BurstTime) DrawBurst(dc, p, age, 0.8 + 0.15 * seed, seed);
            }
        }
        else if (nuke) DrawNuke(dc, t);
        else if (jericho) DrawJericho(dc, t);
        if (jericho) DrawAsh(dc, t);
        DrawCarried(dc, t);
        robot.Draw(dc, t);
    }

    /// <summary>Raster bleibt an : Symbole unterwegs zeichnet die Bühne als Bild, das echte Symbol wartet außerhalb
    /// des Bildschirms und springt erst am Ziel in seine Rasterzelle. Ohne Bildquelle (Vorschau, Prüfung) wird nichts gezeichnet.</summary>
    public Func<string, ImageSource?>? IconImage { get; set; }

    /// <summary>Liegt das Symbol in einer festen Zelle (Ausgangs- oder Zielplatz)?</summary>
    public bool Resting(string id, Point p) => (p - start[id]).LengthSquared < 1 || (p - home[id]).LengthSquared < 1;

    void DrawCarried(DrawingContext dc, double t)
    {
        if (IconImage is null) return;
        foreach (var icon in icons)
        {
            if (IconAt(icon.Id, t) is not { } c || Resting(icon.Id, c) || IconImage(icon.Id) is not { } image) continue;
            dc.DrawImage(image, new Rect(c.X - iconSize / 2, c.Y - iconSize / 2, iconSize, iconSize));
        }
    }

    static readonly Brush Cream = Solid("#FFF4DC"), Amber = Solid("#FFB056"), Ember = Solid("#FF6A1F"), Flare = Solid("#FFD58A"),
        Char = Solid("#1C1916"), CharEdge = Solid("#3A2A1E"), AshGrey = Solid("#8F8780"), SmokeFill = Solid("#B7AFA8");
    static readonly Brush HeatGlow = Glow("#70FF8A2E"), CoreGlow = Glow("#F0FFF4DC");
    static readonly Brush WaveBand = Band();
    static Brush Band()
    {
        var b = new RadialGradientBrush();
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 240, 220), 0.78));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(70, 210, 190, 170), 0.9));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(150, 255, 246, 232), 0.965));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 246, 232), 1));
        b.Freeze();
        return b;
    }

    // ------------------------------------------------------------ Ядерная ракета

    /// <summary>Flugbahn: von oben rechts außerhalb in einem Bogen auf die Bildschirmmitte, Nase voran.</summary>
    Point NukeAt(double t)
    {
        double k = Math.Clamp((t - NukeLaunch) / (Impact - NukeLaunch), 0, 1);
        var a = new Point(stage.Right + length, stage.Top - length * 1.2);
        var b = new Point(stage.Left + stage.Width * 0.85, stage.Top + stage.Height * 0.05);
        var c = new Point(finalFlash.X + stage.Width * 0.12, finalFlash.Y - stage.Height * 0.3);
        double e = k * k * (0.5 + 0.5 * k);   // beschleunigt im Fall
        return Cubic(a, b, c, finalFlash - new Vector(0, length * 0.05), e);
    }

    void DrawNuke(DrawingContext dc, double t)
    {
        if (t >= NukeLaunch && t < Impact)
        {
            var p = NukeAt(t);
            var heading = Unit(NukeAt(t + 0.01) - NukeAt(t - 0.01));
            Ribbon(dc, t, mainTrail, NukeLaunch, Impact, 0.8, s => NukeAt(s) - Unit(NukeAt(s + 0.01) - NukeAt(s - 0.01)) * (52 * u));
            dc.PushTransform(Place(p - heading * (50 * u), heading, u));
            Art.NukeMissile(dc, 1 + 0.12 * Math.Sin(t * 67));
            dc.Pop();
        }
        cloud?.Draw(dc, t - Impact, all);
    }

    static readonly Brush NukeWhite = Solid("#FFF8EE");
    static readonly Brush DustBand = Dust();
    static Brush Dust()
    {
        var b = new RadialGradientBrush();
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, 150, 130, 110), 0.6));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(120, 150, 130, 110), 0.9));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, 150, 130, 110), 1));
        b.Freeze();
        return b;
    }

    void DrawJericho(DrawingContext dc, double t)
    {
        Ribbon(dc, t, mainTrail, 0, AscentEnd, Linger, TailAt);
        Ribbon(dc, t, mainTrail, ReturnStart, Impact, 0.7, TailAt);
        foreach (var mote in motes)
            Ribbon(dc, t, moteTrail, mote.Release, Impact, 0.34, s => { var (p, h) = MoteAt(mote, s); return p - h * (9 * MoteScale(mote)); });
        if (t <= AscentEnd || (t >= ReturnStart && t < Impact))
        {
            dc.PushTransform(Place(CarrierAt(t), CarrierHeading(t), u));
            Art.Rocket(dc, t >= PanelsOpen, 1 + 0.12 * Math.Sin(t * 71) + 0.06 * Math.Sin(t * 43));
            dc.Pop();
            DrawPanels(dc, t);
        }
        foreach (var mote in motes)
        {
            if (t < mote.Release || t >= Impact) continue;
            var (point, direction) = MoteAt(mote, t);
            dc.PushTransform(Place(point, direction, MoteScale(mote)));
            Art.Dart(dc, 0.9 * (1 + 0.18 * Math.Sin(t * 61 + mote.Seed * 40)));
            dc.Pop();
        }
        double age = t - Impact;
        if (age < 0 || age > BurstTime) return;
        foreach (var mote in motes) DrawBurst(dc, mote.End, age, 0.55 + 0.2 * mote.Seed, mote.Seed);
        DrawBurst(dc, finalFlash, age, 1.0, 0.65);
    }

    void DrawPanels(DrawingContext dc, double t)
    {
        for (int index = 0; index < 2; index++)
        {
            double side = index == 0 ? -1 : 1;
            double opened = PanelsOpen + index * 0.05;
            double age = t - opened;
            if (age > 0.9) continue;
            double anchorTime = age < 0 ? t : opened;
            var forward = CarrierHeading(anchorTime);
            var across = new Vector(-forward.Y, forward.X);
            double fall = Math.Max(0, age);
            double swing = 1 - Math.Exp(-7 * fall);
            var position = CarrierAt(anchorTime) + across * (side * (2.4 + 16 * swing) * u) - forward * (34 * fall * u) + new Vector(0, 60 * fall * fall * u);
            double opacity = 1 - Smooth((fall - 0.5) / 0.4);
            if (opacity <= 0) continue;
            dc.PushOpacity(opacity);
            dc.PushTransform(Place(position, forward, u));
            dc.PushTransform(new RotateTransform(side * (160 * fall * fall + 25 * swing)));
            Art.Panel(dc);
            dc.Pop(); dc.Pop(); dc.Pop();
        }
    }

    /// <summary>
    /// Explosion: kurzer Lichtblitz, Bildfolge eines brodelnden Feuerballs, der zu Rauch abkühlt,
    /// Druckwelle als weiches helles Band (nur der Hauptblitz läuft weit), Glutfunken mit Schwerkraft.
    /// </summary>
    void DrawBurst(DrawingContext dc, Point p, double age, double size, double seed)
    {
        double s = 0.42 * length * size;
        // Lichtblitz (erhellt kurz die Umgebung)
        double flash = 1 - Smooth(age / 0.28);
        if (flash > 0.01) { Soft.Draw(dc, HeatGlow, p, s * 3.2, s * 3.0, flash); Soft.Draw(dc, CoreGlow, p, s * 0.9, s * 0.9, flash); }
        // Druckwelle: weiches Band, leicht staubig
        double wave = 1 - Smooth(age / 0.75);
        if (wave > 0.01 && size >= 1)   // Druckwelle nur beim Hauptblitz
        {
            double radius = size >= 1 ? waveSpeed * age * 0.85 : Math.Min(waveSpeed * age * 0.45, length * 1.3);
            Soft.Draw(dc, WaveBand, p, radius, radius * 0.9, wave * (size >= 1 ? 0.55 : 0.3), 256);
        }
        // Feuerball und Rauch aus der Bildfolge, je Blitz gedreht/gespiegelt
        var frames = Art.Explosion;
        double k = Math.Clamp(age / BurstTime, 0, 0.999);
        var image = frames[(int)(k * frames.Length)];
        double d = s * (size >= 1 ? 6.5 : 5.6);
        double turn = (seed - 0.5) * 50;
        double mirror = seed > 0.5 ? -1 : 1;
        dc.PushTransform(new TranslateTransform(p.X, p.Y));
        dc.PushTransform(new RotateTransform(turn));
        dc.PushTransform(new ScaleTransform(mirror, 1));
        dc.DrawImage(image, new Rect(-d / 2, -d / 2, d, d));
        dc.Pop(); dc.Pop(); dc.Pop();
        // Glutfunken: Bögen mit Schwerkraft, glühen aus
        double sparks = 1 - Smooth((age - 0.05) / 0.85);
        if (sparks > 0.01)
        {
            var pen = new Pen(Flare, Math.Max(1, s * 0.028)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            dc.PushOpacity(sparks);
            int count = size >= 1 ? 18 : 9;
            for (int n = 0; n < count; n++)
            {
                double a = n * Math.Tau / count + seed * 5.4 + (n % 3) * 0.23;
                double speed = s * (3.6 + 2.2 * ((n * 7 + (int)(seed * 10)) % 5) / 4.0) * Math.Sqrt(size);
                var v = new Vector(Math.Cos(a), Math.Sin(a) - 0.45) * speed;
                Point At(double q) => p + v * (1 - Math.Exp(-2.2 * q)) / 2.2 + new Vector(0, s * 2.6 * q * q);
                dc.DrawLine(pen, At(Math.Max(0, age - 0.05)), At(age));
            }
            dc.Pop();
        }
    }

    static Geometry StarShape(Point c, double outer, double inner, double turn)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
            for (int k = 0; k < 16; k++)
            {
                double a = turn + k * Math.PI / 8;
                double rr = k % 2 == 0 ? (k % 4 == 0 ? outer : outer * 0.6) : inner;
                var q = c + new Vector(Math.Cos(a), Math.Sin(a)) * rr;
                if (k == 0) ctx.BeginFigure(q, true, true); else ctx.LineTo(q, true, false);
            }
        g.Freeze();
        return g;
    }

    /// <summary>Verbrennen eines Symbols: Glutrand, verkohlte Form mit Rissen, zerfällt in fallende Ascheflocken.</summary>
    void DrawAsh(DrawingContext dc, double t)
    {
        double s = iconSize;
        foreach (var icon in icons)
        {
            if (!burnAt.TryGetValue(icon.Id, out var burn)) continue;
            double a = t - burn;
            if (a < 0 || a > BurnTime) continue;
            var c = start[icon.Id];
            double seed = (StableHash(icon.Id) & 0xffff) / 65535.0;
            double glow = 1 - Smooth(a / 0.35);
            if (glow > 0.01) Soft.Draw(dc, HeatGlow, c, s * 0.95, s * 0.95, glow);
            double body = a < HideAfter ? Smooth(a / HideAfter) : 1 - Smooth((a - 0.2) / 0.3);
            if (body > 0.01)
            {
                double k = 1 - 0.25 * Smooth((a - 0.15) / 0.35);
                var box = new Rect(c.X - s * 0.42 * k, c.Y - s * 0.42 * k, s * 0.84 * k, s * 0.84 * k);
                dc.PushOpacity(body);
                dc.DrawRoundedRectangle(Char, new Pen(CharEdge, Math.Max(1, s * 0.03)), box, s * 0.12, s * 0.12);
                var crack = new Pen(Ember, Math.Max(1, s * 0.03)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                dc.PushOpacity(1 - Smooth((a - 0.1) / 0.3));
                dc.DrawLine(crack, c + new Vector(-0.3, -0.1 + seed * 0.2) * s * k, c + new Vector(0.05, 0.08) * s * k);
                dc.DrawLine(crack, c + new Vector(0.05, 0.08) * s * k, c + new Vector(0.28, -0.22) * s * k);
                dc.DrawLine(crack, c + new Vector(0.05, 0.08) * s * k, c + new Vector(-0.05, 0.32) * s * k);
                dc.Pop();
                dc.DrawRoundedRectangle(Char, null, new Rect(c.X - s * 0.55, c.Y + s * 0.5, s * 1.1, s * 0.28 * k), s * 0.06, s * 0.06);
                dc.Pop();
            }
            double flakes = Smooth((a - 0.18) / 0.15) * (1 - Smooth((a - 0.45) / 0.35));
            if (flakes > 0.01)
            {
                dc.PushOpacity(flakes);
                for (int f = 0; f < 10; f++)
                {
                    double fx = ((f * 37 + (int)(seed * 97)) % 100) / 100.0 - 0.5, fy = ((f * 53 + (int)(seed * 61)) % 100) / 100.0 - 0.5;
                    double q = Math.Max(0, a - 0.18);
                    var at = c + new Vector(fx * s * 0.8 + Math.Sin(q * 9 + f) * s * 0.06, fy * s * 0.7 + q * q * s * 2.4 + q * s * 0.3);
                    double size = s * (0.035 + 0.03 * (f % 3));
                    dc.DrawRectangle(f % 4 == 0 ? Ember : AshGrey, null, new Rect(at.X - size / 2, at.Y - size / 2, size, size));
                }
                dc.Pop();
            }
        }
    }

    static int StableHash(string text) { int h = 17; foreach (char ch in text) h = h * 31 + ch; return h; }

    void Ribbon(DrawingContext dc, double t, Pen[] pens, double begin, double end, double linger, Func<double, Point> emitter)
    {
        double head = Math.Min(t, end), tail = Math.Max(begin, t - linger);
        if (head <= tail || t < begin) return;
        const int samples = 12;
        double step = linger / samples;
        var previous = emitter(head);
        for (int index = 1; index <= samples; index++)
        {
            double sample = head - index * step;
            if (sample < tail) break;
            double age = (t - sample) / linger;
            var point = emitter(sample) + new Vector(0, -9 * age * u);
            dc.DrawLine(pens[Math.Clamp((int)(age * pens.Length), 0, pens.Length - 1)], previous, point);
            previous = point;
        }
    }

    // ------------------------------------------------------------ Flugbahnen

    Point CarrierAt(double s)
    {
        Point At(double x, double y) => new(stage.Left + x * stage.Width, stage.Top + y * stage.Height);
        double margin = length * 0.7;
        if (s <= AscentEnd)
            return Cubic(At(0, 0.8) - new Vector(margin, 0), At(0.26, 0.82), At(0.46, 0.42), At(0.56, 0) - new Vector(0, margin), Ease(s / AscentEnd));
        var bend = At(0.46, 0.36);
        var enter = At(0.66, 0.18);
        if (s <= Turn)
            return Cubic(At(0.8, 0) - new Vector(0, margin), At(0.8, 0.08), enter, bend, (s - ReturnStart) / (Turn - ReturnStart));
        var aim = finalFlash - bend; if (aim.LengthSquared < 1) aim = new Vector(1, 1); aim.Normalize();
        var end = finalFlash - aim * (52 * u);
        var exit = bend + (bend - enter) * ((Impact - Turn) / (Turn - ReturnStart));
        return Cubic(bend, exit, end - aim * (length * 0.8), end, (s - Turn) / (Impact - Turn));
    }

    Vector CarrierHeading(double s)
    {
        double a = s <= AscentEnd ? Math.Max(0, s - 0.004) : Math.Max(ReturnStart, s - 0.004);
        double b = s <= AscentEnd ? Math.Min(AscentEnd, s + 0.004) : s + 0.004;
        return Unit(CarrierAt(b) - CarrierAt(a));
    }

    Point TailAt(double s) => CarrierAt(s) - CarrierHeading(s) * (54 * u);
    double MoteScale(Mote mote) => Math.Max(0.9, 0.62 * u) * (0.9 + 0.2 * mote.Seed);

    (Point, Vector) MoteAt(Mote mote, double s)
    {
        double d = Impact - mote.Release;
        double u = Math.Clamp((s - mote.Release) / d, 0, 1);
        double u2 = u * u, u3 = u2 * u;
        // kubische Hermite-Kurve: Ort und Geschwindigkeit stetig (Start = Träger, Ende = Sturzflug)
        var point = (Vector)mote.Start * (2 * u3 - 3 * u2 + 1) + mote.V0 * d * (u3 - 2 * u2 + u) + (Vector)mote.End * (-2 * u3 + 3 * u2) + mote.V1 * d * (u3 - u2);
        var velocity = (Vector)mote.Start * (6 * u2 - 6 * u) + mote.V0 * d * (3 * u2 - 4 * u + 1) + (Vector)mote.End * (-6 * u2 + 6 * u) + mote.V1 * d * (3 * u2 - 2 * u);
        var heading = Unit(velocity);
        return ((Point)point - heading * (10 * MoteScale(mote)) * Smooth(u / 0.3), heading);
    }

    /// <summary>Gleichmäßig über alle Bildschirme (Fläche), mit Abstand vom Rand; erster Punkt = Mitte des Hauptbildschirms.</summary>
    List<Point> SpreadTargets(Random random)
    {
        var candidates = new List<Point>();
        foreach (var screen in screens)
        {
            double step = Math.Min(screen.Width, screen.Height) / 10;
            for (double y = screen.Top + screen.Height * 0.1; y < screen.Bottom - screen.Height * 0.08; y += step)
                for (double x = screen.Left + screen.Width * 0.07; x < screen.Right - screen.Width * 0.05; x += step)
                    candidates.Add(new Point(x + (random.NextDouble() - 0.5) * step * 0.6, y + (random.NextDouble() - 0.5) * step * 0.6));
        }
        // Иерихон trifft dort, wo die meisten Symbole liegen (Dichte mit Gauß-Kern), sonst die Bildschirmmitte.
        Point main = new(stage.Left + stage.Width * 0.55, stage.Top + stage.Height * 0.55);
        if (icons.Count > 0)
        {
            double sigma = iconSize * 2.6;
            main = icons.Select(i => i.Center).OrderByDescending(c => icons.Sum(o => Math.Exp(-(o.Center - c).LengthSquared / (2 * sigma * sigma)))).First();
            var cluster = icons.Where(o => (o.Center - main).Length < sigma * 1.5).Select(o => o.Center).ToList();
            main = new Point(cluster.Average(c => c.X), cluster.Average(c => c.Y));
        }
        var chosen = new List<Point> { main };
        // Kleine Raketen zuerst auf Symbole außerhalb der Hauptexplosion: jeweils dorthin, wo die meisten
        // noch nicht getroffenen Symbole liegen (gierige Abdeckung), dann freie Flächen.
        var open = icons.Select(i => i.Center).Where(c => (c - main).Length > MainBlastRadius).ToList();
        while (chosen.Count < 19 && open.Count > 0)
        {
            var seed = open.OrderByDescending(c => open.Count(o => (o - c).Length <= MiniBlastRadius)).First();
            var group = open.Where(o => (o - seed).Length <= MiniBlastRadius).ToList();
            var at = new Point(group.Average(c => c.X), group.Average(c => c.Y));
            chosen.Add(at);
            open.RemoveAll(o => (o - at).Length <= MiniBlastRadius * 1.05 || (o - seed).Length <= MiniBlastRadius);
        }
        while (chosen.Count < 19 && candidates.Count > 0)
        {
            var best = candidates.OrderByDescending(p => chosen.Min(c => (c - p).LengthSquared)).First();
            candidates.Remove(best);
            chosen.Add(best);
        }
        while (chosen.Count < 19) chosen.Add(new Point(stage.Left + random.NextDouble() * stage.Width, stage.Top + random.NextDouble() * stage.Height));
        return chosen;
    }

    /// <summary>Wirkradius der Hauptexplosion bzw. einer kleinen (Bildschirmpixel).</summary>
    internal double MainBlastRadius => length * 1.9;
    internal double MiniBlastRadius => length * 0.95;

    Rect ScreenOf(Point p) { foreach (var s in screens) if (s.Contains(p)) return s; return stage; }

    // ------------------------------------------------------------ Hilfen

    static Transform Place(Point point, Vector direction, double scale)
    {
        double a = Math.Atan2(direction.Y, direction.X);
        double c = Math.Cos(a) * scale, s = Math.Sin(a) * scale;
        return new MatrixTransform(c, s, -s, c, point.X, point.Y);
    }

    static Pen[] TrailPens(double width, double alpha)
    {
        var pens = new Pen[6];
        var warm = (Color)ColorConverter.ConvertFromString("#FFC690");
        var smoke = (Color)ColorConverter.ConvertFromString("#B9B2AA");
        for (int index = 0; index < pens.Length; index++)
        {
            double k = index / (double)(pens.Length - 1);
            double m = Math.Min(1, k * 2.2);
            var color = Color.FromArgb((byte)(255 * alpha * Math.Pow(1 - k * 0.92, 1.4)),
                (byte)(warm.R + (smoke.R - warm.R) * m), (byte)(warm.G + (smoke.G - warm.G) * m), (byte)(warm.B + (smoke.B - warm.B) * m));
            var pen = new Pen(new SolidColorBrush(color), width * (0.7 + 1.5 * k)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            pen.Freeze();
            pens[index] = pen;
        }
        return pens;
    }

    static double Rotate(double from, double to, double k)
    {
        double d = to - from;
        while (d > Math.PI) d -= Math.Tau;
        while (d < -Math.PI) d += Math.Tau;
        return from + d * k;
    }
    /// <summary>Weich an, leicht überschwingen, einschwingen.</summary>
    static double Overshoot(double k)
    {
        k = Math.Clamp(k, 0, 1);
        double e = k * k * (3 - 2 * k);
        return e + 0.06 * Math.Sin(Math.PI * k) * Math.Max(0, k - 0.5) * 2;
    }
    /// <summary>Fahrprofil: sanft anfahren und bremsen.</summary>
    static double Drive(double k) { k = Math.Clamp(k, 0, 1); return k * k * k * (k * (6 * k - 15) + 10); }
    static Point Cubic(Point a, Point b, Point c, Point d, double t)
    {
        double w = 1 - t;
        return a + (b - a) * (3 * w * w * t) + (c - a) * (3 * w * t * t) + (d - a) * (t * t * t);
    }
    static Vector Unit(Vector v) { if (v.LengthSquared < 1e-6) return new Vector(1, 0); v.Normalize(); return v; }
    static double Ease(double t) { t = Math.Clamp(t, 0, 1); return t * t * (1.6 - 0.6 * t); }
    static double Smooth(double v) { v = Math.Clamp(v, 0, 1); return v * v * (3 - 2 * v); }
    static Brush Solid(string c) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(c)); b.Freeze(); return b; }
    static Brush Glow(string c)
    {
        var color = (Color)ColorConverter.ConvertFromString(c);
        var b = new RadialGradientBrush();
        b.GradientStops.Add(new GradientStop(color, 0));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
        b.Freeze();
        return b;
    }
}
