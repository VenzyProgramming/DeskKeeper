using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace DeskKeeper;

/// <summary>Zugriff des Hüters auf Schreibtisch und Speicher (echt: ShellDesktop + Store; Prüfung: Attrappe).</summary>
interface IKeeperWorld
{
    string Key();
    Dictionary<string, int[]>? Read();
    /// <summary>Soll-Anordnung für die aktuellen Bildschirme: gemerkter Stand dieser Anordnung, fehlende Symbole
    /// von ihrem zuletzt bekannten Bildschirm abgeleitet. null = noch nichts bekannt.</summary>
    Dictionary<string, int[]>? Target(string key, Dictionary<string, int[]> current);
    /// <summary>Zurückstellen (echt: Дубина fährt und trägt die Symbole).</summary>
    void Apply(Dictionary<string, int[]> layout);
    void Save(string key, Dictionary<string, int[]> layout);
    Dictionary<string, int[]>? Saved(string key);
    bool Blocked { get; }   // Szene läuft oder Sicherung offen
}

/// <summary>
/// Merkt sich die Symbolanordnung je Bildschirmanordnung (Geräte, Lage, Auflösung, Skalierung, Hauptbildschirm)
/// und je Symbol den Bildschirm, auf dem es zuletzt lag:
///  · Start (auch nach einem Herunterfahren, bei dem Windows die Lage vergessen hat): nach der Beruhigung zurückstellen;
///  · Wechsel der Anordnung (Monitor an/ab, Notebook zu, anderer Hauptbildschirm): nichts speichern, bis es ruhig ist,
///    dann zurückstellen; eine neue Anordnung bekommt ihre Symbole auf den Bildschirm, auf dem sie zuletzt lagen;
///  · im Ruhezustand: eine veränderte Anordnung erst speichern, wenn sie zwei Abfragen lang gleich bleibt.
/// Windows' eigene Umordnung beim Wechsel wird nie unter dem alten Schlüssel gespeichert.
/// </summary>
sealed class Keeper
{
    readonly IKeeperWorld world;
    public bool AutoRestore = true, AutoSave = true;
    /// <summary>Beim Start zurückstellen (Einstellung: beim Windows-Start bzw. beim Programmstart).</summary>
    public bool RestoreOnStart = true;
    public double SettleSeconds = 4, StartSeconds = 8;
    string? key;
    double changedAt = double.NegativeInfinity;
    bool restorePending;
    Dictionary<string, int[]>? candidate;
    public string LastAction { get; private set; } = "";
    public event Action<string>? Acted;

    public Keeper(IKeeperWorld world) { this.world = world; }

    /// <summary>Windows hat die Anzeige geändert (Ereignis); die nächste Abfrage wartet die Beruhigung ab.</summary>
    public void DisplayChanged(double now) { changedAt = now; restorePending = true; candidate = null; }

    /// <summary>Eine Abfrage (etwa alle 3 s). now in Sekunden.</summary>
    public void Tick(double now)
    {
        if (world.Blocked) { candidate = null; return; }
        string k = world.Key();
        if (key is null) { key = k; restorePending = RestoreOnStart; changedAt = now - SettleSeconds + StartSeconds; }
        if (k != key) { key = k; DisplayChanged(now); return; }
        if (now - changedAt < SettleSeconds) return;

        if (restorePending)
        {
            var current = world.Read();
            if (current is null) return;   // Explorer noch nicht bereit: nächste Abfrage
            restorePending = false; candidate = null;
            var target = AutoRestore ? world.Target(k, current) : null;
            if (target is not null && Differs(target, current)) { world.Apply(target); Report("restored"); return; }
            if (AutoSave && world.Saved(k) is null) { world.Save(k, current); Report("first"); }
            return;
        }

        if (!AutoSave) return;
        var now1 = world.Read();
        if (now1 is null || world.Key() != k) return;   // Wechsel mitten in der Abfrage
        if (candidate is not null && !Differs(candidate, now1)) { world.Save(k, now1); candidate = null; Report("saved"); return; }
        candidate = world.Saved(k) is { } stored && !Differs(stored, now1) ? null : now1;
    }

    /// <summary>Windows meldet ab oder fährt herunter: jetzige Anordnung sofort merken (wenn ruhig und nichts läuft).</summary>
    public void SaveNow(double now)
    {
        if (!AutoSave || world.Blocked || restorePending || key is null || now - changedAt < SettleSeconds) return;
        string k = world.Key();
        if (k != key || world.Read() is not { } layout) return;
        if (world.Saved(k) is { } stored && !Differs(stored, layout)) return;
        world.Save(k, layout); Report("saved");
    }

    void Report(string what) { LastAction = what; Acted?.Invoke(what); }

    /// <summary>Unterschied in Lage (mehr als 2 px) oder Menge der Symbole.</summary>
    internal static bool Differs(Dictionary<string, int[]> a, Dictionary<string, int[]> b)
    {
        if (a.Count != b.Count) return true;
        foreach (var (id, p) in a)
            if (!b.TryGetValue(id, out var q) || Math.Abs(p[0] - q[0]) > 2 || Math.Abs(p[1] - q[1]) > 2) return true;
        return false;
    }

    /// <summary>
    /// Soll-Lage in Bildschirmpixeln. exact: gemerkter Stand genau dieser Anordnung (hat Vorrang).
    /// Sonst je Symbol der jüngste bekannte Platz auf einem jetzt angeschlossenen Bildschirm (gleiche Auflösung: pixelgenau,
    /// andere: anteilig). Symbole ohne bekannten Platz bleiben, wo sie sind. Abgeleitete Plätze rücken aus, wenn dort schon eins liegt.
    /// </summary>
    internal static Dictionary<string, Point> Plan(IReadOnlyDictionary<string, Point> current, IReadOnlyDictionary<string, Point>? exact,
        IEnumerable<IReadOnlyDictionary<string, Placement>> newestFirst, IReadOnlyList<MonitorInfo> monitors, double spacing, out int derived)
    {
        var result = new Dictionary<string, Point>(current, StringComparer.OrdinalIgnoreCase);
        var moved = new List<string>();
        var history = newestFirst.ToList();
        foreach (var id in current.Keys)
        {
            if (exact is not null && exact.TryGetValue(id, out var e)) { result[id] = e; continue; }
            foreach (var plan in history)
                if (plan.TryGetValue(id, out var place) && Monitors.Locate(place, monitors, spacing * 0.45) is { } p) { result[id] = p; moved.Add(id); break; }
        }
        derived = moved.Count;
        // Zusammenstöße: abgeleitete Symbole weichen nach unten, dann in die nächste Spalte aus (wie das Windows-Raster)
        foreach (var id in moved)
        {
            var p = result[id];
            var m = monitors.FirstOrDefault(x => x.Bounds.Contains(p))?.Bounds ?? Rect.Empty;
            for (int step = 0; step < 400 && Taken(result, id, p, spacing); step++)
            {
                p.Y += spacing;
                if (!m.IsEmpty && p.Y > m.Bottom - spacing * 0.45) { p.Y = m.Top + spacing * 0.5; p.X += spacing; if (p.X > m.Right - spacing * 0.45) p.X = m.Left + spacing * 0.5; }
            }
            result[id] = p;
        }
        return result;
    }

    static bool Taken(Dictionary<string, Point> all, string self, Point p, double spacing)
    {
        foreach (var (id, q) in all) if (id != self && Math.Abs(q.X - p.X) < spacing * 0.6 && Math.Abs(q.Y - p.Y) < spacing * 0.6) return true;
        return false;
    }
}

/// <summary>Echte Welt: Explorer-Schreibtisch, Profile unter %LOCALAPPDATA%\DeskKeeper, Rückstellung durch Дубина.</summary>
sealed class RealWorld : IKeeperWorld
{
    public string Key() => Store.ScreenKey();
    public Dictionary<string, int[]>? Read() { try { using var d = new ShellDesktop(); return d.Snapshot(); } catch { return null; } }
    public Dictionary<string, int[]>? Saved(string key) => Store.LoadAuto(key);
    public bool Blocked => Runner.Busy || Store.HasPending;

    public Dictionary<string, int[]>? Target(string key, Dictionary<string, int[]> current)
    {
        try
        {
            using var d = new ShellDesktop();
            var exactRaw = Store.LoadAuto(key);
            var plans = Store.Plans();
            if (exactRaw is null && plans.Count == 0) return null;
            var centers = current.ToDictionary(e => e.Key, e => d.CenterOf(e.Value), StringComparer.OrdinalIgnoreCase);
            var exact = exactRaw?.ToDictionary(e => e.Key, e => d.CenterOf(e.Value), StringComparer.OrdinalIgnoreCase);
            var plan = Keeper.Plan(centers, exact, plans, Monitors.Current(), d.Spacing, out _);
            return plan.ToDictionary(e => e.Key, e => d.ItemOf(e.Value), StringComparer.OrdinalIgnoreCase);
        }
        catch { return null; }
    }

    public void Apply(Dictionary<string, int[]> layout) => Restore(layout);

    /// <summary>Zurückstellen laut Einstellung: sofort (wie DesktopOK) oder mit Дубина, die jedes Symbol trägt.</summary>
    public static void Restore(Dictionary<string, int[]> layout)
    {
        if (Store.LoadOptions().RestoreMode == "robot") { _ = Runner.Play(false, layout); return; }
        try { using var d = new ShellDesktop(); d.RestoreSnapshot(layout); } catch { }
    }

    public void Save(string key, Dictionary<string, int[]> layout)
    {
        try
        {
            Store.SaveAuto(key, layout);
            using var d = new ShellDesktop();
            var monitors = Monitors.Current();
            var places = new Dictionary<string, Placement>(StringComparer.OrdinalIgnoreCase);
            foreach (var (id, item) in layout) if (Monitors.Place(d.CenterOf(item), monitors) is { } p) places[id] = p;
            Store.SavePlan(key, places);
        }
        catch { }
    }
}
