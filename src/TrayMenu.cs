using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DeskKeeper;

/// <summary>Eigenes Menü für das Infobereich-Symbol (statt des Windows-Kontextmenüs): dunkle, abgerundete Karte
/// mit Einträgen und Schaltern; schließt, sobald man daneben klickt.</summary>
sealed class TrayMenu : Window
{
    static TrayMenu? open;

    public static void Show(Background host)
    {
        open?.Close();
        open = new TrayMenu(host);
        open.Show();
        open.Activate();
    }

    TrayMenu(Background host)
    {
        var look = Look.Current;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; Topmost = true; SizeToContent = SizeToContent.WidthAndHeight;
        FontFamily = look.Font; FontSize = 13.5;
        var stack = new StackPanel { Margin = new Thickness(4) };
        stack.Children.Add(Item("", Loc.T("tray.open"), () => host.ShowCard()));
        stack.Children.Add(Item("", Loc.T("tray.save"), host.SaveNowManual));
        stack.Children.Add(Item("", Loc.T("tray.restore"), host.RestoreNow));
        stack.Children.Add(Separator(look));
        stack.Children.Add(Check(Loc.T("s.watch"), Store.LoadOptions().Watch, on => host.SetWatch(on)));
        stack.Children.Add(Check(Loc.T("s.autostart"), Autostart.Enabled, on => host.SetAutostart(on)));
        stack.Children.Add(Separator(look));
        stack.Children.Add(Item("", Loc.T("tray.exit"), host.Exit));
        Content = new Border
        {
            Child = stack, Background = look.Card, BorderBrush = look.Stroke, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(look.Radius + 2),
            Margin = new Thickness(10), Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = 0.4, Color = Colors.Black },
            MinWidth = 260,
        };
        Foreground = look.Text;
        Deactivated += (_, _) => Close();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Closed += (_, _) => { if (open == this) open = null; };
        Loaded += (_, _) =>
        {
            // an der Maus, innerhalb des Arbeitsbereichs
            GetCursorPos(out var p);
            double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            var work = SystemParameters.WorkArea;
            double x = p.X / scale - ActualWidth + 12, y = p.Y / scale - ActualHeight + 12;
            Left = Math.Clamp(x, work.Left, work.Right - ActualWidth);
            Top = Math.Clamp(y, work.Top, work.Bottom - ActualHeight);
        };
    }

    Border Item(string glyph, string text, Action click)
    {
        var look = Look.Current;
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new TextBlock { Text = glyph, FontFamily = look.Icons, FontSize = 14, Width = 30, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        var b = new Border { Child = row, Padding = new Thickness(12, 8, 18, 8), CornerRadius = new CornerRadius(look.Radius), Background = Brushes.Transparent, Cursor = Cursors.Hand };
        b.MouseEnter += (_, _) => b.Background = look.Hover; b.MouseLeave += (_, _) => b.Background = Brushes.Transparent;
        b.MouseLeftButtonUp += (_, _) => { Close(); click(); };
        return b;
    }

    Border Check(string text, bool on, Action<bool> change)
    {
        var look = Look.Current;
        var mark = new TextBlock { Text = on ? "" : "", FontFamily = look.Icons, FontSize = 13, Width = 30, Foreground = look.Accent, VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(mark); row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        var b = new Border { Child = row, Padding = new Thickness(12, 8, 18, 8), CornerRadius = new CornerRadius(look.Radius), Background = Brushes.Transparent, Cursor = Cursors.Hand };
        b.MouseEnter += (_, _) => b.Background = look.Hover; b.MouseLeave += (_, _) => b.Background = Brushes.Transparent;
        b.MouseLeftButtonUp += (_, _) => { on = !on; mark.Text = on ? "" : ""; change(on); };
        return b;
    }

    static Border Separator(Look look) => new() { Height = 1, Background = look.Stroke, Margin = new Thickness(8, 4, 8, 4) };

    [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] static extern bool GetCursorPos(out NativePoint p);
}
