using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskKeeper;

/// <summary>
/// Short-Serie für TikTok, Instagram Reels und YouTube Shorts : je Fahrzeug ein eigenes Hochformat-Video 1080×1920.
/// Der Desktop ist ein echter Breitbild-Desktop (1920×1080), groß in der Bildmitte, Text darüber und darunter in der sicheren Zone.
/// Ablauf: Aufhänger mit dem Modell → „Inventar“-Karte mit echten Daten → Einsatz auf dem Desktop → DeskKeeper stellt alles wieder her
/// → Abspann „alle Szenen und Helfer auf DeskKeeper“. DeskKeeper.exe --shorts &lt;ordner&gt; [kennung …] (mit DESKKEEPER_DEMO=1).
/// </summary>
static class Shorts
{
    const int W = 1080, H = 1920, Fps = 30;
    static readonly Size Frame = new(W, H);
    internal static readonly Rect Screen = new(0, 650, W, W * 1080.0 / 1920);   // Desktop 16:9 über die ganze Breite
    static readonly Typeface Heavy = new(new FontFamily("Segoe UI Black, Segoe UI"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal);
    static readonly Typeface Semi = new(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    static readonly Typeface Mono = new(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    static readonly Brush Accent = Frozen(new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF)));
    static readonly Brush Hot = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0x6A, 0x3D)));
    static readonly Brush Muted = Frozen(new SolidColorBrush(Color.FromRgb(0xB8, 0xC4, 0xDA)));
    static readonly Brush ChipBack = Frozen(new SolidColorBrush(Color.FromArgb(0xC8, 0x12, 0x18, 0x2C)));

    /// <summary>Ein Fahrzeug der Serie: Kennung, Name, Aufhänger, drei Kennzahlen, Satz zum Einsatz, Zeichnung (Mitte, Größe der längsten Ausdehnung, Zeit).</summary>
    internal record Entry(string Kind, bool Helper, string Name, string Hook, string[] Specs, string Action, Action<DrawingContext, Point, double, double>? Model);

    static readonly double Up = -Math.PI / 2;
    internal static readonly Entry[] Series =
    [
        new("jericho", false, "Jericho", "One missile. A cloud of mini rockets.", ["Main warhead", "Cluster of rockets", "One per icon group"], "It splits over your icons.",
            (dc, c, s, t) => Art.Missile(dc, c, new Vector(0, -1), 0, true, 0.3, s / 60, t)),
        new("nuke", false, "Nuke", "The one you shouldn't press.", ["Flash", "Fireball", "Mushroom cloud"], "Everything is ash.", null),
        new(Attacks.Tu160, false, "Tu-160M", "The heaviest bomber flying.", ["54.1 m long", "55.7 m span, swing wing", "Kh-101 cruise missiles"], "Cruise missiles on every group of icons.",
            (dc, c, s, t) => Art.Bomber(dc, c, Up, 20, false, 0, s / 112, t)),
        new(Attacks.Tu22, false, "Tu-22M3", "Swing wing. Carpet bombing.", ["42.5 m long", "Wings 20° to 65°", "Afterburner on the way out"], "Bombs along every row, then wings back and gone.",
            (dc, c, s, t) => Planes.Tu22(dc, c, Up, s / 110, 0, 20 + 22 * (1 - Math.Cos(t * 1.4)), 0, t)),
        new(Attacks.Su34, false, "Su-34", "Precision strike, row by row.", ["23.3 m long", "Side-by-side cockpit", "KAB-500 · Kh-29 · R-73"], "Guided bombs on your icon rows.",
            (dc, c, s, t) => Planes.Su34(dc, c, Up, s / 116, 0, t)),
        new(Attacks.Ka52, false, "Ka-52", "Two rotors. No tail rotor.", ["14.5 m coaxial rotors", "30 mm cannon", "S-8 rockets · Vikhr"], "It hunts every icon one by one.",
            (dc, c, s, t) => Planes.Ka52(dc, c, Up, s / 105, 0, t)),
        new(Attacks.Tu95, false, "Tu-95MS", "Four engines. Eight propellers.", ["46.2 m long", "Contra-rotating props", "Kh-101 cruise missiles"], "Missiles from the wing stations.",
            (dc, c, s, t) => Planes.Tu95(dc, c, Up, s / 112, 0, t)),
        new(Attacks.Il76, false, "Il-76", "It doesn't bomb. It steals.", ["46.6 m long", "Rear ramp open", "Paratroopers"], "Paratroopers carry your icons away.",
            (dc, c, s, t) => Planes.Il76(dc, c, Up, s / 113, 0, true)),
        new(Attacks.Ac130, false, "AC-130U", "A flying gun battery.", ["40.4 m span", "105 · 40 · 25 mm guns", "Circles every group"], "It circles each group and takes it apart.",
            (dc, c, s, t) => Planes.AC130(dc, c, Up, s / 137, 0, t)),
        new(Attacks.Shahed, false, "Shahed-136", "You hear them before you see them.", ["3.5 m long", "Pusher prop, delta wing", "A whole swarm"], "One drone for every icon.",
            (dc, c, s, t) => Planes.Shahed(dc, c, Up, s / 96, 0, t)),
        new(Attacks.A10, false, "A-10C", "Built around a gun.", ["16.3 m long", "GAU-8 30 mm", "AGM-65 Maverick"], "Gun runs along your icon rows.",
            (dc, c, s, t) => Planes.A10(dc, c, Up, s / 108, 0, t)),
        new(Attacks.Su25, false, "Su-25", "The flying tank.", ["15.4 m long", "B-8M1 rocket pods", "30 mm gun"], "Rocket salvos, then gun runs.",
            (dc, c, s, t) => Planes.Su25(dc, c, Up, s / 101, 0, t)),
        new(Helpers.Dume, true, "DUM-E", "The original helper.", ["Robot arm", "Trailer", "One icon at a time"], "Carries every icon back home.",
            (dc, c, s, t) => Helpers.DrawPortrait(dc, Helpers.Dume, c, s / 3)),
        new(Helpers.Forklift, true, "Forklift", "Pallet goes in. Icons go home.", ["Four icons per pallet", "Turns on the spot", "Sets them down gently"], "Four at a time on the pallet.",
            (dc, c, s, t) => Helpers.DrawPortrait(dc, Helpers.Forklift, c, s / 2.2)),
        new(Helpers.Crane, true, "Crane truck", "Outriggers down. Boom out.", ["Four-axle chassis", "Telescopic boom", "Never parks on your icons"], "Lifts each icon onto its place.",
            (dc, c, s, t) => CraneHelper.DrawTruck(dc, c + new Vector(0, s * 0.2), Up, s / 2.6, 1, 0.8 + 0.3 * Math.Sin(t), s * 0.9, 0.3)),
        new(Helpers.Drones, true, "Delivery drones", "A swarm with a job.", ["Up to six drones", "Landing pad", "One icon each"], "Every drone brings one icon home.",
            (dc, c, s, t) => Helpers.DrawPortrait(dc, Helpers.Drones, c, s / 3)),
        new(Helpers.Chinook, true, "CH-47 Chinook", "Two rotors, one cargo net.", ["15.9 m fuselage", "Tandem rotors 18.3 m", "Up to four icons as sling load"], "Icons in a cargo net under the hook.",
            (dc, c, s, t) => LiftHelper.DrawChinook(dc, c, Up, s / 30.2, 0.2, t * 9)),
        new(Helpers.Mi26, true, "Mi-26", "The biggest helicopter in the world.", ["33.7 m fuselage", "Eight-blade rotor, 32 m", "Up to eight icons"], "Eight icons per trip in the net.",
            (dc, c, s, t) => LiftHelper.DrawMi26(dc, c, Up, s / 40, 0.2, t * 9)),
    ];

    record Shot(double Start, double End, Action<DrawingContext, double, double> Draw);

    /// <summary>Aussprache für die Sprecherstimme.</summary>
    static readonly Dictionary<string, string> Say = new()
    {
        ["jericho"] = "Jericho", ["nuke"] = "The nuke", [Attacks.Tu160] = "The Tu one sixty M", [Attacks.Tu22] = "The Tu twenty-two M three", [Attacks.Su34] = "The Sukhoi thirty-four",
        [Attacks.Ka52] = "The Ka fifty-two", [Attacks.Tu95] = "The Tu ninety-five", [Attacks.Il76] = "The Ilyushin seventy-six", [Attacks.Ac130] = "The A C one thirty gunship",
        [Attacks.Shahed] = "A Shahed swarm", [Attacks.A10] = "The A ten Warthog", [Attacks.Su25] = "The Sukhoi twenty-five", [Helpers.Dume] = "Dummy the robot arm",
        [Helpers.Forklift] = "A forklift", [Helpers.Crane] = "A crane truck", [Helpers.Drones] = "Delivery drones", [Helpers.Chinook] = "A Chinook", [Helpers.Mi26] = "The Mil twenty-six",
    };

    public static int Run(string output, IReadOnlyCollection<string> only)
    {
        int part = 0;
        foreach (var e in Series)
        {
            part++;
            if (only.Count > 0 && !only.Contains(e.Kind)) continue;
            var dir = Path.Combine(output, e.Kind);
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.EnumerateFiles(dir, "*.png")) File.Delete(f);
            RenderOne(e, part, dir);
        }
        Helpers.Current = Helpers.Dume;
        return 0;
    }

    static void RenderOne(Entry e, int part, string dir)
    {
        var shots = new List<Shot>(); var cues = new List<Promo.Cue>();
        double at = 0;
        void Add(double len, string kind, Action<DrawingContext, double, double> draw) { shots.Add(new Shot(at, at + len, draw)); cues.Add(new Promo.Cue(at, kind, 0, 1)); at += len; }
        string series = $"PART {part} / {Series.Length}";
        string title = e.Helper ? $"{e.Name} cleans up my desktop" : $"{e.Name} vs. my desktop";

        // 1) Aufhänger: Modell groß, Name, Satz; im ersten Bild schon lesbar
        Add(2.2, "title", (dc, t, d) =>
        {
            Blueprint(dc, t);
            if (e.Model is { } m) { double s = 760 * (1.06 - 0.06 * Film.Ease.Out(t / d)); Float(dc, t, () => m(dc, new Point(W / 2.0, 1000), s, t)); }
            else NukeHint(dc, t);
            Film.Chip(dc, 1, series, 24, Mono, Accent, ChipBack, W / 2.0, 214);
            Film.Words(dc, t + 1, title, 72, Heavy, Brushes.White, Accent, e.Name.Split(' ')[0], W / 2.0, 290, 12);
            Film.Words(dc, t, e.Hook, 44, Semi, Muted, Accent, null, W / 2.0, 1560, 8, 0.3);
        });
        // 2) Inventar: Modell, drei Kennzahlen nacheinander
        double specs = at;
        Add(2.4, "card", (dc, t, d) =>
        {
            Blueprint(dc, t + 2.2);
            if (e.Model is { } m) Float(dc, t + 2.2, () => m(dc, new Point(W / 2.0, 900), 700, t + 2.2));
            else NukeHint(dc, t + 2.2);
            Film.Chip(dc, 1, "INVENTORY", 24, Mono, Accent, ChipBack, W / 2.0, 214);
            Film.Words(dc, 1, e.Name, 96, Heavy, Brushes.White, Accent, null, W / 2.0, 280, 14);
            for (int i = 0; i < e.Specs.Length; i++)
                Film.Chip(dc, Math.Clamp(t - 0.25 - i * 0.35, 0, 1) * 3.3, e.Specs[i], 46, Semi, Brushes.White, ChipBack, W / 2.0, 1300 + i * 100, label: false);
        });
        for (int i = 0; i < e.Specs.Length; i++) cues.Add(new Promo.Cue(specs + 0.25 + i * 0.35, "drop", 0, 0.8));

        // 3) Einsatz auf dem echten Breitbild-Desktop
        var (draw, ev, scene, icons, homes) = e.Helper ? HelperAction(e.Kind, 6.4) : AttackAction(e.Kind, 6.4);
        double act = at;
        Add(6.4, "clip", (dc, t, d) =>
        {
            Backdrop(dc);
            Film.WhipIn(dc, Frame, t, () => Monitor(dc, () => Film.Camera(dc, new Size(1920, 1080), t, d, 0.06, Film.Shake(ev, t, 1), () => draw(dc, t))));
            Film.Chip(dc, 1, series, 24, Mono, Accent, ChipBack, W / 2.0, 214);
            Film.Words(dc, t + 1, e.Name, 88, Heavy, Brushes.White, Accent, null, W / 2.0, 300, 14);
            Film.Words(dc, t, e.Action, 46, Semi, Brushes.White, Hot, null, W / 2.0, 1360, 9, 0.2);
        });
        cues.AddRange(ev.Select(c => c with { T = c.T + act }));

        // 4) Auflösung: alles wieder an seinem Platz (bei Angriffen fliegen die Symbole aus der Asche zurück)
        double back = at;
        Add(2.0, "caption", (dc, t, d) =>
        {
            Backdrop(dc);
            Monitor(dc, () =>
            {
                Wallpaper(dc);
                for (int i = 0; i < icons.Count; i++)
                {
                    var icon = icons[i]; var home = homes[icon.Id];
                    double k = e.Helper ? 1 : Film.Ease.Out(Math.Clamp((t - i * 0.03) / 0.5, 0, 1));
                    var from = new Point(home.X, 1180);
                    DeskIcon(dc, icon, from + (home - from) * k);
                }
            });
            Check(dc, Math.Clamp((t - (e.Helper ? 0.1 : 0.7)) / 0.3, 0, 1));
            Film.Words(dc, t, "Every icon back.", 76, Heavy, Brushes.White, Accent, null, W / 2.0, 300, 12);
            Film.Words(dc, t, "Exactly where it was.", 52, Semi, Accent, Accent, null, W / 2.0, 400, 9, 0.15);
        });
        cues.Add(new Promo.Cue(back + (e.Helper ? 0.1 : 0.7), "success", 0, 1));
        if (!e.Helper) for (int i = 0; i < icons.Count; i += 3) cues.Add(new Promo.Cue(back + i * 0.03 + 0.45, "drop", Math.Clamp(homes[icons[i].Id].X / 1920 * 2 - 1, -1, 1), 0.5));

        // 5) Abspann: wo es das alles gibt
        Add(2.2, "end", (dc, t, d) => EndCard(dc, t));

        Promo.WriteTimeline(Path.Combine(dir, "timeline.json"), shots.Select(s => s.Start).ToList(), at, cues);
        // Sprechertext (werkzeuge/voice.py, Kokoro): kurz, je Abschnitt ein Satz; Namen in Lautschrift-naher Schreibung
        string say = Say.TryGetValue(e.Kind, out var spoken) ? spoken : e.Name;
        var lines = new List<(double T, string Text)>
        {
            (0.15, e.Helper ? $"{say} cleans up my desktop." : $"{say} versus my desktop."),
            (specs + 0.15, e.Hook),
            (act + 0.4, e.Action),
            (back + 0.1, "Every icon back."),
            (at - 2.2 + 0.25, "All of it on DeskKeeper."),
        };
        File.WriteAllText(Path.Combine(dir, "voice.json"), "[" + string.Join(",", lines.Select(l => $"{{\"t\":{l.T.ToString("0.###", CultureInfo.InvariantCulture)},\"say\":\"{l.Text.Replace("\"", "")}\"}}")) + "]");
        int frames = (int)Math.Round(at * Fps);
        var target = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
        for (int f = 0; f < frames; f++)
        {
            double time = f / (double)Fps;
            var s = shots.Last(x => time >= x.Start - 1e-9);
            var v = new DrawingVisual();
            using (var dc = v.RenderOpen()) { s.Draw(dc, time - s.Start, s.End - s.Start); Film.Look(dc, Frame, f, 0.06); }
            target.Clear(); target.Render(v);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(target));
            using var file = File.Create(Path.Combine(dir, $"{f:D5}.png")); enc.Save(file);
        }
    }

    // ------------------------------------------------------------ Einsätze
    internal static (Action<DrawingContext, double> Draw, List<Promo.Cue> Cues, Scene Scene, List<IconInfo> Icons, Dictionary<string, Point> Homes) AttackAction(string kind, double length, double lead = 2.0)
    {
        Helpers.Current = Helpers.Dume;
        var desk = new Rect(0, 0, 1920, 1080);
        var (icons, homes) = Promo.DemoDesk();
        bool nuke = kind == "nuke", jericho = kind is "jericho" or "nuke";
        var scene = new Scene(icons, homes, 56, [desk], desk, true, 64, nuclear: nuke, attack: jericho ? null : kind);
        var raid = scene.Raid;
        double first = raid.Count > 0 ? raid.Min(r => r.T) : Scene.Impact, last = raid.Count > 0 ? raid.Max(r => r.T) : Scene.Impact;
        double t0 = jericho ? Scene.Impact - 2.4 : Math.Max(0, first - lead);
        double span = jericho ? length : Math.Clamp(last + 1.0 - t0, length, length * 2.6);
        double speed = span / length;
        double V(double st) => (st - t0) / speed;
        bool In(double v) => v >= 0 && v <= length;
        var cues = new List<Promo.Cue>();
        string bed = kind switch { Attacks.Tu95 or Attacks.Il76 or Attacks.Ac130 => "props", Attacks.Ka52 => "rotor", Attacks.Shahed => "moped", "jericho" or "nuke" => "", _ => "jet" };
        if (bed != "") cues.Add(new Promo.Cue(0, bed, 0, 1.2, length));
        if (jericho) cues.Add(new Promo.Cue(V(Scene.Impact), nuke ? "nuke" : "bigboom", 0, 1));
        foreach (var r in raid) if (In(V(r.T))) cues.Add(new Promo.Cue(V(r.T), "boom", Math.Clamp(r.P.X / 1920 * 2 - 1, -1, 1), 0.8));
        if (scene.Attack is GunshipRaid g) foreach (var sh in g.Shots) if (In(V(sh.Fire))) cues.Add(new Promo.Cue(V(sh.Fire), sh.Gun == GunshipRaid.Gun.G105 ? "cannon" : sh.Gun == GunshipRaid.Gun.G40 ? "pom" : "tick", 0, 0.6));
        if (scene.Attack is StrafeRaid sr) foreach (var sh in sr.Shots) if (In(V(sh.Fire))) cues.Add(new Promo.Cue(V(sh.Fire), sh.Weapon == StrafeRaid.Weapon.Gun ? "brrt" : "rocket", 0, 0.6));
        var thin = new List<Promo.Cue>();
        foreach (var c in cues.OrderBy(c => c.T)) if (c.Kind != "boom" || !thin.Any(o => o.Kind == "boom" && Math.Abs(o.T - c.T) < 0.06)) thin.Add(c);
        return ((dc, t) => DrawScene(dc, scene, icons, t0 + t * speed), thin, scene, icons, homes);
    }

    internal static (Action<DrawingContext, double> Draw, List<Promo.Cue> Cues, Scene Scene, List<IconInfo> Icons, Dictionary<string, Point> Homes) HelperAction(string kind, double length)
    {
        var desk = new Rect(0, 0, 1920, 1080);
        var (all, homes0) = Promo.DemoDesk();
        var icons0 = all.Take(10).ToList();
        var homes = icons0.ToDictionary(i => i.Id, i => homes0[i.Id] + new Vector(260, 120));   // Ziele mit Abstand zum Rand
        var rnd = new Random(kind.Length * 13);
        var icons = icons0.Select(i => i with { Center = new Point(900 + rnd.Next(0, 800), 260 + rnd.Next(0, 600)) }).ToList();
        Helpers.Current = kind;
        var scene = new Scene(icons, homes, 64, [desk], desk, false, 110);
        double speed = Math.Clamp((scene.Duration - scene.RobotStart) / (length * 1.6), 1.2, 6);
        double t0 = Math.Max(scene.RobotStart, Promo.FirstDrop(scene, icons, homes) - length * speed * 0.3);
        var cues = new List<Promo.Cue> { new(0, kind is Helpers.Chinook or Helpers.Mi26 ? "rotor" : kind == Helpers.Drones ? "drones" : "servo", 0, 1.2, length) };
        var placed = new HashSet<string>();
        for (double v = 0; v <= length; v += 1.0 / 30)
        {
            var c = scene.IconCenters(t0 + v * speed);
            foreach (var i in icons)
                if (!placed.Contains(i.Id) && c[i.Id] is { } p && (p - homes[i.Id]).Length < 0.5) { placed.Add(i.Id); if (v > 0.02) cues.Add(new Promo.Cue(v, "drop", Math.Clamp(p.X / 1920 * 2 - 1, -1, 1), 0.7)); }
        }
        return ((dc, t) => DrawScene(dc, scene, icons, t0 + t * speed), cues, scene, icons, homes);
    }

    // ------------------------------------------------------------ Bild
    static readonly Brush WallBrush = Frozen(new LinearGradientBrush(Color.FromRgb(0x14, 0x22, 0x4A), Color.FromRgb(0x2A, 0x16, 0x4A), 35));
    internal static void Wallpaper(DrawingContext dc) => dc.DrawRectangle(WallBrush, null, new Rect(0, 0, 1920, 1080));

    internal static void DeskIcon(DrawingContext dc, IconInfo icon, Point c)
    {
        const double z = 58;
        dc.DrawImage(Demo.Image(icon.Id), new Rect(c.X - z / 2, c.Y - z * 0.58, z, z));
        var label = new FormattedText(icon.Name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Semi, 15, Brushes.White, 1);
        dc.DrawText(label, new Point(c.X - label.Width / 2, c.Y + z * 0.46));
    }

    internal static void DrawScene(DrawingContext dc, Scene scene, List<IconInfo> icons, double time)
    {
        Wallpaper(dc);
        var centers = scene.IconCenters(time);
        foreach (var icon in icons) if (centers[icon.Id] is { } c) DeskIcon(dc, icon, c);
        scene.Draw(dc, time);
    }

    /// <summary>Breitbild-Desktop in der Bildmitte: 1920×1080 auf die volle Breite, mit Rahmen und Schatten.</summary>
    internal static void Monitor(DrawingContext dc, Action draw)
    {
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x90, 0, 0, 0)), null, new Rect(Screen.X, Screen.Y + 18, Screen.Width, Screen.Height));
        dc.PushClip(new RectangleGeometry(Screen));
        dc.PushTransform(new MatrixTransform(Screen.Width / 1920, 0, 0, Screen.Height / 1080, Screen.X, Screen.Y));
        draw();
        dc.Pop(); dc.Pop();
        dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(0x60, 255, 255, 255)), 2), Screen);
    }

    internal static void Backdrop(DrawingContext dc)
    {
        dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0x0A, 0x0E, 0x1A), Color.FromRgb(0x13, 0x17, 0x2B), 90), null, new Rect(Frame));
        dc.DrawEllipse(new RadialGradientBrush(Color.FromArgb(0x40, 0x4C, 0x7B, 0xFF), Color.FromArgb(0, 0x4C, 0x7B, 0xFF)), null, new Point(W / 2.0, H * 0.5), 760, 760);
    }

    /// <summary>Bauplan-Hintergrund für die Inventar-Karten: dunkles Blau mit Raster, das langsam wandert.</summary>
    internal static void Blueprint(DrawingContext dc, double t)
    {
        Backdrop(dc);
        var minor = new Pen(new SolidColorBrush(Color.FromArgb(0x16, 0x4C, 0xC2, 0xFF)), 1);
        var major = new Pen(new SolidColorBrush(Color.FromArgb(0x2C, 0x4C, 0xC2, 0xFF)), 1.5);
        double o = (t * 12) % 60;
        for (double x = -60 + o; x < W + 60; x += 60) dc.DrawLine(((int)Math.Round((x - o) / 60) % 4 == 0) ? major : minor, new Point(x, 0), new Point(x, H));
        for (double y = -60 + o; y < H + 60; y += 60) dc.DrawLine(((int)Math.Round((y - o) / 60) % 4 == 0) ? major : minor, new Point(0, y), new Point(W, y));
    }

    internal static void Float(DrawingContext dc, double t, Action draw)
    {
        dc.PushTransform(new TranslateTransform(Math.Sin(t * 0.9) * 10, Math.Sin(t * 1.3) * 8));
        dc.PushTransform(new RotateTransform(Math.Sin(t * 0.6) * 4, W / 2.0, 950));
        draw();
        dc.Pop(); dc.Pop();
    }

    static void NukeHint(DrawingContext dc, double t)
    {
        double pulse = 0.85 + 0.15 * Math.Sin(t * 6);
        var c = new Point(W / 2.0, 950);
        dc.DrawEllipse(new RadialGradientBrush(Color.FromArgb(0xC0, 0xFF, 0xE0, 0x90), Color.FromArgb(0, 0xFF, 0x60, 0x20)), null, c, 380 * pulse, 380 * pulse);
        var g = new FormattedText("☢", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI Symbol"), 360, Brushes.Black, 1);
        dc.DrawText(g, new Point(c.X - g.Width / 2, c.Y - g.Height / 2));
    }

    internal static void Check(DrawingContext dc, double k, double y = 1450)
    {
        var c = new Point(W / 2.0, y);
        double r = 80 * (0.6 + 0.4 * Film.Ease.Out(k));
        dc.PushOpacity(k);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0xF0, 0x16, 0xA3, 0x4A)), new Pen(Brushes.White, 6), c, r, r);
        var g = new FormattedText("", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe Fluent Icons"), r, Brushes.White, 1);
        dc.DrawText(g, new Point(c.X - g.Width / 2, c.Y - g.Height / 2));
        dc.Pop();
    }

    static void EndCard(DrawingContext dc, double t)
    {
        Backdrop(dc);
        double k = Film.Ease.Out(t / 0.5);
        var logo = new Rect(W / 2.0 - 80, 560 + (1 - k) * 30, 160, 160);
        dc.PushOpacity(k);
        dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(0x4C, 0xC2, 0xFF), Color.FromRgb(0x00, 0x5F, 0xB8), 45), null, logo, 36, 36);
        var glyph = new FormattedText("", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe Fluent Icons"), 78, Brushes.White, 1);
        dc.DrawText(glyph, new Point(logo.X + (logo.Width - glyph.Width) / 2, logo.Y + (logo.Height - glyph.Height) / 2));
        dc.Pop();
        Film.Words(dc, t, "All of it on DeskKeeper", 72, Heavy, Brushes.White, Accent, "DeskKeeper", W / 2.0, 780, 12);
        Film.Words(dc, t, "12 scenes · 6 helpers · more coming", 42, Semi, Muted, Accent, null, W / 2.0, 900, 8, 0.25);
        Film.Words(dc, t, "Free & open source for Windows", 42, Semi, Muted, Accent, null, W / 2.0, 965, 8, 0.4);
        Film.Chip(dc, Math.Clamp(t - 0.7, 0, 1) * 3.3, "github.com/VenzyProgramming/DeskKeeper", 32, Semi, Accent, ChipBack, W / 2.0, 1090);
    }

    static T Frozen<T>(T f) where T : Freezable { f.Freeze(); return f; }
}
