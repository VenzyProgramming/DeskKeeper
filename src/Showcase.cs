#if PUBLIC_EDITION
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskKeeper;

/// <summary>
/// Werbevideo „Showcase“ (20.11.2026: auf Englisch, ein Mauszeiger klickt in Echtzeit durch die Oberfläche, passende Musik, von
/// Militär bis Haustier, Botschaft „nicht täglich, aber regelmäßig neu“). Die echte öffentliche Oberfläche wird außerhalb des
/// Bildschirms gerendert (nur Vorführdaten in einer Datenkopie); der Zeiger ist gezeichnet, Klicks schalten nur Seite, Szene und
/// Helfer um. Nach „Start“ minimiert sich das Fenster wie in der App, und die Szene läuft auf dem Vorführschreibtisch. Schnitte sitzen
/// auf dem Takt (120 BPM); timeline.json trägt Takt und Musikteile für werkzeuge/promo_audio.py.
/// Aufruf: DESKKEEPER_DEMO=1 DeskKeeper.exe --showcase &lt;ordner&gt; (öffentliche Ausgabe).
/// </summary>
static partial class Promo
{
    const double Bpm = 120, BeatS = 60 / Bpm, BarS = 4 * BeatS;   // ein Takt = 2 s
    const double UiScale = 1.5;                                   // Oberfläche schärfer rendern als angezeigt (für das Heranfahren)

    sealed record Seg(double Start, double End, string Kind, Action<DrawingContext, double> Draw);   // Draw(dc, globale Zeit)
    sealed record Hover(double T0, double T1, Rect R);

    /// <summary>Zeiger, Klicks, Kamera und Zustandswechsel der Oberfläche als Zeitleiste (globale Zeit).</summary>
    sealed class Director
    {
        public readonly List<(double T, Point P)> Cursor = [];
        public readonly List<double> Clicks = [];
        public readonly List<(double T, double Dur, Point C, double Z)> Cam = [];
        public readonly List<(double T, Action Do)> Actions = [];
        public readonly List<(double T0, double T1, double From, double To)> Scrolls = [];
        public readonly List<Hover> Hovers = [];

        public Point CursorAt(double t)
        {
            if (Cursor.Count == 0) return new Point(W + 100, H + 100);
            if (t <= Cursor[0].T) return Cursor[0].P;
            for (int i = 0; i + 1 < Cursor.Count; i++)
            {
                var (ta, a) = Cursor[i]; var (tb, b) = Cursor[i + 1];
                if (t > tb) continue;
                double k = tb - ta < 1e-6 ? 1 : Ease((t - ta) / (tb - ta));
                var d = b - a;
                var n = new Vector(-d.Y, d.X); if (n.Length > 0) n /= n.Length;
                return a + d * k + n * Math.Sin(Math.PI * k) * d.Length * 0.06;   // leichter Bogen wie eine echte Handbewegung
            }
            return Cursor[^1].P;
        }

        public (Point C, double Z) CameraAt(double t)
        {
            Point c = new(W / 2.0, H / 2.0); double z = 1;
            foreach (var (t0, dur, to, tz) in Cam)
            {
                if (t < t0) break;
                double k = Ease(Math.Clamp((t - t0) / dur, 0, 1));
                c = c + (to - c) * k; z += (tz - z) * k;
            }
            return (c, z);
        }

        public double ScrollAt(double t)
        {
            double v = 0;
            foreach (var (t0, t1, from, to) in Scrolls)
            {
                if (t < t0) break;
                v = t >= t1 ? to : from + (to - from) * Ease((t - t0) / (t1 - t0));
            }
            return v;
        }
    }

    public static int Showcase(string output)
    {
        Directory.CreateDirectory(output);
        foreach (var f in Directory.EnumerateFiles(output, "*.png")) File.Delete(f);
        var L = Look.Current;
        var desk = new Rect(0, 0, 1920, 1080);
        var (deskIcons, deskHomes) = DemoDesk();

        // ------------------------------------------------ Szenen vorab berechnen (vor jedem Klick in der Oberfläche: Helpers.Current)
        Helpers.Current = Helpers.Dume;
        var military = new (string Id, string Name, string Line)[]
        {
            ("jericho", "Jericho", "One missile, a cloud of mini rockets"),
            ("nuke", "Nuke", "Flash, fireball, mushroom cloud"),
            (Attacks.Tu160, "Tu-160M", "Cruise missiles on every group of icons"),
            (Attacks.Ka52, "Ka-52", "Rockets, cannon and Vikhr missiles"),
            (Attacks.Ac130, "AC-130U", "Circles each group: 105, 40 and 25 mm"),
            (Attacks.Tu22, "Tu-22M3", "Carpet bombing along your rows"),
            (Attacks.Duel, "F-35A vs Su-57", "A dogfight right across your screen"),
            (Attacks.MiG31, "MiG-31K", "Kinzhal: drop, climb, glide, dive"),
            (Attacks.SR71, "SR-71", "Mach 3. The shock wave does the rest"),
        };
        var weird = new (string Id, string Name, string Line)[]
        {
            (Attacks.Vacuum, "Robot vacuum", "Sucks your icons up, one by one"),
            (Attacks.Gravity, "Gravity off", "Everything just falls"),
        };
        var pets = new (string Id, string Name, string Line)[]
        {
            (Attacks.Cat, "Bengal cat", "Knocks everything off the table"),
            (Attacks.Pug, "Pug", "One sneeze, whole rows gone"),
            (Attacks.Shepherd, "German Shepherd", "Herds your icons and buries them"),
        };
        var helpers = new (string Id, string Name, string Line)[]
        {
            (Helpers.Dume, "DUM-E", "The robot arm with its trailer"),
            (Helpers.Chinook, "CH-47 Chinook", "Icons in a cargo net"),
        };
        const double uiPart = 1.3, clipPart = 2 * BarS - uiPart;   // ein Szenenschnitt = 2 Takte
        var clips = new Dictionary<string, ShowClip>();
        foreach (var s in military.Concat(weird).Concat(pets)) clips[s.Id] = SceneClip(s.Id, clipPart, desk, deskIcons, deskHomes);
        foreach (var h in helpers) clips["h:" + h.Id] = HelperShowClip(h.Id, clipPart, desk);
        Helpers.Current = Helpers.Dume;

        // ------------------------------------------------ echte Oberfläche außerhalb des Bildschirms
        var window = new PublicWindow(null, true) { Width = 1440, Height = 900 };
        window.Show();
        window.UpdateLayout();
        var ui = window.ForShowcase();
        var root = ui.Root;
        double rw = root.ActualWidth, rh = root.ActualHeight, bar = 32;
        var winRect = new Rect(Math.Round((W - rw) / 2), Math.Round((H - rh - bar) / 2), rw, rh + bar);
        var rootAt = new Point(winRect.X, winRect.Y + bar);
        Rect R(FrameworkElement e) { var r = e.TransformToAncestor(root).TransformBounds(new Rect(e.RenderSize)); r.Offset(rootAt.X, rootAt.Y); return r; }
        Point Mid(Rect r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);
        double ScrollFor(FrameworkElement e)
        {   // Versatz, bei dem das Element im oberen Drittel der Szenenseite steht
            var content = (FrameworkElement)ui.ScenePage.Content;
            double y = e.TransformToAncestor(content).Transform(new Point(0, 0)).Y;
            return Math.Clamp(y - ui.ScenePage.ViewportHeight * 0.3, 0, ui.ScenePage.ScrollableHeight);
        }
        void Layout(double scroll) { ui.ScenePage.ScrollToVerticalOffset(scroll); window.UpdateLayout(); }

        // ------------------------------------------------ Zeitleiste
        var segs = new List<Seg>();
        var cues = new List<Cue>();
        var parts = new List<(double T, string Part)>();
        var dir = new Director();
        RenderTargetBitmap? uiBmp = null; double uiScroll = double.NaN; bool dirty = true; int nextAction = 0;
        double at = 0;
        void Add(double len, string kind, Action<DrawingContext, double> draw) { segs.Add(new Seg(at, at + len, kind, draw)); at += len; }
        void Card(double len, Action<DrawingContext, double> draw, string sound = "card") { double s0 = at; cues.Add(new Cue(at, sound, 0, 1)); Add(len, "card", (dc, t) => draw(dc, t - s0)); }

        // Zeiger (Planung in Bildkoordinaten bei ruhender Kamera)
        Point cur = new(W * 0.78, H + 40);
        double scrollNow = 0;
        void MoveTo(double t0, double t1, Point p) { dir.Cursor.Add((t0, cur)); dir.Cursor.Add((t1, p)); cur = p; }
        void Click(double t, Rect target, double hoverFrom, double hoverTo, Action? act = null)
        {
            dir.Clicks.Add(t); cues.Add(new Cue(t, "click", Pan(Mid(target).X), 0.8));
            dir.Hovers.Add(new Hover(hoverFrom, hoverTo, target));
            if (act is not null) dir.Actions.Add((t + 0.06, act));
        }
        void ScrollTo(double t0, double t1, double to) { if (Math.Abs(to - scrollNow) < 1) return; dir.Scrolls.Add((t0, t1, scrollNow, to)); scrollNow = to; }

        // Fenster-Abschnitt: öffnet (außer direkt nach einem anderen Fensterabschnitt) und minimiert sich am Ende, wenn eine Szene folgt
        void Ui(double len, bool open, bool minimize, Action<DrawingContext, double, double>? overlay = null)
        {
            double s0 = at, s1 = at + len;
            if (open) cues.Add(new Cue(s0, "open", 0, 0.7));
            if (minimize) cues.Add(new Cue(s1 - 0.22, "minimize", 0, 0.8));
            Add(len, "ui", (dc, t) =>
            {
                double a = 1;
                if (open) a = Math.Min(a, Ease(Math.Clamp((t - s0) / 0.24, 0, 1)));
                if (minimize) a = Math.Min(a, Ease(Math.Clamp((s1 - t) / 0.24, 0, 1)));
                DrawUi(dc, t, a);
                overlay?.Invoke(dc, t - s0, len);
            });
        }

        // ---- 1. Titel (2 Takte)
        parts.Add((0, "intro"));
        Card(2 * BarS, (dc, t) => Title(dc, t, "DeskKeeper", "Your desktop icons. Exactly where you left them."), "title");

        // ---- 2. Das Problem und die Lösung (3 Takte)
        var view = new ScreensView { AccentColor = Accent, IconPixels = 68 };
        Rect stage = new(170, 210, 1580, 800);
        view.Measure(stage.Size); view.Arrange(new Rect(stage.Size));
        var home = Demo.Home();
        var names = home.Keys.ToList();
        var laptopDump = names.Select((n, k) => (n, new Point(760 + 70 + (k / 8) * 118, 1440 + 80 + (k % 8) * 132))).ToDictionary(x => x.n, x => x.Item2);
        var rnd = new Random(7);
        var mixed = names.OrderBy(_ => rnd.Next()).Select((n, i) => (n, home[names[(i * 7 + 3) % names.Count]] + new Vector(rnd.Next(-90, 260), rnd.Next(-40, 220)))).ToDictionary(x => x.n, x => x.Item2);
        DeskIcon Icon(string n, Point p) => new(p, n, Demo.Image(n));
        {
            double s0 = at;
            cues.Add(new Cue(at, "caption", 0, 1)); cues.Add(new Cue(at + 0.5, "shuffle", 0, 0.9)); cues.Add(new Cue(at + 1.5, "shuffle", 0, 0.9));
            Add(2 * BarS, "card", (dc, gt) =>
            {
                double t = gt - s0;
                Caption(dc, t, "Unplug a monitor. Plug it back in.", "Windows shuffles your icons every time.");
                double k1 = Ease(Math.Clamp((t - 0.5) / 0.8, 0, 1)), k2 = Ease(Math.Clamp((t - 1.6) / 0.8, 0, 1));
                Stage(dc, view, stage, t < 0.5 || t > 1.6 ? Demo.Both : [Demo.Both[1]], names.Select(n => Icon(n, Lerp(Lerp(home[n], laptopDump[n], k1), mixed[n], k2))).ToList(), 1);
            });
            double s1 = at;
            cues.Add(new Cue(at, "caption", 0, 1));
            for (int i = 0; i < names.Count; i++) cues.Add(new Cue(s1 + 0.2 + i * 0.035 + 0.6, "tick", Pan(home[names[i]].X / 3440 * 1920), 0.45));
            cues.Add(new Cue(s1 + 1.45, "success", 0, 1));
            Add(BarS, "card", (dc, gt) =>
            {
                double t = gt - s1;
                Caption(dc, t, "DeskKeeper puts every icon back.", "Automatically. Per display, per arrangement.");
                Stage(dc, view, stage, Demo.Both, names.Select((n, i) => Icon(n, Lerp(mixed[n], home[n], Ease(Math.Clamp((t - 0.2 - i * 0.035) / 0.6, 0, 1))))).ToList(), 1);
                if (t > 1.45) Badge(dc, Math.Clamp((t - 1.45) / 0.3, 0, 1));
            });
        }

        // ---- 3. Die Oberfläche: Speichern, dann zu den Szenen (2 Takte)
        parts.Add((at, "build"));
        {
            double s0 = at;
            Layout(0); ui.Go(0); window.UpdateLayout();
            var saveR = R(ui.Save);
            ui.Go(1); window.UpdateLayout();
            var navR = R(ui.Nav[1]);
            ui.Go(0); window.UpdateLayout();
            MoveTo(s0 + 0.35, s0 + 1.15, Mid(saveR) + new Vector(6, 4));
            Click(s0 + 1.35, saveR, s0 + 1.0, s0 + 1.7);
            MoveTo(s0 + 2.0, s0 + 2.75, Mid(navR) + new Vector(-30, 2));
            Click(s0 + 2.95, navR, s0 + 2.65, s0 + 3.3, () => ui.Go(1));
            dir.Actions.Add((s0, () => { ui.Go(0); Layout(0); }));
            Ui(2 * BarS, true, false, (dc, t, len) =>
            {
                if (t > 1.45 && t < 2.9) Toast(dc, t - 1.45, $"Layout saved · {names.Count} icons", L);
                LowerThird(dc, t, len, "Save your layout once.", "DeskKeeper watches your displays and restores it.");
            });
        }

        // ---- 4. Szenenfolgen: Karte (1 Takt), dann je Szene Kachel → Start → Szene (2 Takte)
        void Chapter(string kicker, string title, string sub)
        {
            Card(BarS, (dc, t) => ChapterCard(dc, t, kicker, title, sub));
        }
        void Pick(string clipKey, ToggleButton chip, string name, string line)
        {
            double s0 = at;
            ui.Go(1); window.UpdateLayout();
            double target = ScrollFor(chip);
            Layout(target);
            var chipR = R(chip); var startR = R(ui.Start);
            ScrollTo(s0 + 0.05, s0 + 0.42, target);
            var focus = new Point((Mid(chipR).X + Mid(startR).X) / 2, (Mid(chipR).Y + Mid(startR).Y) / 2);
            dir.Cam.Add((s0, 0.6, new Point(W / 2.0 + (focus.X - W / 2.0) * 0.45, H / 2.0 + (focus.Y - H / 2.0) * 0.45), 1.1));
            MoveTo(s0 + 0.05, s0 + 0.48, Mid(chipR) + new Vector(chipR.Width * 0.12, chipR.Height * 0.08));
            Click(s0 + 0.55, chipR, s0 + 0.42, s0 + 0.7, () => chip.IsChecked = true);
            MoveTo(s0 + 0.66, s0 + 0.98, Mid(startR) + new Vector(4, 3));
            Click(s0 + 1.04, startR, s0 + 0.9, s0 + 1.3);
            Ui(uiPart, true, true);
            var clip = clips[clipKey];
            double c0 = at;
            cues.Add(new Cue(c0, "scene", 0, 1));
            cues.AddRange(clip.Cues.Select(c => c with { T = c.T + c0 }));
            Add(clipPart, "clip", (dc, gt) =>
            {
                double t = gt - c0;
                var shake = Film.Shake(clip.Cues, t, 0.8);
                double zoom = 1.06 - 0.06 * Ease(Math.Clamp(t / 0.35, 0, 1));
                dc.PushTransform(new TranslateTransform(shake.X, shake.Y));
                dc.PushTransform(new ScaleTransform(zoom, zoom, W / 2.0, H / 2.0));
                double st = clip.T0 + t * clip.Speed;
                var (fc, fz) = clip.Follow?.Invoke(st) ?? (new Point(W / 2.0, H / 2.0), 1);
                dc.PushTransform(new MatrixTransform(fz, 0, 0, fz, W / 2.0 - fz * fc.X, H / 2.0 - fz * fc.Y));
                FullDesk(dc, clip.Scene, clip.Icons, st, clip.IconScale);
                dc.Pop(); dc.Pop(); dc.Pop();
                LowerThird(dc, t, clipPart, name, line);
            });
        }

        dir.Actions.Add((0, () => { }));
        parts.Add((at, "build"));
        Chapter("26 scenes", "Military.", "From cruise missiles to gunships.");
        parts.Add((at, "drop"));
        foreach (var (id, name, line) in military) Pick(id, ui.Scenes[id], name, line);
        parts.Add((at, "break"));
        Chapter("Not only military", "Something else.", "A robot vacuum. Gravity, switched off.");
        parts.Add((at, "drop"));
        foreach (var (id, name, line) in weird) Pick(id, ui.Scenes[id], name, line);
        parts.Add((at, "break"));
        Chapter("New in 1.7", "Pets.", "Pixel art. Zero respect for your icons.");
        parts.Add((at, "drop"));
        foreach (var (id, name, line) in pets) Pick(id, ui.Scenes[id], name, line);
        parts.Add((at, "break"));
        Chapter("6 helpers", "Then it all comes back.", "Pick who brings your icons home.");
        parts.Add((at, "groove"));
        foreach (var (id, name, line) in helpers) Pick("h:" + id, ui.HelperChips.First(c => (string)c.Tag == id), name, line);

        // ---- 5. Botschaften und Abspann
        parts.Add((at, "outro"));
        Card(2 * BarS, (dc, t) => Title(dc, t, "26 scenes so far.", "Military, pets, a robot vacuum, gravity off. More on the way."));
        Card(3 * BarS, (dc, t) => Versions(dc, t));
        Card(2 * BarS, (dc, t) => Title(dc, t, "It lives on your feedback.", "Report a bug or a wish, and it gets fixed."));
        Card(2 * BarS, (dc, t) => Title(dc, t, "Open source.", "Fix it, mod it, add your own scenes and helpers."));
        parts.Add((at, "end"));
        Card(3 * BarS, (dc, t) => Title(dc, t, "DeskKeeper", "Free and open source · Windows 10 & 11 · github.com/VenzyProgramming/DeskKeeper"), "end");
        double total = at;

        // Ausgangszustand der Oberfläche für das Rendern
        ui.Go(0); Layout(0); ui.Scenes["jericho"].IsChecked = true; window.UpdateLayout();
        dir.Actions.Sort((a, b) => a.T.CompareTo(b.T));

        string extra = $",\"bpm\":{Bpm},\"parts\":[" + string.Join(",", parts.Select(p => $"{{\"t\":{p.T.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)},\"part\":\"{p.Part}\"}}")) + "]";
        WriteTimeline(Path.Combine(output, "timeline.json"), segs.Select(s => s.Start).ToList(), total, cues, extra);

        // ------------------------------------------------ Rendern
        void DrawUi(DrawingContext dc, double t, double appear)
        {
            while (nextAction < dir.Actions.Count && dir.Actions[nextAction].T <= t) { dir.Actions[nextAction].Do(); nextAction++; dirty = true; }
            double scroll = dir.ScrollAt(t);
            if (dirty || Math.Abs(scroll - uiScroll) > 0.4)
            {
                Layout(scroll); uiScroll = scroll; dirty = false;
                uiBmp = new RenderTargetBitmap((int)Math.Ceiling(rw * UiScale), (int)Math.Ceiling(rh * UiScale), 96 * UiScale, 96 * UiScale, PixelFormats.Pbgra32);
                uiBmp.Render(root); uiBmp.Freeze();
            }
            var (c, z) = dir.CameraAt(t);
            dc.PushTransform(new MatrixTransform(z, 0, 0, z, W / 2.0 - z * c.X, H / 2.0 - z * c.Y));
            FullDesk(dc, null, deskIcons, 0, 1);
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)(70 * appear), 0, 0, 0)), null, new Rect(0, 0, W, H));
            if (appear > 0.001)
            {
                double s = 0.88 + 0.12 * appear;
                dc.PushOpacity(appear);
                dc.PushTransform(new ScaleTransform(s, s, winRect.X + winRect.Width / 2, H + 60));
                WindowFrame(dc, winRect, bar, L);
                dc.DrawRectangle(L.Bg, null, new Rect(rootAt, new Size(rw, rh)));
                if (uiBmp is not null) dc.DrawImage(uiBmp, new Rect(rootAt, new Size(rw, rh)));
                foreach (var h in dir.Hovers) if (t >= h.T0 && t <= h.T1)
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0x16, 255, 255, 255)), null, h.R, L.Radius, L.Radius);
                dc.Pop(); dc.Pop();
            }
            DrawCursor(dc, dir.CursorAt(t), dir.Clicks, t, appear);
            dc.Pop();
        }

        int frames = (int)Math.Round(total * Fps);
        var targetBmp = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
        // Prüfbilder: DK_SHOWCASE_AT="10.5 17.2 …" rendert nur diese Zeitpunkte
        var only = (Environment.GetEnvironmentVariable("DK_SHOWCASE_AT") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => (int)Math.Round(double.Parse(x, System.Globalization.CultureInfo.InvariantCulture) * Fps)).ToHashSet();
        Console.WriteLine($"dauer {total:0.0} s, {frames} bilder");
        for (int f = 0; f < frames; f++)
        {
            if (only.Count > 0 && !only.Contains(f)) continue;
            double time = f / (double)Fps;
            var s = segs.Last(x => time >= x.Start);
            int si = segs.IndexOf(s);
            double t = time - s.Start, d = s.End - s.Start;
            // Karten blenden weich über Schwarz, Fenster und Szenen haben eigene Übergänge
            double fade = s.Kind != "card" ? 1 : Math.Min(Math.Clamp(t / 0.3, 0, 1), Math.Clamp((d - t) / 0.3, 0, 1));
            var v = new DrawingVisual();
            using (var dc = v.RenderOpen())
            {
                Background(dc, time);
                dc.PushOpacity(fade);
                s.Draw(dc, time);
                dc.Pop();
                Film.Look(dc, new Size(W, H), f, 0.04);
            }
            targetBmp.Clear(); targetBmp.Render(v);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(targetBmp));
            using var file = File.Create(Path.Combine(output, $"{f:D5}.png")); enc.Save(file);
            if (f % 300 == 0) Console.WriteLine($"{f}/{frames}");
        }
        window.AllowClose = true; window.Close();
        Console.WriteLine($"ok {frames} Bilder, {total:0.0} s");
        return 0;
    }

    // ================================================================ Szenen
    sealed record ShowClip(Scene Scene, List<IconInfo> Icons, double T0, double Speed, List<Cue> Cues, double IconScale, Func<double, (Point C, double Z)>? Follow = null);

    /// <summary>Beginn des Fensters (Länge span Szenenzeit), in dem am meisten passiert; bei Gleichstand das frühere.</summary>
    static double Busiest(IReadOnlyList<double> events, double span, double from, double to)
    {
        double best = from; int most = -1;
        for (double t0 = from; t0 <= to; t0 += 0.1)
        {
            int n = events.Count(e => e >= t0 + 0.35 && e <= t0 + span - 0.25);
            if (n > most) { most = n; best = t0; }
        }
        return best;
    }

    /// <summary>Eine Szene in Echtzeit (höchstens 1,7-fach gerafft) um ihren stärksten Moment, auf dem ganzen Bild.</summary>
    static ShowClip SceneClip(string kind, double length, Rect desk, List<IconInfo> icons, Dictionary<string, Point> homes)
    {
        bool nuke = kind == "nuke", jericho = kind is "jericho" or "nuke";
        var scene = new Scene(icons, homes, 52, [desk], desk, true, 64, nuclear: nuke, attack: jericho ? null : kind);
        var raid = scene.Raid;
        double first = raid.Count > 0 ? raid.Min(r => r.T) : Scene.Impact, last = raid.Count > 0 ? raid.Max(r => r.T) : Scene.Impact;
        double t0, speed;
        Func<double, (Point C, double Z)>? follow = null;
        // Ereignisse, nach denen das dichteste Fenster gewählt wird
        var events = raid.Select(r => r.T).ToList();
        if (scene.Attack is GunshipRaid g0) events.AddRange(g0.Shots.Select(s => s.Fire));
        if (scene.Attack is StrafeRaid s0) events.AddRange(s0.Shots.Select(s => s.Fire));
        if (scene.Attack is DuelRaid d0) { events.AddRange(d0.Shots.Select(s => s.Fire)); events.AddRange(d0.Bursts.Select(b => b.Start)); }
        if (jericho) { t0 = Scene.Impact - (nuke ? 0.8 : 1.1); speed = 1; }
        else if (scene.Attack is PetRaid pet)
        {
            speed = kind == Attacks.Shepherd ? 1.6 : 1.4;
            t0 = Busiest(events, length * speed, Math.Max(0, pet.PreviewTime - 3), pet.PreviewTime + 8);
            // Kamera folgt dem Tier (geglättet über 0,6 s Szenenzeit) und fährt heran, damit die Pixel zu sehen sind
            follow = st =>
            {
                double x = 0, y = 0; int n = 0;
                for (double k = 0; k <= 0.6; k += 0.1) if (pet.AnimalAt(st - k) is { } a) { x += a.X; y += a.Y; n++; }
                const double z = 1.7;
                if (n == 0) return (new Point(W / 2.0, H / 2.0), 1);
                double hw = W / (2 * z), hh = H / (2 * z);
                return (new Point(Math.Clamp(x / n, hw, W - hw), Math.Clamp(y / n, hh, H - hh)), z);
            };
        }
        else
        {
            speed = kind == Attacks.Duel ? 1.3 : kind == Attacks.SR71 ? 1.0 : 1.6;
            t0 = Busiest(events, length * speed, Math.Max(0, first - 1.5), last);
        }
        t0 = Math.Max(0, t0);
        double V(double sceneT) => (sceneT - t0) / speed;
        bool In(double v) => v >= 0.05 && v <= length - 0.1;
        var cues = new List<Cue>();
        string bed = kind switch
        {
            Attacks.Tu95 or Attacks.Il76 or Attacks.Ac130 => "props",
            Attacks.Ka52 => "rotor",
            Attacks.Shahed => "moped",
            Attacks.Vacuum => "servo",
            "jericho" or "nuke" or Attacks.Gravity or Attacks.Cat or Attacks.Pug or Attacks.Shepherd => "",
            _ => "jet",
        };
        if (bed != "") cues.Add(new Cue(0, bed, 0, 1, length));
        bool soft = scene.Attack is PetRaid || kind is Attacks.Vacuum or Attacks.Gravity;
        if (jericho) cues.Add(new Cue(V(Scene.Impact), nuke ? "nuke" : "bigboom", 0, 1));
        foreach (var r in raid.OrderBy(r => r.T)) if (In(V(r.T))) cues.Add(new Cue(V(r.T), soft ? "drop" : "boom", Pan(r.P.X), soft ? 0.45 : 0.7));
        if (scene.Attack is GunshipRaid gun)
            foreach (var shot in gun.Shots) if (In(V(shot.Fire))) cues.Add(new Cue(V(shot.Fire), shot.Gun == GunshipRaid.Gun.G105 ? "cannon" : shot.Gun == GunshipRaid.Gun.G40 ? "pom" : "tick", 0, 0.5));
        if (scene.Attack is StrafeRaid strafe)
            foreach (var shot in strafe.Shots) if (In(V(shot.Fire))) cues.Add(new Cue(V(shot.Fire), shot.Weapon == StrafeRaid.Weapon.Gun ? "brrt" : "rocket", Pan(strafe.PlaneAt(shot.Fire).X), 0.5));
        if (scene.Attack is DuelRaid duel)
        {
            foreach (var s in duel.Shots) if (In(V(s.Fire))) cues.Add(new Cue(V(s.Fire), "rocket", Pan(duel.PlaneAt(s.ByF35, s.Fire).X), 0.55));
            foreach (var b in duel.Bursts) if (In(V(b.Start))) cues.Add(new Cue(V(b.Start), "brrt", Pan(duel.PlaneAt(b.ByF35, b.Start).X), 0.45));
        }
        var thinned = new List<Cue>();
        foreach (var c in cues.OrderBy(c => c.T))
            if (c.Kind is not ("boom" or "drop") || !thinned.Any(o => o.Kind == c.Kind && Math.Abs(o.T - c.T) < 0.07)) thinned.Add(c);
        return new ShowClip(scene, icons, t0, speed, thinned, 1, follow);
    }

    /// <summary>Ein Helfer bringt verstreute Symbole zurück (gerafft um das erste Absetzen), auf dem ganzen Bild.</summary>
    static ShowClip HelperShowClip(string kind, double length, Rect desk)
    {
        var names = Demo.IconSet.Take(8).Select(x => x.Name).ToList();
        var homes = names.Select((n, i) => (n, new Point(560 + (i / 4) * 150, 330 + (i % 4) * 140))).ToDictionary(x => x.n, x => x.Item2);
        var rnd = new Random(11);
        var icons = names.Select(n => new IconInfo(n, n, new Point(1050 + rnd.Next(0, 650), 260 + rnd.Next(0, 560)))).ToList();
        Helpers.Current = kind;
        var scene = new Scene(icons, homes, 72, [desk], desk, false, 130);
        double speed = Math.Clamp((scene.Duration - scene.RobotStart) / 9, 1.5, 4);
        double t0 = Math.Max(scene.RobotStart, FirstDrop(scene, icons, homes) - length * speed * 0.4);
        var cues = new List<Cue> { new(0, kind is Helpers.Chinook or Helpers.Mi26 ? "rotor" : kind == Helpers.Drones ? "drones" : "servo", 0, 1, length) };
        var done = new HashSet<string>();
        for (double v = 0; v <= length; v += 1.0 / 30)
        {
            var centers = scene.IconCenters(t0 + v * speed);
            foreach (var n in names)
                if (!done.Contains(n) && centers[n] is { } c && (c - homes[n]).Length < 0.5) { done.Add(n); if (v > 0.05) cues.Add(new Cue(v, "drop", Pan(c.X), 0.6)); }
        }
        return new ShowClip(scene, icons, t0, speed, cues, 1.4);
    }

    /// <summary>Vorführschreibtisch auf dem ganzen Bild (wie der echte Desktop), Szene darüber.</summary>
    static void FullDesk(DrawingContext dc, Scene? scene, List<IconInfo> icons, double time, double iconScale)
    {
        dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0x14, 0x22, 0x4A), Color.FromRgb(0x2A, 0x16, 0x4A), 35), null, new Rect(-400, -400, W + 800, H + 800));
        var centers = scene?.IconCenters(time);
        foreach (var icon in icons)
        {
            Point? at = centers is null ? icon.Center : centers[icon.Id];
            if (at is not { } c) continue;
            double z = 52 * iconScale;
            dc.DrawImage(Demo.Image(icon.Id), new Rect(c.X - z / 2, c.Y - z * 0.58, z, z));
            var label = Text(icon.Name, 13 * iconScale, Brushes.White, Regular);
            dc.DrawText(label, new Point(c.X - label.Width / 2, c.Y + z * 0.46));
        }
        scene?.Draw(dc, time);
    }

    // ================================================================ Bausteine
    static void WindowFrame(DrawingContext dc, Rect r, double bar, Look L)
    {
        for (int i = 1; i <= 6; i++)
        {
            var s = r; s.Inflate(i * 5, i * 5); s.Offset(0, 14);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb((byte)(26 - i * 3), 0, 0, 0)), null, s, 10 + i * 5, 10 + i * 5);
        }
        dc.PushClip(new RectangleGeometry(r, 8, 8));
        dc.DrawRectangle(L.Bg, null, new Rect(r.X, r.Y, r.Width, bar));
        var title = new FormattedText("DeskKeeper", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(L.Font, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 12, L.Secondary, 1);
        dc.DrawText(title, new Point(r.X + 16, r.Y + (bar - title.Height) / 2));
        var icons = new Typeface(L.Icons, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        string[] glyphs = ["", "", ""];
        for (int i = 0; i < 3; i++)
        {
            var g = new FormattedText(glyphs[i], System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, icons, 10, L.Text, 1);
            dc.DrawText(g, new Point(r.Right - 46 * (3 - i) + (46 - g.Width) / 2, r.Y + (bar - g.Height) / 2));
        }
        dc.Pop();
        dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255)), 1), r, 8, 8);
    }

    /// <summary>Windows-Pfeilzeiger mit Klick: kurz eingedrückt, Ring breitet sich aus.</summary>
    static void DrawCursor(DrawingContext dc, Point p, List<double> clicks, double t, double appear)
    {
        double press = 1;
        foreach (var c in clicks)
        {
            double age = t - c;
            if (age < -0.08 || age > 0.5) continue;
            if (age < 0.12) press = Math.Min(press, 0.86 + 0.14 * Math.Abs(age + 0.02) / 0.1);
            if (age >= 0)
            {
                double k = Ease(Math.Clamp(age / 0.45, 0, 1));
                dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb((byte)(200 * (1 - k)), Accent.R, Accent.G, Accent.B)), 3), p, 8 + 30 * k, 8 + 30 * k);
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(80 * (1 - k)), 255, 255, 255)), null, p, 10 * (1 - k) + 4, 10 * (1 - k) + 4);
            }
        }
        var arrow = Geometry.Parse("M 0,0 L 0,25 L 6,19.5 L 10.2,29 L 14,27.4 L 9.9,18.2 L 17.6,18.2 Z");
        double s = 1.25 * press;
        dc.PushTransform(new TranslateTransform(p.X, p.Y));
        dc.PushTransform(new ScaleTransform(s, s));
        dc.PushTransform(new TranslateTransform(1.2, 2));
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(0x50, 0, 0, 0)), null, arrow);
        dc.Pop();
        dc.DrawGeometry(Brushes.White, new Pen(Brushes.Black, 1.3) { LineJoin = PenLineJoin.Round }, arrow);
        dc.Pop(); dc.Pop();
    }

    static void LowerThird(DrawingContext dc, double t, double len, string title, string sub)
    {
        double k = Math.Min(Ease(Math.Clamp((t - 0.15) / 0.35, 0, 1)), Ease(Math.Clamp((len - t) / 0.25, 0, 1)));
        if (k <= 0) return;
        var big = Text(title, 34, Brushes.White, Bold);
        var small = Text(sub, 21, new SolidColorBrush(Color.FromRgb(0xC8, 0xD2, 0xE6)), Regular);
        double w = Math.Max(big.Width, small.Width) + 56, h = 96;
        var box = new Rect(64 - (1 - k) * 40, H - 64 - h, w, h);
        dc.PushOpacity(k);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0xC8, 0x0B, 0x10, 0x1E)), new Pen(new SolidColorBrush(Color.FromArgb(0x30, 255, 255, 255)), 1), box, 14, 14);
        dc.DrawRoundedRectangle(new SolidColorBrush(Accent), null, new Rect(box.X + 14, box.Y + 18, 4, h - 36), 2, 2);
        dc.DrawText(big, new Point(box.X + 32, box.Y + 12));
        dc.DrawText(small, new Point(box.X + 32, box.Y + 56));
        dc.Pop();
    }

    static void Toast(DrawingContext dc, double t, string text, Look L)
    {
        double k = Math.Min(Ease(Math.Clamp(t / 0.25, 0, 1)), Ease(Math.Clamp((1.4 - t) / 0.25, 0, 1)));
        var ft = Text(text, 17, Brushes.White, Regular);
        var check = Text("", 16, new SolidColorBrush(Color.FromRgb(0x6C, 0xCB, 0x5F)), new Typeface(L.Icons, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal));
        var box = new Rect(W / 2.0 - (ft.Width + 70) / 2, 150 - (1 - k) * 16, ft.Width + 70, 46);
        dc.PushOpacity(k);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0xF0, 0x2B, 0x2B, 0x2B)), new Pen(new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255)), 1), box, 10, 10);
        dc.DrawText(check, new Point(box.X + 20, box.Y + (46 - check.Height) / 2));
        dc.DrawText(ft, new Point(box.X + 48, box.Y + (46 - ft.Height) / 2));
        dc.Pop();
    }

    static void ChapterCard(DrawingContext dc, double t, string kicker, string title, string sub)
    {
        Film.Chip(dc, t, kicker, 18, Regular, new SolidColorBrush(Accent), new SolidColorBrush(Color.FromArgb(0x30, Accent.R, Accent.G, Accent.B)), W / 2.0, 360);
        Film.Words(dc, t, title, 112, Bold, Brushes.White, new SolidColorBrush(Accent), null, W / 2.0, 420, delay: 0.08);
        var small = Text(sub, 34, new SolidColorBrush(Color.FromRgb(0xC8, 0xD2, 0xE6)), Regular);
        double k = Ease(Math.Clamp((t - 0.3) / 0.5, 0, 1));
        dc.PushOpacity(k);
        dc.DrawText(small, new Point((W - small.Width) / 2, 590 + (1 - k) * 12));
        dc.Pop();
    }

    /// <summary>„Nicht täglich, aber regelmäßig“: Versionen als Zeitstrahl, Knoten erscheinen nacheinander.</summary>
    static void Versions(DrawingContext dc, double t)
    {
        Film.Words(dc, t, "Not every day. But regularly.", 76, Bold, Brushes.White, new SolidColorBrush(Accent), "regularly.", W / 2.0, 200);
        var sub = Text("One developer, steady updates: new scenes, new helpers, fixes from your reports.", 28, new SolidColorBrush(Color.FromRgb(0xC8, 0xD2, 0xE6)), Regular);
        double ks = Ease(Math.Clamp((t - 0.4) / 0.5, 0, 1));
        dc.PushOpacity(ks); dc.DrawText(sub, new Point((W - sub.Width) / 2, 318)); dc.Pop();
        (string V, string What)[] steps = [("1.0", "Icons stay put"), ("1.3", "Six helpers"), ("1.5", "F-35A vs Su-57"), ("1.6", "MiG-31K, SR-71"), ("1.7", "Pixel-art pets")];
        double x0 = 260, x1 = W - 260, y = 600;
        double line = Ease(Math.Clamp((t - 0.6) / 2.4, 0, 1));
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(0x50, 255, 255, 255)), 3), new Point(x0, y), new Point(x1, y));
        dc.DrawLine(new Pen(new SolidColorBrush(Accent), 3), new Point(x0, y), new Point(x0 + (x1 - x0) * line, y));
        for (int i = 0; i < steps.Length; i++)
        {
            double x = x0 + (x1 - x0) * i / (steps.Length - 1);
            double k = Ease(Math.Clamp((t - 0.6 - i * 0.5) / 0.4, 0, 1));
            if (k <= 0) continue;
            dc.PushOpacity(k);
            dc.DrawEllipse(new SolidColorBrush(i == steps.Length - 1 ? Accent : Color.FromRgb(0x1B, 0x24, 0x3C)), new Pen(new SolidColorBrush(Accent), 3), new Point(x, y), 13 * k, 13 * k);
            var v = Text(steps[i].V, 40, Brushes.White, Bold);
            dc.DrawText(v, new Point(x - v.Width / 2, y - 84 + (1 - k) * 10));
            var w = Text(steps[i].What, 22, new SolidColorBrush(Color.FromRgb(0xA9, 0xB6, 0xCF)), Regular);
            dc.DrawText(w, new Point(x - w.Width / 2, y + 30));
            dc.Pop();
        }
        double kn = Ease(Math.Clamp((t - 3.6) / 0.5, 0, 1));
        var next = Text("Next: more military, natural disasters, and whatever you ask for.", 26, new SolidColorBrush(Accent), Regular);
        dc.PushOpacity(kn); dc.DrawText(next, new Point((W - next.Width) / 2, 760 + (1 - kn) * 10)); dc.Pop();
    }
}
#endif
