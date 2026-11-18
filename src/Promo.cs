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
/// Oberfläche → DUM-E trägt Symbole → Explosion als Gag → Abspann. 1920×1080, 30 Bilder/s, nur Vorführdaten.
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
        var robot = RobotClip(); var blast = BlastClip();

        DeskIcon Icon(string n, Point p) => new(p, n, Demo.Image(n));
        List<DeskIcon> At(Func<string, Point> where) => names.Select(n => Icon(n, where(n))).ToList();

        var sections = new List<Section>
        {
            new(0, 2.8, (dc, t, d) => Title(dc, t, "DeskKeeper", "Your desktop icons. Exactly where you left them.")),
            new(2.8, 5.8, (dc, t, d) => { Caption(dc, t, "Laptop + ultrawide.", "Icons sorted the way you like."); Stage(dc, view, stage, Demo.Both, At(n => home[n]), 1); }),
            new(5.8, 8.8, (dc, t, d) =>
            {
                Caption(dc, t, "Unplug the monitor …", "Windows piles everything onto the laptop.");
                double k = Ease(Math.Clamp((t - 0.5) / 1.1, 0, 1));
                var laptopAt = Demo.LaptopOnly;
                Stage(dc, view, stage, t < 0.5 ? Demo.Both : [Demo.Both[1]], At(n => Lerp(home[n], laptopDump[n], k)), 1);
            }),
            new(8.8, 11.8, (dc, t, d) =>
            {
                Caption(dc, t, "Plug it back in.", "And Windows shuffles your icons.");
                double k = Ease(Math.Clamp((t - 0.3) / 0.9, 0, 1));
                Stage(dc, view, stage, Demo.Both, At(n => Lerp(laptopDump[n], mixed[n], k)), 1);
            }),
            new(11.8, 15.6, (dc, t, d) =>
            {
                Caption(dc, t, "DeskKeeper puts every icon back.", "Automatically. Per display, per arrangement.");
                Stage(dc, view, stage, Demo.Both, names.Select((n, i) =>
                {
                    double k = Ease(Math.Clamp((t - 0.4 - i * 0.07) / 0.9, 0, 1));
                    return Icon(n, Lerp(mixed[n], home[n], k));
                }).ToList(), 1);
                if (t > 2.6) Badge(dc, Math.Clamp((t - 2.6) / 0.35, 0, 1));
            }),
            new(15.6, 20.2, (dc, t, d) =>
            {
                Caption(dc, t, "Looks right at home on Windows 10 and 11.", "Layouts · snapshots · auto-save · tray · English, Deutsch, Русский");
                if (shot is not null) Screenshot(dc, shot, t, d);
            }),
            new(20.2, 24.6, (dc, t, d) => { Caption(dc, t, "Or let DUM-E carry them back.", "A tiny robot arm with a trailer, one icon at a time."); robot(dc, t); }),
            new(24.6, 27.4, (dc, t, d) => { Caption(dc, t, "… or blow them up first.", "Just for fun. Everything comes back."); blast(dc, t); }),
            new(27.4, 31, (dc, t, d) => Title(dc, t, "DeskKeeper", "Free and open source · Windows 10 & 11 · on GitHub")),
        };

        double total = sections[^1].End;
        int frames = (int)(total * Fps);
        var target = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
        for (int f = 0; f < frames; f++)
        {
            double time = f / (double)Fps;
            var s = sections.Last(x => time >= x.Start);
            double t = time - s.Start, d = s.End - s.Start;
            double fade = Math.Min(Math.Clamp(t / 0.35, 0, 1), Math.Clamp((d - t) / 0.35, 0, 1));
            var v = new DrawingVisual();
            using (var dc = v.RenderOpen())
            {
                Background(dc, time);
                dc.PushOpacity(fade);
                s.Draw(dc, t, d);
                dc.Pop();
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
        var big = Text(title, 104, Brushes.White, Bold);
        dc.DrawText(big, new Point((W - big.Width) / 2, 450 + rise));
        var small = Text(sub, 36, new SolidColorBrush(Color.FromRgb(0xC8, 0xD2, 0xE6)), Regular);
        dc.DrawText(small, new Point((W - small.Width) / 2, 600 + rise));
    }

    static void Caption(DrawingContext dc, double t, string title, string sub)
    {
        double k = Ease(Math.Clamp(t / 0.5, 0, 1));
        dc.PushOpacity(k);
        var big = Text(title, 58, Brushes.White, Bold);
        dc.DrawText(big, new Point((W - big.Width) / 2, 58 + (1 - k) * 14));
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

    static FormattedText Text(string s, double size, Brush b, Typeface face) => new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, b, 1);
    static double Ease(double x) => x < 0.5 ? 4 * x * x * x : 1 - Math.Pow(-2 * x + 2, 3) / 2;
    static Point Lerp(Point a, Point b, double k) => a + (b - a) * k;
    static BitmapSource Load(string path) { var b = new BitmapImage(); b.BeginInit(); b.CacheOption = BitmapCacheOption.OnLoad; b.UriSource = new Uri(Path.GetFullPath(path)); b.EndInit(); b.Freeze(); return b; }
}
