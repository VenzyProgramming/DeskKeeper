using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskKeeper;

/// <summary>
/// Filmische Bausteine für Werbevideo und Kurzvideo (19.11.2026: „noch nicht professionell genug“), nach dem Motion-Design-Skill der
/// Bibliothek: Kamera (langsames Heranfahren, Wackeln nach Stärke des Einschlags), Wischblende mit Bewegungsunschärfe, Wortweise
/// Maskenaufdeckung mit Akzentwort, Filmkörnung, Vignette und Farbstimmung. Alles reine Funktion der Zeit (kein Zufall pro Lauf).
/// </summary>
static class Film
{
    // ------------------------------------------------------------ Kamera
    /// <summary>Wackeln aus Ton-Ereignissen: je Einschlag eine abklingende Auslenkung (Atombombe am stärksten).</summary>
    public static Vector Shake(IEnumerable<Promo.Cue> cues, double t, double scale)
    {
        double x = 0, y = 0;
        foreach (var c in cues)
        {
            double a = c.Kind switch { "nuke" => 26, "bigboom" => 16, "boom" => 5, "cannon" => 7, "pom" => 2.5, "brrt" => 1.2, _ => 0 };
            if (a == 0 || t < c.T || t > c.T + 0.6) continue;
            double age = t - c.T, k = a * Math.Exp(-age / (c.Kind == "nuke" ? 0.35 : 0.12));
            x += k * Math.Sin(age * 83 + c.T * 13); y += k * Math.Cos(age * 71 + c.T * 7);
        }
        var v = new Vector(x, y) * scale;
        return v.Length > 30 * scale ? v / v.Length * 30 * scale : v;
    }

    /// <summary>Kamera um den Bildmittelpunkt: langsames Heranfahren (1 → 1 + push) und Wackeln.</summary>
    public static void Camera(DrawingContext dc, Size frame, double t, double d, double push, Vector shake, Action draw)
    {
        double s = 1 + push * Ease.Out(Math.Clamp(t / Math.Max(0.01, d), 0, 1));
        dc.PushTransform(new TranslateTransform(shake.X, shake.Y));
        dc.PushTransform(new ScaleTransform(s, s, frame.Width / 2, frame.Height / 2));
        draw();
        dc.Pop(); dc.Pop();
    }

    /// <summary>Wischblende: das neue Bild schießt in den ersten 0,16 s von rechts herein, mit Nachzieh-Kopien als Bewegungsunschärfe.</summary>
    public static void WhipIn(DrawingContext dc, Size frame, double t, Action draw, double length = 0.16)
    {
        if (t >= length) { draw(); return; }
        double k = Ease.Out(t / length);
        double offset = (1 - k) * frame.Width * 0.55;
        for (int g = 3; g >= 1; g--)
        {
            dc.PushOpacity(0.18);
            dc.PushTransform(new TranslateTransform(offset + g * frame.Width * 0.04 * (1 - k), 0));
            draw();
            dc.Pop(); dc.Pop();
        }
        dc.PushTransform(new TranslateTransform(offset, 0));
        draw();
        dc.Pop();
    }

    // ------------------------------------------------------------ Bildlook
    static readonly Dictionary<(int, int), BitmapSource[]> grain = [];

    /// <summary>Filmkörnung (vier vorberechnete Rauschbilder im Wechsel) und Vignette, darüber eine leichte Farbstimmung.</summary>
    public static void Look(DrawingContext dc, Size frame, int frameIndex, double grainOpacity = 0.07)
    {
        int w = (int)frame.Width / 3, h = (int)frame.Height / 3;
        if (!grain.TryGetValue((w, h), out var tiles))
        {
            var rnd = new Random(1234);
            tiles = new BitmapSource[4];
            for (int n = 0; n < 4; n++)
            {
                var px = new byte[w * h * 4];
                for (int i = 0; i < w * h; i++) { byte v = (byte)rnd.Next(0, 256); px[i * 4] = v; px[i * 4 + 1] = v; px[i * 4 + 2] = v; px[i * 4 + 3] = 255; }
                var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, w * 4); bmp.Freeze();
                tiles[n] = bmp;
            }
            grain[(w, h)] = tiles;
        }
        var rect = new Rect(frame);
        // Farbstimmung: kühle Tiefen, warme Lichter (sehr dezent)
        dc.DrawRectangle(new LinearGradientBrush(Color.FromArgb(0x10, 0x20, 0x40, 0xA0), Color.FromArgb(0x0C, 0xFF, 0x90, 0x40), 90), null, rect);
        dc.PushOpacity(grainOpacity);
        dc.DrawImage(tiles[frameIndex % 4], rect);
        dc.Pop();
        var vignette = new RadialGradientBrush { GradientOrigin = new Point(0.5, 0.5), Center = new Point(0.5, 0.5), RadiusX = 0.75, RadiusY = 0.75 };
        vignette.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.55));
        vignette.GradientStops.Add(new GradientStop(Color.FromArgb(0x70, 0, 0, 0), 1));
        dc.DrawRectangle(vignette, null, rect);
    }

    // ------------------------------------------------------------ Schrift
    /// <summary>
    /// Wortweise Maskenaufdeckung: jedes Wort gleitet aus einer Maske nach oben (0,5 s, Versatz 0,07 s), ein Wort im Akzent.
    /// outline &gt; 0 zeichnet eine dunkle Kontur (für Text über bewegtem Bild).
    /// </summary>
    public static void Words(DrawingContext dc, double t, string text, double size, Typeface face, Brush fill, Brush accent, string? accentWord,
        double centerX, double y, double outline = 0, double delay = 0)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var brushes = words.Select(w => string.Equals(w.Trim('.', ',', '!', '…'), accentWord, StringComparison.OrdinalIgnoreCase) ? accent : fill).ToList();
        var fts = words.Select((w, i) => Make(w, size, face, brushes[i])).ToList();
        double space = size * 0.28, total = fts.Sum(f => f.WidthIncludingTrailingWhitespace) + space * (fts.Count - 1), x = centerX - total / 2;
        for (int i = 0; i < fts.Count; i++)
        {
            var ft = fts[i];
            double k = Ease.Out(Math.Clamp((t - delay - i * 0.07) / 0.5, 0, 1));
            var clip = new Rect(x - size * 0.2, y - size * 0.1, ft.WidthIncludingTrailingWhitespace + size * 0.4, ft.Height + size * 0.25);
            dc.PushClip(new RectangleGeometry(clip));
            var at = new Point(x, y + (1 - k) * ft.Height * 1.05);
            if (outline > 0)
            {
                var g = ft.BuildGeometry(at);
                dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(0xE6, 0x08, 0x0A, 0x14)), outline) { LineJoin = PenLineJoin.Round }, g);
                dc.DrawGeometry(brushes[i], null, g);
            }
            else dc.DrawText(ft, at);
            dc.Pop();
            x += ft.WidthIncludingTrailingWhitespace + space;
        }
    }

    /// <summary>Kleiner Kennungs-Chip (Großbuchstaben, gesperrt), z. B. „03 / 12“.</summary>
    public static void Chip(DrawingContext dc, double t, string text, double size, Typeface face, Brush fill, Brush back, double centerX, double y, bool label = true)
    {
        double k = Ease.Out(Math.Clamp(t / 0.3, 0, 1));
        var ft = Make(label ? string.Join(" ", text.ToUpperInvariant().ToCharArray()) : text, size, face, fill);
        var box = new Rect(centerX - ft.Width / 2 - size * 0.7, y, ft.Width + size * 1.4, ft.Height + size * 0.5);
        dc.PushOpacity(k);
        dc.PushTransform(new TranslateTransform(0, (1 - k) * 12));
        dc.DrawRoundedRectangle(back, null, box, box.Height / 2, box.Height / 2);
        dc.DrawText(ft, new Point(box.X + size * 0.7, y + size * 0.25));
        dc.Pop(); dc.Pop();
    }

    static FormattedText Make(string s, double size, Typeface face, Brush b) => new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, b, 1);

    public static class Ease
    {
        public static double Out(double x) { x = Math.Clamp(x, 0, 1); return 1 - Math.Pow(1 - x, 3); }   // ähnlich bezier(0.16, 1, 0.3, 1)
        public static double In(double x) { x = Math.Clamp(x, 0, 1); return x * x * x; }
    }
}
