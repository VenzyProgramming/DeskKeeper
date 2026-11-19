using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace DeskKeeper;

/// <summary>
/// Motor von DeskKeeper ohne Oberfläche: Hüter der Anordnung (Abfrage alle 3 s), Bildschirmwechsel, Benutzerwechsel,
/// automatische Schnappschüsse, Speichern bei Herunterfahren und Beenden. Wird von der eigenständigen Fassung (Background)
/// und von Seitenleisten-Ausgabe gleichermaßen genutzt. ownThread: Abfragen laufen auf einem eigenen STA-Thread, damit der
/// Gastgeber (z. B. die Seitenleisten-Ausgabe-Seitenleiste) beim Zugriff auf Explorer nicht stockt; Meldungen kommen auf dem UI-Thread an.
/// </summary>
sealed class Engine : IDisposable
{
    readonly Dispatcher ui;
    readonly Keeper keeper = new(new RealWorld());
    readonly Stopwatch clock = Stopwatch.StartNew();
    Dispatcher? worker;
    DispatcherTimer? timer;
    DateTime lastAutoSnapshot = DateTime.Now;
    bool disposed;

    /// <summary>Meldung des Hüters (restored, saved, first) – auf dem UI-Thread.</summary>
    public event Action<string>? Acted;
    /// <summary>Daten haben sich geändert (Schnappschuss, Einstellung) – auf dem UI-Thread.</summary>
    public event Action? Changed;
    public string? Recovered { get; }

    public Engine(Dispatcher ui, bool autostarted, bool ownThread)
    {
        this.ui = ui;
        // Abgebrochene Szene (Absturz, Abmelden) zuerst zurückstellen, sonst bleibt der Hüter gesperrt
        Recovered = Runner.RecoverPending();
        var options = Store.LoadOptions();
        keeper.AutoRestore = keeper.AutoSave = options.Watch;
        keeper.RestoreOnStart = autostarted ? options.RestoreAtLogin : options.RestoreAtStart;
        keeper.Acted += what =>
        {
            Fault.Note($"Hüter: {what} · {keeper.LastDetail}");   // nachvollziehbar ohne Fernsteuerung (Echttest Monitor aus/an)
            ui.BeginInvoke(() => Acted?.Invoke(what));
        };

        if (ownThread)
        {
            using var ready = new ManualResetEventSlim();
            var thread = new Thread(() =>
            {
                worker = Dispatcher.CurrentDispatcher;
                StartTimer();
                ready.Set();
                Dispatcher.Run();
            }) { IsBackground = true, Name = "DeskKeeper-Hüter" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.Wait();
        }
        else { worker = ui; StartTimer(); }
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplay;
        Microsoft.Win32.SystemEvents.SessionSwitch += OnSwitch;
    }

    void StartTimer()
    {
        timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) =>
        {
            try { keeper.Tick(clock.Elapsed.TotalSeconds); AutoSnapshotIfDue(); }
            catch (Exception e) { Fault.Report(e); }
        };
        timer.Start();
    }

    /// <summary>Auf dem Hüter-Thread ausführen (wartet, bis es fertig ist).</summary>
    void OnWorker(Action action) { if (worker is null || disposed) return; if (worker.CheckAccess()) action(); else worker.Invoke(action); }

    void OnDisplay(object? sender, EventArgs e) => worker?.BeginInvoke(() => keeper.DisplayChanged(clock.Elapsed.TotalSeconds));

    /// <summary>Benutzerwechsel, Sperren, Abmelden: Anordnung sichern (Einstellung „Beim Benutzerwechsel speichern“).</summary>
    void OnSwitch(object? sender, Microsoft.Win32.SessionSwitchEventArgs e)
    {
        if (e.Reason is Microsoft.Win32.SessionSwitchReason.ConsoleDisconnect or Microsoft.Win32.SessionSwitchReason.RemoteDisconnect
            or Microsoft.Win32.SessionSwitchReason.SessionLock or Microsoft.Win32.SessionSwitchReason.SessionLogoff && Store.LoadOptions().SaveOnUserSwitch)
            worker?.BeginInvoke(SaveAllHere);
    }

    void AutoSnapshotIfDue()
    {
        int minutes = Store.LoadOptions().AutoSnapshotMinutes;
        if (minutes <= 0 || (DateTime.Now - lastAutoSnapshot).TotalMinutes < minutes || Runner.Busy || Store.HasPending || DesktopLock.RobotActive) return;
        lastAutoSnapshot = DateTime.Now;
        try { using var d = new ShellDesktop(); if (Store.AutoSnapshot(Store.ScreenKey(), d.Snapshot())) ui.BeginInvoke(() => Changed?.Invoke()); } catch { }
    }

    void SaveAllHere()
    {
        try
        {
            keeper.SaveNow(clock.Elapsed.TotalSeconds);
            if (Runner.Busy || Store.HasPending || DesktopLock.RobotActive) return;
            using var d = new ShellDesktop();
            Store.AutoSnapshot(Store.ScreenKey(), d.Snapshot());
        }
        catch (Exception e) { Fault.Report(e); }
    }

    /// <summary>Jetzige Anordnung merken und als automatischen Schnappschuss ablegen (wenn sie sich unterscheidet).</summary>
    public void SaveAll() => OnWorker(SaveAllHere);

    public void SaveNowManual()
    {
        try { using var d = new ShellDesktop(); Store.SaveSnapshot(Store.ScreenKey(), d.Snapshot()); } catch (Exception e) { Fault.Report(e); }
        Changed?.Invoke();
    }

    /// <summary>Jüngsten gespeicherten Stand dieser Anordnung zurückstellen (Art laut Einstellung).</summary>
    public void RestoreNow()
    {
        if (Store.LoadProfile() is { } layout) RealWorld.Restore(layout);
        Changed?.Invoke();
    }

    public void SetWatch(bool on)
    {
        OnWorker(() => keeper.AutoRestore = keeper.AutoSave = on);
        Store.SaveOptions(Store.LoadOptions() with { Watch = on });
        Changed?.Invoke();
    }

    public void SaveOnShutdown() { if (Store.LoadOptions().SaveOnShutdown) SaveAll(); }
    public void SaveOnExit() { if (Store.LoadOptions().SaveOnExit) SaveAll(); }

    public void Dispose()
    {
        if (disposed) return;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplay;
        Microsoft.Win32.SystemEvents.SessionSwitch -= OnSwitch;
        OnWorker(() => timer?.Stop());
        disposed = true;
        if (worker is not null && worker != ui) worker.BeginInvokeShutdown(DispatcherPriority.Normal);
    }
}

/// <summary>Fehler nie still schlucken: ins Protokoll unter %LOCALAPPDATA%\DeskKeeper schreiben.</summary>
static class Fault
{
    /// <summary>Ablaufnotiz (Szenen), damit sich Probleme ohne Fernsteuerung nachvollziehen lassen.</summary>
    public static void Note(string text) { try { System.IO.Directory.CreateDirectory(Store.Folder); System.IO.File.AppendAllText(System.IO.Path.Combine(Store.Folder, "protokoll.txt"), DateTime.Now.ToString("s") + " " + text + Environment.NewLine); } catch { } }

    public static void Report(Exception? error)
    {
        if (error is null) return;
        try { System.IO.Directory.CreateDirectory(Store.Folder); System.IO.File.AppendAllText(System.IO.Path.Combine(Store.Folder, "protokoll.txt"), DateTime.Now.ToString("s") + " " + error + Environment.NewLine); } catch { }
    }
}
