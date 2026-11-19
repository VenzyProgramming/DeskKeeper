using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskKeeper;

/// <summary>
/// Weiche Formen als vorgerenderte Bilder (18.11.2026: Atomexplosion ruckelte):
/// Verläufe werden einmal in kleine Bilder gerendert (Deckkraft in 8 Stufen eingebacken) und dann nur gestreckt.
/// Kein PushOpacity je Teilchen (das erzeugt in WPF je Teilchen eine eigene Ebene) und keine großen Verlaufsflächen je Bild.
/// </summary>
static class Soft
{
    const int Levels = 8;
    static readonly Dictionary<(Brush, int, int), BitmapSource> cache = new(ReferenceEqualityComparer2.Instance);

    /// <summary>Ellipse mit Verlaufspinsel, Deckkraft 0…1, auflösung = Bildgröße (64 für Teilchen, 256 für dünne Ringe).</summary>
    public static void Draw(DrawingContext dc, Brush brush, Point center, double rx, double ry, double opacity, int resolution = 64)
    {
        if (opacity <= 0.01 || rx < 0.5 || ry < 0.5) return;
        int level = Math.Clamp((int)Math.Round(opacity * Levels), 1, Levels);
        var key = (brush, level, resolution);
        if (!cache.TryGetValue(key, out var image))
        {
            var visual = new DrawingVisual();
            using (var c = visual.RenderOpen())
            {
                c.PushOpacity(level / (double)Levels);
                c.DrawEllipse(brush, null, new Point(resolution / 2.0, resolution / 2.0), resolution / 2.0, resolution / 2.0);
                c.Pop();
            }
            var bitmap = new RenderTargetBitmap(resolution, resolution, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            cache[key] = image = bitmap;
        }
        dc.DrawImage(image, new Rect(center.X - rx, center.Y - ry, rx * 2, ry * 2));
    }

    const int Steps = 32;
    static readonly Dictionary<(Brush, int, int), Brush> faded = new(ReferenceEqualityComparer2.Instance);
    static readonly Dictionary<(Brush, int, int), Pen> fadedPens = new(ReferenceEqualityComparer2.Instance);

    /// <summary>Einfarbiger Pinsel mit eingebackener Deckkraft (32 Stufen) statt PushOpacity: jede Deckkraftgruppe legt in WPF
    /// eine eigene Zwischenfläche an. Mit 32 brennenden Symbolen kostete das 60 % der Bildzeit (19.11.2026: „ein bisschen laggy“).</summary>
    public static Brush Fade(Brush solid, double opacity)
    {
        int level = Math.Clamp((int)Math.Round(opacity * Steps), 0, Steps);
        if (level == Steps) return solid;
        var key = (solid, level, -1);
        if (!faded.TryGetValue(key, out var brush))
        {
            var c = solid is SolidColorBrush s ? s.Color : Colors.Gray;
            var b = new SolidColorBrush(Color.FromArgb((byte)Math.Round(c.A * level / (double)Steps), c.R, c.G, c.B));
            b.Freeze();
            faded[key] = brush = b;
        }
        return brush;
    }

    /// <summary>Stift mit runden Enden, Farbe mit eingebackener Deckkraft; Breite auf 0,05 px gerundet.</summary>
    public static Pen FadePen(Brush solid, double width, double opacity)
    {
        int level = Math.Clamp((int)Math.Round(opacity * Steps), 0, Steps), w = Math.Max(1, (int)Math.Round(width * 20));
        var key = (solid, level, w);
        if (!fadedPens.TryGetValue(key, out var pen))
        {
            pen = new Pen(Fade(solid, level / (double)Steps), w / 20.0) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            pen.Freeze();
            fadedPens[key] = pen;
        }
        return pen;
    }

    sealed class ReferenceEqualityComparer2 : IEqualityComparer<(Brush, int, int)>
    {
        public static readonly ReferenceEqualityComparer2 Instance = new();
        public bool Equals((Brush, int, int) a, (Brush, int, int) b) => ReferenceEquals(a.Item1, b.Item1) && a.Item2 == b.Item2 && a.Item3 == b.Item3;
        public int GetHashCode((Brush, int, int) k) => HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(k.Item1), k.Item2, k.Item3);
    }
}
