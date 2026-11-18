using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskKeeper;

/// <summary>Vorschaubilder für die Karte: echte Szenenbilder auf einem Testschreibtisch und Дубина je Skin.</summary>
static class Preview
{
    static readonly string[] Names = ["Проекты", "Заметки", "Корзина", "Картинки", "Документы", "Музыка", "Игры", "Загрузки"];
    static readonly Rect Desk = new(0, 0, 1600, 900);

    static List<IconInfo> Icons()
    {
        var icons = new List<IconInfo>();
        for (int i = 0; i < 21; i++) icons.Add(new IconInfo("p" + i, Names[i % Names.Length], new Point(60 + (i / 7) * 96, 70 + (i % 7) * 112)));
        for (int i = 0; i < 6; i++) icons.Add(new IconInfo("q" + i, Names[(i + 2) % Names.Length], new Point(1500 - (i / 3) * 96, 70 + (i % 3) * 112)));
        return icons;
    }

    /// <summary>scene: jericho | nuke | tu160; Bild 16:9 in der gegebenen Breite (physische Pixel).</summary>
    public static BitmapSource SceneImage(string scene, int width)
    {
        var icons = Icons();
        var homes = icons.ToDictionary(i => i.Id, i => i.Center);
        var s = new Scene(icons, homes, 48, [Desk], Desk, true, 64, nuclear: scene == "nuke", tu160: scene == "tu160");
        double t = scene switch { "nuke" => DeskKeeper.Scene.Impact + 1.3, "tu160" => 1.8, _ => DeskKeeper.Scene.Impact + 0.35 };
        double scale = width / Desk.Width;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(scale, scale));
            dc.DrawRectangle(Wall, null, Desk);
            var centers = s.IconCenters(t);
            foreach (var icon in icons)
            {
                if (centers[icon.Id] is not { } c) continue;
                dc.DrawRoundedRectangle(Folder, null, new Rect(c.X - 24, c.Y - 24, 48, 40), 6, 6);
                dc.DrawRoundedRectangle(Label, null, new Rect(c.X - 26, c.Y + 22, 52, 8), 4, 4);
            }
            s.Draw(dc, t);
        }
        var bitmap = new RenderTargetBitmap(width, (int)Math.Round(Desk.Height * scale), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Дубина mit Anhänger von oben im gegebenen Skin.</summary>
    public static BitmapSource SkinImage(Art.Skin skin, int width, int height)
    {
        var keep = Art.Current;
        Art.Current = skin;
        try
        {
            var one = new Scene([new IconInfo("a", "", new Point(100, 100))], new Dictionary<string, Point> { ["a"] = new Point(100, 100) }, 48,
                [new Rect(0, 0, width, height)], new Rect(0, 0, width, height), true, height * 0.34);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Wall, null, new Rect(0, 0, width, height));
                one.Dubina.DrawPortrait(dc, new Point(width * 0.52, height * 0.5), -0.4, 0.35, 1.0, 0.5, 0.2);
            }
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
        finally { Art.Current = keep; }
    }

    static readonly Brush Wall = Frozen(new LinearGradientBrush(Color.FromRgb(0x24, 0x3B, 0x55), Color.FromRgb(0x5A, 0x3A, 0x4E), 35));
    static readonly Brush Folder = Frozen(new SolidColorBrush(Color.FromRgb(0xE7, 0xB4, 0x7B)));
    static readonly Brush Label = Frozen(new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)));
    static Brush Frozen(Brush b) { b.Freeze(); return b; }
}
