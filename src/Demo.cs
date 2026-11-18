using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace DeskKeeper;

/// <summary>
/// Vorführmodus für Screenshots und Werbevideo (DESKKEEPER_DEMO=1): erfundene Geräte (Breitbild als Hauptbildschirm,
/// Notebook darunter) und neutrale, selbst gezeichnete Symbole. Nichts vom echten Desktop erscheint, nichts Echtes wird gelesen.
/// </summary>
static class Demo
{
    public static bool On => Environment.GetEnvironmentVariable("DESKKEEPER_DEMO") == "1";

    public static readonly List<MonitorInfo> Both =
    [
        new("demo-wide", new Rect(0, 0, 3440, 1440), true) { Number = 1, Maker = "LG", Model = "UltraWide 34", Kind = DeviceKind.Monitor, Diagonal = 34 },
        new("demo-laptop", new Rect(760, 1440, 1920, 1200), false) { Number = 2, Maker = "Встроенный", Model = "", Kind = DeviceKind.Laptop, Diagonal = 14 },
    ];
    public static readonly List<MonitorInfo> LaptopOnly =
    [
        new("demo-laptop", new Rect(0, 0, 1920, 1200), true) { Number = 1, Maker = "Встроенный", Model = "", Kind = DeviceKind.Laptop, Diagonal = 14 },
    ];
    public static string KeyOf(IEnumerable<MonitorInfo> screens) => string.Join(";", screens.Select(m => $"{m.Bounds.X:0},{m.Bounds.Y:0},{m.Bounds.Width:0},{m.Bounds.Height:0},{(m.Primary ? 1 : 0)},96").OrderBy(x => x, StringComparer.Ordinal));
    public static string CurrentKey => KeyOf(Both);

    /// <summary>Neutrale Symbole: Name, Glyphe (Segoe Fluent), Farbe.</summary>
    public static readonly (string Name, string Glyph, string Color)[] IconSet =
    [
        ("Projects", "", "#E8A33D"), ("Documents", "", "#3B82F6"), ("Pictures", "", "#22A06B"), ("Music", "", "#E5487D"),
        ("Videos", "", "#E5534B"), ("Games", "", "#8B5CF6"), ("Downloads", "", "#14B8A6"), ("Notes", "", "#EAB308"),
        ("Browser", "", "#2563EB"), ("Mail", "", "#0EA5E9"), ("Code", "", "#6366F1"), ("Calculator", "", "#64748B"),
        ("Photos", "", "#F97316"), ("Chat", "", "#10B981"), ("Terminal", "", "#334155"), ("Recycle Bin", "", "#94A3B8"),
        ("Design", "", "#DB2777"), ("Backup", "", "#0891B2"),
    ];

    /// <summary>Geordnete Anordnung: zwei Spalten links auf dem Breitbild, eine Gruppe rechts, vier Symbole auf dem Notebook.</summary>
    public static Dictionary<string, Point> Home()
    {
        var home = new Dictionary<string, Point>();
        for (int i = 0; i < 10; i++) home[IconSet[i].Name] = new Point(70 + (i / 5) * 118, 80 + (i % 5) * 132);
        for (int i = 10; i < 14; i++) home[IconSet[i].Name] = new Point(3370 - ((i - 10) / 2) * 118, 80 + ((i - 10) % 2) * 132);
        for (int i = 14; i < 18; i++) home[IconSet[i].Name] = new Point(830 + (i - 14) * 118, 1520);
        return home;
    }

    static readonly Dictionary<string, ImageSource> images = [];
    public static ImageSource Image(string name)
    {
        if (images.TryGetValue(name, out var hit)) return hit;
        var (_, glyph, color) = IconSet.FirstOrDefault(x => x.Name == name);
        var c = (Color)ColorConverter.ConvertFromString(color ?? "#64748B");
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            var fill = new LinearGradientBrush(Lighten(c, 0.18), c, 90);
            dc.DrawRoundedRectangle(fill, null, new Rect(4, 4, 56, 56), 13, 13);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0x30, 255, 255, 255)), null, new Rect(4, 4, 56, 24), 13, 13);
            var text = new FormattedText(glyph ?? "", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 28, Brushes.White, 1);
            dc.DrawText(text, new Point(32 - text.Width / 2, 32 - text.Height / 2));
        }
        group.Freeze();
        var image = new DrawingImage(group); image.Freeze();
        images[name] = image;
        return image;
    }

    static Color Lighten(Color c, double t) => Color.FromRgb((byte)(c.R + (255 - c.R) * t), (byte)(c.G + (255 - c.G) * t), (byte)(c.B + (255 - c.B) * t));

    /// <summary>Elementposition (wie Explorer) aus einer Symbolmitte; passt zu ViewData (Abstand 118, Symbol 68).</summary>
    static int[] Item(Point center, Rect all) => [(int)Math.Round(center.X - all.X - 59), (int)Math.Round(center.Y - all.Y - 36)];

    /// <summary>Vorführdaten in einen (leeren) Datenordner schreiben: zwei Anordnungen, Schnappschüsse.</summary>
    public static void Seed()
    {
        var home = Home();
        var allBoth = Both.Select(m => m.Bounds).Aggregate(Rect.Union);
        var layout = home.ToDictionary(e => e.Key, e => Item(e.Value, allBoth));
        Store.SaveAuto(CurrentKey, layout);
        Store.SaveScreens(CurrentKey, Both);
        // Notebook allein: alles in Spalten links (so wie Windows es hinlegt)
        var laptopLayout = new Dictionary<string, int[]>();
        int k = 0;
        foreach (var name in home.Keys) { laptopLayout[name] = Item(new Point(70 + (k / 8) * 118, 80 + (k % 8) * 132), new Rect(0, 0, 1920, 1200)); k++; }
        string laptopKey = KeyOf(LaptopOnly);
        Store.SaveAuto(laptopKey, laptopLayout);
        Store.SaveScreens(laptopKey, LaptopOnly);
        // Schnappschüsse mit festen Zeiten
        string dir = Path.Combine(Store.Folder, "profile");
        string prefix = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(CurrentKey)))[..16] + "-snap-";
        var now = DateTime.Now;
        foreach (var (minutes, auto) in new[] { (12, false), (95, true), (60 * 26, false) })
            File.WriteAllText(Path.Combine(dir, prefix + now.AddMinutes(-minutes).ToString("yyyyMMdd-HHmmss") + (auto ? "-auto" : "") + ".json"), JsonSerializer.Serialize(layout));
    }
}
