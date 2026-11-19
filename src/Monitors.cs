using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace DeskKeeper;

/// <summary>Ein angeschlossener Bildschirm: feste Kennung des Geräts (bleibt gleich, auch wenn er umgesteckt,
/// anders angeordnet oder zum Hauptbildschirm wird), Lage in Pixeln, Hauptbildschirm.</summary>
record MonitorInfo(string Id, Rect Bounds, bool Primary)
{
    /// <summary>Nummer wie in den Windows-Anzeigeeinstellungen (aus \.\DISPLAYn), 0 = unbekannt.</summary>
    public int Number { get; init; }
    /// <summary>Hersteller aus der Gerätekennung (GSM → LG …), eingebaute Notebook-Anzeige als „Встроенный“.</summary>
    public string Maker { get; init; } = "";
    /// <summary>Modellname laut Windows (z. B. „LG ULTRAWIDE“), Geräteart, Bilddiagonale in Zoll (0 = unbekannt).</summary>
    public string Model { get; init; } = "";
    public DeviceKind Kind { get; init; } = DeviceKind.Monitor;
    public double Diagonal { get; init; }
}

/// <summary>Wo ein Symbol liegt, unabhängig von der Anordnung: auf welchem Gerät, Abstand von dessen linker oberer Ecke
/// in Pixeln und als Anteil (für eine andere Auflösung desselben Geräts).</summary>
record Placement(string Monitor, double Dx, double Dy, double Fx, double Fy, double W, double H);

static class Monitors
{
    public static List<MonitorInfo> Current()
    {
        if (Demo.On) return Demo.Both.ToList();
        var list = new List<MonitorInfo>();
        var facts = DeviceFacts.Query();
        bool touch = DeviceFacts.HasTouchScreen();
        foreach (var s in Physical.Displays())
        {
            facts.TryGetValue(s.Device, out var f);
            string? device = DeviceId(s.Device) ?? (f?.DevicePath is { Length: > 0 } dp ? dp : null);
            int number = int.TryParse(new string(s.Device.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray()), out int n) ? n : 0;
            list.Add(new MonitorInfo(device ?? s.Device + "|" + (int)s.Bounds.Width + "x" + (int)s.Bounds.Height, s.Bounds, s.Primary)
            {
                Number = number, Maker = MakerOf(device), Model = f?.Model ?? "", Diagonal = f?.DiagonalInch ?? 0,
                Kind = DeviceFacts.Classify(f, s.Bounds.Width, s.Bounds.Height, touch),
            });
        }
        // Nummern wie Windows fortlaufend ab 1
        var order = list.OrderBy(m => m.Number).ThenBy(m => m.Bounds.X).ToList();
        for (int i = 0; i < order.Count; i++) list[list.IndexOf(order[i])] = order[i] with { Number = i + 1 };
        // gleiche Modelle doppelt: Kennung bleibt durch den Gerätepfad eindeutig; sonst Reihenfolge anhängen
        foreach (var g in list.GroupBy(m => m.Id).Where(g => g.Count() > 1).ToList())
        {
            int n = 0;
            foreach (var m in g.ToList()) { int i = list.IndexOf(m); list[i] = m with { Id = m.Id + "#" + n++ }; }
        }
        return list;
    }

    /// <summary>Symbolmitte (Bildschirmpixel) → Platz auf dem Gerät, das sie enthält (oder dem nächsten).</summary>
    public static Placement? Place(Point center, IReadOnlyList<MonitorInfo> monitors)
    {
        if (monitors.Count == 0) return null;
        var m = monitors.FirstOrDefault(x => x.Bounds.Contains(center)) ?? monitors.OrderBy(x => Distance(x.Bounds, center)).First();
        double dx = center.X - m.Bounds.Left, dy = center.Y - m.Bounds.Top;
        return new Placement(m.Id, dx, dy, dx / m.Bounds.Width, dy / m.Bounds.Height, m.Bounds.Width, m.Bounds.Height);
    }

    /// <summary>Platz → Symbolmitte, falls das Gerät angeschlossen ist. Andere Auflösung: anteilig.</summary>
    public static Point? Locate(Placement p, IReadOnlyList<MonitorInfo> monitors, double margin)
    {
        var m = monitors.FirstOrDefault(x => x.Id == p.Monitor);
        if (m is null) return null;
        bool same = Math.Abs(m.Bounds.Width - p.W) < 1 && Math.Abs(m.Bounds.Height - p.H) < 1;
        double x = same ? p.Dx : p.Fx * m.Bounds.Width, y = same ? p.Dy : p.Fy * m.Bounds.Height;
        x = Math.Clamp(x, margin, Math.Max(margin, m.Bounds.Width - margin));
        y = Math.Clamp(y, margin, Math.Max(margin, m.Bounds.Height - margin));
        return new Point(m.Bounds.Left + x, m.Bounds.Top + y);
    }

    /// <summary>PnP-Herstellerkürzel aus \?\DISPLAY#GSM77A1#… → Name.</summary>
    internal static string MakerOf(string? device)
    {
        if (device is null) return "";
        var parts = device.Split('#');
        if (parts.Length < 2 || parts[1].Length < 3) return "";
        string code = parts[1][..3].ToUpperInvariant();
        return code switch
        {
            "GSM" => "LG", "SAM" or "SEC" => "Samsung", "DEL" => "Dell", "AUS" => "ASUS", "ACR" => "Acer", "BNQ" => "BenQ", "HWP" or "HPN" => "HP",
            "LEN" => "Lenovo", "AOC" => "AOC", "PHL" => "Philips", "MSI" => "MSI", "GBT" => "Gigabyte", "VSC" => "ViewSonic", "IVM" => "iiyama",
            "EIZ" or "ENC" => "EIZO", "SNY" => "Sony", "HSD" or "HKC" => "HKC", "XMI" => "Xiaomi", "HUA" or "HWV" => "Huawei", "MEI" => "Panasonic",
            "BOE" or "AUO" or "CMN" or "LGD" or "SDC" or "SHP" or "IVO" or "CSO" or "NCP" or "CEC" => "Встроенный",
            _ => code,
        };
    }

    static double Distance(Rect r, Point p)
    {
        double dx = Math.Max(Math.Max(r.Left - p.X, 0), p.X - r.Right), dy = Math.Max(Math.Max(r.Top - p.Y, 0), p.Y - r.Bottom);
        return dx * dx + dy * dy;
    }

    /// <summary>Gerätepfad des Bildschirms (enthält Hersteller, Modell, Anschluss), z. B. \\?\DISPLAY#GSM5B09#…</summary>
    static string? DeviceId(string adapter)
    {
        try
        {
            var d = new DisplayDevice { cb = Marshal.SizeOf<DisplayDevice>() };
            for (uint i = 0; EnumDisplayDevices(adapter, i, ref d, 1); i++)
            {
                if ((d.StateFlags & 1) != 0 && d.DeviceID.Length > 0) return d.DeviceID;   // aktiv
                d = new DisplayDevice { cb = Marshal.SizeOf<DisplayDevice>() };
            }
        }
        catch { }
        return null;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DisplayDevice
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplayDevices(string device, uint index, ref DisplayDevice info, uint flags);
}

/// <summary>
/// Bildschirmgeometrie immer in echten Pixeln, wie Explorer sie für die Symbole benutzt. Seitenleisten-Ausgabe läuft mit System-DPI:
/// startet es, während das Notebook (125 %) Hauptbildschirm ist, meldet Windows dem Programm den 3440er-Monitor als 4300×1800
/// und alle Skalierungen als 120 dpi (19.11.2026: Schnappschüsse verschwunden, Symbole gestaucht). Abfragen und das
/// Szenenfenster laufen deshalb kurz im Modus „pro Monitor“, egal wie der Gastgeber eingestellt ist.
/// </summary>
static class Physical
{
    internal sealed record Display(string Device, Rect Bounds, Rect Work, bool Primary, uint Dpi);

    /// <summary>Für die Dauer des Blocks rechnet dieser Thread in echten Pixeln (Fenster, die darin entstehen, ebenso).</summary>
    public static IDisposable Scope() => new Back(Enter());
    sealed class Back(IntPtr old) : IDisposable
    {
        public void Dispose() { if (old != IntPtr.Zero) try { SetThreadDpiAwarenessContext(old); } catch (EntryPointNotFoundException) { } }
    }
    static IntPtr Enter() { try { return SetThreadDpiAwarenessContext(new IntPtr(-4)); } catch (EntryPointNotFoundException) { return IntPtr.Zero; } }   // PER_MONITOR_AWARE_V2

    /// <summary>Alle Bildschirme: Gerätename (\\.\DISPLAYn), Lage und Arbeitsfläche in Pixeln, Hauptbildschirm, Skalierung (dpi).</summary>
    public static List<Display> Displays()
    {
        using var scope = Scope();
        var list = new List<Display>();
        MonitorEnumProc each = (monitor, _, _, _) =>
        {
            var info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>() };
            if (GetMonitorInfo(monitor, ref info))
            {
                uint dpi = 96;
                try { if (GetDpiForMonitor(monitor, 0, out uint x, out _) == 0 && x > 0) dpi = x; } catch (DllNotFoundException) { }
                list.Add(new Display(info.Device, R(info.Monitor), R(info.Work), (info.Flags & 1) != 0, dpi));
            }
            return true;
        };
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, each, IntPtr.Zero);
        GC.KeepAlive(each);
        if (list.Count == 0)
        {
            var s = System.Windows.Forms.Screen.PrimaryScreen ?? System.Windows.Forms.Screen.AllScreens[0];
            list.Add(new Display(s.DeviceName, new Rect(s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height), new Rect(s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height), true, 96));
        }
        return list;
    }

    public static Display Primary() { var all = Displays(); return all.FirstOrDefault(d => d.Primary) ?? all[0]; }

    /// <summary>Mauszeiger in echten Pixeln.</summary>
    public static Point? Cursor() { using var scope = Scope(); return GetCursorPos(out var p) ? new Point(p.X, p.Y) : null; }

    static Rect R(NativeRect r) => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

    [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MonitorInfoEx
    {
        public int Size; public NativeRect Monitor, Work; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }
    delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx info);
    [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out NativePoint p);
}
