using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskKeeper;

/// <summary>
/// Pixeltiere : Spritesheets aus werkzeuge/tiere/pixeltiere.py. Jedes Bild wird als eingefrorene Zeichnung mit
/// Nächster-Nachbar-Skalierung vorgehalten, damit die Kunstpixel beim Vergrößern scharfe Kanten behalten.
/// </summary>
static partial class PetSprites
{
    public sealed record Anim(string Name, int Row, int Count, double Fps, (double X, double Y)[] Mouth, (double X, double Y)[] Paw);

    public sealed class Sheet
    {
        public string File { get; }
        public int W { get; }
        public int H { get; }
        /// <summary>Bodenlinie (Kunstpixel von oben).</summary>
        public int G { get; }
        /// <summary>Drehpunkt in x (Kunstpixel von links), Blick nach rechts.</summary>
        public int Px { get; }
        public Anim[] Anims { get; }
        readonly Dictionary<string, int> index;
        readonly Dictionary<(int, int, bool), Drawing?> frames = [];

        public Sheet(string file, int w, int h, int g, int px, Anim[] anims)
        {
            File = file; W = w; H = h; G = g; Px = px; Anims = anims;
            index = anims.Select((a, i) => (a.Name, i)).ToDictionary(x => x.Name, x => x.i);
        }

        public int Of(string name) => index[name];

        /// <summary>Ein Bild als Zeichnung in Kunstpixeln (0,0)–(W,H); left: gespiegelt (Blick nach links).</summary>
        public Drawing? Frame(int anim, int frame, bool left)
        {
            lock (frames)
            {
                if (frames.TryGetValue((anim, frame, left), out var d)) return d;
                d = null;
                if (Load(File) is { } bmp)
                {
                    var a = Anims[anim];
                    var crop = new CroppedBitmap(bmp, new Int32Rect(frame * W, a.Row * H, W, H));
                    crop.Freeze();
                    d = Pixel(crop, new Rect(0, 0, W, H), left ? new MatrixTransform(-1, 0, 0, 1, W, 0) : null);
                }
                frames[(anim, frame, left)] = d;
                return d;
            }
        }
    }

    /// <summary>Vorschaubild: ein Bild groß (ganzzahlige Vergrößerung auf etwa width Pixel), Mitte c, mit Bodenschatten.</summary>
    public static void Hero(DrawingContext dc, Sheet s, string anim, int frame, Point c, double width)
    {
        if (s.Frame(s.Of(anim), frame, false) is not { } d) return;
        double k = Math.Max(1, Math.Round(width / s.W));
        double top = Math.Round(c.Y - s.H / 2.0 * k), left = Math.Round(c.X - s.W / 2.0 * k);
        dc.DrawEllipse(Fly.Shadow, null, new Point(c.X, top + (s.G + 0.5) * k), s.W * 0.34 * k, 2.2 * k);
        dc.PushTransform(new MatrixTransform(k, 0, 0, k, left, top));
        dc.DrawDrawing(d);
        dc.Pop();
    }

    static readonly Dictionary<string, BitmapSource?> bitmaps = [];

    static BitmapSource? Load(string file)
    {
        lock (bitmaps)
        {
            if (bitmaps.TryGetValue(file, out var b)) return b;
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                var assembly = typeof(PetSprites).Assembly.GetName().Name;
                image.UriSource = new Uri($"pack://application:,,,/{assembly};component/{(assembly == "DeskKeeper" ? "" : "DeskKeeper/")}Assets/{file}", UriKind.Absolute);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
                b = image;
            }
            catch (Exception e) when (e is System.IO.IOException or NotSupportedException or UriFormatException) { b = null; }
            bitmaps[file] = b;
            return b;
        }
    }

    static Drawing Pixel(BitmapSource image, Rect rect, Transform? transform)
    {
        var g = new DrawingGroup();
        RenderOptions.SetBitmapScalingMode(g, BitmapScalingMode.NearestNeighbor);
        RenderOptions.SetEdgeMode(g, EdgeMode.Aliased);
        g.Children.Add(new ImageDrawing(image, rect));
        if (transform is not null) g.Transform = transform;
        g.Freeze();
        return g;
    }

    static Drawing? bed, bedFront;
    static readonly Drawing?[] mounds = new Drawing?[MoundStages];

    /// <summary>Hundekorb des Mopses (Kunstpixel BedW × BedH); front: nur der vordere Rand (über den Symbolen im Korb).</summary>
    public static Drawing? Bed(bool front)
    {
        if (Load("tiere-extras.png") is not { } bmp) return null;
        if (front)
        {
            if (bedFront is null)
            {
                int top = BedH * 9 / 16;
                var crop = new CroppedBitmap(bmp, new Int32Rect(0, top, BedW, BedH - top)); crop.Freeze();
                bedFront = Pixel(crop, new Rect(0, top, BedW, BedH - top), null);
            }
            return bedFront;
        }
        if (bed is null) { var crop = new CroppedBitmap(bmp, new Int32Rect(0, 0, BedW, BedH)); crop.Freeze(); bed = Pixel(crop, new Rect(0, 0, BedW, BedH), null); }
        return bed;
    }

    /// <summary>Erdhügel in Stufe 0 (flach) bis MoundStages − 1 (voll), Kunstpixel MoundW × MoundH.</summary>
    public static Drawing? Mound(int stage)
    {
        stage = Math.Clamp(stage, 0, MoundStages - 1);
        if (mounds[stage] is { } m) return m;
        if (Load("tiere-extras.png") is not { } bmp) return null;
        var crop = new CroppedBitmap(bmp, new Int32Rect(stage * MoundW, BedH, MoundW, MoundH)); crop.Freeze();
        return mounds[stage] = Pixel(crop, new Rect(0, 0, MoundW, MoundH), null);
    }

    // ---------------------------------------------------------------- Pixelschrift 3×5 für Sprechblasen

    static readonly Dictionary<char, string[]> glyphs = new()
    {
        ['A'] = [".#.", "#.#", "###", "#.#", "#.#"], ['C'] = [".##", "#..", "#..", "#..", ".##"], ['E'] = ["###", "#..", "##.", "#..", "###"],
        ['F'] = ["###", "#..", "##.", "#..", "#.."], ['H'] = ["#.#", "#.#", "###", "#.#", "#.#"], ['I'] = ["###", ".#.", ".#.", ".#.", "###"],
        ['K'] = ["#.#", "#.#", "##.", "#.#", "#.#"], ['M'] = ["#.#", "###", "###", "#.#", "#.#"], ['O'] = [".#.", "#.#", "#.#", "#.#", ".#."],
        ['P'] = ["##.", "#.#", "##.", "#..", "#.."], ['R'] = ["##.", "#.#", "##.", "#.#", "#.#"], ['S'] = [".##", "#..", ".#.", "..#", "##."],
        ['T'] = ["###", ".#.", ".#.", ".#.", ".#."], ['U'] = ["#.#", "#.#", "#.#", "#.#", "###"], ['W'] = ["#.#", "#.#", "###", "###", "#.#"],
        ['Z'] = ["###", "..#", ".#.", "#..", "###"], ['!'] = [".#.", ".#.", ".#.", "...", ".#."], ['?'] = ["##.", "..#", ".#.", "...", ".#."],
        ['Г'] = ["###", "#..", "#..", "#..", "#.."], ['А'] = [".#.", "#.#", "###", "#.#", "#.#"], ['В'] = ["##.", "#.#", "##.", "#.#", "##."],
        ['П'] = ["###", "#.#", "#.#", "#.#", "#.#"], ['Ч'] = ["#.#", "#.#", ".##", "..#", "..#"], ['Х'] = ["#.#", "#.#", ".#.", "#.#", "#.#"],
        ['И'] = ["#.#", "#.#", "#.#", "##.", "#.#"], ['Ш'] = ["#.#", "#.#", "#.#", "###", "###"], [' '] = ["...", "...", "...", "...", "..."],
    };

    /// <summary>Text in Pixelschrift, linke obere Ecke at, Kunstpixel k Bildschirmpixel groß.</summary>
    public static void Text(DrawingContext dc, string text, Point at, double k, Brush brush)
    {
        int i = 0;
        foreach (char raw in text.ToUpperInvariant())
        {
            if (glyphs.TryGetValue(raw, out var g))
                for (int y = 0; y < 5; y++)
                    for (int x = 0; x < 3; x++)
                        if (g[y][x] == '#') dc.DrawRectangle(brush, null, new Rect(at.X + (i * 4 + x) * k, at.Y + y * k, k, k));
            i++;
        }
    }

    public static int TextWidth(string text) => text.Length * 4 - 1;
}
