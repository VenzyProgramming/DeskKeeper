using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace DeskKeeper;

/// <summary>Eigenständige Fassung: Symbol im Infobereich (eigenes Menü), Fenster auf Abruf; die Arbeit macht der Motor (Engine).</summary>
sealed class Background : IDisposable
{
    readonly Application app;
    readonly Forms.NotifyIcon tray = new();
    readonly Engine engine;
    IDeskWindow? card;
    bool exiting;
    public string? Recovered => engine.Recovered;
    public bool Embedded { get; }

    public Background(Application app, bool autostarted, bool embedded = false)
    {
        this.app = app;
        Embedded = embedded;
        engine = new Engine(app.Dispatcher, autostarted, ownThread: false);
        engine.Acted += what => card?.KeeperActed(what);
        engine.Changed += () => card?.Refresh();
        tray.Text = "DeskKeeper";
        tray.Icon = MakeIcon();
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) ShowCard(); else if (e.Button == Forms.MouseButtons.Right) TrayMenu.Show(this); };
        tray.Visible = !embedded;   // eingebettet: ein Gastgeber übernimmt die Bedienung
    }

    public void SaveAll() => engine.SaveAll();
    public void SaveNowManual() => engine.SaveNowManual();
    public void RestoreNow() => engine.RestoreNow();
    internal void SetWatch(bool on) => engine.SetWatch(on);
    internal void SetAutostart(bool on) { Autostart.Enabled = on; card?.Refresh(); }

    public void ShowCard()
    {
        card ??= Edition.Create(this, false);
        card.Open();
    }

    /// <summary>Design oder Sprache geändert: Fenster neu aufbauen.</summary>
    public void Rebuild()
    {
        Loc.Use(Store.LoadOptions().Language);
        if (card is not null) { card.AllowClose = true; ((Window)card).Close(); card = null; }
        ShowCard();
    }

    public void Exit()
    {
        if (exiting) return;
        exiting = true;
        engine.SaveOnExit();
        if (card is not null) card.AllowClose = true;
        Dispose();
        app.Shutdown();
    }

    public void SaveOnShutdown() => engine.SaveOnShutdown();

    /// <summary>Eingebettet: beenden, sobald der Gastgeber endet.</summary>
    public void FollowHost(int pid)
    {
        try
        {
            var p = Process.GetProcessById(pid);
            p.EnableRaisingEvents = true;
            p.Exited += (_, _) => app.Dispatcher.BeginInvoke(Exit);
            if (p.HasExited) Exit();
        }
        catch (ArgumentException) { Exit(); }   // Gastgeber schon weg
    }

    public void Dispose()
    {
        engine.Dispose();
        tray.Visible = false;
        tray.Dispose();
    }

    /// <summary>Symbol: Roboter-Glyphe im orangen Quadrat wie der Kartenkopf.</summary>
    static System.Drawing.Icon MakeIcon()
    {
        const int size = 32;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var fill = new SolidColorBrush(Color.FromRgb(0xC2, 0x5E, 0x1A));
            dc.DrawRoundedRectangle(fill, null, new Rect(1, 1, size - 2, size - 2), 8, 8);
            var glyph = new FormattedText("", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 19, Brushes.White, 1);
            dc.DrawText(glyph, new Point((size - glyph.Width) / 2, (size - glyph.Height) / 2));
        }
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var png = new System.IO.MemoryStream(); encoder.Save(png);
        // ICO mit eingebettetem PNG
        using var ico = new System.IO.MemoryStream();
        using (var w = new System.IO.BinaryWriter(ico, System.Text.Encoding.UTF8, true))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)1);
            w.Write((byte)size); w.Write((byte)size); w.Write((byte)0); w.Write((byte)0);
            w.Write((short)1); w.Write((short)32); w.Write((int)png.Length); w.Write(22);
            w.Write(png.ToArray());
        }
        ico.Position = 0;
        return new System.Drawing.Icon(ico);
    }
}

/// <summary>Start mit Windows über HKCU\…\Run (nur auf ausdrücklichen Schalter, standardmäßig aus).</summary>
static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", Name = "DeskKeeper", OldName = "Dubina";
    static string Command => $"\"{Environment.ProcessPath}\" --tray --autostart";
    public static bool Enabled
    {
        get { try { using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey); return k?.GetValue(Name) is string s && s.Length > 0; } catch { return false; } }
        set
        {
            try
            {
                using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey);
                if (value) k.SetValue(Name, Command); else k.DeleteValue(Name, false);
                k.DeleteValue(OldName, false);   // Eintrag aus der Zeit vor der Umbenennung
            }
            catch (Exception e) { Fault.Report(e); }
        }
    }
}

/// <summary>Zweiter Start: zeigt die Karte der laufenden Instanz.</summary>
static class Single
{
    const string MutexName = "DeskKeeper.Einzeln", SignalName = "DeskKeeper.Zeigen";
    public static Mutex? Claim(out bool first) { var m = new Mutex(true, MutexName, out first); return m; }
    public static void Signal() { try { using var e = EventWaitHandle.OpenExisting(SignalName); e.Set(); } catch { } }
    public static void Listen(Action show)
    {
        var e = new EventWaitHandle(false, EventResetMode.AutoReset, SignalName);
        ThreadPool.RegisterWaitForSingleObject(e, (_, _) => show(), null, Timeout.Infinite, false);
    }
}
