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
        if (args.Length >= 3 && args[0] == "--szene") return Check.SceneFrames(args[1], args[2], args.Skip(3).Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToList());
        if (args.Length == 2 && args[0] == "--modelle") { Check.Models(args[1]); return 0; }   // Einzelbilder der Modelle (nur Lesen)
        if (args.Length == 2 && args[0] == "--check")
        {
            int code = 1;
            app.Startup += async (_, _) => { code = await Check.Run(args[1]); app.Shutdown(code); };
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Run();
            return code;
        }
        if (args.Length == 3 && args[0] == "--story")
        {
            // erzählte Story-Folge im Hochformat (Zeitleiste aus werkzeuge/story_tts.py), nur mit Vorführdaten
            if (!Demo.On) { Console.WriteLine("DESKKEEPER_DEMO=1 setzen"); return 2; }
            int result = 1;
            app.Startup += (_, _) => { try { result = Story.Run(args[1], args[2]); } catch (Exception e) { Console.WriteLine(e); } app.Shutdown(); };
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Run();
            return result;
        }
        if (args.Length >= 2 && args[0] == "--shorts")
        {
            // Short-Serie: ein Hochformat-Video je Fahrzeug, Breitbild-Desktop in der Mitte, nur mit Vorführdaten
            if (!Demo.On) { Console.WriteLine("DESKKEEPER_DEMO=1 setzen"); return 2; }
            int result = 1;
            app.Startup += (_, _) => { try { result = Shorts.Run(args[1], args.Skip(2).ToList()); } catch (Exception e) { Console.WriteLine(e); } app.Shutdown(); };
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Run();
            return result;
        }
        if (args.Length == 2 && args[0] == "--reel")
        {
            // Hochformat-Kurzvideo (TikTok, Reels, Shorts) als Bildfolge, nur mit Vorführdaten
            if (!Demo.On) { Console.WriteLine("DESKKEEPER_DEMO=1 setzen"); return 2; }
            int result = 1;
            app.Startup += (_, _) => { try { result = Reel.Run(args[1]); } catch (Exception e) { Console.WriteLine(e); } app.Shutdown(); };
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Run();
            return result;
        }
        if (args.Length == 2 && args[0] == "--duell-film") return Promo.DuelFilm(args[1]);
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
        if (args.Length == 2 && args[0] == "--check-stage-bild")
        {
            // Sichtbarkeit: Bühne mit einer Tu-160-Szene aus Testsymbolen 1,5 s zeigen (Standbild t=2 s), Bildschirmfoto davon und
            // ohne Bühne vergleichen. Kein echtes Symbol wird bewegt, kein Fenster minimiert.
            app.Startup += async (_, _) =>
            {
                var scr = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
                var all = new Rect(scr.X, scr.Y, scr.Width, scr.Height);
                var icons = Enumerable.Range(0, 12).Select(i => new IconInfo("t" + i, "Test " + i, new Point(scr.Width * 0.3 + (i % 6) * 110, scr.Height * 0.4 + (i / 6) * 110))).ToList();
                var scene = new Scene(icons, icons.ToDictionary(i => i.Id, i => i.Center), 48, [all], all, true, 64, tu160: true);
                string Shot(string name) { using var bmp = new System.Drawing.Bitmap(scr.Width, scr.Height); using (var g = System.Drawing.Graphics.FromImage(bmp)) g.CopyFromScreen(scr.X, scr.Y, 0, 0, bmp.Size); string f = System.IO.Path.Combine(args[1], name); bmp.Save(f); return f; }
                System.IO.Directory.CreateDirectory(args[1]);
                Shot("ohne.png");
                var stage = new Stage(all, scene); stage.Show(); stage.SendToBottom(); stage.Seek(2.0);
                await System.Threading.Tasks.Task.Delay(1500);
                Shot("mit.png");
                Console.WriteLine("bühne sichtbar=" + stage.IsVisible + " " + Stage.ZOrderNow(stage));
                stage.Close(); app.Shutdown();
            };
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.Run();
            return 0;
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
            // optional: links freizuhaltende Breite in Pixeln (Desktop-Anzeigen)
            if (args.Length >= 2 && double.TryParse(args[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double reserve)) Sorter.ReserveLeft = reserve;
            if (args.Length >= 3 && double.TryParse(args[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double reserveHeight)) Sorter.ReserveHeight = reserveHeight;
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
                    var nowPos = desktop.Snapshot();
                    foreach (var (id, item) in layout) if (nowPos.TryGetValue(id, out var q) && (q[0] != item[0] || q[1] != item[1])) Console.WriteLine($"  {(names.TryGetValue(id, out var nm) ? nm : id)}: {q[0]},{q[1]} → {item[0]},{item[1]}");
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
        if (args.Length == 1 && args[0] == "--zeit")
        {
            // Nur lesen: Dauer der Teile eines Hüter-Durchgangs (Bildschirmschlüssel, Symbole lesen) messen
            var w = new RealWorld();
            for (int i = 0; i < 4; i++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew(); string k = w.Key(); double key = sw.Elapsed.TotalMilliseconds;
                sw.Restart(); var read = w.Read(); double rd = sw.Elapsed.TotalMilliseconds;
                sw.Restart(); var mon = Monitors.Current(); double m = sw.Elapsed.TotalMilliseconds;
                sw.Restart(); using (var d = new ShellDesktop()) { } double sd = sw.Elapsed.TotalMilliseconds;
                Console.WriteLine($"schlüssel {key:0} ms · lesen {rd:0} ms ({read?.Count}) · bildschirme {m:0} ms · desktop {sd:0} ms");
            }
            return 0;
        }
        if (args.Length == 2 && args[0] == "--lage-bild")
        {
            // Nur lesen: jede gemerkte Anordnung wie in der Anordnungsansicht als Bild untereinander (Echttest auswerten)
            string current = Store.ScreenKey();
            var known = Store.Known().OrderByDescending(k => k.Key == current).ThenByDescending(k => k.Saved).ToList();
            var stack = new System.Windows.Controls.StackPanel { Background = System.Windows.Media.Brushes.White };
            foreach (var (key, count, saved) in known)
            {
                var screens = Store.ScreensOf(key);
                var view = new ScreensView { Width = 1400, Height = 620 };
                view.Show(screens, ViewData.Icons(key, current, screens));
                stack.Children.Add(new System.Windows.Controls.TextBlock { Text = $"{(key == current ? "jetzt · " : "")}{Store.Describe(key)} · {count} Symbole · {saved:HH:mm:ss}", FontSize = 18, Margin = new Thickness(10, 10, 0, 0) });
                stack.Children.Add(view);
            }
            stack.Measure(new Size(1400, double.PositiveInfinity)); stack.Arrange(new Rect(stack.DesiredSize)); stack.UpdateLayout();
            var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(1400, (int)Math.Ceiling(stack.ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bmp.Render(stack);
            var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
            using (var f = System.IO.File.Create(args[1])) png.Save(f);
            Console.WriteLine($"ok {known.Count} Anordnungen");
            return 0;
        }
        if (args.Length == 1 && args[0] == "--spiegel")
        {
            // Nur lesen: was das Spiegeln für die jetzige Anordnung planen würde (ohne gemerkten Stand) und wo die Symbole liegen
            using var desktop = new ShellDesktop();
            var now = desktop.Snapshot();
            string key = Store.ScreenKey();
            var centers = now.ToDictionary(e => e.Key, e => desktop.CenterOf(e.Value), StringComparer.OrdinalIgnoreCase);
            var monitors = Monitors.Current();
            var history = Keeper.Rank(Store.PlansWithDevices().Skip(1), monitors);   // ohne die eigene, gerade gespeicherte Anordnung
            var plan = Keeper.Plan(centers, null, history, monitors, desktop.Spacing, out int derived, desktop.SpacingY);
            var names = desktop.Icons.ToDictionary(i => i.Id, i => i.Name, StringComparer.OrdinalIgnoreCase);
            Console.WriteLine($"abstand {desktop.SpacingX}x{desktop.SpacingY} · abgeleitet {derived} · {string.Join(" | ", monitors.Select(m => $"{m.Model} {m.Bounds} {m.Dpi} dpi"))}");
            foreach (var (id, c) in centers.OrderBy(e => e.Value.X).ThenBy(e => e.Value.Y))
            {
                var src = history.Select(h => h.TryGetValue(id, out var pl) ? pl : null).FirstOrDefault(pl => pl is not null);
                var target = plan[id];
                Console.WriteLine($"{(names.TryGetValue(id, out var n) ? n : id),-28} quelle {(src is null ? "-" : $"{src.Dx:0},{src.Dy:0} von {src.W:0}x{src.H:0}@{src.Dpi}")} · ziel {target.X:0},{target.Y:0} · jetzt {c.X:0},{c.Y:0}{((target - c).Length > 40 ? "  <== ABWEICHUNG" : "")}");
            }
            return 0;
        }
        if (args.Length == 1 && args[0] == "--qdc") { foreach (var l in Physical.TargetsReport()) Console.WriteLine(l); return 0; }
        if (args.Length == 1 && args[0] == "--lage")
        {
            // Nur lesen (Rekorder beim Echttest): Anordnung, je Bildschirm Symbolzahl und Umriss, Abstand zum gemerkten Stand
            try
            {
                using var desktop = new ShellDesktop();
                var now = desktop.Snapshot();
                string key = Store.ScreenKey();
                var saved = Store.LoadAuto(key);
                var line = new System.Text.StringBuilder($"{Store.Describe(key)} · {now.Count} Symbole · gemerkt {(saved is null ? "nein" : Keeper.MovedCount(saved, now) + " woanders")}");
                foreach (var m in Monitors.Current())
                {
                    var on = now.Values.Select(desktop.CenterOf).Where(c => m.Bounds.Contains(c)).ToList();
                    string box = on.Count == 0 ? "" : $" x {on.Min(c => c.X) - m.Bounds.X:0}..{on.Max(c => c.X) - m.Bounds.X:0} y {on.Min(c => c.Y) - m.Bounds.Y:0}..{on.Max(c => c.Y) - m.Bounds.Y:0}";
                    line.Append($" | {m.Model} {m.Bounds.Width}x{m.Bounds.Height}{(m.Primary ? " haupt" : "")}: {on.Count}{box}");
                }
                Console.WriteLine(line.ToString());
                return 0;
            }
            catch (Exception error) { Console.WriteLine("fehler " + error.Message); return 1; }
        }
        if (args.Length == 2 && args[0] == "--abweichung")
        {
            // Nur lesen: wie viele Symbole liegen anders als in einer gespeicherten Anordnung (Schnappschuss, Profil)?
            using var desktop = new ShellDesktop();
            var now = desktop.Snapshot();
            var saved = Store.LoadSnapshot(args[1]) ?? [];
            int moved = Keeper.MovedCount(saved, now), missing = saved.Keys.Count(k => !now.ContainsKey(k));
            Console.WriteLine($"anordnung {Store.ScreenKey()} · symbole {now.Count} · gespeichert {saved.Count} · woanders {moved} · fehlen {missing}");
            return 0;
        }
        if (args.Length == 1 && args[0] == "--probe")
        {
            // Nur lesen: Schreibtisch öffnen, Symbole zählen, nichts bewegen.
            try
            {
                using var desktop = new ShellDesktop();
                Console.WriteLine($"ok symbole={desktop.Icons.Count} groesse={desktop.IconSize:0} autoanordnen={desktop.AutoArrange} raster={desktop.SnapInfo}");
                foreach (var m in Monitors.Current()) Console.WriteLine($"bildschirm {m.Number} {(m.Primary ? "haupt " : "")}{m.Kind} {m.Model} {m.Diagonal:0.0}in {m.Bounds.Width}x{m.Bounds.Height} @{m.Bounds.X},{m.Bounds.Y} {m.Id}");
                foreach (var d in Physical.Displays()) Console.WriteLine($"anzeige {d.Device} {d.Bounds} arbeitsfläche {d.Work} {d.Dpi} dpi · {d.Name} nativ {d.NativeWidth}x{d.NativeHeight} · {d.Hardware}");
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
