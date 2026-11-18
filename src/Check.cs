using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskKeeper;

/// <summary>
/// Prüfung ohne Explorer und ohne echten Schreibtisch: zwei Testbildschirme, Testsymbole,
/// Bilder der Schlüsselmomente und eine Laufzeitmessung je Bild als JSON.
/// </summary>
static class Check
{
    public static Task<int> Run(string output)
    {
        Directory.CreateDirectory(output);
        var results = new Dictionary<string, object>();
        int code = 0;
        try
        {
            Rect primary = new(0, 0, 1920, 1080), second = new(1920, -360, 1280, 1440);
            Rect[] screens = [primary, second];
            double iconSize = 48;
            var icons = new List<IconInfo>();
            for (int i = 0; i < 14; i++) icons.Add(new IconInfo("i" + i, Names[i % Names.Length], new Point(52 + (i / 8) * 86, 52 + (i % 8) * 104)));
            for (int i = 0; i < 6; i++) icons.Add(new IconInfo("s" + i, Names[(i + 3) % Names.Length], new Point(1980 + i * 90, -300)));
            var homes = icons.ToDictionary(i => i.Id, i => i.Center);
            var scene = new Scene(icons, homes, iconSize, screens, primary, true, 64);
            var all = Rect.Union(primary, second);

            var centers = scene.FlashCenters.ToArray();
            bool Pass(string name, bool ok) { results[name] = ok; if (!ok) code = 1; return ok; }
            Pass("rumpf-3-symbolbreiten", Math.Abs(scene.BodyLength - 3 * iconSize) < 0.5);
            Pass("19-blitze-auf-sichtbaren-flaechen", centers.Length == 19 && centers.All(c => screens.Any(s => s.Contains(c))));
            Pass("blitze-auf-beiden-bildschirmen", centers.Any(primary.Contains) && centers.Any(second.Contains));
            // Verteilung über die Fläche: jedes Viertel des Hauptbildschirms bekommt Blitze.
            Pass("blitze-ueber-den-ganzen-bildschirm", new[] { (0.0, 0.0), (0.5, 0.0), (0.0, 0.5), (0.5, 0.5) }.All(q => centers.Any(c => new Rect(q.Item1 * 1920, q.Item2 * 1080, 960, 540).Contains(c))));
            Pass("dubina-so-gross-wie-der-saugroboter", Math.Abs(scene.RobotSize - 64) < 0.01);
            Pass("jedes-symbol-einzeln-gestellt", scene.CarriedCount == icons.Count);
            var burnt = scene.IconCenters(scene.RobotStart - 0.05);
            Pass("alle-symbole-verbrannt-bevor-dubina-kommt", burnt.Values.All(v => v is null));
            var end = scene.IconCenters(scene.Duration);
            Pass("am-ende-alle-symbole-zuhause", end.All(e => e.Value is { } v && (v - homes[e.Key]).Length < 0.5));
            var before = scene.IconCenters(Scene.Impact - 0.01);
            Pass("echte-symbole-bis-zum-blitz-unbewegt", before.All(e => e.Value is { } v && (v - homes[e.Key]).Length < 0.01));
            Pass("dauer-unter-100-s", scene.Duration < 100);   // DUM-E stellt ruhig ab (19.11.2026)   // 18.11.: Дубина langsam wie ein schwerer Manipulator
            // Иерихон trifft die dichteste Gruppe: die linke Symbolspalte des Hauptbildschirms
            var main = centers[^1];
            Pass("hauptrakete-trifft-die-dichteste-gruppe", main.X < 200 && primary.Contains(main));
            // Jedes Symbol liegt in der Hauptexplosion oder im Wirkradius einer kleinen Rakete
            var minis = centers.Take(18).ToArray();
            Pass("kleine-raketen-treffen-symbole-ausserhalb-der-hauptexplosion", icons.All(i => (i.Center - main).Length <= scene.MainBlastRadius || minis.Any(m => (m - i.Center).Length <= scene.MiniBlastRadius * 1.05)));
            results["dauer-s"] = Math.Round(scene.Duration, 2);
            // Festes Raster: während DUM-E trägt, gibt es Symbole unterwegs (die Bühne zeichnet sie), am Ende liegen alle in festen Zellen
            Pass("raster-symbole-unterwegs-erkannt", Enumerable.Range(0, 400).Select(k => scene.RobotStart + k * 0.25).Any(t => scene.IconCenters(t).Any(e => e.Value is { } v && !scene.Resting(e.Key, v))));
            Pass("raster-am-ende-alle-in-festen-zellen", scene.IconCenters(scene.Duration).All(e => e.Value is { } v && scene.Resting(e.Key, v)));
            // Nach Kategorien ordnen : Zuordnung und Bereiche auf einem 3440×1392-Raster
            {
                string[] sample = ["::{645FF040-5081-101B-9F08-00AA002F954E}|Papierkorb", "a|Word", "b|Excel", "c|OBS Studio", "d|VLC media player", "e|Telegram", "f|Steam", "g|Minecraft Launcher", "h|Visual Studio 2026", "i|LM Studio", "j|Sortiert", "k|Dieser PC"];
                var cat = sample.Select(x => x.Split('|')).Select(x => new IconInfo(x[0], x[1], new Point(0, 0))).ToList();
                var grid = Categories.Grid.From(new Rect(0, 0, 3440, 1392), 116, 98, [[34, 0]], it => new Point(it[0] + 24, it[1] + 35));
                var plan = Categories.Plan(cat, grid);
                Point P(string name) => plan[cat.First(c => c.Name == name).Id];
                Pass("kategorien-alle-verschiedene-zellen", plan.Count == cat.Count && plan.Values.Distinct().Count() == cat.Count);
                Pass("kategorien-papierkorb-links-oben", P("Papierkorb").X < 100 && P("Papierkorb").Y < 60 && P("Dieser PC").X < 100);
                Pass("kategorien-office-rechts-oben", P("Word").X > 3200 && P("Word").Y < 700);
                Pass("kategorien-video-links-neben-office", P("OBS Studio").X < P("Word").X && P("OBS Studio").X > 2800);
                Pass("kategorien-social-oben-mitte", Math.Abs(P("Telegram").X - 1720) < 400 && P("Telegram").Y < 60);
                Pass("kategorien-spiele-unten-mitte", Math.Abs(P("Steam").X - 1720) < 400 && P("Steam").Y > 1200);
                Pass("kategorien-entwicklung-links-unten", P("Visual Studio 2026").X < 300 && P("Visual Studio 2026").Y > 700);
                Pass("kategorien-rest-rechts-unten", P("Sortiert").X > 3200 && P("Sortiert").Y > 1200);
            }
            var (jumpYaw, jumpReach) = scene.Dubina.LargestArmStep();
            results["arm-groesster-schritt-grad"] = Math.Round(jumpYaw, 2);
            results["arm-groesster-schritt-reichweite"] = Math.Round(jumpReach, 3);
            Pass("arm-ohne-sprung", jumpYaw < 6 && jumpReach < 0.06);   // weich: höchstens 6° bzw. 0,06 Breiten je Bild bei 60 Bildern/s
            results["fahrten"] = scene.Dubina.Drives;
            results["lenkwinkel-summe-grad"] = Math.Round(scene.Dubina.SteeringDegrees);
            // Realistischer Maßstab wie auf einem Ultrawide-Desktop (Symbol 68 px, Дубина 96 px): wie viele Fahrten?
            var real = new Scene(icons.Select(i => i with { Center = new Point(i.Center.X * 1.4, i.Center.Y * 1.4 - 360 * 0.4 * (i.Center.Y < 0 ? 1 : 0)) }).ToList(),
                icons.ToDictionary(i => i.Id, i => new Point(i.Center.X * 1.4, i.Center.Y * 1.4 - 360 * 0.4 * (i.Center.Y < 0 ? 1 : 0))), 68, [new Rect(0, 0, 2880, 1620), new Rect(2880, -540, 1920, 2160)], new Rect(0, 0, 2880, 1620), true, 96);
            results["fahrten-ultrawide"] = real.Dubina.Drives;
            results["lenkwinkel-ultrawide"] = Math.Round(real.Dubina.SteeringDegrees);

            // Restore-only: ohne Rakete, verschobene Symbole.
            var shuffled = icons.Select((i, n) => i with { Center = i.Center + new Vector(300 + n * 13, 40) }).ToList();
            var plain = new Scene(shuffled, homes, iconSize, screens, primary, false, 64);
            Pass("wiederherstellen-ohne-rakete-endet-zuhause", plain.IconCenters(plain.Duration).All(e => e.Value is { } v && (v - homes[e.Key]).Length < 0.5));

            // Bilder: Testfläche 3200×1440 auf halbe Größe.
            double rs = scene.RobotStart;
            double[] times = [0.8, Scene.PauseSample, Scene.ReturnStart + 0.7, Scene.PanelsOpen + 0.15, Scene.Impact - 0.5, Scene.Impact + 0.06, Scene.Impact + 0.3,
                Scene.Impact + 0.75, rs + 1.0, rs + 1.25, rs + 1.45, rs + 9];
            string[] names = ["1-aufstieg", "2-pause", "3-rueckkehr", "4-panels", "5-schwarm", "6-blitz", "7-welle", "8-asche", "9-greift-im-wagen", "10-schwenkt", "11-setzt", "12-spaeter"];
            var cost = new List<double>();
            for (int k = 0; k < times.Length; k++) Save(Frame(scene, all, icons, times[k], 0.5), Path.Combine(output, names[k] + ".png"));
            // Nahaufnahme 1:1 um Дубина beim Greifen und Absetzen (wie auf dem echten Schreibtisch)
            foreach (var (t, name) in new[] { (rs + 2.2, "13-nah-greift"), (rs + 2.6, "14-nah-schwenkt"), (rs + 3.0, "15-nah-setzt") })
            {
                var full = Frame(scene, all, icons, t, 1.0);
                Save(new CroppedBitmap(full, new Int32Rect(0, 360, 900, 560)), Path.Combine(output, name + ".png"));
            }
            // Zeichenaufwand: Szene 200 Mal in ein kleines Ziel rendern (CPU-Seite, ohne GPU-Anteil).
            var probe = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (double t = 0; t < scene.Duration; t += scene.Duration / 200)
            {
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen()) { dc.PushTransform(new ScaleTransform(0.02, 0.02)); scene.Draw(dc, t); }
                var lap = watch.Elapsed.TotalMilliseconds;
                probe.Render(visual);
                cost.Add(watch.Elapsed.TotalMilliseconds - lap);
            }
            results["zeichnen-ms-mittel"] = Math.Round(cost.Average(), 2);
            results["zeichnen-ms-max"] = Math.Round(cost.Max(), 2);
            results["arbeitsspeicher-mb"] = Environment.WorkingSet / 1048576;
            Save(Portrait(), Path.Combine(output, "0-figuren.png"));
            Save(SkinSheet(), Path.Combine(output, "0-skins.png"));
            foreach (var (name, ok) in KeeperChecks()) Pass(name, ok);
            // Ту-160М: Abwurf nacheinander, alle Symbole verbrennen, Bomber verlässt das Bild, Дубина stellt alle zurück
            var raid = new Scene(icons, homes, iconSize, screens, primary, true, 64, tu160: true);
            var raidTimes = raid.Raid.Select(b => b.T).OrderBy(x => x).ToList();
            Pass("tu160-einschlaege-nacheinander", raid.Raid.Count == 14 && raidTimes[^1] - raidTimes[0] > 1.0 && raidTimes.Distinct().Count() == 14);
            Pass("tu160-alle-symbole-verbrannt", raid.IconCenters(raid.RobotStart - 0.05).Values.All(v => v is null));
            Pass("tu160-am-ende-alle-zuhause", raid.IconCenters(raid.Duration).All(e => e.Value is { } v && (v - homes[e.Key]).Length < 0.5));
            results["tu160-dauer-s"] = Math.Round(raid.Duration, 1);
            foreach (var (tt, name) in new[] { (1.2, "t1-anflug"), (2.0, "t2-abwurf"), (3.0, "t3-mitte-nachbrenner"), (3.6, "t3-flug-weg"), (raidTimes[3] + 0.2, "t4-einschlaege") })
                Save(Frame(raid, all, icons, tt, 0.5), Path.Combine(output, name + ".png"));
            {
                var v = new DrawingVisual();
                using (var dc = v.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x2B, 0x3A, 0x4E)), null, new Rect(0, 0, 1400, 420));
                    Art.Bomber(dc, new Point(230, 210), 0, 20, true, 0, 3.4, 0.3);
                    Art.Bomber(dc, new Point(640, 210), 0, 42, false, 0.4, 3.4, 0.3);
                    Art.Bomber(dc, new Point(1020, 210), 0, 65, false, 1, 3.4, 0.3);
                    Art.Missile(dc, new Point(1260, 120), new Vector(1, 0), 0, false, 1, 9, 0);
                    Art.Missile(dc, new Point(1260, 300), new Vector(1, 0), 1, true, 0.5, 9, 0);
                }
                var bmp = new RenderTargetBitmap(1400, 420, 96, 96, PixelFormats.Pbgra32); bmp.Render(v);
                Save(bmp, Path.Combine(output, "0-tu160.png"));
            }

            var skinBefore = Art.Current;
            bool allSkins = true;
            foreach (var skin in Art.Skins)
            {
                Art.Current = skin;
                try
                {
                    for (double t = 0; t < scene.Duration; t += 0.25)
                    {
                        var v = new DrawingVisual();
                        using (var dc = v.RenderOpen()) scene.Draw(dc, t);
                    }
                }
                catch { allSkins = false; }
            }
            Art.Current = skinBefore;
            Pass("alle-skins-zeichnen-die-ganze-szene", allSkins);
            // Atomrakete: ein Einschlag in der Mitte, alle Symbole verbrennen, Дубина stellt alle zurück
            var atom = new Scene(icons, homes, iconSize, screens, primary, true, 64, nuclear: true);
            Pass("atom-ein-einschlag-in-der-mitte", atom.FlashCenters.Count() == 1 && (atom.FlashCenters.First() - new Point(960, 561.6)).Length < 2);
            Pass("atom-alle-symbole-verbrannt", atom.IconCenters(atom.RobotStart - 0.05).Values.All(v => v is null));
            // Zeitbedarf je Bild beim Atompilz: echtes Bild 1920×1080 (Software-Rendern, Richtwert)
            {
                var target = new RenderTargetBitmap(1920, 1080, 96, 96, PixelFormats.Pbgra32);
                var atomTimes = new List<double>();
                for (double tt = Scene.Impact; tt < Scene.Impact + 4.5; tt += 0.15)
                {
                    var v = new DrawingVisual();
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    using (var dc = v.RenderOpen()) atom.Draw(dc, tt);
                    target.Clear(); target.Render(v);
                    atomTimes.Add(sw.Elapsed.TotalMilliseconds);
                }
                results["atom-bild-ms-mittel"] = Math.Round(atomTimes.Average(), 1);
                results["atom-bild-ms-max"] = Math.Round(atomTimes.Max(), 1);
                var tuTimes = new List<double>();
                for (double tt = 0.3; tt < raid.RobotStart; tt += 0.15)
                {
                    var v = new DrawingVisual();
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    using (var dc = v.RenderOpen()) raid.Draw(dc, tt);
                    target.Clear(); target.Render(v);
                    tuTimes.Add(sw.Elapsed.TotalMilliseconds);
                }
                results["tu160-bild-ms-mittel"] = Math.Round(tuTimes.Average(), 1);
                results["tu160-bild-ms-max"] = Math.Round(tuTimes.Max(), 1);
                results["tu160-bild-ms-verlauf"] = string.Join(" ", tuTimes.Select(x => Math.Round(x)));
            }
            Pass("atom-am-ende-alle-zuhause", atom.IconCenters(atom.Duration).All(e => e.Value is { } v && (v - homes[e.Key]).Length < 0.5));
            foreach (var (tt, name) in new[] { (Scene.Impact - 0.8, "a1-atom-anflug"), (Scene.Impact + 0.12, "a2-atom-blitz"), (Scene.Impact + 0.5, "a3-atom-feuerball"), (Scene.Impact + 1.2, "a4-atom-aufstieg"), (Scene.Impact + 2.2, "a5-atom-pilz"), (Scene.Impact + 3.4, "a6-atom-wolke") })
                Save(Frame(atom, all, icons, tt, 0.5), Path.Combine(output, name + ".png"));
            Save(TurnSheet(), Path.Combine(output, "0-wenden.png"));
            if (Environment.GetEnvironmentVariable("DUBINA_VIDEO") is "1" or "atom" or "tu160")
            {
                // Bildfolge für ein Vorschauvideo (30 Bilder je Sekunde, 40 % Größe).
                var frames = Directory.CreateDirectory(Path.Combine(output, "video"));
                int n = 0;
                var shown = Environment.GetEnvironmentVariable("DUBINA_VIDEO") switch { "atom" => atom, "tu160" => raid, _ => scene };
                var skinId = Environment.GetEnvironmentVariable("DUBINA_SKIN");
                if (skinId is not null) Art.Current = Art.Skins.FirstOrDefault(s => s.Id == skinId) ?? Art.Current;
                for (double t = 0; t < shown.Duration; t += 1 / 30.0) Save(Frame(shown, all, icons, t, 0.4), Path.Combine(frames.FullName, $"{n++:D5}.png"));
            }
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(output, "fehler.txt"), error.ToString()); code = 1; }
        File.WriteAllText(Path.Combine(output, "pruefung.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        return Task.FromResult(code);
    }

    /// <summary>Hüter mit einer Attrappe statt des echten Schreibtischs: Wechsel, Rückkehr, Sperre, Start.</summary>
    sealed class FakeWorld : IKeeperWorld
    {
        public string Screens = "A";
        public Dictionary<string, int[]> Desk = new() { ["x"] = [10, 10], ["y"] = [100, 10] };
        public readonly Dictionary<string, Dictionary<string, int[]>> Stored = [];
        public int Applied;
        public bool Blocked { get; set; }
        public string Key() => Screens;
        public Dictionary<string, int[]>? Read() => Desk.ToDictionary(e => e.Key, e => e.Value.ToArray());
        public Dictionary<string, int[]>? Target(string key, Dictionary<string, int[]> current)
            => Stored.TryGetValue(key, out var d) ? current.ToDictionary(e => e.Key, e => d.TryGetValue(e.Key, out var p) ? p.ToArray() : e.Value) : null;
        public void Apply(Dictionary<string, int[]> layout) { Applied++; foreach (var (k, v) in layout) Desk[k] = v.ToArray(); }
        public Dictionary<string, int[]>? Saved(string key) => Stored.TryGetValue(key, out var d) ? d : null;
        public void Save(string key, Dictionary<string, int[]> layout) => Stored[key] = layout.ToDictionary(e => e.Key, e => e.Value.ToArray());
    }

    static IEnumerable<(string, bool)> KeeperChecks()
    {
        var w = new FakeWorld();
        var keeper = new Keeper(w);
        double t = 0;
        void Run(double seconds) { for (double end = t + seconds; t < end; t += 3) keeper.Tick(t); }
        Run(15);
        yield return ("hueter-erster-start-merkt-anordnung", w.Applied == 0 && w.Stored.ContainsKey("A"));
        w.Desk["x"] = [300, 300]; Run(9);
        yield return ("hueter-merkt-verschobenes-symbol", w.Stored["A"]["x"][0] == 300);
        // zweiter Bildschirm ab: Windows ordnet um
        w.Screens = "B"; w.Desk["x"] = [5, 5]; w.Desk["y"] = [5, 90]; Run(12);
        yield return ("hueter-umordnung-nie-unter-altem-schluessel", w.Stored["A"]["x"][0] == 300 && w.Stored["A"]["y"][0] == 100);
        yield return ("hueter-neue-anordnung-gemerkt", w.Stored.ContainsKey("B") && w.Stored["B"]["y"][1] == 90);
        // zurück zu A: Windows ordnet wieder um, Hüter stellt A zurück
        w.Screens = "A"; w.Desk["x"] = [7, 7]; Run(12);
        yield return ("hueter-stellt-bei-rueckkehr-zurueck", w.Applied == 1 && w.Desk["x"][0] == 300 && w.Desk["y"][0] == 100);
        // während einer Szene: nichts speichern
        w.Blocked = true; w.Desk["y"] = [999, 999]; Run(12);
        yield return ("hueter-speichert-nicht-waehrend-szene", w.Stored["A"]["y"][0] == 100);
        w.Blocked = false; w.Desk["y"] = [100, 10];
        // Neustart, Windows hat die Lage vergessen (gleiche Bildschirme): zurückstellen
        var w2 = new FakeWorld { Screens = "A" };
        w2.Stored["A"] = new() { ["x"] = [50, 50], ["y"] = [60, 60] };
        var k2 = new Keeper(w2); for (double q = 0; q < 15; q += 3) k2.Tick(q);
        yield return ("hueter-start-stellt-vergessene-lage-zurueck", w2.Applied == 1 && w2.Desk["x"][0] == 50);
        // Neustart, alles noch richtig: nichts tun
        var w4 = new FakeWorld(); w4.Stored["A"] = w4.Read()!;
        var k4 = new Keeper(w4); for (double q = 0; q < 15; q += 3) k4.Tick(q);
        yield return ("hueter-start-ohne-aenderung-tut-nichts", w4.Applied == 0);
        // Abmelden/Herunterfahren: verschobenes Symbol sofort merken, ohne zwei Abfragen abzuwarten
        var w5 = new FakeWorld(); var k5 = new Keeper(w5);
        for (double q = 0; q < 15; q += 3) k5.Tick(q);
        w5.Desk["x"] = [444, 444]; k5.SaveNow(16);
        yield return ("hueter-speichert-beim-abmelden-sofort", w5.Stored["A"]["x"][0] == 444);
        // Rasterversatz von wenigen Pixeln: nicht zurückstellen (DUM-E fuhr sonst für 3–23 px los)
        var w6 = new FakeWorld(); w6.Stored["A"] = new() { ["x"] = [13, 10], ["y"] = [123, 10] };
        var k6 = new Keeper(w6); for (double q = 0; q < 15; q += 3) k6.Tick(q);
        yield return ("hueter-kleiner-versatz-kein-zurueckstellen", w6.Applied == 0);
        // ausgeschaltet: weder merken noch zurückstellen
        var w3 = new FakeWorld(); var k3 = new Keeper(w3) { AutoRestore = false, AutoSave = false };
        for (double q = 0; q < 15; q += 3) k3.Tick(q);
        yield return ("hueter-aus-tut-nichts", w3.Stored.Count == 0 && w3.Applied == 0);

        // Plätze je Bildschirmgerät: Notebook L (1920×1080, Haupt) + Breitbild W (3440×1440) rechts daneben
        var placeA = new Placement("W", 100, 120, 100 / 3440.0, 120 / 1440.0, 3440, 1440);
        var placeB = new Placement("L", 50, 50, 50 / 1920.0, 50 / 1080.0, 1920, 1080);
        var history = new List<IReadOnlyDictionary<string, Placement>> { new Dictionary<string, Placement> { ["a"] = placeA, ["b"] = placeB } };
        // Notebook zugeklappt, nur das Breitbild (jetzt Hauptbildschirm bei 0,0); Windows hat alles links oben gestapelt
        var onlyWide = new List<MonitorInfo> { new("W", new Rect(0, 0, 3440, 1440), true) };
        var cur = new Dictionary<string, Point> { ["a"] = new(40, 40), ["b"] = new(40, 150) };
        var plan1 = Keeper.Plan(cur, null, history, onlyWide, 100, out int n1);
        yield return ("plan-notebook-zu-symbol-bleibt-auf-seinem-bildschirm", plan1["a"] == new Point(100, 120) && plan1["b"] == new Point(40, 150) && n1 == 1);
        // Hauptbildschirm getauscht: Breitbild links (negativ), Notebook bei 0,0
        var swapped = new List<MonitorInfo> { new("W", new Rect(-3440, 0, 3440, 1440), false), new("L", new Rect(0, 0, 1920, 1080), true) };
        var plan2 = Keeper.Plan(new Dictionary<string, Point> { ["a"] = new(40, 40), ["b"] = new(40, 400) }, null, history, swapped, 100, out _);
        yield return ("plan-hauptbildschirm-getauscht", plan2["a"] == new Point(-3340, 120) && plan2["b"] == new Point(50, 50));
        // andere Auflösung des Breitbilds: anteilig
        var smaller = new List<MonitorInfo> { new("W", new Rect(0, 0, 2560, 1080), true) };
        var plan3 = Keeper.Plan(new Dictionary<string, Point> { ["a"] = new(400, 400) }, null, history, smaller, 60, out _);
        yield return ("plan-andere-aufloesung-anteilig", Math.Abs(plan3["a"].X - 100 / 3440.0 * 2560) < 0.5 && Math.Abs(plan3["a"].Y - 90) < 0.5);
        // Zusammenstoß: auf dem Zielplatz liegt schon ein anderes Symbol, das nicht wandert
        var plan4 = Keeper.Plan(new Dictionary<string, Point> { ["a"] = new(900, 900), ["c"] = new(100, 120) }, null, history, onlyWide, 100, out _);
        yield return ("plan-zusammenstoss-weicht-aus", plan4["c"] == new Point(100, 120) && Math.Abs(plan4["a"].X - 100) < 0.5 && plan4["a"].Y >= 220);
        // genau diese Anordnung gemerkt: hat Vorrang vor abgeleiteten Plätzen
        var plan5 = Keeper.Plan(cur, new Dictionary<string, Point> { ["a"] = new(777, 333) }, history, onlyWide, 100, out _);
        yield return ("plan-gemerkte-anordnung-hat-vorrang", plan5["a"] == new Point(777, 333));
    }

    static readonly string[] Names = ["Проекты", "Заметки", "Корзина", "Картинки", "Документы", "Музыка", "Игры", "Загрузки"];

    /// <summary>Testbild: Tapete, Testsymbole an ihrer Szenenposition, darüber die Darsteller.</summary>
    static BitmapSource Frame(Scene scene, Rect all, List<IconInfo> icons, double t, double scale)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(scale, scale));
            dc.PushTransform(new TranslateTransform(-all.X, -all.Y));
            var wall = new LinearGradientBrush(Color.FromRgb(0x24, 0x3B, 0x55), Color.FromRgb(0x5A, 0x3A, 0x4E), 35);
            dc.DrawRectangle(Brushes.Black, null, all);
            dc.DrawRectangle(wall, null, new Rect(0, 0, 1920, 1080));
            dc.DrawRectangle(wall, null, new Rect(1920, -360, 1280, 1440));
            var centers = scene.IconCenters(t);
            var face = new Typeface("Segoe UI");
            foreach (var icon in icons)
            {
                if (centers[icon.Id] is not { } c) continue;
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(0xE7, 0xB4, 0x7B)), null, new Rect(c.X - 24, c.Y - 24, 48, 40), 6, 6);
                var text = new FormattedText(icon.Name, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, 12, Brushes.White, 1);
                dc.DrawText(text, new Point(c.X - text.Width / 2, c.Y + 20));
            }
            scene.Draw(dc, t);
        }
        var bitmap = new RenderTargetBitmap((int)(all.Width * scale), (int)(all.Height * scale), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    /// <summary>Großaufnahme der Figuren: Иерихон, kleine Rakete, Дубина in drei Posen mit Anhänger.</summary>
    static BitmapSource Portrait()
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x2B, 0x3A, 0x4E)), null, new Rect(0, 0, 1400, 760));
            dc.PushTransform(new MatrixTransform(6, 0, 0, 6, 420, 110)); Art.Rocket(dc, false, 1); dc.Pop();
            dc.PushTransform(new MatrixTransform(6, 0, 0, 6, 1120, 110)); Art.Rocket(dc, true, 1.1); dc.Pop();
            dc.PushTransform(new MatrixTransform(8, 0, 0, 8, 1290, 250)); Art.Dart(dc, 1); dc.Pop();
            var shuffled = new List<IconInfo> { new("a", "Тест", new Point(560, 470)) };
            var one = new Scene(shuffled, new Dictionary<string, Point> { ["a"] = new Point(560, 470) }, 48, [new Rect(0, 0, 1400, 760)], new Rect(0, 0, 1400, 760), true, 260);
            one.Dubina.DrawPortrait(dc, new Point(330, 560), -0.3, 0.2, 0.42, 0.85, 0.25);
            one.Dubina.DrawPortrait(dc, new Point(1050, 520), 0.1, -2.6, 1.15, 0.4, -0.4);
        }
        var bitmap = new RenderTargetBitmap(1400, 760, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    /// <summary>Wendebilder nebeneinander: Дубина mit Anhänger dreht sich wie eine Lok.</summary>
    static BitmapSource TurnSheet()
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x2B, 0x3A, 0x4E)), null, new Rect(0, 0, 1800, 420));
            var one = new Scene([new IconInfo("a", "Тест", new Point(100, 100))], new Dictionary<string, Point> { ["a"] = new Point(100, 100) }, 48, [new Rect(0, 0, 1800, 420)], new Rect(0, 0, 1800, 420), true, 130);
            for (int f = 0; f < 5; f++) one.Dubina.DrawPortrait(dc, new Point(260 + f * 330, 230), f * 0.55, 0, 0.42, 0.85, -0.12 * f);
        }
        var bitmap = new RenderTargetBitmap(1800, 420, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    /// <summary>Alle Skins nebeneinander (von oben, mit Anhänger).</summary>
    static BitmapSource SkinSheet()
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x2B, 0x3A, 0x4E)), null, new Rect(0, 0, 1700, 380));
            var one = new Scene([new IconInfo("a", "Тест", new Point(100, 100))], new Dictionary<string, Point> { ["a"] = new Point(100, 100) }, 48, [new Rect(0, 0, 1700, 380)], new Rect(0, 0, 1700, 380), true, 140);
            var keep = Art.Current;
            for (int i = 0; i < Art.Skins.Length; i++)
            {
                Art.Current = Art.Skins[i];
                one.Dubina.DrawPortrait(dc, new Point(230 + i * 330, 180), -0.4, 0.35, 1.0, 0.5, 0.2);
                var label = new FormattedText(Art.Skins[i].Name, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 18, Brushes.White, 1);
                dc.DrawText(label, new Point(150 + i * 330, 340));
            }
            Art.Current = keep;
        }
        var bitmap = new RenderTargetBitmap(1700, 380, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    static void Save(BitmapSource image, string path)
    {
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(path);
        png.Save(file);
    }
}
