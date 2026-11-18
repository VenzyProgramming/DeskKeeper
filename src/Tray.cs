using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace DeskKeeper;

/// <summary>Hintergrundbetrieb: Symbol im Infobereich (eigenes Menü statt Windows-Menü), Hüter der Anordnung,
/// Speichern bei Zeitplan/Herunterfahren/Benutzerwechsel/Beenden, Fenster auf Abruf.</summary>
sealed class Background : IDisposable
{
    readonly Application app;
    readonly Forms.NotifyIcon tray = new();
    readonly Keeper keeper = new(new RealWorld());
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(3) };
    readonly Stopwatch clock = Stopwatch.StartNew();
    DateTime lastAutoSnapshot = DateTime.Now;
    IDeskWindow? card;
    public string? Recovered { get; private set; }
    bool exiting;

    public Background(Application app, bool autostarted)
    {
        this.app = app;
        // Abgebrochene Szene (Absturz, Abmelden) zuerst zurückstellen, sonst bleibt der Hüter gesperrt
        Recovered = Runner.RecoverPending();
        var options = Store.LoadOptions();
        keeper.AutoRestore = keeper.AutoSave = options.Watch;
        keeper.RestoreOnStart = autostarted ? options.RestoreAtLogin : options.RestoreAtStart;
        keeper.Acted += what => card?.KeeperActed(what);

        tray.Text = "DeskKeeper";
        tray.Icon = MakeIcon();
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) ShowCard(); else if (e.Button == Forms.MouseButtons.Right) TrayMenu.Show(this); };
        tray.Visible = true;

        timer.Tick += (_, _) =>
        {
            try { keeper.Tick(clock.Elapsed.TotalSeconds); AutoSnapshotIfDue(); }
            catch (Exception e) { Program.Report(e); }
        };
        timer.Start();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplay;
        Microsoft.Win32.SystemEvents.SessionSwitch += OnSwitch;
    }

    void OnDisplay(object? sender, EventArgs e) => app.Dispatcher.BeginInvoke(() => keeper.DisplayChanged(clock.Elapsed.TotalSeconds));

    /// <summary>Benutzerwechsel, Sperren, Abmelden: Anordnung sichern (Einstellung „Beim Benutzerwechsel speichern“).</summary>
    void OnSwitch(object? sender, Microsoft.Win32.SessionSwitchEventArgs e)
    {
        if (e.Reason is Microsoft.Win32.SessionSwitchReason.ConsoleDisconnect or Microsoft.Win32.SessionSwitchReason.RemoteDisconnect
            or Microsoft.Win32.SessionSwitchReason.SessionLock or Microsoft.Win32.SessionSwitchReason.SessionLogoff && Store.LoadOptions().SaveOnUserSwitch)
            app.Dispatcher.BeginInvoke(() => SaveAll());
    }

    void AutoSnapshotIfDue()
    {
        int minutes = Store.LoadOptions().AutoSnapshotMinutes;
        if (minutes <= 0 || (DateTime.Now - lastAutoSnapshot).TotalMinutes < minutes || Runner.Busy || Store.HasPending) return;
        lastAutoSnapshot = DateTime.Now;
        try { using var d = new ShellDesktop(); if (Store.AutoSnapshot(Store.ScreenKey(), d.Snapshot())) card?.Refresh(); } catch { }
    }

    /// <summary>Jetzige Anordnung merken und als automatischen Schnappschuss ablegen (wenn sie sich unterscheidet).</summary>
    public void SaveAll()
    {
        try
        {
            keeper.SaveNow(clock.Elapsed.TotalSeconds);
            if (Runner.Busy || Store.HasPending) return;
            using var d = new ShellDesktop();
            Store.AutoSnapshot(Store.ScreenKey(), d.Snapshot());
        }
        catch (Exception e) { Program.Report(e); }
    }

    public void SaveNowManual()
    {
        try { using var d = new ShellDesktop(); Store.SaveSnapshot(Store.ScreenKey(), d.Snapshot()); } catch (Exception e) { Program.Report(e); }
        card?.Refresh();
    }

    /// <summary>Jüngsten gespeicherten Stand dieser Anordnung zurückstellen (Art laut Einstellung).</summary>
    public void RestoreNow()
    {
        if (Store.LoadProfile() is { } layout) RealWorld.Restore(layout);
        card?.Refresh();
    }

    internal void SetWatch(bool on)
    {
        keeper.AutoRestore = keeper.AutoSave = on;
        Store.SaveOptions(Store.LoadOptions() with { Watch = on });
        card?.Refresh();
    }

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
        if (Store.LoadOptions().SaveOnExit) SaveAll();
        if (card is not null) card.AllowClose = true;
        Dispose();
        app.Shutdown();
    }

    public void SaveOnShutdown() { if (Store.LoadOptions().SaveOnShutdown) SaveAll(); }

    public void Dispose()
    {
        timer.Stop();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplay;
        Microsoft.Win32.SystemEvents.SessionSwitch -= OnSwitch;
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
            catch (Exception e) { Program.Report(e); }
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
