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
        foreach (var s in System.Windows.Forms.Screen.AllScreens)
        {
            facts.TryGetValue(s.DeviceName, out var f);
            string? device = DeviceId(s.DeviceName) ?? (f?.DevicePath is { Length: > 0 } dp ? dp : null);
            int number = int.TryParse(new string(s.DeviceName.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray()), out int n) ? n : 0;
            list.Add(new MonitorInfo(device ?? s.DeviceName + "|" + s.Bounds.Width + "x" + s.Bounds.Height,
                new Rect(s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height), s.Primary)
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
