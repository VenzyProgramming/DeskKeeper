using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace DeskKeeper;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        Store.Migrate();
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        if (args.Length == 2 && args[0] == "--check")
        {
            int code = 1;
            app.Startup += async (_, _) => { code = await Check.Run(args[1]); app.Shutdown(code); };
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Run();
            return code;
        }
        if (args.Length >= 2 && args[0] == "--promo")
        {
            // Werbevideo als Bildfolge (nur mit Vorführdaten)
            if (!Demo.On) { Console.WriteLine("DESKKEEPER_DEMO=1 setzen"); return 2; }
            Loc.Use("en");
            int result = 1;
            app.Startup += (_, _) => { try { result = Promo.Run(args[1], args.Length > 2 ? args[2] : null); } catch (Exception e) { Console.WriteLine(e); } app.Shutdown(); };
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Run();
            return result;
        }
        if (args.Length == 1 && args[0] == "--probe")
        {
            // Nur lesen: Schreibtisch öffnen, Symbole zählen, nichts bewegen.
            try
            {
                using var desktop = new ShellDesktop();
                Console.WriteLine($"ok symbole={desktop.Icons.Count} groesse={desktop.IconSize:0} autoanordnen={desktop.AutoArrange} raster={desktop.SnapInfo}");
                foreach (var m in Monitors.Current()) Console.WriteLine($"bildschirm {m.Number} {(m.Primary ? "haupt " : "")}{m.Kind} {m.Model} {m.Diagonal:0.0}in {m.Bounds.Width}x{m.Bounds.Height} @{m.Bounds.X},{m.Bounds.Y} {m.Id}");
                Console.WriteLine("anordnung " + Store.Describe(Store.ScreenKey()) + " · abstand " + desktop.Spacing);
                return 0;
            }
            catch (Exception error) { Console.WriteLine(error.ToString()); return 1; }
        }
        // Fehler nie still schließen: protokollieren, melden, weiterlaufen
        app.DispatcherUnhandledException += (_, e) => { Report(e.Exception); e.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report(e.ExceptionObject as Exception);
        if (args.Length == 2 && args[0] == "--ui-test")
        {
            // Oberfläche außerhalb des Bildschirms öffnen und Auswahl durchschalten (ohne Maus/Tastatur)
            string log = args[1];
            // Prüfung arbeitet auf einer Kopie der Daten neben dem Protokoll, nie auf den echten Daten;
            // zwei Beispiel-Schnappschüsse aus dem gemerkten Stand
            string copy = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(log))!, "daten-kopie");
            try { if (System.IO.Directory.Exists(copy)) System.IO.Directory.Delete(copy, true); } catch { }
            if (System.IO.Directory.Exists(Store.Folder))
                foreach (var f in System.IO.Directory.EnumerateFiles(Store.Folder, "*", System.IO.SearchOption.AllDirectories))
                {
                    var t = System.IO.Path.Combine(copy, System.IO.Path.GetRelativePath(Store.Folder, f));
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(t)!);
                    System.IO.File.Copy(f, t, true);
                }
            Store.Folder = copy;
            if (Demo.On) { try { System.IO.Directory.Delete(copy, true); } catch { } System.IO.Directory.CreateDirectory(copy); Demo.Seed(); }
            Loc.Use(Environment.GetEnvironmentVariable("DESKKEEPER_LANG") ?? Store.LoadOptions().Language);
            if (Store.LoadAuto(Store.ScreenKey()) is { } seed) { Store.SaveSnapshot(Store.ScreenKey(), seed); }
            app.Startup += (_, _) =>
            {
                var w = Edition.Create(null, true);
                try
                {
                    ((Window)w).Show();
                    w.UiTest(System.IO.Path.ChangeExtension(log, ".png"));
                    System.IO.File.WriteAllText(log, "ok");
                }
                catch (Exception error) { System.IO.File.WriteAllText(log, error.ToString()); }
                try { System.IO.Directory.Delete(copy, true); } catch { }   // Datenkopie der Prüfung wieder entfernen
                app.Shutdown();
            };
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Run();
            return 0;
        }
        using var single = Single.Claim(out bool first);
        if (!first) { Single.Signal(); return 0; }
        // Hintergrundbetrieb: Infobereich + Hüter; ohne --tray fährt die Karte gleich herunter
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var options = Store.LoadOptions();
        Loc.Use(options.Language);
        bool autostarted = args.Contains("--autostart");
        bool quiet = args.Contains("--tray") || options.StartMinimized;
        app.Startup += (_, _) =>
        {
            var host = new Background(app, autostarted);
            Single.Listen(() => app.Dispatcher.BeginInvoke(host.ShowCard));
            app.SessionEnding += (_, _) => { host.SaveOnShutdown(); host.Dispose(); };
            if (!quiet) host.ShowCard();
        };
        app.Run();
        return 0;
    }

    internal static void Report(Exception? error)
    {
        if (error is null) return;
        try { System.IO.Directory.CreateDirectory(Store.Folder); System.IO.File.AppendAllText(System.IO.Path.Combine(Store.Folder, "protokoll.txt"), DateTime.Now.ToString("s") + " " + error + Environment.NewLine); } catch { }
    }
}
