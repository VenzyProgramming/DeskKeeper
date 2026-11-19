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
/// Werbevideo als Bildfolge (DeskKeeper.exe --promo &lt;ordner&gt; [bildschirmfoto.png], mit DESKKEEPER_DEMO=1):
/// Titel → Notebook + Breitbild geordnet → Monitor ab → wieder an, Windows mischt → DeskKeeper stellt zurück →
/// Oberfläche → alle Zerstörungsarten → alle Helfer → Ausblick, Rückmeldungen, Open Source → Abspann. 1920×1080, 30 Bilder/s, nur
/// Vorführdaten. Schreibt timeline.json für die Tonspur (werkzeuge/promo_audio.py).
/// </summary>
static class Promo
{
    const int W = 1920, H = 1080, Fps = 30;
    static readonly Typeface Bold = new(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    static readonly Typeface Regular = new(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    static readonly Color Accent = Color.FromRgb(0x4C, 0xC2, 0xFF);

    record Section(double Start, double End, Action<DrawingContext, double, double> Draw);   // (dc, lokale Zeit, Dauer)

    public static int Run(string output, string? screenshot)
    {
        Directory.CreateDirectory(output);
        foreach (var f in Directory.EnumerateFiles(output, "*.png")) File.Delete(f);
        var home = Demo.Home();
        var names = home.Keys.ToList();
        // Notebook allein: Windows legt alles in Spalten links ab
        var laptopDump = names.Select((n, k) => (n, new Point(760 + 70 + (k / 8) * 118, 1440 + 80 + (k % 8) * 132))).ToDictionary(x => x.n, x => x.Item2);
        // wieder an: durcheinander über beide Bildschirme
        var rnd = new Random(7);
        var shuffled = names.OrderBy(_ => rnd.Next()).ToList();
        var slots = names.Select(n => home[n]).ToList();
        var mixed = shuffled.Select((n, i) => (n, slots[(i * 7 + 3) % slots.Count] + new Vector(rnd.Next(-90, 260), rnd.Next(-40, 220)))).ToDictionary(x => x.n, x => x.Item2);
        foreach (var n in names.Take(5)) mixed[n] = new Point(1000 + rnd.Next(0, 1400), 200 + rnd.Next(0, 900));

        var view = new ScreensView { AccentColor = Accent, IconPixels = 68 };
        Rect stage = new(170, 210, 1580, 800);
        view.Measure(stage.Size); view.Arrange(new Rect(stage.Size));
        BitmapSource? shot = screenshot is not null && File.Exists(screenshot) ? Load(screenshot) : null;


        DeskIcon Icon(string n, Point p) => new(p, n, Demo.Image(n));
        List<DeskIcon> At(Func<string, Point> where) => names.Select(n => Icon(n, where(n))).ToList();

        // Zeitleiste : Bild und Ton lesen dieselben Zeitpunkte (Skill-Bibliothek: sound-design, motion-design)
        var sections = new List<Section>();
        var cues = new List<Cue>();
        double at = 0; int clipNo = 0, helperNo = 0;
        var kinds = new List<string>();
        void Add(double length, string kind, Action<DrawingContext, double, double> draw) { sections.Add(new Section(at, at + length, draw)); kinds.Add(kind); cues.Add(new Cue(at, kind, 0, 1)); at += length; }

        Add(2.8, "title", (dc, t, d) => Title(dc, t, "DeskKeeper", "Your desktop icons. Exactly where you left them."));
        Add(2.6, "caption", (dc, t, d) => { Caption(dc, t, "Laptop + ultrawide.", "Icons sorted the way you like."); Stage(dc, view, stage, Demo.Both, At(n => home[n]), 1); });
        Add(2.6, "caption", (dc, t, d) =>
        {
            Caption(dc, t, "Unplug the monitor …", "Windows piles everything onto the laptop.");
            double k = Ease(Math.Clamp((t - 0.4) / 1.0, 0, 1));
            Stage(dc, view, stage, t < 0.4 ? Demo.Both : [Demo.Both[1]], At(n => Lerp(home[n], laptopDump[n], k)), 1);
        });
        Add(2.6, "caption", (dc, t, d) =>
        {
            Caption(dc, t, "Plug it back in.", "And Windows shuffles your icons.");
            double k = Ease(Math.Clamp((t - 0.3) / 0.9, 0, 1));
            Stage(dc, view, stage, Demo.Both, At(n => Lerp(laptopDump[n], mixed[n], k)), 1);
        });
        double putBack = at;
        Add(3.6, "caption", (dc, t, d) =>
        {
            Caption(dc, t, "DeskKeeper puts every icon back.", "Automatically. Per display, per arrangement.");
            Stage(dc, view, stage, Demo.Both, names.Select((n, i) =>
            {
                double k = Ease(Math.Clamp((t - 0.4 - i * 0.07) / 0.9, 0, 1));
                return Icon(n, Lerp(mixed[n], home[n], k));
            }).ToList(), 1);
            if (t > 2.5) Badge(dc, Math.Clamp((t - 2.5) / 0.35, 0, 1));
        });
        cues.Add(new Cue(putBack + 2.5, "success", 0, 1));
        for (int i = 0; i < names.Count; i++) cues.Add(new Cue(putBack + 0.4 + i * 0.07 + 0.9, "tick", Pan(home[names[i]].X / 3440 * 1920), 0.5));
        if (shot is not null)
            Add(3.4, "caption", (dc, t, d) => { Caption(dc, t, "Looks right at home on Windows 10 and 11.", "Layouts · snapshots · auto-save · tray · English, Deutsch, Русский"); Screenshot(dc, shot, t, d); });

        // Zerstörungen: jede Szene echt berechnet, gerafft auf 2,6 s
        Add(2.6, "card", (dc, t, d) => Title(dc, t, "Or have some fun first.", "12 ways to wipe your desktop."));
        cues.Add(new Cue(at - 0.9, "riser", 0, 1));
        foreach (var (kind, name, line) in new[]
        {
            ("jericho", "Jericho", "One missile, a cloud of mini rockets"),
            ("nuke", "Nuke", "Flash, fireball, mushroom cloud"),
            (Attacks.Tu160, "Tu-160M", "Cruise missiles on every group of icons"),
            (Attacks.Tu22, "Tu-22M3", "Carpet bombing along your rows, then afterburner"),
            (Attacks.Su34, "Su-34", "Precision strikes, row by row"),
            (Attacks.Ka52, "Ka-52", "Rockets, cannon and Vikhr missiles, one icon at a time"),
            (Attacks.Tu95, "Tu-95MS", "Kh-101 cruise missiles from the wing stations"),
            (Attacks.Il76, "Il-76", "Paratroopers carry your icons away"),
            (Attacks.Ac130, "AC-130U", "Circles each group: 105 mm, 40 mm, 25 mm"),
            (Attacks.Shahed, "Shahed-136", "A swarm of loitering drones"),
            (Attacks.A10, "A-10C", "Gun runs along the row, plus a Maverick"),
            (Attacks.Su25, "Su-25", "S-8 rocket salvos and 30 mm gun runs"),
        })
        {
            var (draw, events) = AttackClip(kind, 2.6);
            double start = at; int no = ++clipNo;
            Add(2.6, "clip", (dc, t, d) =>
            {
                Film.WhipIn(dc, new Size(W, H), t, () => Film.Camera(dc, new Size(W, H), t, d, 0.05, Film.Shake(events, t, 1), () => draw(dc, t)));
                Caption(dc, t, name, line); Film.Chip(dc, t, $"{no:00} / 12", 16, Regular, Brushes.White, new SolidColorBrush(Color.FromArgb(0x90, 0x10, 0x14, 0x24)), W / 2.0, 22);
            });
            cues.AddRange(events.Select(e => e with { T = e.T + start }));
        }

        // Helfer: Wiederherstellen von verstreuten Symbolen, gerafft
        Add(2.4, "card", (dc, t, d) => Title(dc, t, "Then bring it all back.", "Pick your helper."));
        foreach (var (kind, name, line) in new[]
        {
            (Helpers.Dume, "DUM-E", "The robot arm with its trailer"),
            (Helpers.Forklift, "Forklift", "Four icons on a pallet"),
            (Helpers.Crane, "Crane truck", "Outriggers down, boom out"),
            (Helpers.Drones, "Delivery drones", "A swarm, one icon each"),
            (Helpers.Chinook, "CH-47 Chinook", "Icons in a cargo net, like containers"),
            (Helpers.Mi26, "Mi-26", "The biggest helicopter of them all"),
        })
        {
            var (draw, events) = HelperClip(kind, 2.6);
            double start = at; int no = ++helperNo;
            Add(2.6, "clip", (dc, t, d) =>
            {
                Film.WhipIn(dc, new Size(W, H), t, () => Film.Camera(dc, new Size(W, H), t, d, 0.04, default, () => draw(dc, t)));
                Caption(dc, t, name, line); Film.Chip(dc, t, $"Helper {no} / 6", 16, Regular, Brushes.White, new SolidColorBrush(Color.FromArgb(0x90, 0x10, 0x14, 0x24)), W / 2.0, 22);
            });
            cues.AddRange(events.Select(e => e with { T = e.T + start }));
        }
        Helpers.Current = Helpers.Dume;

        // Ausblick : mehr kommt, lebt von Rückmeldungen, Open Source
        Add(3.4, "card", (dc, t, d) => Title(dc, t, "More is coming.", "Military, natural disasters, anything. Step by step."));
        Add(3.4, "card", (dc, t, d) => Title(dc, t, "It lives on your feedback.", "Report a bug or a complaint, and it gets fixed."));
        Add(3.4, "card", (dc, t, d) => Title(dc, t, "Open source.", "Fix it, mod it, add your own scenes and helpers."));
        Add(4.2, "end", (dc, t, d) => Title(dc, t, "DeskKeeper", "Free and open source · Windows 10 & 11 · github.com/VenzyProgramming/DeskKeeper"));
        WriteTimeline(Path.Combine(output, "timeline.json"), sections.Select(x => x.Start).ToList(), at, cues);
        double total = sections[^1].End;
        int frames = (int)(total * Fps);
        var target = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
        for (int f = 0; f < frames; f++)
        {
            double time = f / (double)Fps;
            var s = sections.Last(x => time >= x.Start);
            double t = time - s.Start, d = s.End - s.Start;
            int si = sections.IndexOf(s);
            bool clipIn = kinds[si] == "clip", clipOut = si + 1 < kinds.Count && kinds[si + 1] == "clip";
            // zwischen Clips Wischblende statt Abblende (Szenen überlappen statt leerer Bilder)
            double fade = Math.Min(clipIn ? 1 : Math.Clamp(t / 0.35, 0, 1), clipOut ? 1 : Math.Clamp((d - t) / 0.35, 0, 1));
            var v = new DrawingVisual();
            using (var dc = v.RenderOpen())
            {
                Background(dc, time);
                dc.PushOpacity(fade);
                s.Draw(dc, t, d);
                dc.Pop();
                Film.Look(dc, new Size(W, H), f, 0.05);
            }
            target.Clear(); target.Render(v);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(target));
            using var file = File.Create(Path.Combine(output, $"{f:D5}.png")); enc.Save(file);
        }
        return 0;
    }

    // ------------------------------------------------------------ Bausteine
    static void Background(DrawingContext dc, double time)
    {
        dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0x0A, 0x0E, 0x1A), Color.FromRgb(0x13, 0x17, 0x2B), 90), null, new Rect(0, 0, W, H));
        var glow = new RadialGradientBrush(Color.FromArgb(0x38, 0x4C, 0x7B, 0xFF), Color.FromArgb(0, 0x4C, 0x7B, 0xFF));
        dc.DrawEllipse(glow, null, new Point(W * 0.5 + Math.Sin(time * 0.3) * 120, H * 0.62), 900, 520);
    }

    static void Title(DrawingContext dc, double t, string title, string sub)
    {
        double rise = (1 - Ease(Math.Clamp(t / 0.8, 0, 1))) * 30;
        var logo = new Rect(W / 2.0 - 56, 300 + rise, 112, 112);
        dc.DrawRoundedRectangle(new LinearGradientBrush(Accent, Color.FromRgb(0x00, 0x5F, 0xB8), 45), null, logo, 26, 26);
        var glyph = Text("", 54, Brushes.White, new Typeface(new FontFamily("Segoe Fluent Icons"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal));
        dc.DrawText(glyph, new Point(logo.X + (logo.Width - glyph.Width) / 2, logo.Y + (logo.Height - glyph.Height) / 2));
        Film.Words(dc, t, title, 104, Bold, Brushes.White, new SolidColorBrush(Accent), null, W / 2.0, 450 + rise);
        var small = Text(sub, 36, new SolidColorBrush(Color.FromRgb(0xC8, 0xD2, 0xE6)), Regular);
        dc.DrawText(small, new Point((W - small.Width) / 2, 600 + rise));
    }

    static void Caption(DrawingContext dc, double t, string title, string sub)
    {
        double k = Ease(Math.Clamp(t / 0.5, 0, 1));
        dc.PushOpacity(k);
        Film.Words(dc, t, title, 58, Bold, Brushes.White, new SolidColorBrush(Accent), null, W / 2.0, 58);
        var small = Text(sub, 28, new SolidColorBrush(Color.FromRgb(0xA9, 0xB6, 0xCF)), Regular);
        dc.DrawText(small, new Point((W - small.Width) / 2, 140 + (1 - k) * 14));
        dc.Pop();
    }

    static void Stage(DrawingContext dc, ScreensView view, Rect stage, IReadOnlyList<MonitorInfo> monitors, List<DeskIcon> icons, double opacity)
    {
        view.Show(monitors, icons);
        view.InvalidateVisual(); view.Measure(stage.Size); view.Arrange(new Rect(stage.Size)); view.UpdateLayout();
        var bmp = new RenderTargetBitmap((int)stage.Width, (int)stage.Height, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(view);
        dc.PushOpacity(opacity);
        dc.DrawImage(bmp, stage);
        dc.Pop();
    }

    static void Badge(DrawingContext dc, double k)
    {
        var c = new Point(W / 2.0, 640);
        double r = 70 * (0.6 + 0.4 * Ease(k));
        dc.PushOpacity(k);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0xE6, 0x16, 0xA3, 0x4A)), new Pen(Brushes.White, 4), c, r, r);
        var g = Text("", r * 0.95, Brushes.White, new Typeface(new FontFamily("Segoe Fluent Icons"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal));
        dc.DrawText(g, new Point(c.X - g.Width / 2, c.Y - g.Height / 2));
        dc.Pop();
    }

    static void Screenshot(DrawingContext dc, BitmapSource shot, double t, double d)
    {
        double zoom = 1 + 0.05 * (t / d);
        double w = 1360 * zoom, h = w * shot.PixelHeight / shot.PixelWidth;
        var r = new Rect((W - w) / 2, 230 + (800 - h) / 2 + 20, w, h);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0x70, 0, 0, 0)), null, new Rect(r.X + 10, r.Y + 18, r.Width, r.Height), 14, 14);
        dc.PushClip(new RectangleGeometry(r, 12, 12));
        dc.DrawImage(shot, r);
        dc.Pop();
        dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(0x50, 255, 255, 255)), 1), r, 12, 12);
    }

    /// <summary>DUM-E-Rückstellung auf einem Vorführschreibtisch (doppelte Geschwindigkeit).</summary>
    static Action<DrawingContext, double> RobotClip()
    {
        var desk = new Rect(0, 0, 1920, 1080);
        var names = Demo.IconSet.Take(10).Select(x => x.Name).ToList();
        var homes = names.Select((n, i) => (n, new Point(110 + (i / 5) * 150, 120 + (i % 5) * 170))).ToDictionary(x => x.n, x => x.Item2);
        var rnd = new Random(3);
        var icons = names.Select(n => new IconInfo(n, n, new Point(700 + rnd.Next(0, 1000), 200 + rnd.Next(0, 700)))).ToList();
        var scene = new Scene(icons, homes, 72, [desk], desk, false, 150);   // groß, damit DUM-E im Video erkennbar ist
        double start = scene.RobotStart + 0.4;
        return (dc, t) => DeskClip(dc, scene, icons, start + t * 2.2, 1.5);
    }

    /// <summary>Иерихон/Jericho-Einschlag auf dem Vorführschreibtisch.</summary>
    static Action<DrawingContext, double> BlastClip()
    {
        var desk = new Rect(0, 0, 1920, 1080);
        var names = Demo.IconSet.Select(x => x.Name).ToList();
        var homes = names.Select((n, i) => (n, new Point(90 + (i / 6) * 118, 90 + (i % 6) * 132))).ToDictionary(x => x.n, x => x.Item2);
        var icons = names.Select(n => new IconInfo(n, n, homes[n])).ToList();
        var scene = new Scene(icons, homes, 48, [desk], desk, true, 64);
        return (dc, t) => DeskClip(dc, scene, icons, Scene.Impact - 1.1 + t, 1);
    }

    static void DeskClip(DrawingContext dc, Scene scene, List<IconInfo> icons, double time, double iconScale)
    {
        var frame = new Rect(250, 236, 1420, 799);   // 1920×1080 → 74 %
        double s = frame.Width / 1920;
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0x70, 0, 0, 0)), null, new Rect(frame.X + 8, frame.Y + 16, frame.Width, frame.Height), 14, 14);
        dc.PushClip(new RectangleGeometry(frame, 12, 12));
        dc.PushTransform(new MatrixTransform(s, 0, 0, s, frame.X, frame.Y));
        var wall = new LinearGradientBrush(Color.FromRgb(0x14, 0x22, 0x4A), Color.FromRgb(0x2A, 0x16, 0x4A), 35);
        dc.DrawRectangle(wall, null, new Rect(0, 0, 1920, 1080));
        var centers = scene.IconCenters(time);
        foreach (var icon in icons)
        {
            if (centers[icon.Id] is not { } c) continue;
            double z = 52 * iconScale;
            dc.DrawImage(Demo.Image(icon.Id), new Rect(c.X - z / 2, c.Y - z * 0.58, z, z));
            var label = Text(icon.Name, 13 * iconScale, Brushes.White, Regular);
            dc.DrawText(label, new Point(c.X - label.Width / 2, c.Y + z * 0.46));
        }
        scene.Draw(dc, time);
        dc.Pop(); dc.Pop();
        dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(0x50, 255, 255, 255)), 1), frame, 12, 12);
    }

    /// <summary>Ton-Ereignis der Zeitleiste: Zeit, Art, Stereoposition (−1 links … +1 rechts), Stärke; Dauer für Dauergeräusche.</summary>
    internal record Cue(double T, string Kind, double Pan, double Gain, double Length = 0);
    static double Pan(double x) => Math.Clamp(x / 1920 * 2 - 1, -1, 1);

    internal static void WriteTimeline(string path, List<double> beats, double total, List<Cue> cues)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();
        sb.Append("{\"total\":").Append(total.ToString("0.###", inv)).Append(",\"beats\":[").Append(string.Join(",", beats.Select(b => b.ToString("0.###", inv)))).Append("],\"cues\":[");
        sb.Append(string.Join(",", cues.OrderBy(c => c.T).Select(c => $"{{\"t\":{c.T.ToString("0.###", inv)},\"kind\":\"{c.Kind}\",\"pan\":{c.Pan.ToString("0.##", inv)},\"gain\":{c.Gain.ToString("0.##", inv)},\"len\":{c.Length.ToString("0.###", inv)}}}")));
        sb.Append("]}");
        File.WriteAllText(path, sb.ToString());
    }

    /// <summary>Vorführschreibtisch für die Szenen: zwei Spalten links, eine Gruppe oben rechts, eine Reihe unten.</summary>
    internal static (List<IconInfo> Icons, Dictionary<string, Point> Homes) DemoDesk()
    {
        var names = Demo.IconSet.Select(x => x.Name).ToList();
        var homes = new Dictionary<string, Point>();
        for (int i = 0; i < names.Count; i++)
        {
            Point p = i < 10 ? new Point(80 + (i / 5) * 118, 90 + (i % 5) * 132)
                : i < 14 ? new Point(1840 - ((i - 10) / 2) * 118, 90 + ((i - 10) % 2) * 132)
                : new Point(760 + (i - 14) * 118, 960);
            homes[names[i]] = p;
        }
        return (names.Select(n => new IconInfo(n, n, homes[n])).ToList(), homes);
    }

    /// <summary>Eine Zerstörungsszene, gerafft auf length Sekunden um die Einschläge; dazu ihre Ton-Ereignisse (Zeit relativ zum Clip).</summary>
    /// <summary>Film vom Duell F-35A gegen Su-57 (19.11.2026: „den Kampf der beiden Jets in einem Video zeigen“): Titelkarte,
    /// der Kampf in Echtzeit mit Kamerafahrt und Erschütterung bei Treffern, Schlusskarte. Ton aus derselben Zeitleiste (promo_audio.py).</summary>
    public static int DuelFilm(string output)
    {
        Directory.CreateDirectory(output);
        foreach (var old in Directory.GetFiles(output, "*.png")) File.Delete(old);
        var desk = new Rect(0, 0, 1920, 1080);
        var (icons, homes) = DemoDesk();
        var scene = new Scene(icons, homes, 52, [desk], desk, true, 64, attack: Attacks.Duel);
        var duel = (DuelRaid)scene.Attack!;
        const double intro = 2.4, outro = 3.2, s0 = 0.9;   // s0: Szenenzeit beim Schnitt, die Jets sind dann schon im Bild
        double fight = duel.BreakAt + 2.3 - s0;
        var cues = new List<Cue>
        {
            new(0.2, "riser", 0, 0.8),
            new(intro, "jet", 0, 1, fight),
            new(intro + fight, "cut", 0, 0.8),
        };
        double At(double sceneT) => intro + sceneT - s0;
        bool InFight(double sceneT) => sceneT >= s0 && sceneT <= s0 + fight;
        foreach (var s in duel.Shots) if (InFight(s.Fire)) cues.Add(new Cue(At(s.Fire), "rocket", Pan(duel.PlaneAt(s.ByF35, s.Fire).X), 0.55));
        foreach (var b in duel.Bursts) if (InFight(b.Start)) cues.Add(new Cue(At(b.Start), "brrt", Pan(duel.PlaneAt(b.ByF35, b.Start).X), 0.45));
        foreach (var f in duel.Fans) if (InFight(f.Start)) cues.Add(new Cue(At(f.Start), "pom", Pan(duel.PlaneAt(f.OfF35, f.Start).X), 0.35));
        var booms = new List<Cue>();
        foreach (var r in scene.Raid.OrderBy(r => r.T))
            if (InFight(r.T) && !booms.Any(o => Math.Abs(o.T - At(r.T)) < 0.06)) booms.Add(new Cue(At(r.T), "boom", Pan(r.P.X), 0.75));
        cues.AddRange(booms);
        var shake = booms.Select(c => c with { T = c.T - intro }).ToList();
        double total = intro + fight + outro;
        WriteTimeline(Path.Combine(output, "timeline.json"), [0, intro, intro + fight], total, cues);

        int frames = (int)(total * Fps);
        var target = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
        for (int f = 0; f < frames; f++)
        {
            double time = f / (double)Fps;
            var v = new DrawingVisual();
            using (var dc = v.RenderOpen())
            {
                Background(dc, time);
                if (time < intro)
                {
                    double fade = Math.Min(1, (intro - time) / 0.35);
                    dc.PushOpacity(fade);
                    Title(dc, time, "F-35A vs Su-57", "The duel. On your desktop.");
                    dc.Pop();
                }
                else if (time < intro + fight)
                {
                    double t = time - intro;
                    double st = s0 + t;
                    var shakeNow = Film.Shake(shake, t, 1.6);
                    Film.WhipIn(dc, new Size(W, H), t, () =>
                    {
                        dc.PushTransform(new TranslateTransform(shakeNow.X, shakeNow.Y));
                        DeskFollow(dc, scene, duel, icons, st);
                        dc.Pop();
                    });
                    if (t < 3.4)
                    {
                        dc.PushOpacity(Math.Min(1, (3.4 - t) / 0.5));
                        Caption(dc, t, "Duel", "Rolling scissors · missiles · flares · nobody wins");
                        dc.Pop();
                    }
                    Film.Chip(dc, t, "DeskKeeper 1.5", 16, Regular, Brushes.White, new SolidColorBrush(Color.FromArgb(0x90, 0x10, 0x14, 0x24)), W / 2.0, 22);
                }
                else Title(dc, time - intro - fight, "DeskKeeper", "Free and open source · github.com/VenzyProgramming/DeskKeeper");
                Film.Look(dc, new Size(W, H), f, 0.05);
            }
            target.Clear(); target.Render(v);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(target));
            using var file = File.Create(Path.Combine(output, $"{f:D5}.png")); enc.Save(file);
        }
        Console.WriteLine($"ok {frames} Bilder, {total:0.0} s");
        return 0;
    }

    /// <summary>Vollbild-Desktop mit Verfolgerkamera: zielt auf die Mitte zwischen den Jets (geglättet über 0,8 s), zoomt näher heran,
    /// je dichter sie beieinander sind, und fährt beim Auseinanderbrechen auf die ganze Fläche zurück.</summary>
    static void DeskFollow(DrawingContext dc, Scene scene, DuelRaid duel, List<IconInfo> icons, double time)
    {
        Point focus = new(); double zoom = 0; int n = 0;
        for (double k = 0; k <= 0.8; k += 0.1, n++)
        {
            double ti = time - k;
            var a = duel.PlaneAt(true, ti); var b = duel.PlaneAt(false, ti);
            focus.X += (a.X + b.X) / 2; focus.Y += (a.Y + b.Y) / 2;
            zoom += Math.Clamp(1250 / ((a - b).Length + 520), 1.2, 1.75);
        }
        focus = new Point(focus.X / n, focus.Y / n); zoom /= n;
        double wide = Ease(Math.Clamp((time - duel.BreakAt) / 1.4, 0, 1));
        zoom = zoom + (1 - zoom) * wide;
        double hw = W / (2 * zoom), hh = H / (2 * zoom);
        focus = new Point(Math.Clamp(focus.X, hw, 1920 - hw), Math.Clamp(focus.Y, hh, 1080 - hh));
        focus = new Point(focus.X + (960 - focus.X) * wide, focus.Y + (540 - focus.Y) * wide);
        dc.PushTransform(new MatrixTransform(zoom, 0, 0, zoom, W / 2.0 - zoom * focus.X, H / 2.0 - zoom * focus.Y));
        var wall = new LinearGradientBrush(Color.FromRgb(0x14, 0x22, 0x4A), Color.FromRgb(0x2A, 0x16, 0x4A), 35);
        dc.DrawRectangle(wall, null, new Rect(-200, -200, 2320, 1480));
        var centers = scene.IconCenters(time);
        foreach (var icon in icons)
        {
            if (centers[icon.Id] is not { } c) continue;
            double z = 52;
            dc.DrawImage(Demo.Image(icon.Id), new Rect(c.X - z / 2, c.Y - z * 0.58, z, z));
            var label = Text(icon.Name, 13, Brushes.White, Regular);
            dc.DrawText(label, new Point(c.X - label.Width / 2, c.Y + z * 0.46));
        }
        scene.Draw(dc, time);
        dc.Pop();
    }

    static (Action<DrawingContext, double> Draw, List<Cue> Cues) AttackClip(string kind, double length)
    {
        var desk = new Rect(0, 0, 1920, 1080);
        var (icons, homes) = DemoDesk();
        bool nuke = kind == "nuke", jericho = kind is "jericho" or "nuke";
        var scene = new Scene(icons, homes, 52, [desk], desk, true, 64, nuclear: nuke, attack: jericho ? null : kind);
        var raid = scene.Raid;
        double first = raid.Count > 0 ? raid.Min(r => r.T) : Scene.Impact, last = raid.Count > 0 ? raid.Max(r => r.T) : Scene.Impact;
        double t0 = jericho ? Scene.Impact - (nuke ? 0.9 : 1.2) : first - 1.1;
        double span = jericho ? length * 1.0 : Math.Clamp(last + 0.8 - t0, length, length * 4.0);
        double speed = span / length;
        double V(double sceneT) => (sceneT - t0) / speed;
        var cues = new List<Cue>();
        bool In(double v) => v >= 0.05 && v <= length - 0.1;
        // Dauergeräusch des Flugzeugs bzw. Hubschraubers
        string bed = kind switch
        {
            Attacks.Tu95 or Attacks.Il76 or Attacks.Ac130 => "props",
            Attacks.Ka52 => "rotor",
            Attacks.Shahed => "moped",
            "jericho" or "nuke" => "",
            _ => "jet",
        };
        if (bed != "") cues.Add(new Cue(0, bed, 0, 1, length));
        if (jericho) cues.Add(new Cue(V(Scene.Impact), nuke ? "nuke" : "bigboom", 0, 1));
        foreach (var r in raid.OrderBy(r => r.T)) if (In(V(r.T))) cues.Add(new Cue(V(r.T), "boom", Pan(r.P.X), 0.7));
        if (scene.Attack is GunshipRaid gun)
            foreach (var shot in gun.Shots) if (In(V(shot.Fire))) cues.Add(new Cue(V(shot.Fire), shot.Gun == GunshipRaid.Gun.G105 ? "cannon" : shot.Gun == GunshipRaid.Gun.G40 ? "pom" : "tick", 0, 0.5));
        if (scene.Attack is StrafeRaid strafe)
            foreach (var shot in strafe.Shots) if (In(V(shot.Fire))) cues.Add(new Cue(V(shot.Fire), shot.Weapon == StrafeRaid.Weapon.Gun ? "brrt" : "rocket", Pan(strafe.PlaneAt(shot.Fire).X), 0.5));
        // Höchstens ein Einschlagsgeräusch je 60 ms, sonst wird die Mischung matschig
        var thinned = new List<Cue>();
        foreach (var c in cues.OrderBy(c => c.T))
            if (c.Kind != "boom" || !thinned.Any(o => o.Kind == "boom" && Math.Abs(o.T - c.T) < 0.06)) thinned.Add(c);
        return ((dc, t) => DeskClip(dc, scene, icons, t0 + t * speed, 1), thinned);
    }

    /// <summary>Szenenzeit, zu der das erste Symbol wieder an seinem Platz ist (dort ist der Helfer sicher im Bild).</summary>
    internal static double FirstDrop(Scene scene, IReadOnlyList<IconInfo> icons, IReadOnlyDictionary<string, Point> homes)
    {
        for (double t = scene.RobotStart; t < scene.Duration; t += 0.1)
        {
            var c = scene.IconCenters(t);
            if (icons.Any(i => c[i.Id] is { } p && (p - homes[i.Id]).Length < 0.5 && (i.Center - homes[i.Id]).Length > 1)) return t;
        }
        return scene.RobotStart;
    }

    /// <summary>Ein Helfer bringt verstreute Symbole zurück, gerafft auf length Sekunden; Ton beim Absetzen jedes Symbols.</summary>
    static (Action<DrawingContext, double> Draw, List<Cue> Cues) HelperClip(string kind, double length)
    {
        var desk = new Rect(0, 0, 1920, 1080);
        var names = Demo.IconSet.Take(8).Select(x => x.Name).ToList();
        var homes = names.Select((n, i) => (n, new Point(520 + (i / 4) * 150, 300 + (i % 4) * 150))).ToDictionary(x => x.n, x => x.Item2);   // Ziele mit Abstand zum Rand: auch die Mi-26 bleibt im Bild
        var rnd = new Random(11);
        var icons = names.Select(n => new IconInfo(n, n, new Point(1000 + rnd.Next(0, 700), 260 + rnd.Next(0, 560)))).ToList();
        Helpers.Current = kind;
        var scene = new Scene(icons, homes, 72, [desk], desk, false, 130);
        double end = scene.Duration;
        double speed = Math.Clamp((end - scene.RobotStart) / 8, 1.5, 5);
        double t0 = Math.Max(scene.RobotStart, FirstDrop(scene, icons, homes) - length * speed * 0.35);
        var cues = new List<Cue>();
        string bed = kind switch { Helpers.Chinook or Helpers.Mi26 => "rotor", Helpers.Drones => "drones", _ => "servo" };
        cues.Add(new Cue(0, bed, 0, 1, length));
        var done = new HashSet<string>();
        for (double v = 0; v <= length; v += 1.0 / 30)
        {
            var centers = scene.IconCenters(t0 + v * speed);
            foreach (var n in names)
                if (!done.Contains(n) && centers[n] is { } c && (c - homes[n]).Length < 0.5) { done.Add(n); if (v > 0.05) cues.Add(new Cue(v, "drop", Pan(c.X * 1420 / 1920 + 250), 0.6)); }
        }
        return ((dc, t) => DeskClip(dc, scene, icons, t0 + t * speed, 1.4), cues);
    }

    static FormattedText Text(string s, double size, Brush b, Typeface face) => new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, b, 1);
    static double Ease(double x) => x < 0.5 ? 4 * x * x * x : 1 - Math.Pow(-2 * x + 2, 3) / 2;
    static Point Lerp(Point a, Point b, double k) => a + (b - a) * k;
    static BitmapSource Load(string path) { var b = new BitmapImage(); b.BeginInit(); b.CacheOption = BitmapCacheOption.OnLoad; b.UriSource = new Uri(Path.GetFullPath(path)); b.EndInit(); b.Freeze(); return b; }
}
