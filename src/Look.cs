using System;
using System.Windows.Media;

namespace DeskKeeper;

/// <summary>
/// Erscheinungsbild der öffentlichen Ausgabe: erkennt Windows 10 oder 11 (Build ab 22000) und hell/dunkel
/// (Einstellung „Design“ oder Windows-Vorgabe) und liefert Farben, Rundungen und Schriften wie im jeweiligen System.
/// Windows 11: runde Karten (8 px), Segoe UI Variable, Fluent-Symbole; Windows 10: eckige Flächen, Segoe UI, MDL2-Symbole.
/// Die Seitenleisten-Ausgabe nutzt die Seitenleisten-Ausgabe-Farben.
/// </summary>
sealed class Look
{
    public bool Win10, Dark;
    public Brush Bg = null!, Nav = null!, Card = null!, CardHover = null!, Stroke = null!, Selected = null!, Hover = null!,
        Text = null!, Secondary = null!, Tertiary = null!, Accent = null!, AccentHover = null!, AccentText = null!, AccentSoft = null!,
        ButtonFill = null!, ButtonHover = null!, ButtonStroke = null!, ToggleOff = null!, ToggleKnobOff = null!, ToggleKnobOn = null!;
    public string ThumbHex = "", ThumbHoverHex = "", TipFillHex = "", TipLineHex = "";
    public Color AccentColor;
    public double Radius, ButtonRadius;
    public FontFamily Font = null!, Display = null!, Icons = null!;

    static Look? current;
    public static Look Current => current ??= Create();
    public static void Reset() => current = null;

    public static bool IsWindows10 => Environment.GetEnvironmentVariable("DESKKEEPER_LOOK") switch
    {
        "win10" => true, "win11" => false, _ => Environment.OSVersion.Version.Build < 22000,
    };

    static Look Create()
    {
        if (!Edition.Public) return Sidebar();
        string theme = Environment.GetEnvironmentVariable("DESKKEEPER_THEME") ?? Store.LoadOptions().Theme;
        bool dark = theme switch { "dark" => true, "light" => false, _ => SystemDark() };
        bool win10 = IsWindows10;
        var accent = SystemAccent() ?? Color.FromRgb(0x00, 0x78, 0xD4);
        // Windows nutzt im dunklen Design eine aufgehellte Akzentstufe, im hellen eine abgedunkelte
        var shown = dark ? Mix(accent, Colors.White, win10 ? 0.0 : 0.45) : Mix(accent, Colors.Black, win10 ? 0.0 : 0.12);
        var l = new Look { Win10 = win10, Dark = dark, AccentColor = shown };
        l.Radius = win10 ? 0 : 8; l.ButtonRadius = win10 ? 0 : 4;
        l.Font = new FontFamily(win10 ? "Segoe UI" : "Segoe UI Variable Text, Segoe UI");
        l.Display = new FontFamily(win10 ? "Segoe UI Light, Segoe UI" : "Segoe UI Variable Display, Segoe UI");
        l.Icons = new FontFamily(win10 ? "Segoe MDL2 Assets" : "Segoe Fluent Icons, Segoe MDL2 Assets");
        l.Accent = B(shown); l.AccentHover = B(Mix(shown, dark ? Colors.Black : Colors.White, 0.1));
        l.AccentText = B(Luma(shown) > 0.55 ? Colors.Black : Colors.White);
        l.AccentSoft = B(Color.FromArgb(0x40, shown.R, shown.G, shown.B));
        if (dark)
        {
            l.Bg = H(win10 ? "#000000" : "#202020"); l.Nav = H(win10 ? "#1F1F1F" : "#1C1C1C");
            l.Card = H(win10 ? "#1F1F1F" : "#2B2B2B"); l.CardHover = H(win10 ? "#2B2B2B" : "#323232"); l.Stroke = H(win10 ? "#333333" : "#1D1D1D");
            l.Selected = H(win10 ? "#333333" : "#2D2D2D"); l.Hover = H(win10 ? "#2B2B2B" : "#2A2A2A");
            l.Text = H("#FFFFFF"); l.Secondary = H("#C8C8C8"); l.Tertiary = H("#9D9D9D");
            l.ButtonFill = H(win10 ? "#333333" : "#2D2D2D"); l.ButtonHover = H(win10 ? "#454545" : "#323232"); l.ButtonStroke = H(win10 ? "#333333" : "#3A3A3A");
            l.ToggleOff = H("#9D9D9D"); l.ToggleKnobOff = H(win10 ? "#FFFFFF" : "#CFCFCF"); l.ToggleKnobOn = H(win10 ? "#FFFFFF" : "#000000");
            l.ThumbHex = "#5A5A5A"; l.ThumbHoverHex = "#8A8A8A"; l.TipFillHex = "#2C2C2C"; l.TipLineHex = "#3E3E3E";
        }
        else
        {
            l.Bg = H(win10 ? "#FFFFFF" : "#F3F3F3"); l.Nav = H(win10 ? "#F2F2F2" : "#EEEEEE");
            l.Card = H(win10 ? "#F2F2F2" : "#FBFBFB"); l.CardHover = H(win10 ? "#E6E6E6" : "#F6F6F6"); l.Stroke = H(win10 ? "#E6E6E6" : "#E5E5E5");
            l.Selected = H(win10 ? "#DADADA" : "#EAEAEA"); l.Hover = H(win10 ? "#E6E6E6" : "#EDEDED");
            l.Text = H("#1B1B1B"); l.Secondary = H("#5D5D5D"); l.Tertiary = H("#8A8A8A");
            l.ButtonFill = H(win10 ? "#CCCCCC" : "#FBFBFB"); l.ButtonHover = H(win10 ? "#B8B8B8" : "#F6F6F6"); l.ButtonStroke = H(win10 ? "#CCCCCC" : "#E0E0E0");
            l.ToggleOff = H("#5D5D5D"); l.ToggleKnobOff = H("#5D5D5D"); l.ToggleKnobOn = H("#FFFFFF");
            l.ThumbHex = "#A0A0A0"; l.ThumbHoverHex = "#707070"; l.TipFillHex = "#F9F9F9"; l.TipLineHex = "#E0E0E0";
        }
        return l;
    }

    /// <summary>Seitenleisten-Ausgabe-Farben (Ui/Kit.xaml) für die Seitenleisten-Ausgabe.</summary>
    static Look Sidebar() => new()
    {
        Dark = true, Radius = 8, ButtonRadius = 10, AccentColor = Color.FromRgb(0xF0, 0x88, 0x3E),
        Bg = H("#16171B"), Nav = H("#16171B"), Card = H("#1F2126"), CardHover = H("#292C32"), Stroke = H("#2F3239"), Selected = H("#292C32"), Hover = H("#30343B"),
        Text = H("#E8EAEE"), Secondary = H("#A5ABB6"), Tertiary = H("#7E8491"), Accent = H("#F0883E"), AccentHover = H("#D46C26"), AccentText = H("#FFFFFF"), AccentSoft = H("#3A2516"),
        ButtonFill = H("#2A2624"), ButtonHover = H("#30343B"), ButtonStroke = H("#4A3A30"), ToggleOff = H("#4A4D54"), ToggleKnobOff = H("#D0D0D0"), ToggleKnobOn = H("#FFFFFF"),
        ThumbHex = "#4A4D54", ThumbHoverHex = "#6A6E77", TipFillHex = "#24262B", TipLineHex = "#3A3D44",
        Font = new FontFamily("Segoe UI Variable Text, Segoe UI"), Display = new FontFamily("Segoe UI Variable Display, Segoe UI"), Icons = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
    };

    /// <summary>Windows-Vorgabe „App-Modus“ (nur lesen).</summary>
    static bool SystemDark()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("AppsUseLightTheme") is not int v || v == 0;
        }
        catch { return true; }
    }

    /// <summary>Akzentfarbe von Windows (nur lesen, ABGR in DWM\AccentColor).</summary>
    static Color? SystemAccent()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (k?.GetValue("AccentColor") is int abgr)
            {
                uint v = unchecked((uint)abgr);
                return Color.FromRgb((byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)((v >> 16) & 0xFF));
            }
        }
        catch { }
        return null;
    }

    static Color Mix(Color a, Color b, double t) => Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
    static double Luma(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255;
    static Brush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
    static Brush H(string hex) => B((Color)ColorConverter.ConvertFromString(hex));
}
