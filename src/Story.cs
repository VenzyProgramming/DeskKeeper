using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskKeeper;

/// <summary>
/// Erzählte Story-Folgen im Hochformat (19.11.2026: „so eine Storyline … das muss wirklich spannend sein“). Die Zeitleiste kommt aus
/// der Erzählung (werkzeuge/story_tts.py schreibt story.json mit Abschnitten und Untertitel-Gruppen); hier entstehen die Bilder dazu.
/// Aufbau je Bild: Folgen-Chip oben, Schlagzeile, Bildbaustein in der Mitte (Breitbild-Desktop, Bildschirmfoto, Modell, Zahlen …),
/// Untertitel in Wortgruppen unten in der sicheren Zone. DeskKeeper.exe --story &lt;story.json&gt; &lt;ordner&gt; (mit DESKKEEPER_DEMO=1).
/// </summary>
static class Story
{
    const int W = 1080, H = 1920, Fps = 30;
    static readonly Size Frame = new(W, H);
    static readonly Typeface Heavy = new(new FontFamily("Segoe UI Black, Segoe UI"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal);
    static readonly Typeface Semi = new(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    static readonly Typeface Mono = new(new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    static readonly Brush Accent = Frozen(new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF)));
    static readonly Brush Hot = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0x6A, 0x3D)));
    static readonly Brush Muted = Frozen(new SolidColorBrush(Color.FromRgb(0xB8, 0xC4, 0xDA)));
    static readonly Brush ChipBack = Frozen(new SolidColorBrush(Color.FromArgb(0xC8, 0x12, 0x18, 0x2C)));
    const string Media = "docs/media";   // Bildschirmfotos der öffentlichen Fassung (aus dem Repo-Ordner aufrufen)

    record Caption(double T, double Len, string Text);
    record Segment(double Start, double Len, string Visual, string Big, List<Caption> Captions);

    public static int Run(string storyJson, string output)
    {
        Directory.CreateDirectory(output);
        foreach (var f in Directory.EnumerateFiles(output, "*.png")) File.Delete(f);
        using var doc = JsonDocument.Parse(File.ReadAllText(storyJson));
        string part = doc.RootElement.GetProperty("part").GetString() ?? "";
        double total = doc.RootElement.GetProperty("total").GetDouble();
        var segs = doc.RootElement.GetProperty("segments").EnumerateArray().Select(s => new Segment(s.GetProperty("start").GetDouble(), s.GetProperty("len").GetDouble(),
            s.GetProperty("visual").GetString() ?? "", s.GetProperty("big").GetString() ?? "",
            s.GetProperty("captions").EnumerateArray().Select(c => new Caption(c.GetProperty("t").GetDouble(), c.GetProperty("len").GetDouble(), c.GetProperty("text").GetString() ?? "")).ToList())).ToList();

        var cues = new List<Promo.Cue>();
        var draws = new List<Action<DrawingContext, double, double>>();
        foreach (var s in segs)
        {
            var (draw, ev) = Visual(s.Visual, s.Len);
            draws.Add(draw);
            cues.Add(new Promo.Cue(s.Start, s.Visual.StartsWith("end") ? "end" : "caption", 0, 1));
            cues.AddRange(ev.Select(c => c with { T = c.T + s.Start }));
        }
        Helpers.Current = Helpers.Dume;
        Promo.WriteTimeline(Path.Combine(output, "timeline.json"), segs.Select(s => s.Start).ToList(), total, cues);

        int frames = (int)Math.Round(total * Fps);
        var target = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
        for (int f = 0; f < frames; f++)
        {
            double time = f / (double)Fps;
            int i = segs.FindLastIndex(s => time >= s.Start - 1e-9); if (i < 0) i = 0;
            var s = segs[i]; double t = time - s.Start;
            var v = new DrawingVisual();
            using (var dc = v.RenderOpen())
            {
                Shorts.Backdrop(dc);
                Film.WhipIn(dc, Frame, i == 0 ? 1 : t, () => draws[i](dc, t, s.Len));
                bool card = s.Visual.StartsWith("words") || s.Visual is "logo" || s.Visual.StartsWith("end") || s.Visual is "numbers";
                Film.Chip(dc, 1, part, 24, Mono, Accent, ChipBack, W / 2.0, 214);
                if (!card && s.Big != "") Film.Words(dc, i == 0 ? t + 1 : t, s.Big, 76, Heavy, Brushes.White, Accent, null, W / 2.0, 300, 12);
                Subtitle(dc, s, t);
                Film.Look(dc, Frame, f, 0.05);
            }
            target.Clear(); target.Render(v);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(target));
            using var file = File.Create(Path.Combine(output, $"{f:D5}.png")); enc.Save(file);
        }
        return 0;
    }

    /// <summary>Untertitel: die gerade gesprochene Wortgruppe groß, weiß mit Kontur, kurzes Aufploppen.</summary>
    static void Subtitle(DrawingContext dc, Segment s, double t)
    {
        var c = s.Captions.LastOrDefault(x => t >= x.T);
        if (c is null || t > c.T + c.Len + 0.25) return;
        double k = Film.Ease.Out((t - c.T) / 0.12);
        var ft = new FormattedText(c.Text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Heavy, 62, Brushes.White, 1);
        double scale = 0.9 + 0.1 * k;
        var g = ft.BuildGeometry(new Point((W - ft.Width) / 2, 1400));
        dc.PushTransform(new ScaleTransform(scale, scale, W / 2.0, 1400 + ft.Height / 2));
        dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(0xF0, 0x06, 0x08, 0x12)), 12) { LineJoin = PenLineJoin.Round }, g);
        dc.DrawGeometry(Brushes.White, null, g);
        dc.Pop();
    }

    // ------------------------------------------------------------ Bildbausteine
    static (Action<DrawingContext, double, double> Draw, List<Promo.Cue> Cues) Visual(string spec, double len)
    {
        var parts = spec.Split(':', 2);
        string kind = parts[0], arg = parts.Length > 1 ? parts[1] : "";
        var none = new List<Promo.Cue>();
        switch (kind)
        {
            case "stage": return StageVisual(arg, len);
            case "words": return ((dc, t, d) => BigWords(dc, t, arg), none);
            case "logo": return ((dc, t, d) => Logo(dc, t), new List<Promo.Cue> { new(0.3, "success", 0, 1) });
            case "shot": { var img = Load(Path.Combine(Media, arg)); return ((dc, t, d) => Shot(dc, img, t, d), none); }
            case "attack":
                {
                    var (draw, ev, _, _, _) = Shorts.AttackAction(arg, len);
                    return ((dc, t, d) => Shorts.Monitor(dc, () => Film.Camera(dc, new Size(1920, 1080), t, d, 0.06, Film.Shake(ev, t, 1), () => draw(dc, t))), ev);
                }
            case "helper":
                {
                    var (draw, ev, _, _, _) = Shorts.HelperAction(arg, len);
                    return ((dc, t, d) => Shorts.Monitor(dc, () => Film.Camera(dc, new Size(1920, 1080), t, d, 0.05, default, () => draw(dc, t))), ev);
                }
            case "montage":
                {
                    var kinds = arg.Split(',');
                    double piece = len / kinds.Length;
                    var clips = kinds.Select(k => Shorts.AttackAction(k, piece, 0.4)).ToList();   // Montage: gleich in die Einschläge
                    var cues = new List<Promo.Cue>();
                    for (int i = 0; i < clips.Count; i++) { cues.AddRange(clips[i].Cues.Select(c => c with { T = c.T + i * piece })); if (i > 0) cues.Add(new Promo.Cue(i * piece, "cut", 0, 0.8)); }
                    return ((dc, t, d) =>
                    {
                        int i = Math.Min(clips.Count - 1, (int)(t / piece)); double lt = t - i * piece;
                        var (draw, ev, _, _, _) = clips[i];
                        Film.WhipIn(dc, Frame, i == 0 ? 1 : lt, () => Shorts.Monitor(dc, () => Film.Camera(dc, new Size(1920, 1080), lt, piece, 0.06, Film.Shake(ev, lt, 1), () => draw(dc, lt))));
                    }, cues);
                }
            case "numbers": return ((dc, t, d) => Numbers(dc, t), new List<Promo.Cue> { new(0.2, "pom", 0, 1), new(1.6, "boom", 0, 0.5), new(2.6, "success", 0, 1) });
            case "model": return ((dc, t, d) => ModelCard(dc, t, arg), none);
            case "compare": return ((dc, t, d) => Compare(dc, t), none);
            case "scale": return ((dc, t, d) => Lineup(dc, t), none);
            case "quake": return Quake(len);
            case "end": return ((dc, t, d) => End(dc, t, arg), none);
        }
        return ((dc, t, d) => { }, none);
    }

    /// <summary>Monitor-Bühne wie im Werbevideo: Notebook unter dem Breitbild, Symbole ordentlich, abgesteckt, gemischt, zurück.</summary>
    static (Action<DrawingContext, double, double>, List<Promo.Cue>) StageVisual(string mode, double len)
    {
        var home = Demo.Home(); var names = home.Keys.ToList();
        var dump = names.Select((n, k) => (n, new Point(760 + 70 + (k / 8) * 118, 1440 + 80 + (k % 8) * 132))).ToDictionary(x => x.n, x => x.Item2);
        var rnd = new Random(7);
        var shuffled = names.OrderBy(_ => rnd.Next()).ToList();
        var slots = names.Select(n => home[n]).ToList();
        var mixed = shuffled.Select((n, i) => (n, slots[(i * 7 + 3) % slots.Count] + new Vector(rnd.Next(-90, 260), rnd.Next(-40, 220)))).ToDictionary(x => x.n, x => x.Item2);
        foreach (var n in names.Take(5)) mixed[n] = new Point(1000 + rnd.Next(0, 1400), 200 + rnd.Next(0, 900));
        var view = new ScreensView { AccentColor = Color.FromRgb(0x4C, 0xC2, 0xFF), IconPixels = 115 };   // Symbole auf dem Handy erkennbar
        var stage = new Rect(20, 590, 1040, 780);
        view.Measure(stage.Size); view.Arrange(new Rect(stage.Size));
        var cues = new List<Promo.Cue>();
        if (mode is "shuffle" or "plug") cues.Add(new Promo.Cue(0.6, "shuffle", 0, 1));
        if (mode == "unplug") cues.Add(new Promo.Cue(0.5, "pom", 0, 0.8));
        if (mode == "restore") { cues.Add(new Promo.Cue(1.9, "success", 0, 1)); for (int i = 0; i < names.Count; i += 3) cues.Add(new Promo.Cue(0.4 + i * 0.06 + 0.8, "drop", 0, 0.5)); }
        return ((dc, t, d) =>
        {
            double k = Film.Ease.Out(Math.Clamp((t - 0.5) / 1.0, 0, 1));
            IReadOnlyList<MonitorInfo> monitors = Demo.Both;
            Func<string, Point> at = mode switch
            {
                "tidy" => n => home[n],
                "shuffle" => n => home[n] + (mixed[n] - home[n]) * k,
                "unplug" => n => home[n] + (dump[n] - home[n]) * k,
                "plug" => n => dump[n] + (mixed[n] - dump[n]) * k,
                _ => n => { double kk = Film.Ease.Out(Math.Clamp((t - 0.4 - names.IndexOf(n) * 0.06) / 0.8, 0, 1)); return mixed[n] + (home[n] - mixed[n]) * kk; },
            };
            if (mode == "unplug" && t > 0.5) monitors = [Demo.Both[1]];
            view.Show(monitors, names.Select(n => new DeskIcon(at(n), n, Demo.Image(n))).ToList());
            view.InvalidateVisual(); view.Measure(stage.Size); view.Arrange(new Rect(stage.Size)); view.UpdateLayout();
            var bmp = new RenderTargetBitmap((int)stage.Width, (int)stage.Height, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(view);
            double push = 1 + 0.04 * Film.Ease.Out(t / d);
            dc.PushTransform(new ScaleTransform(push, push, W / 2.0, stage.Y + stage.Height / 2));
            dc.DrawImage(bmp, stage);
            dc.Pop();
            if (mode == "restore") Shorts.Check(dc, Math.Clamp((t - 1.9) / 0.3, 0, 1), 980);
        }, cues);
    }

    static void BigWords(DrawingContext dc, double t, string text)
    {
        var words = text.Split(' ');
        Film.Words(dc, t, text, 128, Heavy, Brushes.White, Hot, words[^1].Trim('.', '?', '!'), W / 2.0, 820, 18);
    }

    static void Logo(DrawingContext dc, double t)
    {
        double k = Film.Ease.Out(t / 0.6);
        var logo = new Rect(W / 2.0 - 110, 600 + (1 - k) * 40, 220, 220);
        dc.PushOpacity(k);
        dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(0x4C, 0xC2, 0xFF), Color.FromRgb(0x00, 0x5F, 0xB8), 45), null, logo, 50, 50);
        var glyph = new FormattedText("\uE7F4", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe Fluent Icons"), 110, Brushes.White, 1);
        dc.DrawText(glyph, new Point(logo.X + (logo.Width - glyph.Width) / 2, logo.Y + (logo.Height - glyph.Height) / 2));
        dc.Pop();
        Film.Words(dc, t - 0.2, "DeskKeeper", 130, Heavy, Brushes.White, Accent, "DeskKeeper", W / 2.0, 880, 18);
        Film.Words(dc, t - 0.5, "Your icons. Exactly where you left them.", 42, Semi, Muted, Accent, null, W / 2.0, 1060, 8);
    }

    static void Shot(DrawingContext dc, BitmapSource img, double t, double d)
    {
        double w = 1000 * (1 + 0.05 * Film.Ease.Out(t / d)), h = w * img.PixelHeight / img.PixelWidth;
        if (h > 760) { h = 760 * (1 + 0.05 * Film.Ease.Out(t / d)); w = h * img.PixelWidth / img.PixelHeight; }
        var r = new Rect((W - w) / 2, 960 - h / 2, w, h);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0)), null, new Rect(r.X + 10, r.Y + 20, r.Width, r.Height), 18, 18);
        dc.PushClip(new RectangleGeometry(r, 16, 16));
        dc.DrawImage(img, r);
        dc.Pop();
        dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(0x50, 255, 255, 255)), 2), r, 16, 16);
    }

    static void Numbers(DrawingContext dc, double t)
    {
        double k1 = Film.Ease.Out(t / 0.4);
        dc.PushOpacity(k1);
        Film.Words(dc, t, "4300 × 1800", 132, Heavy, Hot, Hot, null, W / 2.0, 640, 18);
        dc.Pop();
        double strike = Film.Ease.Out(Math.Clamp((t - 1.4) / 0.3, 0, 1));
        if (strike > 0) dc.DrawLine(new Pen(Brushes.White, 14) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, new Point(150, 735), new Point(150 + 780 * strike, 735));
        Film.Words(dc, t - 0.3, "showed Windows", 44, Semi, Muted, Accent, null, W / 2.0, 820, 8);
        Film.Words(dc, t - 2.4, "3440 × 1440", 132, Heavy, Accent, Accent, null, W / 2.0, 960, 18);
        Film.Words(dc, t - 2.7, "is the real screen", 44, Semi, Muted, Accent, null, W / 2.0, 1140, 8);
    }

    static void ModelCard(DrawingContext dc, double t, string kind)
    {
        Shorts.Blueprint(dc, t);
        var e = Shorts.Series.First(x => x.Kind == kind);
        if (e.Model is { } m) Shorts.Float(dc, t, () => m(dc, new Point(W / 2.0, 960), 760, t));
    }

    static readonly BitmapSource? Reference = File.Exists("vorlage/su34-top-dvids.jpg") ? Load("vorlage/su34-top-dvids.jpg") : null;

    /// <summary>Echte Draufsicht (gemeinfrei, DVIDS) neben dem nachgezeichneten Modell.</summary>
    static void Compare(DrawingContext dc, double t)
    {
        Shorts.Blueprint(dc, t);
        double k = Film.Ease.Out(t / 0.5), k2 = Film.Ease.Out((t - 0.6) / 0.5);
        if (Reference is not null)
        {
            dc.PushOpacity(k);
            var r = new Rect(50, 620, 470, 627);
            dc.DrawRoundedRectangle(Brushes.White, null, r, 14, 14);
            dc.PushClip(new RectangleGeometry(r, 14, 14)); dc.DrawImage(Reference, r); dc.Pop();
            Film.Chip(dc, 1, "Real top view", 26, Semi, Brushes.White, ChipBack, 285, 1270, label: false);
            dc.Pop();
        }
        dc.PushOpacity(Math.Max(0, k2));
        var e = Shorts.Series.First(x => x.Kind == Attacks.Su34);
        e.Model!(dc, new Point(805, 935), 520, t);
        Film.Chip(dc, 1, "DeskKeeper", 26, Semi, Accent, ChipBack, 805, 1270, label: false);
        dc.Pop();
    }

    /// <summary>Alle Flugzeuge in einem Maßstab, darunter Symbole als 6-m-Container.</summary>
    static void Lineup(DrawingContext dc, double t)
    {
        Shorts.Blueprint(dc, t);
        const double pm = 3.6;   // Pixel je Meter
        double up = -Math.PI / 2;
        (double X, double Y, Action<DrawingContext, Point> Draw)[] all =
        [
            (180, 760, (dc, c) => Art.Bomber(dc, c, up, 20, false, 0, 54.1 * pm / 100, t)),
            (460, 760, (dc, c) => Planes.Tu95(dc, c, up, 44.7 * pm / 100, 0, t)),
            (760, 760, (dc, c) => Planes.Il76(dc, c, up, 45.3 * pm / 100, 0, false)),
            (180, 1060, (dc, c) => Planes.AC130(dc, c, up, 40.4 * pm / 137, 0, t)),
            (420, 1060, (dc, c) => Planes.Su34(dc, c, up, 20.1 * pm / 100, 0, t)),
            (600, 1060, (dc, c) => Planes.A10(dc, c, up, 17.53 * pm / 107.8, 0, t)),
            (760, 1060, (dc, c) => Planes.Ka52(dc, c, up, 13.8 * pm / 100, 0, t)),
            (900, 1060, (dc, c) => Planes.Shahed(dc, c, up, 2.5 * pm / 71.6, 0, t)),
        ];
        for (int i = 0; i < all.Length; i++)
        {
            double k = Film.Ease.Out((t - i * 0.12) / 0.4);
            if (k <= 0) continue;
            dc.PushOpacity(k);
            all[i].Draw(dc, new Point(all[i].X, all[i].Y + (1 - k) * 30));
            dc.Pop();
        }
        var folder = new SolidColorBrush(Color.FromRgb(0xE7, 0xB4, 0x7B));
        double s = 6 * pm;
        for (int i = 0; i < 9; i++) dc.DrawRoundedRectangle(folder, null, new Rect(W / 2.0 - 9 * s * 0.6 + i * s * 1.2, 1230, s, s * 0.85), 3, 3);   // 9 Symbole = Länge der Tu-160
    }

    static (Action<DrawingContext, double, double>, List<Promo.Cue>) Quake(double len)
    {
        var (icons, homes) = Promo.DemoDesk();
        var cues = new List<Promo.Cue> { new(0.3, "nuke", 0, 0.6), new(1.4, "boom", 0, 0.6), new(2.6, "boom", 0, 0.6) };
        return ((dc, t, d) =>
        {
            Shorts.Monitor(dc, () =>
            {
                double amp = 16 * Math.Max(0, Math.Sin(Math.Min(t, d) / d * Math.PI)) + 4;
                dc.PushTransform(new TranslateTransform(Math.Sin(t * 41) * amp, Math.Cos(t * 37) * amp * 0.6));
                Shorts.Wallpaper(dc);
                foreach (var icon in icons)
                {
                    var h = homes[icon.Id];
                    var j = new Vector(Math.Sin(t * 23 + h.X) * amp * 0.8, Math.Cos(t * 19 + h.Y) * amp * 0.8 + Math.Max(0, t - 1.5) * 30 * (Math.Sin(h.X) + 1));
                    Shorts.DeskIcon(dc, icon, h + j);
                }
                dc.Pop();
                // Riss im Bildschirm
                double crack = Film.Ease.Out((t - 1.2) / 0.4);
                if (crack > 0)
                {
                    var p = new Pen(new SolidColorBrush(Color.FromArgb(0xD0, 255, 255, 255)), 5);
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(new Point(960, 0), false, false);
                        foreach (var q in new[] { (900, 180), (1010, 330), (930, 520), (1040, 700), (980, 1080) }) c.LineTo(new Point(q.Item1, q.Item2 * crack), true, false);
                    }
                    dc.DrawGeometry(null, p, g);
                }
            });
        }, cues);
    }

    static void End(DrawingContext dc, double t, string next)
    {
        double k = Film.Ease.Out(t / 0.5);
        var logo = new Rect(W / 2.0 - 80, 520 + (1 - k) * 30, 160, 160);
        dc.PushOpacity(k);
        dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(0x4C, 0xC2, 0xFF), Color.FromRgb(0x00, 0x5F, 0xB8), 45), null, logo, 36, 36);
        var glyph = new FormattedText("\uE7F4", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe Fluent Icons"), 78, Brushes.White, 1);
        dc.DrawText(glyph, new Point(logo.X + (logo.Width - glyph.Width) / 2, logo.Y + (logo.Height - glyph.Height) / 2));
        dc.Pop();
        Film.Words(dc, t, "DeskKeeper", 100, Heavy, Brushes.White, Accent, null, W / 2.0, 730, 14);
        Film.Words(dc, t - 0.3, next, 64, Heavy, Hot, Hot, null, W / 2.0, 880, 10);
        Film.Chip(dc, Math.Clamp(t - 0.8, 0, 1) * 3.3, "github.com/VenzyProgramming/DeskKeeper", 32, Semi, Accent, ChipBack, W / 2.0, 1030, label: false);
    }

    static BitmapSource Load(string path) { var b = new BitmapImage(); b.BeginInit(); b.CacheOption = BitmapCacheOption.OnLoad; b.UriSource = new Uri(Path.GetFullPath(path)); b.EndInit(); b.Freeze(); return b; }
    static T Frozen<T>(T f) where T : Freezable { f.Freeze(); return f; }
}
