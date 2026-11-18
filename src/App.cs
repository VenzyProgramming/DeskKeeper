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
        if (args.Length == 1 && args[0] == "--check-stage-z")
        {
            // Lage des (leeren) Szenenfensters in der Fensterreihenfolge – keine Symbolbewegung
            app.Startup += (_, _) => { var all = System.Windows.Forms.Screen.AllScreens.Select(s => new Rect(s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height)).Aggregate(Rect.Union); Console.WriteLine(Stage.ZOrderProbe(all)); app.Shutdown(); };
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Run();
            return 0;
        }
        if (args.Length >= 1 && args[0] is "--sortieren" or "--sortieren-vorschau")
        {
            // Nach Kategorien ordnen; die Vorschau zeigt nur die Zuordnung und bewegt nichts. Vorher Schnappschuss.
            bool apply = args[0] == "--sortieren";
            try
            {
                string key = Store.ScreenKey();
                System.Collections.Generic.Dictionary<string, int[]> layout; System.Collections.Generic.Dictionary<string, string> kinds; int moved;
                using (var desktop = new ShellDesktop())
                {
                    if (desktop.AutoArrange) { Console.WriteLine("abgelehnt: automatisch anordnen ist an"); return 1; }
                    (layout, kinds, moved) = Sorter.Prepare(desktop);
                    var names = desktop.Icons.ToDictionary(i => i.Id, i => i.Name, StringComparer.OrdinalIgnoreCase);
                    foreach (var g in kinds.GroupBy(k => k.Value).OrderBy(g => g.Key))
                        Console.WriteLine(g.Key + ": " + string.Join(", ", g.Select(k => names[k.Key] + " " + string.Join(",", layout[k.Key]))));
                    Console.WriteLine($"zu bewegen: {moved} von {kinds.Count}");
                    if (apply && moved > 0) Store.SaveSnapshot(key, desktop.Snapshot());
                }
                if (apply && moved > 0)
                {
                    using (var desktop = new ShellDesktop()) desktop.RestoreSnapshot(layout);
                    Store.SaveProfile(key, layout);
                    new RealWorld().Save(key, layout);
                    Console.WriteLine("geordnet; vorheriger Stand als Schnappschuss gesichert");
                }
                return 0;
            }
            catch (Exception error) { Console.WriteLine(error.ToString()); return 1; }
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
                Console.WriteLine($"symbolmitte geschätzt {desktop.EstimatedAnchor.X:0},{desktop.EstimatedAnchor.Y:0} · gemessen {(desktop.AnchorMeasured ? $"{desktop.Anchor.X:0},{desktop.Anchor.Y:0}" : "nein")} · bildgröße {desktop.ViewIconSize()}");
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
        // Begleitprogramm der Seitenleisten-Ausgabe-Seitenleiste: kein eigenes Infobereich-Symbol, kein eigener Autostart,
        // endet mit dem Gastgeberprozess (--host <pid>)
        bool embedded = args.Contains("--embedded");
        int hostPid = Array.IndexOf(args, "--host") is int hi and >= 0 && hi + 1 < args.Length && int.TryParse(args[hi + 1], out int pid) ? pid : 0;
        bool quiet = args.Contains("--tray") || options.StartMinimized;
        app.Startup += (_, _) =>
        {
            var host = new Background(app, autostarted, embedded);
            if (hostPid > 0) host.FollowHost(hostPid);
            Single.Listen(() => app.Dispatcher.BeginInvoke(host.ShowCard));
            app.SessionEnding += (_, _) => { host.SaveOnShutdown(); host.Dispose(); };
            if (!quiet) host.ShowCard();
        };
        app.Run();
        return 0;
    }

    internal static void Report(Exception? error) => Fault.Report(error);
}
