using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace DeskKeeper;

/// <summary>Art des Bildschirmgeräts, aus Anschlussart (Windows-Anzeigekonfiguration), Bildgröße laut EDID,
/// Touch-/Stifteingabe und Seitenverhältnis bestimmt.</summary>
enum DeviceKind { Monitor, Laptop, Tablet, Phone, TV, Wireless, Virtual }

/// <summary>Was Windows über einen angeschlossenen Bildschirm verrät (nur lesen).</summary>
record DisplayFacts(string GdiName, string DevicePath, string Model, uint Technology, double DiagonalInch);

static class DeviceFacts
{
    const uint Internal = 0x80000000, Hdmi = 5, Miracast = 15, IndirectWired = 16, IndirectVirtual = 17, EmbeddedDp = 11, EmbeddedUdi = 13;

    /// <summary>Art bestimmen. touch: Rechner hat einen eingebauten Touch-/Stiftbildschirm.</summary>
    public static DeviceKind Classify(DisplayFacts? f, double width, double height, bool touch)
    {
        bool portrait = height > width;
        double d = f?.DiagonalInch ?? 0;
        uint tech = f?.Technology ?? 0;
        bool builtIn = tech is Internal or EmbeddedDp or EmbeddedUdi;
        if (builtIn) return touch && (d is > 0 and <= 13.6 || portrait) ? DeviceKind.Tablet : DeviceKind.Laptop;
        if (tech is Miracast or IndirectWired or IndirectVirtual)
        {
            // Handy oder Tablet als zweiter Bildschirm (Miracast, spacedesk, Duet …): nach Größe, sonst nach Format
            if (d is > 0 and < 7.6) return DeviceKind.Phone;
            if (d is >= 7.6 and <= 13.6) return DeviceKind.Tablet;
            if (d <= 0 && portrait) return height / Math.Max(1, width) > 1.7 ? DeviceKind.Phone : DeviceKind.Tablet;
            return tech == Miracast ? DeviceKind.Wireless : DeviceKind.Virtual;
        }
        if (tech == Hdmi && d >= 39) return DeviceKind.TV;
        return DeviceKind.Monitor;
    }

    public static string Name(DeviceKind k) => Loc.T("kind." + k);

    public static bool HasTouchScreen()
    {
        try { return (GetSystemMetrics(94) & 0x05) != 0; }   // SM_DIGITIZER: integrierter Touch (1) oder Stift (4)
        catch { return false; }
    }

    /// <summary>Alle aktiven Bildschirme laut Anzeigekonfiguration, je GDI-Name (\\.\DISPLAYn).</summary>
    public static Dictionary<string, DisplayFacts> Query()
    {
        var result = new Dictionary<string, DisplayFacts>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (GetDisplayConfigBufferSizes(2, out uint pathCount, out uint modeCount) != 0) return result;
            var paths = new PathInfo[pathCount]; var modes = new ModeInfo[modeCount];
            if (QueryDisplayConfig(2, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0) return result;
            for (int i = 0; i < pathCount; i++)
            {
                var source = new SourceName { Header = new Header { Type = 1, Size = (uint)Marshal.SizeOf<SourceName>(), Adapter = paths[i].SourceAdapter, Id = paths[i].SourceId } };
                if (DisplayConfigGetDeviceInfo(ref source) != 0) continue;
                var target = new TargetName { Header = new Header { Type = 2, Size = (uint)Marshal.SizeOf<TargetName>(), Adapter = paths[i].TargetAdapter, Id = paths[i].TargetId } };
                if (DisplayConfigGetDeviceInfo(ref target) != 0) continue;
                string path = target.MonitorDevicePath ?? "";
                result[source.GdiName] = new DisplayFacts(source.GdiName, path, (target.FriendlyName ?? "").Trim(), target.Technology, Diagonal(path));
            }
        }
        catch { }
        return result;
    }

    /// <summary>Bildgröße in Zoll aus der EDID in der Registrierung (Bytes 21/22 = Breite/Höhe in cm). 0 = unbekannt.</summary>
    static double Diagonal(string devicePath)
    {
        try
        {
            // \\?\DISPLAY#GSM77A1#5&36fd9695&0&UID4608#{guid} → Enum\DISPLAY\GSM77A1\5&36fd9695&0&UID4608
            var parts = devicePath.Split('#');
            if (parts.Length < 3) return 0;
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{parts[1]}\{parts[2]}\Device Parameters");
            if (key?.GetValue("EDID") is not byte[] edid || edid.Length < 23) return 0;
            double w = edid[21], h = edid[22];
            return w > 0 && h > 0 ? Math.Sqrt(w * w + h * h) / 2.54 : 0;
        }
        catch { return 0; }
    }

    [StructLayout(LayoutKind.Sequential)] struct Luid { public uint Low; public int High; }

    [StructLayout(LayoutKind.Sequential)]
    struct PathInfo
    {
        public Luid SourceAdapter; public uint SourceId; public uint SourceModeIndex; public uint SourceFlags;
        public Luid TargetAdapter; public uint TargetId; public uint TargetModeIndex; public uint Technology; public uint Rotation; public uint Scaling;
        public uint RefreshNum, RefreshDen; public uint ScanLine; public int TargetAvailable; public uint TargetFlags;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)] struct ModeInfo { [FieldOffset(0)] public uint InfoType; }

    [StructLayout(LayoutKind.Sequential)] struct Header { public uint Type; public uint Size; public Luid Adapter; public uint Id; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SourceName { public Header Header; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string GdiName; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct TargetName
    {
        public Header Header; public uint Flags; public uint Technology; public ushort EdidMaker; public ushort EdidProduct; public uint Connector;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string FriendlyName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string MonitorDevicePath;
    }

    [DllImport("user32.dll")] static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
    [DllImport("user32.dll")] static extern int QueryDisplayConfig(uint flags, ref uint paths, [Out] PathInfo[] pathArray, ref uint modes, [Out] ModeInfo[] modeArray, IntPtr topology);
    [DllImport("user32.dll")] static extern int DisplayConfigGetDeviceInfo(ref SourceName info);
    [DllImport("user32.dll")] static extern int DisplayConfigGetDeviceInfo(ref TargetName info);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
}
