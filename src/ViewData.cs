using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DeskKeeper;

/// <summary>Daten für die Bildschirmansicht beider Ausgaben: gemerkte Symbole einer Anordnung mit Bild und Namen.</summary>
static class ViewData
{
    static double spacing = 118, iconSize = 68;
    public static double IconSize => iconSize;

    public static List<DeskIcon> Icons(string key, string currentKey, IReadOnlyList<MonitorInfo> screens)
    {
        var result = new List<DeskIcon>();
        var raw = Store.LoadAuto(key);
        if (raw is null || screens.Count == 0) return result;
        Dictionary<string, string> names = new(StringComparer.OrdinalIgnoreCase);
        ShellDesktop? desk = null;
        if (DeskKeeper.Demo.On)
        {
            var area = screens.Select(m => m.Bounds).Aggregate(Rect.Union);
            foreach (var (id, v) in raw) result.Add(new DeskIcon(new Point(area.X + v[0] + 59, area.Y + v[1] + 36), id, DeskKeeper.Demo.Image(id)));
            return result;
        }
        try { desk = new ShellDesktop(); spacing = desk.Spacing; iconSize = desk.IconSize; foreach (var i in desk.Icons) names[i.Id] = i.Name; }
        catch { desk = null; }
        try
        {
            var all = screens.Select(m => m.Bounds).Aggregate(Rect.Union);
            foreach (var (id, v) in raw)
            {
                // aktuelle Anordnung: genau über Explorer; andere: Elementposition + Ursprung der damaligen Gesamtfläche
                var center = key == currentKey && desk is not null ? desk.CenterOf(v) : new Point(all.X + v[0] + spacing / 2, all.Y + v[1] + 2 + iconSize / 2);
                string name = names.TryGetValue(id, out var n) ? n : ShellIcons.NameOf(id);
                result.Add(new DeskIcon(center, name, ShellIcons.Get(id)));
            }
        }
        finally { desk?.Dispose(); }
        return result;
    }

    /// <summary>Beispielanordnung für die Prüfbilder: Breitbild (Haupt), Monitor links, Notebook darunter, Tablet und Handy rechts.</summary>
    public static (List<MonitorInfo> Screens, List<DeskIcon> Icons) Demo()
    {
        var screens = new List<MonitorInfo>
        {
            new("a", new Rect(0, 0, 3440, 1440), true) { Number = 1, Maker = "LG", Model = "LG ULTRAWIDE", Kind = DeviceKind.Monitor, Diagonal = 34.2 },
            new("b", new Rect(-2560, 0, 2560, 1440), false) { Number = 2, Maker = "Dell", Model = "DELL U2723QE", Kind = DeviceKind.Monitor, Diagonal = 27 },
            new("c", new Rect(760, 1440, 1920, 1200), false) { Number = 3, Maker = "Встроенный", Model = "", Kind = DeviceKind.Laptop, Diagonal = 14 },
            new("d", new Rect(3440, 120, 1640, 1180), false) { Number = 4, Maker = "", Model = "spacedesk", Kind = DeviceKind.Tablet, Diagonal = 11 },
            new("e", new Rect(3440, 1440, 720, 1560), false) { Number = 5, Maker = "", Model = "Miracast", Kind = DeviceKind.Phone, Diagonal = 6.4 },
        };
        string[] names = ["Проекты", "Заметки", "Корзина", "Картинки", "Документы", "Музыка", "Игры", "Загрузки"];
        var icons = new List<DeskIcon>();
        ImageSource? Img(int i) => DeskKeeper.Demo.On ? DeskKeeper.Demo.Image(DeskKeeper.Demo.IconSet[i % DeskKeeper.Demo.IconSet.Length].Name) : null;
        for (int i = 0; i < 14; i++) icons.Add(new DeskIcon(new Point(60 + (i / 7) * 118, 60 + (i % 7) * 130), names[i % 8], Img(i)));
        for (int i = 0; i < 6; i++) icons.Add(new DeskIcon(new Point(-2500 + i * 118, 60), names[(i + 3) % 8], Img(i + 3)));
        for (int i = 0; i < 4; i++) icons.Add(new DeskIcon(new Point(820, 1500 + i * 130), names[(i + 5) % 8], Img(i + 9)));
        for (int i = 0; i < 3; i++) icons.Add(new DeskIcon(new Point(3520, 200 + i * 130), names[i], Img(i + 13)));
        return (screens, icons);
    }
}
