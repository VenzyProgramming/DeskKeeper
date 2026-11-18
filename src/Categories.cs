using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace DeskKeeper;

/// <summary>
/// Desktop nach Kategorien ordnen : links oben System (Papierkorb, Dieser PC, Werkzeuge), oben Mitte Social Media und Browser,
/// rechts oben Office, links daneben Video und Bild, links unten Entwicklung, unten Mitte Spiele und Launcher, rechts unten der Rest.
/// Zuordnung über den Anzeigenamen (ganze Wörter), Platzierung am Symbolraster des Hauptbildschirms.
/// </summary>
static class Categories
{
    internal const string System = "system", Social = "social", Office = "office", Media = "media", Dev = "dev", Games = "games", Other = "other";

    static readonly (string Category, string[] Words)[] rules =
    [
        (Games, ["steam", "epic games", "gog galaxy", "battle net", "ubisoft connect", "ea app", "origin", "riot client", "valorant", "league of legends", "minecraft", "lunar client",
            "roblox", "stardew valley", "xbox", "playnite", "heroic", "gta", "cyberpunk", "fortnite", "counter strike", "dota", "the sims", "factorio", "terraria", "spiel", "game"]),
        (Office, ["word", "excel", "powerpoint", "outlook", "onenote", "access", "publisher", "visio", "project", "acrobat", "adobe acrobat", "pdf", "libreoffice", "onlyoffice", "writer", "calc", "obsidian", "notion"]),
        (Media, ["obs", "obs studio", "vlc", "vlc media player", "potplayer", "kmplayer", "mpc", "premiere", "davinci", "resolve", "after effects", "vegas", "capcut", "shotcut", "kdenlive",
            "handbrake", "audacity", "spotify", "photoshop", "lightroom", "gimp", "irfanview", "upscayl", "paint net", "krita", "blender", "illustrator", "affinity", "xnview", "foobar2000", "media player", "clipchamp"]),
        (Social, ["telegram", "discord", "whatsapp", "signal", "skype", "teams", "zoom", "slack", "viber", "messenger", "instagram", "tiktok", "twitter", "facebook", "threema",
            "vivaldi", "chrome", "google chrome", "firefox", "edge", "microsoft edge", "opera", "brave", "tor browser"]),
        (Dev, ["visual studio", "code", "vs code", "rider", "intellij", "pycharm", "webstorm", "clion", "android studio", "git", "github", "github desktop", "sourcetree", "docker",
            "postman", "lm studio", "ollama", "db browser", "sqlite", "dbeaver", "heidisql", "putty", "winscp", "filezilla", "terminal", "powershell", "python", "node", "unity", "unreal",
            "godot", "creo", "creo parametric", "solidworks", "autocad", "fusion", "inventor", "catia", "matlab", "arduino", "notepad", "sublime", "cursor", "windsurf", "claude", "codex"]),
        (System, ["papierkorb", "recycle bin", "dieser pc", "this pc", "computer", "systemsteuerung", "control panel", "einstellungen", "settings", "netzwerk", "network",
            "total commander", "explorer", "glary utilities", "ccleaner", "rustdesk", "anydesk", "teamviewer", "remotedesktop", "rdp", "kaspersky", "vpn", "defender",
            "wallpaper engine", "everything", "7 zip", "winrar", "powertoys", "task manager", "treiber", "driver", "bitwarden", "keepass"]),
    ];

    /// <summary>Systemordner des Desktops (Papierkorb, Dieser PC, Systemsteuerung, Netzwerk, Benutzerordner) gehören immer nach links oben.</summary>
    static readonly string[] shellFolders = ["::{645FF040", "::{20D04FE0", "::{5399E694", "::{26EE0668", "::{21EC2020", "::{F02C1A0D", "::{59031A47", "::{018D5C66"];

    static string Normalize(string text)
    {
        var sb = new StringBuilder(" ");
        foreach (char ch in text.ToLowerInvariant()) sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        sb.Append(' ');
        string s = sb.ToString();
        while (s.Contains("  ")) s = s.Replace("  ", " ");
        return s;
    }

    /// <summary>Kategorie eines Symbols aus Kennung (Pfad oder Shell-Ordner) und Anzeigename.</summary>
    internal static string Of(string id, string name)
    {
        if (shellFolders.Any(f => id.StartsWith(f, StringComparison.OrdinalIgnoreCase))) return System;
        string file = System_IO_Name(id);
        string text = Normalize(name + " " + file);
        if (id.EndsWith(".rdp", StringComparison.OrdinalIgnoreCase)) return System;
        foreach (var (category, words) in rules)
            if (words.Any(w => text.Contains(" " + w + " ", StringComparison.Ordinal))) return category;
        return Other;
    }

    static string System_IO_Name(string id)
    {
        try { return id.StartsWith("::", StringComparison.Ordinal) ? "" : global::System.IO.Path.GetFileNameWithoutExtension(id); } catch { return ""; }
    }

    /// <summary>Raster des Hauptbildschirms in Explorer-Elementkoordinaten; <see cref="Cell"/> liefert die Symbolmitte in Bildschirmpixeln.</summary>
    internal sealed record Grid(int X0, int Y0, int StepX, int StepY, int Columns, int Rows, Func<int[], Point> CenterOf)
    {
        public int[] Item(int column, int row) => [X0 + column * StepX, Y0 + row * StepY];
        public Point Cell(int column, int row) => CenterOf(Item(column, row));

        /// <summary>Raster mit der Phase der vorhandenen Elementpositionen (so liegt es genau auf dem Explorer-Raster);
        /// nur Zellen, deren Symbol ganz in der Arbeitsfläche liegt.</summary>
        public static Grid From(Rect work, int stepX, int stepY, IEnumerable<int[]> existing, Func<int[], Point> centerOf)
        {
            static int Phase(IEnumerable<int> values, int step)
            {
                var m = values.Select(v => ((v % step) + step) % step).OrderBy(v => v).ToList();
                return m.Count == 0 ? 0 : m[m.Count / 2];
            }
            var items = existing.ToList();
            int px = Phase(items.Select(i => i[0]), stepX), py = Phase(items.Select(i => i[1]), stepY);
            bool FitsX(int x) { double c = centerOf([x, py]).X; return c - stepX * 0.45 >= work.Left && c + stepX * 0.45 <= work.Right; }
            bool FitsY(int y) { double c = centerOf([px, y]).Y; return c - stepY * 0.25 >= work.Top && c + stepY * 0.7 <= work.Bottom; }
            var xs = Enumerable.Range(-80, 400).Select(k => px + k * stepX).Where(FitsX).ToList();
            var ys = Enumerable.Range(-80, 400).Select(k => py + k * stepY).Where(FitsY).ToList();
            if (xs.Count == 0) xs.Add(px);
            if (ys.Count == 0) ys.Add(py);
            return new Grid(xs[0], ys[0], stepX, stepY, xs.Count, ys.Count, centerOf);
        }
    }

    /// <summary>Reihenfolge in der Gruppe: Systemordner zuerst (Papierkorb, Dieser PC), sonst alphabetisch.</summary>
    static int Rank(IconInfo icon)
    {
        if (icon.Id.StartsWith("::{645FF040", StringComparison.OrdinalIgnoreCase)) return 0;
        if (icon.Id.StartsWith("::{20D04FE0", StringComparison.OrdinalIgnoreCase) || Normalize(icon.Name).Contains(" dieser pc ") || Normalize(icon.Name).Contains(" this pc ")) return 1;
        return icon.Id.StartsWith("::", StringComparison.Ordinal) ? 2 : 3;
    }

    /// <summary>Zielmitten je Symbol. Ecken füllen spaltenweise, die Mitten zeilenweise; belegte Zellen werden übersprungen.</summary>
    internal static Dictionary<string, Point> Plan(IReadOnlyList<IconInfo> icons, Grid grid)
    {
        int cols = grid.Columns, rows = grid.Rows;
        int half = Math.Max(1, (rows - 1) / 2);   // eine freie Zeile zwischen oben und unten, wenn möglich
        var taken = new HashSet<(int, int)>();
        var result = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
        var groups = icons.GroupBy(i => Of(i.Id, i.Name)).ToDictionary(g => g.Key, g => g.OrderBy(Rank).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList());
        List<IconInfo> Take(string c) => groups.TryGetValue(c, out var l) ? l : [];

        // Ecke: spaltenweise (oben → unten bzw. unten → oben), von der Ecke weg (links → rechts bzw. rechts → links)
        int Corner(List<IconInfo> list, int startCol, int dirCol, bool fromTop, int maxRows)
        {
            int used = 0, col = startCol, row = 0, placed = 0, guard = 0;
            while (placed < list.Count && guard++ < cols * rows * 2)
            {
                var cell = (col, fromTop ? row : rows - 1 - row);
                if (col >= 0 && col < cols && !taken.Contains(cell))
                { taken.Add(cell); result[list[placed].Id] = grid.Cell(cell.Item1, cell.Item2); placed++; used = Math.Max(used, Math.Abs(col - startCol) + 1); }
                if (++row >= maxRows) { row = 0; col += dirCol; if (col < 0 || col >= cols) break; }
            }
            return used;
        }
        // Mitte: zeilenweise, mittig um die Bildschirmmitte, höchstens 8 nebeneinander
        void Center(List<IconInfo> list, bool fromTop)
        {
            int width = Math.Min(8, Math.Max(1, list.Count)), start = Math.Max(0, cols / 2 - width / 2), placed = 0;
            for (int r = 0; r < half && placed < list.Count; r++)
                for (int k = 0; k < width && placed < list.Count; k++)
                {
                    var cell = (Math.Min(cols - 1, start + k), fromTop ? r : rows - 1 - r);
                    if (!taken.Add(cell)) continue;
                    result[list[placed].Id] = grid.Cell(cell.Item1, cell.Item2); placed++;
                }
        }

        Corner(Take(System), 0, +1, true, half);
        int officeCols = Corner(Take(Office), cols - 1, -1, true, half);
        Corner(Take(Media), cols - 1 - officeCols - (officeCols > 0 ? 1 : 0), -1, true, half);
        Corner(Take(Dev), 0, +1, false, half);
        Corner(Take(Other), cols - 1, -1, false, half);
        Center(Take(Social), true);
        Center(Take(Games), false);
        // Falls eine Gruppe nicht passte: restliche Symbole in freie Zellen
        foreach (var icon in icons.Where(i => !result.ContainsKey(i.Id)))
        {
            for (int c = 0; c < cols && !result.ContainsKey(icon.Id); c++)
                for (int r = 0; r < rows; r++)
                    if (taken.Add((c, r))) { result[icon.Id] = grid.Cell(c, r); break; }
        }
        return result;
    }
}

/// <summary>Ordnen am echten Desktop: vorher Schnappschuss (zurückholbar), dann sofort oder mit DUM-E laut Einstellung.</summary>
static class Sorter
{
    /// <summary>Zielanordnung für die Symbole des Hauptbildschirms; Symbole anderer Bildschirme bleiben, wo sie sind.</summary>
    internal static (Dictionary<string, int[]> Layout, Dictionary<string, string> Kinds, int Moved) Prepare(ShellDesktop desktop)
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen ?? System.Windows.Forms.Screen.AllScreens[0];
        var work = new Rect(screen.WorkingArea.X, screen.WorkingArea.Y, screen.WorkingArea.Width, screen.WorkingArea.Height);
        var bounds = new Rect(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height);
        var mine = desktop.Icons.Where(i => bounds.Contains(i.Center)).ToList();
        var layout = desktop.Snapshot();
        var grid = Categories.Grid.From(work, (int)Math.Round(desktop.SpacingX), (int)Math.Round(desktop.SpacingY), mine.Select(i => layout[i.Id]), desktop.CenterOf);
        var plan = Categories.Plan(mine, grid);
        int moved = 0;
        foreach (var (id, center) in plan)
        {
            var item = desktop.ItemOf(center);
            if (layout.TryGetValue(id, out var old) && (old[0] != item[0] || old[1] != item[1])) moved++;
            layout[id] = item;
        }
        var kinds = mine.ToDictionary(i => i.Id, i => Categories.Of(i.Id, i.Name), StringComparer.OrdinalIgnoreCase);
        return (layout, kinds, moved);
    }

    /// <summary>Ordnen ausführen: Schnappschuss, dann sofort oder mit DUM-E; danach gilt die neue Anordnung. Liefert die Meldung.</summary>
    internal static async System.Threading.Tasks.Task<string> Run(Func<Dictionary<string, int[]>, System.Threading.Tasks.Task<string>> withRobot)
    {
        string key = Store.ScreenKey();
        Dictionary<string, int[]> layout; int moved;
        using (var desktop = new ShellDesktop())
        {
            if (desktop.AutoArrange) return Loc.T("r.autoArrange");
            Store.SaveSnapshot(key, desktop.Snapshot());   // zurückholbar über die Schnappschüsse
            (layout, _, moved) = Prepare(desktop);
        }
        if (moved == 0) return Loc.T("r.inPlace");
        if (Store.LoadOptions().RestoreMode == "robot") await withRobot(layout);
        else { using var desktop = new ShellDesktop(); desktop.RestoreSnapshot(layout); }
        Store.SaveProfile(key, layout);
        new RealWorld().Save(key, layout);
        return string.Format(Loc.T("m.sorted"), moved);
    }
}
