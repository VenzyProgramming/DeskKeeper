using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DeskKeeper;

/// <summary>
/// Speicher unter %LOCALAPPDATA%\DeskKeeper: Profile je Bildschirmanordnung und die Sicherung während einer Szene.
/// Die Sicherung wird vor der ersten Bewegung geschrieben und erst nach der Rückgabe gelöscht;
/// findet der Start eine Sicherung (Absturz, Abmelden), stellt er die Symbole zuerst zurück.
/// </summary>
static class Store
{
    internal static string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskKeeper");
    static string OldFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dubina");

    /// <summary>Umbenennung (18.11.2026): Profile und Einstellungen aus %LOCALAPPDATA%\Dubina einmalig kopieren; der alte Ordner bleibt unverändert.</summary>
    internal static void Migrate()
    {
        try
        {
            if (Directory.Exists(Folder) || !Directory.Exists(OldFolder)) return;
            foreach (var file in Directory.EnumerateFiles(OldFolder, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == "laufende-szene.json") continue;   // gehört der noch laufenden alten Version
                var target = Path.Combine(Folder, Path.GetRelativePath(OldFolder, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, false);
            }
        }
        catch { }
    }
    static string Pending => Path.Combine(Folder, "laufende-szene.json");
    static string ProfileFile(string key) => Path.Combine(Folder, "profile", Hash(key) + ".json");

    /// <summary>
    /// Schlüssel der aktuellen Bildschirmanordnung (Reihenfolge egal): je Bildschirm die Hardware-Kennung (Gerätepfad des Monitors laut
    /// Windows-Anzeigekonfiguration) und Lage, Größe, Hauptbildschirm, Skalierung in echten Pixeln – „Kennung@x,y,b,h,haupt,dpi“.
    /// 19.11.2026: die Pixel eines Bildschirms an seiner Hardware merken, damit Anordnungen nie verwechselt werden.
    /// </summary>
    internal static string ScreenKey() => Demo.On ? Demo.CurrentKey : Compose(Physical.Displays()
        .Select(s => new KeyPart(s.Hardware, (int)s.Bounds.X, (int)s.Bounds.Y, (int)s.Bounds.Width, (int)s.Bounds.Height, s.Primary, (int)s.Dpi)));

    /// <summary>Ein Bildschirm im Schlüssel. Hardware leer: Schlüssel aus der Zeit vor 1.2.3 (nur Lage) oder Gerät ohne Kennung.</summary>
    internal readonly record struct KeyPart(string Hardware, int X, int Y, int W, int H, bool Primary, int Dpi)
    {
        public override string ToString() => (Hardware.Length > 0 ? Hardware + "@" : "") + $"{X},{Y},{W},{H},{(Primary ? 1 : 0)},{Dpi}";
        public System.Windows.Rect Bounds => new(X, Y, W, H);
    }
    internal static string Compose(IEnumerable<KeyPart> parts) => string.Join(";", parts.Select(p => p.ToString()).OrderBy(x => x, StringComparer.Ordinal));
    /// <summary>Schlüssel zerlegen (alte Form „x,y,b,h,haupt[,dpi]“ und neue Form mit Kennung).</summary>
    internal static List<KeyPart> Split(string key)
    {
        var list = new List<KeyPart>();
        foreach (var part in key.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            int at = part.LastIndexOf('@');
            var f = part[(at + 1)..].Split(',');
            if (f.Length < 5 || !int.TryParse(f[0], out int x) || !int.TryParse(f[1], out int y) || !int.TryParse(f[2], out int w) || !int.TryParse(f[3], out int h)) continue;
            list.Add(new KeyPart(at > 0 ? part[..at] : "", x, y, w, h, f[4] == "1", f.Length > 5 && int.TryParse(f[5], out int dpi) ? dpi : 96));
        }
        return list;
    }
    /// <summary>Schlüssel bis 2.6 (ohne Skalierung), damit vorhandene Profile weiter gelten.</summary>
    static string LegacyKey(string key) => string.Join(";", Split(key).Select(p => $"{p.X},{p.Y},{p.W},{p.H},{(p.Primary ? 1 : 0)}"));

    /// <summary>Gemerkte Anordnungen mit Schlüssel ohne Kennung auf die neue Form umstellen, wenn die Gerätedatei die Geräte nennt:
    /// Dateien umbenennen, Verzeichnis anpassen. Schon vorhandene neue Schlüssel werden nicht überschrieben.</summary>
    internal static int MigrateKeys()
    {
        int moved = 0;
        try
        {
            string dir = Path.Combine(Folder, "profile"), index = Path.Combine(dir, "anordnungen.json");
            if (!File.Exists(index)) return 0;
            var keys = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(index)) ?? [];
            foreach (var k in keys.ToList())
            {
                var parts = Split(k);
                if (parts.Count == 0 || parts.Any(p => p.Hardware.Length > 0) || !File.Exists(ScreensFile(k))) continue;
                var metas = JsonSerializer.Deserialize<List<ScreenMeta>>(File.ReadAllText(ScreensFile(k))) ?? [];
                var mapped = parts.Select(p => metas.FirstOrDefault(m => (int)m.X == p.X && (int)m.Y == p.Y && (int)m.W == p.W && (int)m.H == p.H && m.Id.StartsWith(@"\\?\", StringComparison.Ordinal)) is { } m ? p with { Hardware = m.Id } : p).ToList();
                if (mapped.Any(p => p.Hardware.Length == 0)) continue;
                string nk = Compose(mapped);
                if (keys.Contains(nk)) continue;
                foreach (var f in Directory.EnumerateFiles(dir, Hash(k) + "*").ToList())
                {
                    string target = Path.Combine(dir, Hash(nk) + Path.GetFileName(f)[Hash(k).Length..]);
                    if (!File.Exists(target)) File.Move(f, target);
                }
                keys[keys.IndexOf(k)] = nk; moved++;
                if (LastKey == k) LastKey = nk;
                Fault.Note($"Anordnung mit Hardware-Kennung: {Hash(k)} → {Hash(nk)} ({Describe(nk)})");
            }
            if (moved > 0) File.WriteAllText(index, JsonSerializer.Serialize(keys));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { Fault.Report(e); }
        return moved;
    }

    /// <summary>Geräte merken: Kennung → Name, native Auflösung, gesehene Auflösungen und Skalierungen (profile\geraete.json).</summary>
    internal sealed record DeviceRecord(string Name, int NativeWidth, int NativeHeight, List<string> Seen);
    internal static void RememberDevices(IEnumerable<Physical.Display> displays)
    {
        try
        {
            string file = Path.Combine(Folder, "profile", "geraete.json");
            var known = File.Exists(file) ? JsonSerializer.Deserialize<Dictionary<string, DeviceRecord>>(File.ReadAllText(file)) ?? [] : [];
            bool changed = false;
            foreach (var d in displays.Where(d => d.Hardware.Length > 0))
            {
                string seen = $"{(int)d.Bounds.Width}x{(int)d.Bounds.Height}@{d.Dpi}";
                if (!known.TryGetValue(d.Hardware, out var r)) { known[d.Hardware] = r = new DeviceRecord(d.Name, d.NativeWidth, d.NativeHeight, []); changed = true; }
                if (!r.Seen.Contains(seen)) { r.Seen.Add(seen); changed = true; }
            }
            if (!changed) return;
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, JsonSerializer.Serialize(known, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { Fault.Report(e); }
    }

    /// <summary>Lesbar: „2 Bildschirme · 2560×1440 · 1920×1080 (150 %)“ in der eingestellten Sprache.</summary>
    internal static string Describe(string key)
    {
        var parts = Split(key).OrderByDescending(p => p.Primary).ThenBy(p => p.X).ToArray();
        string screens = Loc.Count(parts.Length, "screens");
        return screens + " · " + string.Join(" · ", parts.Select(p => $"{p.W}×{p.H}" + (p.Dpi != 96 ? $" ({p.Dpi * 100 / 96} %)" : "")));
    }


    static string LastKeyFile => Path.Combine(Folder, "letzte-anordnung.txt");
    internal static string? LastKey
    {
        get { try { return File.Exists(LastKeyFile) ? File.ReadAllText(LastKeyFile) : null; } catch { return null; } }
        set { try { Directory.CreateDirectory(Folder); File.WriteAllText(LastKeyFile, value ?? ""); } catch { } }
    }

    /// <summary>Alle gemerkten Bildschirmanordnungen (Schlüssel, Symbolzahl, Zeitpunkt).</summary>
    internal static List<(string Key, int Count, DateTime Saved)> Known()
    {
        var list = new List<(string, int, DateTime)>();
        try
        {
            string index = Path.Combine(Folder, "profile", "anordnungen.json");
            var keys = File.Exists(index) ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(index)) ?? [] : [];
            string now = ScreenKey();
            if (!keys.Contains(now)) keys.Add(now);   // auch Stände aus der Zeit vor dem Verzeichnis (2.6 und älter)
            foreach (var k in keys)
            {
                foreach (string f in new[] { AutoFile(k), ProfileFile(k), ProfileFile(LegacyKey(k)) })
                    if (Read(f) is { } d) { list.Add((k, d.Count, File.GetLastWriteTime(f))); break; }
            }
        }
        catch { }
        return list;
    }
    /// <summary>
    /// Anordnungen, die nur durch DPI-Virtualisierung entstanden sind: gleiche Bildschirme wie die jetzige, aber alle Maße mit dem
    /// Verhältnis zweier Skalierungen gestreckt (4300×1800 bei 120 dpi statt 3440×1440 bei 96 dpi). Ihre Dateien (Stand, Plätze,
    /// Geräte, Schnappschüsse) wandern nach profile\quarantaene-dpi – wiederherstellbar, nichts wird gelöscht.
    /// </summary>
    internal static int QuarantineTwins(string current)
    {
        int moved = 0;
        try
        {
            string dir = Path.Combine(Folder, "profile"), index = Path.Combine(dir, "anordnungen.json");
            if (!File.Exists(index)) return 0;
            var keys = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(index)) ?? [];
            var real = Parts(current);
            foreach (var k in keys.Where(k => k != current && ScaledTwin(Parts(k), real)).ToList())
            {
                string bin = Path.Combine(dir, "quarantaene-dpi");
                Directory.CreateDirectory(bin);
                foreach (var f in Directory.EnumerateFiles(dir, Hash(k) + "*").ToList()) File.Move(f, Path.Combine(bin, Path.GetFileName(f)), overwrite: true);
                File.AppendAllText(Path.Combine(bin, "liesmich.txt"), $"{DateTime.Now:s} {Hash(k)} = {k} (echt: {current})" + Environment.NewLine);
                keys.Remove(k); moved++;
            }
            if (moved > 0) File.WriteAllText(index, JsonSerializer.Serialize(keys));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { Fault.Report(e); }
        return moved;
    }

    static List<KeyPart> Parts(string key) => Split(key).OrderByDescending(p => p.Primary).ThenBy(p => p.X).ToList();

    internal static bool ScaledTwinKey(string twin, string real) => ScaledTwin(Parts(twin), Parts(real));

    /// <summary>Alle Teile mit derselben (System-)Skalierung und je Bildschirm um dieses Verhältnis gestreckt – mindestens einer wirklich anders.</summary>
    internal static bool ScaledTwin(List<KeyPart> twin, List<KeyPart> real)
    {
        if (twin.Count == 0 || twin.Count != real.Count || twin.Any(p => p.Dpi != twin[0].Dpi)) return false;
        bool scaled = false;
        for (int i = 0; i < twin.Count; i++)
        {
            var (a, b) = (twin[i], real[i]);
            if (a.Primary != b.Primary || b.Dpi <= 0 || a.Hardware.Length > 0 && b.Hardware.Length > 0 && a.Hardware != b.Hardware) return false;
            double f = (double)a.Dpi / b.Dpi;
            if (Math.Abs(a.W - b.W * f) > 2 || Math.Abs(a.H - b.H * f) > 2) return false;
            if (a.Dpi != b.Dpi) scaled = true;
        }
        return scaled;
    }

    static void Remember(string key)
    {
        try
        {
            string index = Path.Combine(Folder, "profile", "anordnungen.json");
            var keys = File.Exists(index) ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(index)) ?? [] : [];
            if (key == ScreenKey()) SaveScreens(key, Monitors.Current());
            if (keys.Contains(key)) return;
            keys.Add(key);
            Directory.CreateDirectory(Path.GetDirectoryName(index)!);
            File.WriteAllText(index, JsonSerializer.Serialize(keys));
        }
        catch { }
    }
    internal static bool HasPending => File.Exists(Pending);

    // Schnappschüsse: jedes „Запомнить“ legt einen datierten Stand an; „Вернуть“ nutzt den jüngsten
    static string SnapPrefix(string key) => Hash(key) + "-snap-";
    internal static string SaveSnapshot(string key, Dictionary<string, int[]> layout)
    {
        string file = Path.Combine(Folder, "profile", SnapPrefix(key) + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
        Write(file, layout);
        SaveProfile(key, layout);
        return file;
    }

    /// <summary>Automatischer Schnappschuss (Zeitplan, Herunterfahren, Benutzerwechsel, Beenden): nur wenn anders als der jüngste;
    /// höchstens 20 automatische je Anordnung, ältere wandern in den Papierkorb.</summary>
    internal static bool AutoSnapshot(string key, Dictionary<string, int[]> layout)
    {
        var latest = Snapshots(key).FirstOrDefault();
        if (latest.File is not null && Read(latest.File) is { } last && !Keeper.Differs(last, layout)) return false;
        string file = Path.Combine(Folder, "profile", SnapPrefix(key) + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-auto.json");
        Write(file, layout);
        foreach (var old in Snapshots(key).Where(s => s.File.EndsWith("-auto.json", StringComparison.OrdinalIgnoreCase)).Skip(20)) RemoveSnapshot(old.File);
        return true;
    }

    /// <summary>Anordnung als Datei (eigenes Format, JSON): Bildschirme und Elementpositionen.</summary>
    internal sealed record LayoutFile(string App, string Key, List<ScreenMeta> Screens, Dictionary<string, int[]> Icons);
    internal static void Export(string path, string key, Dictionary<string, int[]> layout)
    {
        var screens = Monitors.Current().Select(m => new ScreenMeta(m.Id, m.Number, m.Maker, m.Bounds.X, m.Bounds.Y, m.Bounds.Width, m.Bounds.Height, m.Primary) { Kind = m.Kind, Model = m.Model, Diagonal = m.Diagonal }).ToList();
        File.WriteAllText(path, JsonSerializer.Serialize(new LayoutFile("DeskKeeper", key, screens, layout), new JsonSerializerOptions { WriteIndented = true }));
    }
    internal static LayoutFile? Import(string path)
    {
        try { var f = JsonSerializer.Deserialize<LayoutFile>(File.ReadAllText(path)); return f is { App: "DeskKeeper", Icons.Count: > 0 } ? f : null; }
        catch { return null; }
    }
    internal static List<(string File, DateTime Time, int Count)> Snapshots(string key)
    {
        var list = new List<(string, DateTime, int)>();
        try
        {
            string dir = Path.Combine(Folder, "profile");
            if (!Directory.Exists(dir)) return list;
            foreach (var f in Directory.EnumerateFiles(dir, SnapPrefix(key) + "*.json"))
            {
                string stamp = Path.GetFileNameWithoutExtension(f)[SnapPrefix(key).Length..].Replace("-auto", "");
                if (!DateTime.TryParseExact(stamp, "yyyyMMdd-HHmmss", null, System.Globalization.DateTimeStyles.None, out var t)) t = File.GetLastWriteTime(f);
                if (Read(f) is { } d) list.Add((f, t, d.Count));
            }
        }
        catch { }
        return list.OrderByDescending(x => x.Item2).ToList();
    }
    internal static Dictionary<string, int[]>? LoadSnapshot(string file) => Read(file);
    /// <summary>Schnappschuss entfernen: in profile\papierkorb verschieben (wiederherstellbar), nicht löschen.</summary>
    internal static void RemoveSnapshot(string file)
    {
        try
        {
            string bin = Path.Combine(Folder, "profile", "papierkorb");
            Directory.CreateDirectory(bin);
            File.Move(file, Path.Combine(bin, Path.GetFileName(file)), overwrite: true);
            // Papierkorb begrenzen: älteste über 100 endgültig entfernen
            foreach (var old in new DirectoryInfo(bin).GetFiles("*.json").OrderByDescending(x => x.LastWriteTimeUtc).Skip(100)) old.Delete();
        }
        catch { }
    }

    // Geräte einer Anordnung (Nummer, Hersteller, Lage) für die Anordnungsansicht
    static string ScreensFile(string key) => Path.Combine(Folder, "profile", Hash(key) + "-bildschirme.json");
    internal static void SaveScreens(string key, List<MonitorInfo> monitors)
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Folder, "profile"));
            File.WriteAllText(ScreensFile(key), JsonSerializer.Serialize(monitors.Select(m => new ScreenMeta(m.Id, m.Number, m.Maker, m.Bounds.X, m.Bounds.Y, m.Bounds.Width, m.Bounds.Height, m.Primary) { Kind = m.Kind, Model = m.Model, Diagonal = m.Diagonal, Dpi = m.Dpi }).ToList()));
        }
        catch { }
    }
    internal sealed record ScreenMeta(string Id, int Number, string Maker, double X, double Y, double W, double H, bool Primary)
    {
        public DeviceKind Kind { get; init; }
        public string Model { get; init; } = "";
        public double Diagonal { get; init; }
        public int Dpi { get; init; }
    }
    /// <summary>Geräte einer Anordnung; für Stände ohne Gerätedatei aus dem Schlüssel (Nummer nach Lage).</summary>
    internal static List<MonitorInfo> ScreensOf(string key)
    {
        try
        {
            if (File.Exists(ScreensFile(key)) && JsonSerializer.Deserialize<List<ScreenMeta>>(File.ReadAllText(ScreensFile(key))) is { Count: > 0 } list)
            {
                var parts = Split(key);   // Skalierung älterer Gerätedateien aus dem Schlüssel
                return list.Select(m => new MonitorInfo(m.Id, new System.Windows.Rect(m.X, m.Y, m.W, m.H), m.Primary) { Number = m.Number, Maker = m.Maker, Kind = m.Kind, Model = m.Model, Diagonal = m.Diagonal,
                    Dpi = m.Dpi > 0 ? m.Dpi : parts.FirstOrDefault(p => p.X == (int)m.X && p.Y == (int)m.Y && p.W == (int)m.W && p.H == (int)m.H).Dpi }).ToList();
            }
        }
        catch { }
        if (key == ScreenKey()) return Monitors.Current();
        return Split(key).OrderByDescending(p => p.Primary).ThenBy(p => p.X)
            .Select((p, i) => new MonitorInfo(p.Hardware, p.Bounds, p.Primary) { Number = i + 1, Dpi = p.Dpi }).ToList();
    }

    /// <summary>Einstellungen (nach dem Vorbild von DesktopOK, erweitert). RestoreMode: instant | robot.</summary>
    internal sealed record Options(bool Watch = true)
    {
        public string RestoreMode { get; init; } = Edition.Public ? "instant" : "robot";
        public bool RestoreAtLogin { get; init; } = true;
        public bool RestoreAtStart { get; init; } = true;
        public int AutoSnapshotMinutes { get; init; }
        public bool SaveOnShutdown { get; init; } = true;
        public bool SaveOnUserSwitch { get; init; } = true;
        public bool SaveOnExit { get; init; } = true;
        /// <summary>Endpositionen immer am Explorer-Raster einrasten (19.11.2026: Symbole verrutschten nach Szenen).</summary>
        public bool KeepGrid { get; init; } = true;
        public bool StartMinimized { get; init; }
        public bool CloseToTray { get; init; } = true;
        public bool RememberWindow { get; init; } = true;
        public string Theme { get; init; } = "system";
        public string Language { get; init; } = "auto";
        public double[]? Window { get; init; }
    }
    static string OptionsFile => Path.Combine(Folder, "einstellungen.json");
    internal static Options LoadOptions() { try { return File.Exists(OptionsFile) ? JsonSerializer.Deserialize<Options>(File.ReadAllText(OptionsFile)) ?? new() : new(); } catch { return new(); } }
    internal static void SaveOptions(Options o) { try { Directory.CreateDirectory(Folder); File.WriteAllText(OptionsFile, JsonSerializer.Serialize(o)); } catch { } }

    internal sealed record Choice(string Weapon = "jericho", string Skin = "graphit");
    static string ChoiceFile => Path.Combine(Folder, "auswahl.json");
    internal static Choice LoadChoice() { try { return File.Exists(ChoiceFile) ? JsonSerializer.Deserialize<Choice>(File.ReadAllText(ChoiceFile)) ?? new() : new(); } catch { return new(); } }
    internal static void SaveChoice(Choice c) { try { Directory.CreateDirectory(Folder); File.WriteAllText(ChoiceFile, JsonSerializer.Serialize(c)); } catch { } }

    internal static void SavePending(Dictionary<string, int[]> snapshot) => Write(Pending, snapshot);
    internal static Dictionary<string, int[]>? LoadPending() => Read(Pending);
    internal static void ClearPending() { try { File.Delete(Pending); } catch { } }

    internal static void SaveProfile(Dictionary<string, int[]> positions) => SaveProfile(ScreenKey(), positions);
    internal static Dictionary<string, int[]>? LoadProfile() => LoadProfile(ScreenKey());
    internal static void SaveProfile(string key, Dictionary<string, int[]> positions) { Write(ProfileFile(key), positions); Remember(key); }
    internal static Dictionary<string, int[]>? LoadProfile(string key) => Read(ProfileFile(key)) ?? Read(ProfileFile(LegacyKey(key)));
    // Automatisch mitgeführte Anordnung je Bildschirmanordnung (getrennt vom gemerkten Stand für „Вернуть“)
    static string AutoFile(string key) => Path.Combine(Folder, "profile", Hash(key) + "-auto.json");
    internal static void SaveAuto(string key, Dictionary<string, int[]> positions) { Write(AutoFile(key), positions); Remember(key); }
    internal static Dictionary<string, int[]>? LoadAuto(string key) => Read(AutoFile(key)) ?? LoadProfile(key);

    // Platz je Symbol auf seinem Bildschirmgerät, je Anordnung; jüngste zuerst
    static string PlanFile(string key) => Path.Combine(Folder, "profile", Hash(key) + "-plaetze.json");
    internal static void SavePlan(string key, Dictionary<string, Placement> places)
    {
        string path = PlanFile(key), temp = path + ".neu";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(temp, JsonSerializer.Serialize(places));
        File.Move(temp, path, overwrite: true);
    }
    internal static List<IReadOnlyDictionary<string, Placement>> Plans()
    {
        var list = new List<(DateTime, IReadOnlyDictionary<string, Placement>)>();
        try
        {
            string dir = Path.Combine(Folder, "profile");
            if (Directory.Exists(dir))
                foreach (var f in Directory.EnumerateFiles(dir, "*-plaetze.json"))
                    try
                    {
                        if (JsonSerializer.Deserialize<Dictionary<string, Placement>>(File.ReadAllText(f)) is { } d)
                            list.Add((File.GetLastWriteTimeUtc(f), new Dictionary<string, Placement>(d, StringComparer.OrdinalIgnoreCase)));
                    }
                    catch { }
        }
        catch { }
        return list.OrderByDescending(x => x.Item1).Select(x => x.Item2).ToList();
    }

    static void Write(string path, Dictionary<string, int[]> data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".neu";
        File.WriteAllText(temp, JsonSerializer.Serialize(data));
        File.Move(temp, path, overwrite: true);
    }

    static Dictionary<string, int[]>? Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var data = JsonSerializer.Deserialize<Dictionary<string, int[]>>(File.ReadAllText(path));
            return data is null ? null : new Dictionary<string, int[]>(data.Where(e => e.Value is { Length: 2 }), StringComparer.OrdinalIgnoreCase);
        }
        catch { return null; }
    }

    static string Hash(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..16];
}
