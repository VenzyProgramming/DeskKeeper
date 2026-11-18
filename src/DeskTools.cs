using System;
using System.Runtime.InteropServices;

namespace DeskKeeper;

/// <summary>Desktop-Werkzeuge wie bei DesktopOK: alle Fenster minimieren, Symbole aus-/einblenden, Symbolgröße,
/// Raster, automatisch anordnen. Wirkt auf die laufende Desktopansicht von Explorer.</summary>
static class DeskTools
{
    public static void MinimizeAll()
    {
        try
        {
            var type = Type.GetTypeFromProgID("Shell.Application");
            if (type is null) return;
            object shell = Activator.CreateInstance(type)!;
            type.InvokeMember("MinimizeAll", System.Reflection.BindingFlags.InvokeMethod, null, shell, null);
            Marshal.ReleaseComObject(shell);
        }
        catch { }
    }

    const uint NoIcons = 0x1000, AutoArrange = 0x1, Snap = 0x4;

    public static bool IconsShown { get => !Flag(NoIcons); set => SetFlag(NoIcons, !value); }
    public static bool Grid { get => Flag(Snap); set => SetFlag(Snap, value); }
    public static bool Arrange { get => Flag(AutoArrange); set => SetFlag(AutoArrange, value); }

    /// <summary>Symbolgröße in Pixeln (Windows: klein 32, mittel 48, groß 96).</summary>
    public static int IconSize
    {
        get { try { using var d = new ShellDesktop(); return d.ViewIconSize(); } catch { return 48; } }
        set { try { using var d = new ShellDesktop(); d.SetViewIconSize(value); } catch { } }
    }

    static bool Flag(uint mask) { try { using var d = new ShellDesktop(); return (d.FolderFlags() & mask) != 0; } catch { return false; } }
    static void SetFlag(uint mask, bool on) { try { using var d = new ShellDesktop(); d.SetFolderFlags(mask, on ? mask : 0); } catch { } }
}
