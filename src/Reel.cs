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
/// Hochformat-Kurzvideo für TikTok, Instagram Reels und YouTube Shorts , 1080×1920, 30 Bilder/s, nur Vorführdaten:
/// DeskKeeper.exe --reel &lt;ordner&gt; (mit DESKKEEPER_DEMO=1). Aufbau nach den Kurzvideo-Skills der Skill-Bibliothek
/// (nateherkai short-form-edit, whaleyxbt motion-design): stumm verständlicher Aufhänger in den ersten drei Sekunden, eine offene
/// Frage („kommen die Symbole zurück?“), Steigerung in schnellen Schnitten, Auflösung mit ≥ 1,5 s Haltezeit, Abspann, letztes Bild
/// gleich dem ersten (Schleife). Texte nur in der sicheren Zone x 90…930, y 200…1600. Schreibt timeline.json für die Tonspur.
/// </summary>
static class Reel
{
    const int W = 1080, H = 1920, Fps = 30;
    static readonly Rect Desk = new(0, 0, W, H);
    static readonly Size Size = new(W, H);
    static readonly Typeface Heavy = new(new FontFamily("Segoe UI Black, Segoe UI"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal);
    static readonly Typeface Semi = new(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    static readonly Brush Accent = Frozen(new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF)));
    static readonly Brush Hot = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0x6A, 0x3D)));

    record Shot(double Start, double End, Action<DrawingContext, double, double> Draw);

    public static int Run(string output)
    {
        Directory.CreateDirectory(output);
        foreach (var f in Directory.EnumerateFiles(output, "*.png")) File.Delete(f);
        var (icons, homes) = Layout();
        var shots = new List<Shot>(); var cues = new List<Promo.Cue>();
        double at = 0; int attackNo = 1, helperNo = 0;
        void Add(double length, Action<DrawingContext, double, double> draw) { shots.Add(new Shot(at, at + length, draw)); at += length; }

        // 0,0–1,6 s: ordentlicher Desktop, dann wirft Windows alles durcheinander (Spannung ohne Ton verständlich)
        var rnd = new Random(5);
        var messy = icons.ToDictionary(i => i.Id, i => new Point(140 + rnd.Next(0, 800), 520 + rnd.Next(0, 900)));
        Add(1.6, (dc, t, d) =>
        {
            double k = Ease(Math.Clamp((t - 0.35) / 0.6, 0, 1));
            DrawDesk(dc, icons.Select(i => (i, (Point?)Lerp(homes[i.Id], messy[i.Id], k))));
            Headline(dc, t, "Windows shuffled my", "desktop icons. Again.", -1);   // schon im ersten Bild lesbar (Vorschaubild)
        });
        cues.Add(new Promo.Cue(0.35, "shuffle", 0, 1));
        // 1,6–3,4 s: Höhepunkt Nummer eins: der Atompilz
        var (nukeDraw, nukeCues) = AttackShot("nuke", 1.8, messy);
        double s0 = at;
        Add(1.8, (dc, t, d) => { Film.Camera(dc, Size, t, d, 0.08, Film.Shake(nukeCues, t, 1), () => nukeDraw(dc, t)); Headline(dc, t, "So now my desktop", "pays for it.", 0); });
        cues.AddRange(nukeCues.Select(c => c with { T = c.T + s0 }));

        // 3,4–15,5 s: Steigerung in schnellen Schnitten, Name groß
        foreach (var (kind, name) in new[]
        {
            (Attacks.Tu160, "TU-160M"), (Attacks.Su34, "SU-34"), (Attacks.Ka52, "KA-52"), (Attacks.Ac130, "AC-130U"), (Attacks.Shahed, "SHAHED SWARM"),
            (Attacks.A10, "A-10 BRRRT"), (Attacks.Tu22, "TU-22M3"), (Attacks.Su25, "SU-25"), (Attacks.Tu95, "TU-95MS"), ("jericho", "JERICHO"), (Attacks.Il76, "PARATROOPERS"),
        })
        {
            var (draw, ev) = AttackShot(kind, 1.1, homes);
            double s = at; int no = ++attackNo;
            Add(1.1, (dc, t, d) => { Film.WhipIn(dc, Size, t, () => Film.Camera(dc, Size, t, d, 0.07, Film.Shake(ev, t, 1), () => draw(dc, t))); Label(dc, t, name, $"{no:00} / 12"); });
            cues.Add(new Promo.Cue(s, "cut", 0, 1));
            cues.AddRange(ev.Select(c => c with { T = c.T + s }));
        }
        // Atempause: leerer, verbrannter Desktop, offene Frage
        Add(1.0, (dc, t, d) => { DrawDesk(dc, []); Headline(dc, t, "But…", "", 0.15); });
        cues.Add(new Promo.Cue(at - 0.6, "riser", 0, 1));

        // Auflösung: Helfer bringen alles zurück
        foreach (var (kind, name) in new[] { (Helpers.Mi26, "MI-26"), (Helpers.Chinook, "CHINOOK"), (Helpers.Crane, "CRANE TRUCK"), (Helpers.Forklift, "FORKLIFT"), (Helpers.Drones, "DRONES"), (Helpers.Dume, "DUM-E") })
        {
            var (draw, ev) = HelperShot(kind, 1.15, icons, homes);
            double s = at; int no = ++helperNo;
            Add(1.15, (dc, t, d) => { Film.WhipIn(dc, Size, t, () => Film.Camera(dc, Size, t, d, 0.05, default, () => draw(dc, t))); Label(dc, t, name, $"Helper {no} / 6"); });
            cues.Add(new Promo.Cue(s, "cut", 0, 0.7));
            cues.AddRange(ev.Select(c => c with { T = c.T + s }));
        }
        Helpers.Current = Helpers.Dume;
        // Ergebnis halten: jedes Symbol wieder an seinem Platz
        double done = at;
        Add(2.0, (dc, t, d) =>
        {
            DrawDesk(dc, icons.Select(i => (i, (Point?)homes[i.Id])));
            Check(dc, Math.Clamp(t / 0.3, 0, 1));
            Headline(dc, t, "Every icon back.", "Exactly where it was.", 0.1);
        });
        cues.Add(new Promo.Cue(done + 0.05, "success", 0, 1));
        // Abspann: was es ist, was kommt, Mitmachen
        Add(3.2, (dc, t, d) => EndCard(dc, t));
        cues.Add(new Promo.Cue(at - 3.2, "end", 0, 1));
        // Schleife: zurück zum ordentlichen Desktop wie im ersten Bild
        Add(0.6, (dc, t, d) =>
        {
            DrawDesk(dc, icons.Select(i => (i, (Point?)homes[i.Id])));
            dc.PushOpacity(1 - Math.Clamp(t / 0.6, 0, 1)); EndCard(dc, 3.2); dc.Pop();
        });
        Helpers.Current = Helpers.Dume;
        Promo.WriteTimeline(Path.Combine(output, "timeline.json"), shots.Select(s => s.Start).ToList(), at, cues);

        int frames = (int)Math.Round(at * Fps);
        var target = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
        for (int f = 0; f < frames; f++)
        {
            double time = f / (double)Fps;
            var s = shots.Last(x => time >= x.Start - 1e-9);
            var v = new DrawingVisual();
            using (var dc = v.RenderOpen()) { s.Draw(dc, time - s.Start, s.End - s.Start); Film.Look(dc, Size, f, 0.06); }
            target.Clear(); target.Render(v);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(target));
            using var file = File.Create(Path.Combine(output, $"{f:D5}.png")); enc.Save(file);
        }
        return 0;
    }

    // ------------------------------------------------------------ Desktop im Hochformat
    const double IconSize = 64;

    static (List<IconInfo> Icons, Dictionary<string, Point> Homes) Layout()
    {
        var names = Demo.IconSet.Select(x => x.Name).ToList();
        var homes = new Dictionary<string, Point>();
        for (int i = 0; i < names.Count; i++)
        {
            int col = i % 3, row = i / 3;   // zwei Gruppen: links drei Spalten, Reihen in der sicheren Zone
            homes[names[i]] = new Point(150 + col * 140 + (row >= 3 ? 450 : 0), 620 + (row % 3) * 190 + (row >= 3 ? 120 : 0));   // zwei Gruppen ohne Überlappung
        }
        return (names.Select(n => new IconInfo(n, n, homes[n])).ToList(), homes);
    }

    static readonly Brush Wall = Frozen(new LinearGradientBrush(new GradientStopCollection
    {
        new(Color.FromRgb(0x10, 0x1C, 0x40), 0), new(Color.FromRgb(0x24, 0x18, 0x48), 0.55), new(Color.FromRgb(0x3A, 0x16, 0x3E), 1),
    }, 70));

    static void DrawDesk(DrawingContext dc, IEnumerable<(IconInfo Icon, Point? At)> icons, Scene? scene = null, double time = 0)
    {
        dc.DrawRectangle(Wall, null, Desk);
        dc.DrawEllipse(new RadialGradientBrush(Color.FromArgb(0x40, 0x4C, 0x7B, 0xFF), Color.FromArgb(0, 0x4C, 0x7B, 0xFF)), null, new Point(W * 0.55, H * 0.45), 700, 900);
        foreach (var (icon, c) in icons)
        {
            if (c is not { } p) continue;
            double z = IconSize * 1.05;
            dc.DrawImage(Demo.Image(icon.Id), new Rect(p.X - z / 2, p.Y - z * 0.58, z, z));
            var label = Text(icon.Name, 19, Brushes.White, Semi);
            dc.DrawText(label, new Point(p.X - label.Width / 2, p.Y + z * 0.46));
        }
        scene?.Draw(dc, time);
    }

    static (Action<DrawingContext, double> Draw, List<Promo.Cue> Cues) AttackShot(string kind, double length, IReadOnlyDictionary<string, Point> start)
    {
        var (icons0, homes) = Layout();
        var icons = icons0.Select(i => i with { Center = start[i.Id] }).ToList();
        bool nuke = kind == "nuke", jericho = kind is "jericho" or "nuke";
        var scene = new Scene(icons, homes, IconSize, [Desk], Desk, true, 90, nuclear: nuke, attack: jericho ? null : kind);
        var raid = scene.Raid;
        // dichtester Abschnitt der Einschläge: dort schneiden wir hinein
        double center = jericho ? Scene.Impact + (nuke ? 0.6 : 0.3)
            : raid.Count == 0 ? Scene.Impact : raid.Select(r => r.T).OrderBy(x => x).Select(x => (x, raid.Count(o => Math.Abs(o.T - x) < 1.2))).OrderByDescending(p => p.Item2).First().x;
        double span = jericho ? length * 1.1 : length * 2.2;
        double t0 = center - span * 0.45, speed = span / length;
        double V(double st) => (st - t0) / speed;
        var cues = new List<Promo.Cue>();
        string bed = kind switch { Attacks.Tu95 or Attacks.Il76 or Attacks.Ac130 => "props", Attacks.Ka52 => "rotor", Attacks.Shahed => "moped", "jericho" or "nuke" => "", _ => "jet" };
        if (bed != "") cues.Add(new Promo.Cue(0, bed, 0, 1.3, length));
        if (jericho && V(Scene.Impact) is var vi && vi >= 0 && vi < length) cues.Add(new Promo.Cue(vi, nuke ? "nuke" : "bigboom", 0, 1));
        bool In(double v) => v >= 0 && v <= length;
        foreach (var r in raid) if (In(V(r.T))) cues.Add(new Promo.Cue(V(r.T), "boom", Math.Clamp(r.P.X / W * 2 - 1, -1, 1), 0.8));
        if (scene.Attack is GunshipRaid g) foreach (var sh in g.Shots) if (In(V(sh.Fire))) cues.Add(new Promo.Cue(V(sh.Fire), sh.Gun == GunshipRaid.Gun.G105 ? "cannon" : sh.Gun == GunshipRaid.Gun.G40 ? "pom" : "tick", 0, 0.6));
        if (scene.Attack is StrafeRaid sr) foreach (var sh in sr.Shots) if (In(V(sh.Fire))) cues.Add(new Promo.Cue(V(sh.Fire), sh.Weapon == StrafeRaid.Weapon.Gun ? "brrt" : "rocket", 0, 0.6));
        var thin = new List<Promo.Cue>();
        foreach (var c in cues.OrderBy(c => c.T)) if (c.Kind != "boom" || !thin.Any(o => o.Kind == "boom" && Math.Abs(o.T - c.T) < 0.07)) thin.Add(c);
        return ((dc, t) =>
        {
            double st = t0 + t * speed;
            var centers = scene.IconCenters(st);
            DrawDesk(dc, icons.Select(i => (i, centers[i.Id])), scene, st);
        }, thin);
    }

    static (Action<DrawingContext, double> Draw, List<Promo.Cue> Cues) HelperShot(string kind, double length, List<IconInfo> icons0, IReadOnlyDictionary<string, Point> homes)
    {
        var rnd = new Random(kind.Length * 7);
        var icons = icons0.Select(i => i with { Center = new Point(160 + rnd.Next(0, 760), 500 + rnd.Next(0, 1000)) }).ToList();
        Helpers.Current = kind;
        var scene = new Scene(icons, homes, IconSize, [Desk], Desk, false, 120);
        // mitten in die Arbeit hineinschneiden, gerafft
        double speed = Math.Clamp((scene.Duration - scene.RobotStart) / 7, 1.5, 6);
        double t0 = Math.Max(scene.RobotStart, Promo.FirstDrop(scene, icons, homes) - length * speed * 0.3);
        var cues = new List<Promo.Cue> { new(0, kind is Helpers.Chinook or Helpers.Mi26 ? "rotor" : kind == Helpers.Drones ? "drones" : "servo", 0, 1.2, length) };
        var placed = new HashSet<string>();
        for (double v = 0; v <= length; v += 1.0 / 30)
        {
            var c = scene.IconCenters(t0 + v * speed);
            foreach (var i in icons)
                if (!placed.Contains(i.Id) && c[i.Id] is { } p && (p - homes[i.Id]).Length < 0.5) { placed.Add(i.Id); if (v > 0.02) cues.Add(new Promo.Cue(v, "drop", Math.Clamp(p.X / W * 2 - 1, -1, 1), 0.7)); }
        }
        return ((dc, t) =>
        {
            double st = t0 + t * speed;
            var centers = scene.IconCenters(st);
            DrawDesk(dc, icons.Select(i => (i, centers[i.Id])), scene, st);
        }, cues);
    }

    // ------------------------------------------------------------ Schrift: schwer, weiß mit dunkler Kontur (lesbar ohne Unterlegung)
    static void Outlined(DrawingContext dc, string text, double size, Brush fill, Typeface face, double y, double rise = 0, double scale = 1)
    {
        var ft = Text(text, size, fill, face);
        var g = ft.BuildGeometry(new Point((W - ft.Width) / 2, y + rise));
        if (scale != 1) dc.PushTransform(new ScaleTransform(scale, scale, W / 2.0, y + ft.Height / 2));
        dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(0xE6, 0x08, 0x0A, 0x14)), size * 0.16) { LineJoin = PenLineJoin.Round }, g);
        dc.DrawGeometry(fill, null, g);
        if (scale != 1) dc.Pop();
    }

    /// <summary>Zweizeilige Überschrift oben in der sicheren Zone; Zeile zwei 0,12 s später.</summary>
    static void Headline(DrawingContext dc, double t, string one, string two, double delay)
    {
        Film.Words(dc, t, one, 74, Heavy, Brushes.White, Accent, null, W / 2.0, 250, 12, delay);
        if (two != "") Film.Words(dc, t, two, 74, Heavy, Accent, Accent, null, W / 2.0, 340, 12, delay + 0.14);
    }

    /// <summary>Name der Szene: knallt herein (leicht übergroß, setzt sich), unten in der sicheren Zone.</summary>
    static void Label(DrawingContext dc, double t, string name, string chip)
    {
        Film.Chip(dc, t, chip, 26, Semi, Brushes.White, new SolidColorBrush(Color.FromArgb(0xB0, 0x10, 0x14, 0x24)), W / 2.0, 1300);
        double k = Math.Clamp(t / 0.18, 0, 1);
        double scale = 1.25 - 0.25 * Ease(k);
        dc.PushOpacity(Math.Min(1, k * 1.5));
        Outlined(dc, name, 96, Brushes.White, Heavy, 1380, 0, scale);
        dc.Pop();
    }

    static void Check(DrawingContext dc, double k)
    {
        var c = new Point(W / 2.0, 1480);
        double r = 80 * (0.6 + 0.4 * Ease(k));
        dc.PushOpacity(k);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0xF0, 0x16, 0xA3, 0x4A)), new Pen(Brushes.White, 6), c, r, r);
        var g = Text("", r, Brushes.White, new Typeface(new FontFamily("Segoe Fluent Icons"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal));
        dc.DrawText(g, new Point(c.X - g.Width / 2, c.Y - g.Height / 2));
        dc.Pop();
    }

    static void EndCard(DrawingContext dc, double t)
    {
        dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0x0A, 0x0E, 0x1A), Color.FromRgb(0x13, 0x17, 0x2B), 90), null, Desk);
        dc.DrawEllipse(new RadialGradientBrush(Color.FromArgb(0x48, 0x4C, 0x7B, 0xFF), Color.FromArgb(0, 0x4C, 0x7B, 0xFF)), null, new Point(W / 2.0, H * 0.48), 700, 700);
        double k = Ease(Math.Clamp(t / 0.5, 0, 1));
        var logo = new Rect(W / 2.0 - 80, 520 + (1 - k) * 30, 160, 160);
        dc.PushOpacity(k);
        dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(0x4C, 0xC2, 0xFF), Color.FromRgb(0x00, 0x5F, 0xB8), 45), null, logo, 36, 36);
        var glyph = Text("", 78, Brushes.White, new Typeface(new FontFamily("Segoe Fluent Icons"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal));
        dc.DrawText(glyph, new Point(logo.X + (logo.Width - glyph.Width) / 2, logo.Y + (logo.Height - glyph.Height) / 2));
        Outlined(dc, "DeskKeeper", 120, Brushes.White, Heavy, 730, (1 - k) * 30);
        dc.Pop();
        string[] lines = ["Free & open source · Windows 10/11", "12 scenes · 6 helpers · more coming", "Send feedback. Mod it. Add your own."];
        for (int i = 0; i < lines.Length; i++)
        {
            double ki = Ease(Math.Clamp((t - 0.35 - i * 0.22) / 0.35, 0, 1));
            dc.PushOpacity(ki);
            Outlined(dc, lines[i], 46, i == 2 ? Hot : new SolidColorBrush(Color.FromRgb(0xC8, 0xD2, 0xE6)), Semi, 920 + i * 78, (1 - ki) * 20);
            dc.Pop();
        }
        double kg = Ease(Math.Clamp((t - 1.1) / 0.35, 0, 1));
        dc.PushOpacity(kg);
        Outlined(dc, "github.com/VenzyProgramming/DeskKeeper", 38, Accent, Semi, 1210);
        dc.Pop();
    }

    static FormattedText Text(string s, double size, Brush b, Typeface face) => new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, b, 1);
    static double Ease(double x) => 1 - Math.Pow(1 - Math.Clamp(x, 0, 1), 3);
    static Point Lerp(Point a, Point b, double k) => a + (b - a) * k;
    static T Frozen<T>(T f) where T : Freezable { f.Freeze(); return f; }
}
