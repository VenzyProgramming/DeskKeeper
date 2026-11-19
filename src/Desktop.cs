using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace DeskKeeper;

/// <summary>Ein Symbol des Schreibtischs: Mittelpunkt in Bildschirmpixeln (virtueller Bildschirm).</summary>
record IconInfo(string Id, string Name, Point Center);

/// <summary>Fläche, auf der die Szene Symbole bewegt: der echte Schreibtisch oder die Testfläche.</summary>
interface IIconSurface : IDisposable
{
    IReadOnlyList<IconInfo> Icons { get; }
    double IconSize { get; }
    /// <summary>Setzt die Mittelpunkte der genannten Symbole (gesammelt, ein Aufruf je Bild).</summary>
    void Move(IReadOnlyDictionary<string, Point> centers);
}

/// <summary>
/// Der echte Schreibtisch von Windows über die Shell-Ansicht von Explorer.
/// Bewegt die echten Symbole; legt keine Kopien an und blendet nichts aus.
/// </summary>
sealed class ShellDesktop : IIconSurface
{
    const uint AllView = 2, PositionItem = 0x80, AbsoluteParsing = 0x80028000, NormalDisplay = 0;
    const uint SnapToGrid = 0x4;
    readonly List<object> com = [];
    readonly Dictionary<string, IntPtr> pidls = new(StringComparer.OrdinalIgnoreCase);
    readonly List<IconInfo> icons = [];
    readonly FolderView view;
    readonly Point origin;
    Vector anchor;   // Abstand Elementposition → Symbolmitte (gemessen, sonst geschätzt)
    public Vector EstimatedAnchor { get; private set; }
    public Vector Anchor => anchor;
    public bool AnchorMeasured { get; private set; }
    readonly uint flagsBefore;
    bool snapChanged;
    FolderView2? view2;
    internal string Stage = "";

    public IReadOnlyList<IconInfo> Icons => icons;
    public double IconSize { get; }
    public bool AutoArrange { get; }
    public bool DesktopInFront { get; }
    public string SnapInfo => view2 is null ? "keine-IFolderView2" : ((flagsBefore & SnapToGrid) != 0 ? "an" : "aus");

    public ShellDesktop()
    {
        try
        {
            var windows = Track((ShellWindows)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"), true)!)!);
            object location = 0, empty = null!;
            var provider = Track((ServiceProvider?)windows.FindWindowSW(ref location, ref empty, 8, out _, 1)
                ?? throw new IOException(Loc.T("r.desktopLoading")));
            Guid service = new("4C96BE40-915C-11CF-99D3-00AA004AE837"), browserId = typeof(ShellBrowser).GUID;
            var browser = Track((ShellBrowser)provider.QueryService(ref service, ref browserId));
            var shellView = Track(browser.QueryActiveShellView());
            view = (FolderView)shellView;
            Check(((OleWindow)shellView).GetWindow(out var viewWindow));
            var list = FindWindowEx(viewWindow, IntPtr.Zero, "SysListView32", null);
            var corner = new NativePoint();
            using (Physical.Scope()) ClientToScreen(list == IntPtr.Zero ? viewWindow : list, ref corner);   // echte Pixel wie die Elementpositionen
            origin = new Point(corner.X, corner.Y);
            Check(view.GetSpacing(out var spacing));
            IconSize = spacing.X > 0 ? Math.Clamp(spacing.X * 0.58, 16, 160) : 48;
            if (spacing.X > 0) Spacing = Math.Max(spacing.X, spacing.Y);
            if (spacing.X > 0) { SpacingX = spacing.X; SpacingY = spacing.Y; }
            anchor = EstimatedAnchor = new Vector(spacing.X > 0 ? spacing.X / 2.0 : 38, 2 + IconSize / 2);
            listView = list == IntPtr.Zero ? viewWindow : list;
            spacingKey = (spacing.X, spacing.Y);
            AutoArrange = view.GetAutoArrange() == 0;   // S_OK = an
            try { view2 = (FolderView2)view; if (view2.GetCurrentFolderFlags(out flagsBefore) < 0) view2 = null; } catch (InvalidCastException) { view2 = null; }
            var foreground = GetForegroundWindow();
            DesktopInFront = foreground == GetAncestor(viewWindow, 2) || ClassOf(foreground) is "Progman" or "WorkerW";

            Check(view.ItemCount(AllView, out int count));
            for (int index = 0; index < count; index++)
            {
                if (view.Item(index, out var pidl) < 0 || pidl == IntPtr.Zero) continue;
                string id = NameOf(pidl, AbsoluteParsing);
                if (id.Length == 0 || pidls.ContainsKey(id) || view.GetItemPosition(pidl, out var position) < 0) { Marshal.FreeCoTaskMem(pidl); continue; }
                pidls[id] = pidl;
                string name = NameOf(pidl, NormalDisplay);
                icons.Add(new IconInfo(id, name.Length == 0 ? id : name, ToScreen(position)));
            }
            Calibrate();
        }
        catch { Dispose(); throw; }
    }

    IntPtr listView;
    (int X, int Y) spacingKey;
    static readonly Dictionary<(int, int, int), Vector> measuredAnchors = [];
    static readonly HashSet<(int, int, int)> failedAnchors = [];   // Messung ging nicht: nicht bei jedem Lesen erneut versuchen

    /// <summary>
    /// Wo die Symbolmitte relativ zur Elementposition liegt, hängt von Abstand, Symbolgröße und Beschriftung ab; geschätzt lag sie
    /// auf breit gerasterten Desktops sichtbar daneben (Asche und Explosionen neben den Symbolen, 19.11.2026).
    /// Hier wird sie über die Windows-Bedienhilfen (UI Automation, nur lesen) an einigen Symbolen gemessen und zwischengespeichert.
    /// </summary>
    void Calibrate()
    {
        int iconPx = ViewIconSize();
        var keyA = (spacingKey.X, spacingKey.Y, iconPx);
        lock (measuredAnchors) { if (measuredAnchors.TryGetValue(keyA, out var known)) { anchor = known; AnchorMeasured = true; return; } if (failedAnchors.Contains(keyA)) return; }
        try
        {
            var root = System.Windows.Automation.AutomationElement.FromHandle(listView);
            var items = root.FindAll(System.Windows.Automation.TreeScope.Children, System.Windows.Automation.Condition.TrueCondition);
            var byName = icons.GroupBy(i => i.Name).Where(g => g.Count() == 1).ToDictionary(g => g.Key, g => g.First());
            var dx = new List<double>(); var dy = new List<double>();
            foreach (System.Windows.Automation.AutomationElement item in items)
            {
                if (dx.Count >= 12) break;
                string name = item.Current.Name;
                var rect = item.Current.BoundingRectangle;
                if (rect.IsEmpty || rect.Width < 8 || !byName.TryGetValue(name, out var icon) || !pidls.TryGetValue(icon.Id, out var pidl)) continue;
                if (view.GetItemPosition(pidl, out var pos) < 0) continue;
                // Symbolbild sitzt oben mittig im Element; kleiner Rand über dem Bild
                // Meldet Windows nur das Symbolbild (so breit wie das Bild), ist dessen Mitte die Symbolmitte; sonst oben mittig im Element
                bool imageOnly = rect.Width <= iconPx * 1.25;
                double cy = imageOnly ? rect.Y + rect.Height / 2 : rect.Y + Math.Max(2, (rect.Width - iconPx) * 0.12) + iconPx / 2.0;
                dx.Add(rect.X + rect.Width / 2 - (pos.X + origin.X));
                dy.Add(cy - (pos.Y + origin.Y));
            }
            if (dx.Count < 2) { lock (measuredAnchors) failedAnchors.Add(keyA); return; }
            dx.Sort(); dy.Sort();
            var measured = new Vector(dx[dx.Count / 2], dy[dy.Count / 2]);
            if (Math.Abs(measured.X) > 400 || Math.Abs(measured.Y) > 400) { lock (measuredAnchors) failedAnchors.Add(keyA); return; }   // unplausibel: bei der Schätzung bleiben
            lock (measuredAnchors) measuredAnchors[keyA] = measured;
            // Symbolmitten der bereits eingelesenen Symbole mit dem gemessenen Abstand neu berechnen
            var old = anchor; anchor = measured; AnchorMeasured = true;
            for (int k = 0; k < icons.Count; k++) icons[k] = icons[k] with { Center = icons[k].Center - old + measured };
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or System.Runtime.InteropServices.COMException or System.Windows.Automation.ElementNotAvailableException) { lock (measuredAnchors) failedAnchors.Add(keyA); }
    }

    Point ToScreen(NativePoint item) => new Point(item.X, item.Y) + (Vector)origin + anchor;
    NativePoint ToItem(Point center)
    {
        var p = center - anchor - (Vector)origin;
        return new NativePoint { X = (int)Math.Round(p.X), Y = (int)Math.Round(p.Y) };
    }

    /// <summary>Während der Szene: Raster aus, sonst springen die Symbole in Rasterschritten. Wird beim Schließen zurückgestellt.</summary>
    public void FreeMovement()
    {
        if (view2 is null || (flagsBefore & SnapToGrid) == 0 || snapChanged) return;
        if (view2 is not null && view2.SetCurrentFolderFlags(SnapToGrid, 0) >= 0) snapChanged = true;
    }

    // Werkzeuge: Ordnerschalter und Symbolgröße der Desktopansicht
    public uint FolderFlags() => view2 is not null && view2.GetCurrentFolderFlags(out uint f) >= 0 ? f : 0;
    public void SetFolderFlags(uint mask, uint flags) { view2?.SetCurrentFolderFlags(mask, flags); }
    public int ViewIconSize() => view2 is not null && view2.GetViewModeAndIconSize(out _, out int size) >= 0 ? size : 48;
    public void SetViewIconSize(int size) { if (view2 is not null && view2.GetViewModeAndIconSize(out int mode, out _) >= 0) view2.SetViewModeAndIconSize(mode, size); }

    /// <summary>War „Am Raster ausrichten“ vor der Szene an? (für die Sicherung)</summary>
    public bool SnapWasOn => view2 is not null && (flagsBefore & SnapToGrid) != 0;
    /// <summary>Raster nach einem Absturz wieder einschalten.</summary>
    public void SetSnap(bool on) { if (view2 is not null) view2.SetCurrentFolderFlags(SnapToGrid, on ? SnapToGrid : 0); }

    public void Move(IReadOnlyDictionary<string, Point> centers)
    {
        var ids = new List<IntPtr>(centers.Count);
        var points = new List<NativePoint>(centers.Count);
        foreach (var (id, center) in centers)
        {
            if (!pidls.TryGetValue(id, out var pidl)) continue;
            ids.Add(pidl);
            points.Add(ToItem(center));
        }
        if (ids.Count > 0) view.SelectAndPositionItems((uint)ids.Count, ids.ToArray(), points.ToArray(), PositionItem);
    }

    /// <summary>Elementpositionen (Explorer-Koordinaten) für die Sicherung.</summary>
    public Dictionary<string, int[]> Snapshot() => icons.ToDictionary(icon => icon.Id, icon => { var p = ToItem(icon.Center); return new[] { p.X, p.Y }; }, StringComparer.OrdinalIgnoreCase);

    /// <summary>Elementpositionen setzen. Mit „Am Raster festhalten“ ist das Raster dabei kurz an, damit Explorer jede Position
    /// in eine Rasterzelle einrastet (auch nach einer Szene, in der es aus war); danach gilt wieder der vorherige Zustand.</summary>
    public int RestoreSnapshot(IReadOnlyDictionary<string, int[]> saved)
    {
        int placed = RestoreExact(saved);
        if (view2 is null || !Store.LoadOptions().KeepGrid) return placed;
        // Explorer übernimmt gesetzte Positionen genau so, auch mit „Am Raster ausrichten“; eingerastet wird erst beim Einschalten.
        // Deshalb kurz aus und wieder an: jedes Symbol springt in seine Rasterzelle (19.11.2026: Symbole lagen auf zwei um 34 px
        // versetzten Rastern). Danach gilt der vorherige Zustand, die Positionen bleiben eingerastet.
        try
        {
            uint before = FolderFlags();
            view2.SetCurrentFolderFlags(SnapToGrid, 0);
            view2.SetCurrentFolderFlags(SnapToGrid, SnapToGrid);
            if ((before & SnapToGrid) == 0) view2.SetCurrentFolderFlags(SnapToGrid, 0);
            else snapChanged = false;   // Raster ist an: beim Schließen nicht wieder auf den Szenen-Zustand zurückstellen
        }
        catch (COMException) { }
        return placed;
    }

    int RestoreExact(IReadOnlyDictionary<string, int[]> saved)
    {
        var ids = new List<IntPtr>(); var points = new List<NativePoint>();
        foreach (var (id, p) in saved)
            if (pidls.TryGetValue(id, out var pidl)) { ids.Add(pidl); points.Add(new NativePoint { X = p[0], Y = p[1] }); }
        if (ids.Count > 0) Check(view.SelectAndPositionItems((uint)ids.Count, ids.ToArray(), points.ToArray(), PositionItem));
        return ids.Count;
    }

    /// <summary>Linke obere Ecke der Symbolfläche (aller Bildschirme) in Pixeln.</summary>
    public Point Origin => origin;
    public Point CenterOf(int[] item) => ToScreen(new NativePoint { X = item[0], Y = item[1] });
    /// <summary>Symbolmitte (Bildschirmpixel) → Elementposition (Explorer).</summary>
    public int[] ItemOf(Point center) { var p = ToItem(center); return [p.X, p.Y]; }
    /// <summary>Rasterabstand der Symbole in Pixeln.</summary>
    public double Spacing { get; private set; } = 76;
    /// <summary>Rasterabstand waagerecht und senkrecht (Pixel).</summary>
    public double SpacingX { get; private set; } = 76;
    public double SpacingY { get; private set; } = 76;

    /// <summary>Aktueller Mittelpunkt laut Explorer (zum Prüfen, ob eine Position außerhalb angenommen wurde).</summary>
    public Point? CenterNow(string id) => pidls.TryGetValue(id, out var pidl) && view.GetItemPosition(pidl, out var p) >= 0 ? ToScreen(p) : null;

    public void Dispose()
    {
        if (snapChanged) { try { view2?.SetCurrentFolderFlags(SnapToGrid, flagsBefore & SnapToGrid); } catch { } snapChanged = false; }
        foreach (var pidl in pidls.Values) Marshal.FreeCoTaskMem(pidl);
        pidls.Clear();
        for (int index = com.Count - 1; index >= 0; index--) { try { Marshal.ReleaseComObject(com[index]); } catch { } }
        com.Clear();
    }

    T Track<T>(T item) where T : class { com.Add(item); return item; }
    static void Check(int hr) => Marshal.ThrowExceptionForHR(hr);
    static string NameOf(IntPtr pidl, uint type)
    {
        if (SHGetNameFromIDList(pidl, type, out var result) < 0 || result == IntPtr.Zero) return "";
        try { return Marshal.PtrToStringUni(result) ?? ""; } finally { Marshal.FreeCoTaskMem(result); }
    }
    static string ClassOf(IntPtr window)
    {
        var name = new System.Text.StringBuilder(64);
        return window != IntPtr.Zero && GetClassName(window, name, name.Capacity) > 0 ? name.ToString() : "";
    }

    [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X, Y; }

    [DllImport("shell32.dll")] static extern int SHGetNameFromIDList(IntPtr pidl, uint type, out IntPtr name);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int capacity);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window, ref NativePoint point);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string type, string? title);

    [ComImport, Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    interface ShellWindows
    {
        void S1(); void S2(); void S3(); void S4(); void S5(); void S6(); void S7(); void S8();
        [return: MarshalAs(UnmanagedType.IDispatch)]
        object FindWindowSW([MarshalAs(UnmanagedType.Struct)] ref object location, [MarshalAs(UnmanagedType.Struct)] ref object root, int kind, out int hwnd, int options);
    }
    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ServiceProvider { [return: MarshalAs(UnmanagedType.Interface)] object QueryService(ref Guid service, ref Guid id); }
    [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ShellBrowser
    {
        void S1(); void S2(); void S3(); void S4(); void S5(); void S6(); void S7(); void S8(); void S9(); void S10(); void S11(); void S12();
        [return: MarshalAs(UnmanagedType.Interface)] object QueryActiveShellView();
    }
    [ComImport, Guid("000214E3-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface OleWindow { [PreserveSig] int GetWindow(out IntPtr window); }

    [ComImport, Guid("CDE725B0-CCC9-4519-917E-325D72FAB4CE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface FolderView
    {
        void S1(); void S2(); void S3();
        [PreserveSig] int Item(int index, out IntPtr pidl);
        [PreserveSig] int ItemCount(uint flags, out int count);
        void S6(); void S7(); void S8();
        [PreserveSig] int GetItemPosition(IntPtr pidl, out NativePoint point);
        [PreserveSig] int GetSpacing(out NativePoint point);
        void S11();
        [PreserveSig] int GetAutoArrange();
        void S13();
        [PreserveSig] int SelectAndPositionItems(uint count, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] pidls,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] NativePoint[] points, uint flags);
    }
    [ComImport, Guid("1AF3A467-214F-4298-908E-06B03E0B39F9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface FolderView2
    {
        // IFolderView (14 Methoden)
        void S1(); void S2(); void S3(); void S4(); void S5(); void S6(); void S7(); void S8(); void S9(); void S10(); void S11(); void S12(); void S13(); void S14();
        // IFolderView2: SetGroupBy, GetGroupBy, SetViewProperty, GetViewProperty, SetTileViewProperties, SetExtendedTileViewProperties, SetText
        void T1(); void T2(); void T3(); void T4(); void T5(); void T6(); void T7();
        [PreserveSig] int SetCurrentFolderFlags(uint mask, uint flags);
        [PreserveSig] int GetCurrentFolderFlags(out uint flags);
        // GetSortColumnCount, SetSortColumns, GetSortColumns, GetItem, GetVisibleItem, GetSelectedItem, GetSelection, GetSelectionState, InvokeVerbOnSelection
        void U1(); void U2(); void U3(); void U4(); void U5(); void U6(); void U7(); void U8(); void U9();
        [PreserveSig] int SetViewModeAndIconSize(int mode, int size);
        [PreserveSig] int GetViewModeAndIconSize(out int mode, out int size);
    }
}
