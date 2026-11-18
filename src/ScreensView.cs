using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DeskKeeper;

/// <summary>
/// Öffentliche Ausgabe: die Bildschirme als echte Geräte gezeichnet – Rahmen, Bildfläche mit Hintergrundbild,
/// Taskleiste auf dem Hauptbildschirm, Desktopsymbole an ihren gemerkten Plätzen, Standfuß bzw. Notebook-Unterteil.
/// Größe und Lage zueinander maßstäblich wie in „Bildschirme neu anordnen“.
/// </summary>
sealed class ScreensView : FrameworkElement
{
    IReadOnlyList<MonitorInfo> monitors = [];
    IReadOnlyList<DeskIcon> icons = [];
    public Color AccentColor = Color.FromRgb(0x4C, 0xC2, 0xFF);

    public void Show(IReadOnlyList<MonitorInfo> screens, IReadOnlyList<DeskIcon> desk)
    {
        if (focus is not null && !screens.Any(m => m.Id == focus)) focus = null;
        monitors = screens; icons = desk; InvalidateVisual();
    }

    static readonly Brush Bezel = Frozen(new LinearGradientBrush(Color.FromRgb(0x2A, 0x2A, 0x2D), Color.FromRgb(0x0E, 0x0E, 0x10), 90));
    static readonly Brush Stand = Frozen(new LinearGradientBrush(Color.FromRgb(0x3A, 0x3B, 0x3F), Color.FromRgb(0x1C, 0x1C, 0x1F), 90));
    static readonly Brush Deck = Frozen(new LinearGradientBrush(Color.FromRgb(0x4A, 0x4C, 0x52), Color.FromRgb(0x26, 0x27, 0x2B), 90));
    static readonly Brush Taskbar = Frozen(new SolidColorBrush(Color.FromArgb(0xD8, 0x1C, 0x1C, 0x22)));
    static readonly Brush TaskIcon = Frozen(new SolidColorBrush(Color.FromArgb(0xC0, 0xE6, 0xE8, 0xEE)));
    static readonly Brush IconTile = Frozen(new SolidColorBrush(Color.FromArgb(0xE8, 0xF4, 0xD7, 0x9C)));
    static readonly Brush IconLabel = Frozen(new SolidColorBrush(Color.FromArgb(0x90, 0xFF, 0xFF, 0xFF)));
    static readonly Brush Glass = Frozen(new LinearGradientBrush(new GradientStopCollection { new(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF), 0), new(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 0.45) }, new Point(0, 0), new Point(0.6, 1)));
    static readonly Pen Rim = new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0x48, 0x49, 0x4E))), 1).Freezed();
    static readonly Brush Shadow = Frozen(new SolidColorBrush(Color.FromArgb(0x50, 0, 0, 0))), NumberShadow = Frozen(new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0))),
        CameraDot = Frozen(new SolidColorBrush(Color.FromRgb(0x3A, 0x3D, 0x48))), PowerDot = Frozen(new SolidColorBrush(Color.FromRgb(0xE0, 0x40, 0x30)));
    static readonly Pen Dashed = new Pen(Frozen(new SolidColorBrush(Color.FromArgb(0xA0, 0x9D, 0x9D, 0x9D))), 1) { DashStyle = DashStyles.Dash }.Freezed();
    static readonly Brush HoverFill = Frozen(new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF))), CalloutFill = Frozen(new SolidColorBrush(Color.FromArgb(0xF2, 0x2B, 0x2B, 0x2E))),
        CalloutDim = Frozen(new SolidColorBrush(Color.FromRgb(0xB8, 0xBC, 0xC4)));
    static readonly Pen HoverPen = new Pen(Frozen(new SolidColorBrush(Color.FromArgb(0x90, 0xFF, 0xFF, 0xFF))), 1).Freezed(), CalloutPen = new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0x45, 0x46, 0x4B))), 1).Freezed();
    static readonly Typeface LabelFace = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    static readonly Typeface Display = new(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
        if (monitors.Count == 0 || w < 60) return;
        var all = monitors.Select(m => m.Bounds).Aggregate(Rect.Union);
        // Ränder; unten etwas mehr Platz für das Notebook-Unterteil
        double padX = 26, padTop = 16, padBottom = monitors.Any(m => m.Kind == DeviceKind.Laptop) ? 30 : 16;
        double s = Math.Min((w - 2 * padX) / all.Width, (h - padTop - padBottom) / all.Height);
        double ox = (w - all.Width * s) / 2 - all.X * s, oy = padTop + (h - padTop - padBottom - all.Height * s) / 2 - all.Y * s;
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var accent = new SolidColorBrush(AccentColor); accent.Freeze();
        layout.Clear(); hits.Clear();

        foreach (var m in monitors.OrderBy(m => m.Primary))
        {
            bool zoomed = focus is not null && focus == m.Id;
            if (focus is not null && !zoomed) continue;
            double scale = zoomed ? FitScale(m.Bounds, out ox, out oy) : s;
            if (!zoomed) { ox = (w - all.Width * s) / 2 - all.X * s; oy = padTop + (h - padTop - padBottom - all.Height * s) / 2 - all.Y * s; }
            var r = Screen(m, scale, ox, oy, out double frame, out double radius);
            layout.Add((m, r));
            var outer = new Rect(r.Left - frame, r.Top - frame, r.Width + 2 * frame, r.Height + 2 * frame);
            if (m.Kind == DeviceKind.TV) outer.Height += frame * 1.6;   // Fernseher: unten etwas breiterer Rand
            dc.DrawRoundedRectangle(Shadow, null, new Rect(outer.X + 2, outer.Y + 4, outer.Width, outer.Height), radius + frame, radius + frame);
            if (m.Kind == DeviceKind.Laptop)
            {
                // Notebook: flaches Unterteil mit Griffmulde direkt unter dem Bildschirm
                double lip = Math.Max(3, outer.Height * 0.05);
                var deck = new Rect(outer.Left - outer.Width * 0.05, outer.Bottom - 1, outer.Width * 1.1, lip);
                dc.DrawRoundedRectangle(Deck, Rim, deck, lip * 0.5, lip * 0.5);
                dc.DrawRoundedRectangle(Bezel, null, new Rect(deck.X + deck.Width * 0.42, deck.Y, deck.Width * 0.16, lip * 0.45), lip * 0.2, lip * 0.2);
            }
            dc.DrawRoundedRectangle(Bezel, Rim, outer, radius + frame, radius + frame);
            if (m.Kind is DeviceKind.Wireless or DeviceKind.Virtual)
                dc.DrawRoundedRectangle(null, Dashed, new Rect(outer.X - 3, outer.Y - 3, outer.Width + 6, outer.Height + 6), radius + frame + 3, radius + frame + 3);

            dc.PushClip(new RectangleGeometry(r, radius, radius));
            dc.DrawRectangle(Wallpaper(r), null, r);   // jedes Gerät zeigt das ganze Bild, wie bei Windows
            DrawIcons(dc, m, scale, ox, oy, dpi);
            if (m.Primary) DrawTaskbar(dc, r, accent);
            dc.DrawRectangle(Glass, null, r);
            dc.Pop();
            // Kamera: Handy als Pille im Bild, Tablet und Notebook als Punkt im Rand; Fernseher: Betriebslicht
            if (m.Kind == DeviceKind.Phone)
            {
                double pw = r.Width * 0.24, ph = Math.Max(2.5, r.Width * 0.055);
                dc.DrawRoundedRectangle(Brushes.Black, null, new Rect(r.Left + (r.Width - pw) / 2, r.Top + ph * 0.8, pw, ph), ph / 2, ph / 2);
            }
            else if (m.Kind is DeviceKind.Tablet or DeviceKind.Laptop)
                dc.DrawEllipse(CameraDot, null, new Point(r.Left + r.Width / 2, r.Top - frame / 2), Math.Max(1, frame * 0.22), Math.Max(1, frame * 0.22));
            else if (m.Kind == DeviceKind.TV)
                dc.DrawEllipse(PowerDot, null, new Point(outer.Right - frame * 2.5, outer.Bottom - frame * 0.9), Math.Max(0.8, frame * 0.25), Math.Max(0.8, frame * 0.25));
            if (m.Primary) dc.DrawRoundedRectangle(null, new Pen(accent, 2), new Rect(outer.X - 3.5, outer.Y - 3.5, outer.Width + 7, outer.Height + 7), radius + frame + 4, radius + frame + 4);

            // Nummer wie beim „Identifizieren“ (nicht in der Großansicht, dort zählen die Symbole)
            if (!zoomed)
            {
                double size = Math.Clamp(Math.Min(r.Height * 0.4, r.Width * 0.3), 14, 64);
                var number = Text(m.Number > 0 ? m.Number.ToString(CultureInfo.InvariantCulture) : "", size, Brushes.White, dpi);
                var at = new Point(r.Left + (r.Width - number.Width) / 2, r.Top + (r.Height - number.Height) / 2 - (m.Primary ? r.Height * 0.03 : 0));
                dc.DrawText(Text(number.Text, size, NumberShadow, dpi), new Point(at.X + 1.5, at.Y + 2));
                dc.DrawText(number, at);
            }
        }
        if (hover is not null) DrawCallout(dc, dpi);
    }

    /// <summary>Desktopsymbole: echtes Symbolbild an der gemerkten Mitte, darunter der Name, wenn genug Platz ist.</summary>
    void DrawIcons(DrawingContext dc, MonitorInfo m, double s, double ox, double oy, double dpi)
    {
        double cell = Math.Clamp(IconPixels * s, 2.4, 64);
        foreach (var icon in icons)
        {
            if (!m.Bounds.Contains(icon.Center)) continue;
            var p = new Point(ox + icon.Center.X * s, oy + icon.Center.Y * s);
            var box = new Rect(p.X - cell / 2, p.Y - cell / 2, cell, cell);
            hits.Add((icon, m, Rect.Inflate(box, Math.Max(2, cell * 0.2), Math.Max(2, cell * 0.2))));
            if (hover == icon) dc.DrawRoundedRectangle(HoverFill, HoverPen, Rect.Inflate(box, cell * 0.25 + 1.5, cell * 0.25 + 1.5), 3, 3);
            if (icon.Image is not null && cell >= 6) dc.DrawImage(icon.Image, box);
            else dc.DrawRoundedRectangle(IconTile, null, new Rect(p.X - cell / 2, p.Y - cell * 0.4, cell, cell * 0.8), cell * 0.18, cell * 0.18);
            if (cell >= 22 && icon.Name.Length > 0)
            {
                double fs = Math.Clamp(cell * 0.24, 8, 12);
                FormattedText Label(Brush b) => new(icon.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LabelFace, fs, b, dpi)
                { MaxTextWidth = cell * 1.8, MaxLineCount = 2, Trimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center };
                var at = new Point(p.X - cell * 0.9, box.Bottom + 1);
                dc.DrawText(Label(NumberShadow), new Point(at.X + 0.8, at.Y + 0.8));
                dc.DrawText(Label(Brushes.White), at);
            }
            else if (cell > 4 && icon.Image is null)
                dc.DrawRoundedRectangle(IconLabel, null, new Rect(p.X - cell * 0.6, p.Y + cell * 0.5, cell * 1.2, Math.Max(1, cell * 0.16)), 0.5, 0.5);
        }
    }

    // Symbol unter der Maus: Name und Lage auf seinem Gerät (Pixel ab linker oberer Ecke)
    readonly List<(DeskIcon Icon, MonitorInfo M, Rect R)> hits = [];
    DeskIcon? hover;
    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var p = e.GetPosition(this);
        var hit = hits.LastOrDefault(x => x.R.Contains(p)).Icon;
        Cursor = hit is not null || layout.Any(x => x.R.Contains(p)) ? System.Windows.Input.Cursors.Hand : null;
        if (hit == hover) return;
        hover = hit; InvalidateVisual();
    }
    protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e) { base.OnMouseLeave(e); if (hover is null) return; hover = null; InvalidateVisual(); }

    void DrawCallout(DrawingContext dc, double dpi)
    {
        var item = hits.FirstOrDefault(x => x.Icon == hover);
        if (item.Icon is null) return;
        var m = item.M;
        string where = $"{DeviceFacts.Name(m.Kind)} {m.Number} · x {item.Icon.Center.X - m.Bounds.X:0} · y {item.Icon.Center.Y - m.Bounds.Y:0}";
        var title = new FormattedText(item.Icon.Name.Length > 0 ? item.Icon.Name : "—", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Display, 13, Brushes.White, dpi) { MaxTextWidth = 260, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
        var sub = new FormattedText(where, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, LabelFace, 11.5, CalloutDim, dpi);
        double w = Math.Max(title.Width, sub.Width) + 22, h = title.Height + sub.Height + 16;
        var r = item.R;
        double x = Math.Clamp(r.Left + r.Width / 2 - w / 2, 4, ActualWidth - w - 4);
        double y = r.Top - h - 6; if (y < 4) y = r.Bottom + 6;
        dc.DrawRoundedRectangle(CalloutFill, CalloutPen, new Rect(x, y, w, h), 6, 6);
        dc.DrawText(title, new Point(x + 11, y + 7));
        dc.DrawText(sub, new Point(x + 11, y + 9 + title.Height));
    }

    void DrawTaskbar(DrawingContext dc, Rect r, Brush accent)
    {
        double th = Math.Max(4, r.Height * 0.055);
        var bar = new Rect(r.Left, r.Bottom - th, r.Width, th);
        dc.DrawRectangle(Taskbar, null, bar);
        double ic = th * 0.52, gap = ic * 0.55, n = 6, total = n * ic + (n - 1) * gap;
        for (int i = 0; i < n; i++)
            dc.DrawRoundedRectangle(i == 0 ? accent : TaskIcon, null, new Rect(bar.Left + (bar.Width - total) / 2 + i * (ic + gap), bar.Top + (th - ic) / 2, ic, ic), ic * 0.22, ic * 0.22);
        if (r.Width > 120)
            for (int i = 0; i < 3; i++) dc.DrawRoundedRectangle(TaskIcon, null, new Rect(bar.Right - th * (1.1 + i * 0.7), bar.Top + th * 0.38, th * 0.4, th * 0.24), 1, 1);
    }

    static FormattedText Text(string text, double size, Brush brush, double dpi) => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Display, size, brush, dpi);

    // Klick auf ein Gerät: Großansicht dieses Geräts mit Symbolen und Namen; erneuter Klick: wieder alle Geräte
    readonly List<(MonitorInfo M, Rect R)> layout = [];
    string? focus;
    public string? Focus => focus;
    public event Action<MonitorInfo?>? FocusChanged;
    protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        var p = e.GetPosition(this);
        var hit = layout.LastOrDefault(x => x.R.Contains(p)).M;
        Zoom(focus is not null ? null : hit);
    }
    /// <summary>Prüfung: Hinweis zum ersten Symbol zeigen (ohne Maus).</summary>
    public void HoverFirst() { UpdateLayout(); InvalidateVisual(); UpdateLayout(); hover = icons.FirstOrDefault(); InvalidateVisual(); }

    public void Zoom(MonitorInfo? m)
    {
        focus = m?.Id;
        InvalidateVisual();
        FocusChanged?.Invoke(m);
    }

    double FitScale(Rect bounds, out double ox, out double oy)
    {
        double w = ActualWidth, h = ActualHeight;
        double sc = Math.Min((w - 40) / bounds.Width, (h - 36) / bounds.Height);
        ox = (w - bounds.Width * sc) / 2 - bounds.X * sc;
        oy = (h - bounds.Height * sc) / 2 - bounds.Y * sc;
        return sc;
    }

    /// <summary>Symbolgröße auf dem echten Bildschirm in Pixeln (für den Maßstab der Symbolbilder).</summary>
    public double IconPixels = 48;

    /// <summary>Bildfläche ohne Rahmen; frame = Rahmenbreite, radius = Eckenrundung der Bildfläche (je Geräteart).</summary>
    static Rect Screen(MonitorInfo m, double s, double ox, double oy, out double frame, out double radius)
    {
        var r = new Rect(ox + m.Bounds.X * s, oy + m.Bounds.Y * s, m.Bounds.Width * s, m.Bounds.Height * s);
        double small = Math.Min(r.Width, r.Height);
        (double f, double rad) = m.Kind switch
        {
            DeviceKind.Phone => (0.055, 0.11),
            DeviceKind.Tablet => (0.06, 0.05),
            DeviceKind.TV => (0.014, 0.004),
            DeviceKind.Laptop => (0.03, 0.012),
            _ => (0.022, 0.008),
        };
        frame = Math.Clamp(small * f, 2, m.Kind is DeviceKind.Phone or DeviceKind.Tablet ? 14 : 7);
        radius = Math.Clamp(small * rad, 1.5, 26);
        double gap = frame + 6;   // Abstand zwischen anliegenden Geräten
        return new Rect(r.X + gap / 2, r.Y + gap / 2, Math.Max(8, r.Width - gap), Math.Max(8, r.Height - gap));
    }

    static Geometry Trapezoid(Point topCenter, double top, double bottom, double height)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(topCenter.X - top / 2, topCenter.Y), true, true);
            c.LineTo(new Point(topCenter.X + top / 2, topCenter.Y), true, true);
            c.LineTo(new Point(topCenter.X + bottom / 2, topCenter.Y + height), true, true);
            c.LineTo(new Point(topCenter.X - bottom / 2, topCenter.Y + height), true, true);
        }
        g.Freeze();
        return g;
    }

    /// <summary>Eigenes abstraktes Hintergrundbild: tiefes Blau-Violett mit weichen, hellen Blütenblättern.</summary>
    static Brush Wallpaper(Rect area)
    {
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            var unit = new Rect(0, 0, 1, 0.5625);
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0x0B, 0x16, 0x3A), Color.FromRgb(0x1A, 0x10, 0x3E), 30), null, unit);
            void Petal(double cx, double cy, double rx, double ry, Color c, double angle)
            {
                var b = new RadialGradientBrush(c, Color.FromArgb(0, c.R, c.G, c.B));
                dc.PushTransform(new RotateTransform(angle, cx, cy));
                dc.DrawEllipse(b, null, new Point(cx, cy), rx, ry);
                dc.Pop();
            }
            Petal(0.55, 0.33, 0.34, 0.16, Color.FromArgb(0xC0, 0x2E, 0x7B, 0xFF), -28);
            Petal(0.62, 0.30, 0.22, 0.09, Color.FromArgb(0xD0, 0x7A, 0xC4, 0xFF), -40);
            Petal(0.46, 0.38, 0.26, 0.10, Color.FromArgb(0xA0, 0x8E, 0x4D, 0xFF), 18);
            Petal(0.58, 0.36, 0.08, 0.05, Color.FromArgb(0xE0, 0xE6, 0xF3, 0xFF), -30);
            Petal(0.18, 0.48, 0.30, 0.14, Color.FromArgb(0x60, 0x3A, 0x2E, 0xA8), 8);
        }
        group.Freeze();
        var brush = new DrawingBrush(group) { Stretch = Stretch.UniformToFill, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, 1, 0.5625),
            ViewportUnits = BrushMappingMode.Absolute, Viewport = area };
        brush.Freeze();
        return brush;
    }

    static Brush Frozen(Brush b) { b.Freeze(); return b; }
}

static class FreezeExtensions
{
    public static Pen Freezed(this Pen p) { p.Freeze(); return p; }
}

/// <summary>Ein Desktopsymbol für die Ansicht: Mitte in Bildschirmpixeln, Name, Symbolbild (falls geladen).</summary>
record DeskIcon(Point Center, string Name, ImageSource? Image);
