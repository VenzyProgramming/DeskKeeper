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
    /// <summary>Nach dem Zurückstellen so lange aufpassen: mischt Windows die Symbole danach noch einmal (Monitor wacht langsam auf,
    /// Taskleiste kommt nach), wird wieder zurückgestellt statt die Unordnung zu speichern .</summary>
    public double GuardSeconds = 20;
    Dictionary<string, int[]>? guard;
    double guardUntil;
    int guardRepeats;
    string? key;
    double changedAt = double.NegativeInfinity;
    bool restorePending;
    Dictionary<string, int[]>? candidate;
    public string LastAction { get; private set; } = "";
    /// <summary>Zahlen zur letzten Meldung (fürs Protokoll).</summary>
    public string LastDetail { get; private set; } = "";
    public event Action<string>? Acted;

    public Keeper(IKeeperWorld world) { this.world = world; }

    /// <summary>Windows hat die Anzeige geändert (Ereignis); die nächste Abfrage wartet die Beruhigung ab.</summary>
    public void DisplayChanged(double now) { changedAt = now; restorePending = true; candidate = null; }

    /// <summary>Eine Abfrage (etwa alle 3 s). now in Sekunden.</summary>
    public void Tick(double now)
    {
        if (world.Blocked) { candidate = null; if (guard is not null) guardUntil = Math.Max(guardUntil, now + GuardSeconds); return; }
        string k = world.Key();
        if (key is null) { key = k; restorePending = RestoreOnStart; changedAt = now - SettleSeconds + StartSeconds; }
        if (k != key) { key = k; guard = null; DisplayChanged(now); return; }
        if (now - changedAt < SettleSeconds) return;

        if (restorePending)
        {
            var current = world.Read();
            if (current is null) return;   // Explorer noch nicht bereit: nächste Abfrage
            restorePending = false; candidate = null;
            var target = AutoRestore ? world.Target(k, current) : null;
            // Zurückstellen nur bei echten Verschiebungen (≥ 40 px), nicht bei Rasterversatz von wenigen Pixeln
            if (target is not null && Misplaced(target, current))
            {
                world.Apply(target); Report("restored", $"{MovedCount(target, current)} von {current.Count} Symbolen");
                guard = target; guardUntil = now + GuardSeconds; guardRepeats = 0;
                return;
            }
            if (AutoSave && world.Saved(k) is null) { world.Save(k, current); Report("first", $"{current.Count} Symbole"); }
            return;
        }

        if (guard is not null && now > guardUntil) guard = null;
        if (guard is not null)
        {
            var look = world.Read();
            if (look is null || world.Key() != k) return;
            if (Misplaced(guard, look))
            {
                candidate = null;
                if (guardRepeats++ < 2) { world.Apply(guard); Report("restored", $"Nachwache: {MovedCount(guard, look)} Symbole wieder an ihren Platz"); return; }
                guard = null;   // bleibt trotzdem so: gewollt, ab jetzt normal speichern
            }
        }
        if (!AutoSave) return;
        var now1 = world.Read();
        if (now1 is null || world.Key() != k) return;   // Wechsel mitten in der Abfrage
        if (candidate is not null && !Differs(candidate, now1))
        {
            int changed = world.Saved(k) is { } before ? MovedCount(before, now1) : now1.Count;
            world.Save(k, now1); candidate = null; Report("saved", $"{changed} Symbole anders"); return;
        }
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

    void Report(string what, string detail = "") { LastAction = what; LastDetail = detail; Acted?.Invoke(what); }

    /// <summary>Liegt mindestens ein Symbol wirklich woanders (≥ tolerance px)? Kleiner Rasterversatz zählt nicht.</summary>
    internal static bool Misplaced(Dictionary<string, int[]> target, Dictionary<string, int[]> current, int tolerance = 40)
    {
        foreach (var (id, p) in target)
            if (current.TryGetValue(id, out var q) && (Math.Abs(p[0] - q[0]) >= tolerance || Math.Abs(p[1] - q[1]) >= tolerance)) return true;
        return false;
    }

    /// <summary>Wie viele Symbole beider Stände liegen wirklich woanders (≥ tolerance px)?</summary>
    internal static int MovedCount(Dictionary<string, int[]> a, Dictionary<string, int[]> b, int tolerance = 40)
        => a.Count(e => b.TryGetValue(e.Key, out var q) && (Math.Abs(e.Value[0] - q[0]) >= tolerance || Math.Abs(e.Value[1] - q[1]) >= tolerance));

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
    /// <param name="rowStep">senkrechter Rasterabstand (0 = wie spacing)</param>
    internal static Dictionary<string, Point> Plan(IReadOnlyDictionary<string, Point> current, IReadOnlyDictionary<string, Point>? exact,
        IEnumerable<IReadOnlyDictionary<string, Placement>> newestFirst, IReadOnlyList<MonitorInfo> monitors, double spacing, out int derived, double rowStep = 0)
    {
        var result = new Dictionary<string, Point>(current, StringComparer.OrdinalIgnoreCase);
        var moved = new List<string>();
        var history = newestFirst.ToList();
        var main = monitors.FirstOrDefault(m => m.Primary) ?? monitors.FirstOrDefault();
        foreach (var id in current.Keys)
        {
            if (exact is not null && exact.TryGetValue(id, out var e)) { result[id] = e; continue; }
            Placement? newest = null;
            bool found = false;
            foreach (var plan in history)
            {
                if (!plan.TryGetValue(id, out var place)) continue;
                newest ??= place;
                if (Monitors.Locate(place, monitors, spacing * 0.45) is { } p) { result[id] = p; moved.Add(id); found = true; break; }
            }
            // zuletzt auf einem Bildschirm, der fehlt: dieselbe Anordnung auf dem Hauptbildschirm
            if (!found && newest is not null && main is not null) { result[id] = Monitors.Mirror(newest, main, spacing * 0.45); moved.Add(id); }
        }
        derived = moved.Count;
        // Zusammenstöße: abgeleitete Symbole weichen nach unten, dann eine Spalte zur Bildschirmmitte hin aus (wie das Windows-Raster).
        // Es zählen nur feste Plätze: gemerkte, unveränderte und schon gesetzte – nicht die Lage, in die Windows die übrigen geschoben hat.
        double down = rowStep > 0 ? rowStep : spacing;
        var movedSet = new HashSet<string>(moved, StringComparer.OrdinalIgnoreCase);
        var settled = result.Where(e => !movedSet.Contains(e.Key)).ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
        double EdgeDistance(Point p) => monitors.FirstOrDefault(x => x.Bounds.Contains(p))?.Bounds is { } b ? Math.Min(p.X - b.Left, b.Right - p.X) : 0;
        foreach (var id in moved.OrderBy(id => Math.Round(EdgeDistance(result[id]) / spacing)).ThenBy(id => result[id].Y))
        {
            var p = result[id];
            var m = monitors.FirstOrDefault(x => x.Bounds.Contains(p))?.Bounds ?? Rect.Empty;
            double inward = m.IsEmpty || p.X < m.Left + m.Width / 2 ? spacing : -spacing;
            for (int step = 0; step < 400 && Taken(settled, p, spacing, down); step++)
            {
                p.Y += down;
                if (!m.IsEmpty && p.Y > m.Bottom - down * 0.45)
                {
                    p.Y = m.Top + down * 0.5; p.X += inward;
                    if (p.X > m.Right - spacing * 0.45) p.X = m.Left + spacing * 0.5;
                    if (p.X < m.Left + spacing * 0.45) p.X = m.Right - spacing * 0.5;
                }
            }
            result[id] = p; settled[id] = p;
        }
        return result;
    }

    static bool Taken(Dictionary<string, Point> settled, Point p, double spacing, double down)
    {
        foreach (var q in settled.Values) if (Math.Abs(q.X - p.X) < spacing * 0.6 && Math.Abs(q.Y - p.Y) < down * 0.6) return true;
        return false;
    }
}

/// <summary>Echte Welt: Explorer-Schreibtisch, Profile unter %LOCALAPPDATA%\DeskKeeper, Rückstellung durch Дубина.</summary>
sealed class RealWorld : IKeeperWorld
{
    string? checkedKey;
    public string Key()
    {
        string key = Store.ScreenKey();
        // Einmal je Anordnung: Doppelgänger aus der Zeit der DPI-Virtualisierung beiseitelegen (4300×1800 statt 3440×1440)
        if (key != checkedKey)
        {
            if (checkedKey is null) Store.MigrateKeys();
            checkedKey = key;
            Fault.Note("Anordnung: " + Store.Describe(key) + " · " + string.Join(" · ", Store.Split(key).Select(p => (p.Hardware.Length > 0 ? Monitors.MakerOf(p.Hardware) + " " + p.Hardware.Split('#').ElementAtOrDefault(1) : "ohne Kennung") + $" {p.W}×{p.H} @{p.X},{p.Y}" + (p.Primary ? " haupt" : ""))));
            int n = Store.QuarantineTwins(key); if (n > 0) Fault.Note($"{n} Anordnung(en) mit falscher Skalierung nach profile\\quarantaene-dpi verschoben");
            Store.RememberDevices(Physical.Displays());
        }
        return key;
    }
    public Dictionary<string, int[]>? Read() { try { using var d = new ShellDesktop(); return d.Snapshot(); } catch { return null; } }
    public Dictionary<string, int[]>? Saved(string key) => Store.LoadAuto(key);
    public bool Blocked => Runner.Busy || Store.HasPending || DesktopLock.RobotActive;

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
            var plan = Keeper.Plan(centers, exact, plans, Monitors.Current(), d.Spacing, out _, d.SpacingY);
            return plan.ToDictionary(e => e.Key, e => d.ItemOf(e.Value), StringComparer.OrdinalIgnoreCase);
        }
        catch { return null; }
    }

    public void Apply(Dictionary<string, int[]> layout) => Restore(layout);

    /// <summary>Zurückstellen laut Einstellung: sofort (wie DesktopOK) oder mit Дубина, die jedes Symbol trägt.</summary>
    public static void Restore(Dictionary<string, int[]> layout)
    {
        // Szenen laufen immer auf dem UI-Thread (der Motor kann auf einem eigenen Thread arbeiten)
        // Automatisch und viele Symbole: sofort statt minutenlanger DUM-E-Szene ohne Zutun (19.11.2026: 32 Symbole, 270 s beim Start)
        if (Store.LoadOptions().RestoreMode == "robot" && MovedCount(layout) > 12) { try { using var quick = new ShellDesktop(); quick.RestoreSnapshot(layout); } catch { } return; }
        if (Store.LoadOptions().RestoreMode == "robot")
        {
            var ui = Application.Current?.Dispatcher;
            if (ui is null || ui.CheckAccess()) _ = Runner.Play(false, layout);
            else ui.BeginInvoke(() => { _ = Runner.Play(false, layout); });
            return;
        }
        try { using var d = new ShellDesktop(); d.RestoreSnapshot(layout); } catch { }
    }

    /// <summary>Schnappschuss einer anderen Bildschirmanordnung auf die jetzige übertragen: je Symbol über das Gerät, auf dem es lag
    /// (gleiche Auflösung pixelgenau, sonst anteilig). Liegt das Gerät nicht mehr an, kommt das Symbol anteilig auf den Hauptbildschirm.</summary>
    public static Dictionary<string, int[]> Translate(string fromKey, Dictionary<string, int[]> layout)
    {
        if (fromKey == Store.ScreenKey()) return layout;
        using var d = new ShellDesktop();
        var old = Store.ScreensOf(fromKey);
        var now = Monitors.Current();
        if (old.Count == 0 || now.Count == 0) return layout;
        var shift = (Vector)old.Select(m => m.Bounds).Aggregate(Rect.Union).TopLeft - (Vector)d.Origin;   // Explorer zählt ab der linken oberen Ecke aller Bildschirme
        var places = new Dictionary<string, Placement>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, item) in layout)
            if (Monitors.Place(d.CenterOf(item) + shift, old) is { } p) places[id] = p;   // fehlt das Gerät, spiegelt Plan auf den Hauptbildschirm
        var centers = d.Snapshot().ToDictionary(e => e.Key, e => d.CenterOf(e.Value), StringComparer.OrdinalIgnoreCase);
        var plan = Keeper.Plan(centers, null, [places], now, d.Spacing, out _, d.SpacingY);
        return plan.ToDictionary(e => e.Key, e => d.ItemOf(e.Value), StringComparer.OrdinalIgnoreCase);
    }

    static int MovedCount(Dictionary<string, int[]> layout)
    {
        try
        {
            using var d = new ShellDesktop();
            var now = d.Snapshot();
            return layout.Count(e => now.TryGetValue(e.Key, out var q) && (Math.Abs(e.Value[0] - q[0]) >= 40 || Math.Abs(e.Value[1] - q[1]) >= 40));
        }
        catch { return 0; }
    }

    public void Save(string key, Dictionary<string, int[]> layout)
    {
        try
        {
            // Viele Symbole anders als der gemerkte Stand: den alten Stand vorher als Schnappschuss ablegen, damit er zurückholbar bleibt
            if (Store.LoadAuto(key) is { } before && Keeper.MovedCount(before, layout) >= 3) Store.AutoSnapshot(key, before);
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
