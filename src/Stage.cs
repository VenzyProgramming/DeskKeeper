using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace DeskKeeper;

/// <summary>
/// Durchsichtige, durchklickbare Ebene über dem echten Schreibtisch. Sie zeichnet nur die Darsteller
/// (Иерихон, Rauch, Blitze, Дубина) – keine Kopie des Schreibtischs, keine Symbole.
/// Leinwand in Bildschirmpixeln, per Viewbox auf das Fenster gestreckt (gemischte DPI).
/// </summary>
sealed class Stage : Window
{
    readonly Actors actors;
    readonly Rect bounds;
    IntPtr handle;
    public event Action? Escape;
    /// <summary>Ganz unten halten (Standard, Fenster bleiben offen). Aus, wenn vorher alle Fenster minimiert wurden: dann normal
    /// über dem Desktop, sonst schiebt sich die beim Minimieren aktivierte Desktop-Ebene darüber (19.11.2026: Tu-160 unsichtbar).</summary>
    public bool StayAtBottom { get; set; } = true;

    public Stage(Rect virtualBounds, Scene? scene)
    {
        bounds = virtualBounds;
        actors = new Actors(scene, virtualBounds);
        Content = new Viewbox { Stretch = Stretch.Fill, Child = actors };
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = false;   // hinter den Fenstern, nicht darüber
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = Top = -32000; Width = Height = 10;
        SourceInitialized += (_, _) =>
        {
            handle = new WindowInteropHelper(this).Handle;
            // Durchklickbar und nie aktiv: WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE.
            SetWindowLong(handle, -20, GetWindowLong(handle, -20) | 0x20 | 0x80 | 0x08000000);
            SetWindowPos(handle, StayAtBottom ? AboveDesktop(handle) : IntPtr.Zero, (int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height, 0x0010);   // direkt über dem Desktop
            RegisterHotKey(handle, 1, 0x4000, 0x1B);   // Esc, MOD_NOREPEAT
            HwndSource.FromHwnd(handle).AddHook((IntPtr w, int msg, IntPtr wp, IntPtr lp, ref bool handled) =>
            {
                if (msg == 0x0312 && wp.ToInt32() == 1) { handled = true; Escape?.Invoke(); }
                if (msg == 0x0046 && lp != IntPtr.Zero && StayAtBottom)   // WM_WINDOWPOSCHANGING: immer direkt über dem Desktop bleiben
                {
                    var pos = Marshal.PtrToStructure<WindowPos>(lp);
                    pos.InsertAfter = AboveDesktop(handle);
                    pos.Flags &= ~0x0004u;   // SWP_NOZORDER entfernen
                    Marshal.StructureToPtr(pos, lp, false);
                }
                return IntPtr.Zero;
            });
        };
        Closed += (_, _) => { if (handle != IntPtr.Zero) UnregisterHotKey(handle, 1); };
    }

    public void Seek(double t) => actors.Seek(t);

    /// <summary>Messung ohne Symbolbewegung: leeres Szenenfenster kurz öffnen und seine Lage in der Fensterreihenfolge melden
    /// (über oder unter dem Desktop „Progman“/„WorkerW“, sichtbar?).</summary>
    public static string ZOrderProbe(Rect area)
    {
        var stage = new Stage(area, null);
        using (Physical.Scope()) stage.Show();
        stage.SendToBottom();
        var frame = new System.Windows.Threading.DispatcherFrame();
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        t.Tick += (_, _) => { t.Stop(); frame.Continue = false; };
        t.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
        var order = new List<string>();
        int mine = -1, desk = -1;
        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h)) return true;
            var cls = new System.Text.StringBuilder(64); GetClassName(h, cls, 64);
            string c = cls.ToString();
            if (h == stage.handle) mine = order.Count;
            if (c is "Progman" or "WorkerW" && desk < 0) desk = order.Count;
            order.Add(c);
            return true;
        }, IntPtr.Zero);
        stage.Close();
        return $"szene-fenster {(mine < 0 ? "unsichtbar" : "#" + mine)} · desktop #{desk} · {(mine >= 0 && (desk < 0 || mine < desk) ? "ÜBER dem Desktop" : "UNTER dem Desktop")} · fenster gesamt {order.Count}";
    }
    /// <summary>Lage einer offenen Bühne in der Fensterreihenfolge (Prüfung).</summary>
    public static string ZOrderNow(Stage stage)
    {
        int mine = -1, desk = -1, i = 0;
        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h)) return true;
            var cls = new System.Text.StringBuilder(64); GetClassName(h, cls, 64);
            if (h == stage.handle) mine = i;
            if (cls.ToString() is "Progman" or "WorkerW" && desk < 0) desk = i;
            i++; return true;
        }, IntPtr.Zero);
        return $"bühne #{mine} desktop #{desk}";
    }
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
    public void SendToBottom() { if (handle != IntPtr.Zero) SetWindowPos(handle, StayAtBottom ? AboveDesktop(handle) : IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); }   // direkt über dem Desktop bzw. HWND_TOP, ohne Aktivieren

    /// <summary>Einfügestelle direkt über der obersten Desktop-Ebene (Progman/WorkerW): das Fenster davor. Vorher HWND_BOTTOM – ist der
    /// Desktop aktiv (Klick auf den Desktop, „Alle minimieren“), hebt Windows ihn an und die Bühne lag darunter: alle Flugzeuge und Helfer
    /// unsichtbar . Ohne Desktop-Fenster: HWND_BOTTOM wie bisher.</summary>
    static IntPtr AboveDesktop(IntPtr self)
    {
        IntPtr desk = IntPtr.Zero;
        for (var h = GetTopWindow(IntPtr.Zero); h != IntPtr.Zero; h = GetWindow(h, 2))   // GW_HWNDNEXT: von oben nach unten
        {
            if (h == self || !IsWindowVisible(h)) continue;
            var cls = new System.Text.StringBuilder(64); GetClassName(h, cls, 64);
            if (cls.ToString() is "Progman" or "WorkerW") { desk = h; break; }
        }
        if (desk == IntPtr.Zero) return new IntPtr(1);
        var above = GetWindow(desk, 3);   // GW_HWNDPREV
        while (above == self) above = GetWindow(above, 3);
        return above == IntPtr.Zero ? IntPtr.Zero : above;   // nichts darüber: ganz oben
    }
    [DllImport("user32.dll")] static extern IntPtr GetTopWindow(IntPtr parent);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint command);

    [StructLayout(LayoutKind.Sequential)]
    struct WindowPos { public IntPtr Hwnd, InsertAfter; public int X, Y, Cx, Cy; public uint Flags; }

    sealed class Actors : FrameworkElement
    {
        readonly Scene? scene;
        readonly Rect bounds;
        double time = -1;
        public Actors(Scene? scene, Rect bounds) { this.scene = scene; this.bounds = bounds; Width = bounds.Width; Height = bounds.Height; IsHitTestVisible = false; }
        public void Seek(double t) { time = t; InvalidateVisual(); }
        protected override void OnRender(DrawingContext dc)
        {
            if (time < 0 || scene is null) return;
            dc.PushTransform(new TranslateTransform(-bounds.X, -bounds.Y));
            scene.Draw(dc, time);
            dc.Pop();
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] static extern int GetWindowLong(IntPtr w, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] static extern int SetWindowLong(IntPtr w, int i, int v);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr w, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr w, int id, uint mods, uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr w, int id);
}

/// <summary>
/// Führt eine Szene auf dem echten Schreibtisch aus: Sicherung schreiben, Fenster beiseite, Raster aus,
/// Bild für Bild zeichnen und die echten Symbole über Explorer bewegen (≈30 Mal je Sekunde),
/// am Ende exakt auf die Zielpositionen setzen. Esc oder ein Fehler stellt sofort die Ausgangslage her.
/// </summary>
static class Runner
{
    internal static bool Busy { get; private set; }

    /// <param name="targets">Zielpositionen (Explorer-Koordinaten); null = Ausgangslage.</param>
    /// <summary>Unter diesem Versatz (Elementkoordinaten) stellt DeskKeeper sofort zurück statt DUM-E tragen zu lassen; wie Keeper.Misplaced.</summary>
    internal const int NearPx = 40;

    public static async Task<string> Play(bool jericho, Dictionary<string, int[]>? targets, bool nuclear = false, bool tu160 = false, bool showDesktop = false, string? attack = null)
    {
        attack ??= tu160 ? Attacks.Tu160 : null;
        Fault.Note($"Szene angefordert: jericho={jericho} atom={nuclear} angriff={attack ?? "-"} ziele={targets?.Count ?? 0} busy={Busy}");
        if (Busy) { Fault.Note("Szene abgelehnt: läuft schon"); return ""; }
        if (DesktopLock.RobotActive) { Fault.Note("Szene abgelehnt: Saugroboter arbeitet"); return Loc.T("r.robotBusy"); }
        Busy = true;
        using var sceneLock = DesktopLock.Scene();
        note = "";
        ShellDesktop? desktop = null;
        Stage? stage = null;
        bool toggled = false, aborted = false;
        Dictionary<string, int[]>? before = null;
        try
        {
            desktop = new ShellDesktop();
            if (desktop.AutoArrange) { Fault.Note("Szene abgelehnt: Automatisch anordnen ist an"); return Loc.T("r.autoArrange"); }
            if (desktop.Icons.Count == 0 && !jericho) { Fault.Note("Szene abgelehnt: keine Symbole"); return Loc.T("r.noIcons"); }
            before = desktop.Snapshot();
            var pending = new Dictionary<string, int[]>(before, StringComparer.OrdinalIgnoreCase) { ["::raster"] = [desktop.SnapWasOn ? 1 : 0, 0] };
            Store.SavePending(pending);   // mit Rasterzustand, falls die Szene abbricht
            var final = targets is null ? before : before.ToDictionary(e => e.Key, e => targets.TryGetValue(e.Key, out var p) ? p : e.Value, StringComparer.OrdinalIgnoreCase);
            // Nur leicht verrutschte Symbole (Rasterversatz unter 40 px) sofort zurückstellen; DUM-E trägt nur, was wirklich woanders liegt.
            // Sonst plante er für 29 Symbole mit 32 px Versatz 208 s Arbeit, von der kaum etwas zu sehen war .
            int nudged = 0;
            if (!jericho && targets is not null)
            {
                var near = final.Where(e => before.TryGetValue(e.Key, out var q) && (e.Value[0] != q[0] || e.Value[1] != q[1])
                        && Math.Abs(e.Value[0] - q[0]) < NearPx && Math.Abs(e.Value[1] - q[1]) < NearPx)
                    .ToDictionary(e => e.Key, e => e.Value, StringComparer.OrdinalIgnoreCase);
                if (near.Count > 0)
                {
                    desktop.RestoreSnapshot(near);
                    nudged = near.Count;
                    desktop.Dispose();
                    desktop = new ShellDesktop();
                    before = desktop.Snapshot();
                    // Rasterfang kann leicht verschieben: was jetzt nah genug liegt, gilt als erledigt
                    final = before.ToDictionary(e => e.Key, e => final.TryGetValue(e.Key, out var p) && (Math.Abs(p[0] - e.Value[0]) >= NearPx || Math.Abs(p[1] - e.Value[1]) >= NearPx) ? p : e.Value, StringComparer.OrdinalIgnoreCase);
                    Fault.Note($"Szene: {nudged} leicht verrutschte Symbole sofort zurückgestellt");
                }
            }
            var homes = final.ToDictionary(e => e.Key, e => desktop.CenterOf(e.Value), StringComparer.OrdinalIgnoreCase);
            var displays = Physical.Displays();
            var screens = displays.Select(s => s.Bounds).ToArray();
            var main = displays.FirstOrDefault(s => s.Primary) ?? displays[0];
            var primary = main.Bounds;
            var all = screens.Aggregate(Rect.Union);
            // Дубина so groß wie der Saugroboter von Seitenleisten-Ausgabe (Ø 64 DIP) bei der Skalierung des Hauptbildschirms
            double robot = 64 * main.Dpi / 96.0;
            Helpers.Current = Helpers.All.Contains(Store.LoadChoice().Helper) ? Store.LoadChoice().Helper : Helpers.Dume;
            var scene = new Scene(desktop.Icons, homes, desktop.IconSize, screens, primary, jericho, robot, nuclear, attack: attack);
            // Verbrannte Symbole liegen außerhalb aller Bildschirme (unter der Unterkante), bis Дубина sie holt.
            var stash = new Point(all.Left + desktop.IconSize * 2, all.Bottom + desktop.IconSize * 30);
            bool stashChecked = false;
            if (!jericho && scene.Duration < 0.6) { Fault.Note(nudged > 0 ? "Szene nicht nötig: nur leicht verrutschte Symbole" : "Szene nicht nötig: Symbole an ihrem Platz"); Store.ClearPending(); return Loc.T(nudged > 0 ? "m.restored" : "r.inPlace"); }

            // Die Bühne liegt direkt über dem Desktop. Vom Panel gestartet: offene Fenster kurz wegräumen (wie Win+D), sonst sieht man nichts
            // (19.11.2026: „Tu-160 wurde nicht angezeigt“, Szene nach 7 s abgebrochen); am Ende kommen sie zurück.
            // Wie Win+M (alle minimieren), nicht Win+D: „Desktop anzeigen“ holt den Desktop selbst nach vorn und verdeckt die Bühne
            // (19.11.2026: seit preview.252 war keine Szene mehr zu sehen)
            if (showDesktop && !desktop.DesktopInFront) { Shell("MinimizeAll"); toggled = true; await Task.Delay(400); }
            // Am Raster festhalten: das Explorer-Raster bleibt an; echte Symbole stehen nur in festen Zellen oder warten außerhalb
            // Raster während der Szene aus: mit Raster nimmt Explorer die Warteposition außerhalb nicht an und reiht „versteckte“
            // Symbole sichtbar in freie Zellen ein . Am Ende rastet RestoreSnapshot alles in Explorers Raster ein.
            const bool gridLocked = false;
            {   // getragene Symbole zeichnet die Bühne über Palette, Ladefläche und Rumpf (20.11.2026: vorher verdeckt, „unsichtbar“)
                int px = (int)Math.Round(desktop.IconSize);
                foreach (var icon in desktop.Icons) ShellIcons.Get(icon.Id, px);   // vorab laden, nicht erst im Bildtakt
                scene.IconImage = id => ShellIcons.Get(id, px);
            }
            desktop.FreeMovement();   // Raster ist während der Szene immer aus (gridLocked = false)
            stage = new Stage(all, scene);
            stage.Escape += () => aborted = true;
            stage.StayAtBottom = !toggled;
            using (Physical.Scope()) stage.Show();   // Fenster in echten Pixeln, auch wenn der Gastgeber mit System-DPI läuft
            stage.SendToBottom();
            Fault.Note($"Szene läuft: {scene.Duration:0.0} s, Fläche {all}, {desktop.Icons.Count} Symbole, Fenster sichtbar={stage.IsVisible}");
            await Task.Delay(60);

            var clock = Stopwatch.StartNew();
            var last = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
            string? hidden = null;
            double lastMove = -1, lastDraw = -1;
            var done = new TaskCompletionSource();
            void Frame(object? sender, EventArgs e)
            {
                try
                {
                    double t = clock.Elapsed.TotalSeconds;
                    if (aborted || t >= scene.Duration) { done.TrySetResult(); return; }
                    // Während des Atompilzes 30 Bilder je Sekunde: das durchsichtige Vollbildfenster wird nur halb so oft kopiert.
                    // Der Bomber läuft mit voller Bildrate (bei 30 Bildern sprang er sichtbar, 18.11.).
                    bool heavy = scene.Nuke && t >= Scene.Impact && t < Scene.Impact + NukeCloud.Length;
                    if (!heavy || t - lastDraw >= 1 / 30.0) { stage.Seek(t); lastDraw = t; }
                    if (t - lastMove < 1 / 30.0) return;
                    lastMove = t;
                    var changed = new Dictionary<string, Point>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (id, maybe) in scene.IconCenters(t))
                    {
                        // unterwegs (nur bei festem Raster): echtes Symbol wartet außerhalb, die Bühne zeichnet es
                        bool away = maybe is null || gridLocked && !scene.Resting(id, maybe.Value);
                        var p = away ? stash : maybe!.Value;
                        if (away) hidden ??= id;
                        if (!last.TryGetValue(id, out var q) || (q - p).LengthSquared > 0.25) { changed[id] = p; last[id] = p; }
                    }
                    if (changed.Count > 0) desktop.Move(changed);
                    if (!stashChecked && hidden is not null && changed.ContainsKey(hidden))
                    {
                        stashChecked = true;
                        // mit Raster rastet die Warteposition in eine Zelle ein; abgelehnt ist sie nur, wenn das Symbol sichtbar bleibt
                        if (desktop.CenterNow(hidden) is { } now && (gridLocked ? all.Contains(now) : (now - stash).Length > desktop.IconSize))
                        {
                            Log("Проводник не принял позицию за краем: " + now);
                            if (gridLocked) { desktop.FreeMovement(); last.Clear(); Fault.Note("Raster für die Szene aus: Explorer nahm die Warteposition mit Raster nicht an"); }
                        }
                    }
                }
                catch (Exception error) { done.TrySetException(error); }
            }
            CompositionTarget.Rendering += Frame;
            try { await done.Task; }
            finally { CompositionTarget.Rendering -= Frame; }

            // Genau setzen: Ziel bei Erfolg, Ausgangslage bei Abbruch.
            // Иерихон abgebrochen: Ausgangslage. Zurückstellen abgebrochen: gleich ans Ziel (Esc überspringt die Fahrt).
            desktop.RestoreSnapshot(aborted && jericho ? before : final);
            Store.ClearPending();
            Fault.Note(aborted ? "Szene mit Esc beendet" : "Szene fertig");
            return aborted && !jericho ? Loc.T("r.placed") : aborted ? Loc.T("r.aborted") : jericho ? Loc.T("r.done") + (note.Length > 0 ? " · " + note : "") : Loc.T("r.placed");
        }
        catch (Exception error)
        {
            Fault.Report(error);
            try { if (desktop is not null && before is not null) { desktop.RestoreSnapshot(before); Store.ClearPending(); } } catch { }
            return error is COMException ? string.Format(Loc.T("r.explorer"), error.HResult.ToString("X8")) : error.Message;
        }
        finally
        {
            stage?.Close();
            desktop?.Dispose();
            if (toggled) { await Task.Delay(150); Shell("UndoMinimizeALL"); }
            Busy = false;
        }
    }

    static string note = "";
    static void Log(string text)
    {
        note = Loc.T("r.notHidden");
        try { System.IO.Directory.CreateDirectory(Store.Folder); System.IO.File.AppendAllText(System.IO.Path.Combine(Store.Folder, "protokoll.txt"), DateTime.Now.ToString("s") + " " + text + Environment.NewLine); } catch { }
    }

    /// <summary>Nach Absturz/Abmelden: unterbrochene Szene zurückstellen.</summary>
    public static string? RecoverPending()
    {
        var pending = Store.LoadPending();
        if (pending is null) return null;
        try
        {
            using var desktop = new ShellDesktop();
            if (pending.Remove("::raster", out var snap) && snap[0] == 1) desktop.SetSnap(true);
            desktop.RestoreSnapshot(pending);
            Store.ClearPending();
            return Loc.T("r.recovered");
        }
        catch { return null; }
    }

    static void Shell(string method)
    {
        try
        {
            var type = Type.GetTypeFromProgID("Shell.Application");
            if (type is null) return;
            object shell = Activator.CreateInstance(type)!;
            type.InvokeMember(method, System.Reflection.BindingFlags.InvokeMethod, null, shell, null);
            Marshal.ReleaseComObject(shell);
        }
        catch { }
    }
}
