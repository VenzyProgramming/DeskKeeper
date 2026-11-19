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
            // 19.11.2026: Blitze auf Symbole, nicht in leere Viertel des Bildschirms
            Pass("blitze-auf-symbolen", centers.All(c => icons.Any(i => (i.Center - c).Length <= iconSize * 0.6)));
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
                // Desktop-Anzeigen links oben (336 × 560 px): System rückt daneben, Entwicklung bleibt in der ersten Spalte unten
                var held = Categories.Plan(cat, grid, new Rect(0, 0, 336, 560));
                Point H(string name) => held[cat.First(c => c.Name == name).Id];
                Pass("kategorien-anzeigenflaeche-frei", held.Values.All(v => !(v.X < 336 + 40 && v.Y < 560)));
                Pass("kategorien-system-neben-anzeigen", H("Papierkorb").X > 336 && H("Papierkorb").Y < 60);
                Pass("kategorien-entwicklung-erste-spalte", H("Visual Studio 2026").X < 120 && H("Visual Studio 2026").Y > 700);
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
            { var (mean, p95) = DrawCost(raid, raid.RobotStart); results["tu160-bild-ms"] = mean; results["tu160-bild-ms-p95"] = p95; }
            var raidTimes = raid.Raid.Select(b => b.T).OrderBy(x => x).ToList();
            Pass("tu160-einschlaege-nacheinander", raid.Raid.Count is >= 1 and <= 14 && raidTimes[^1] - raidTimes[0] > 0.5 && raidTimes.Distinct().Count() == raid.Raid.Count);
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

            // Neue Luftangriffe : je Angriff alle Symbole weg vor DUM-E, am Ende alle zuhause, Dauer begrenzt, Standbilder
            foreach (var (attack, frames) in new (string, double[])[] { (Attacks.Tu22, [1.6, 2.6, 4.2]), (Attacks.Su34, [1.0, 1.9, 2.9]), (Attacks.Ka52, [1.6, 3.8, 6.0]), (Attacks.Tu95, [2.2, 3.8, 5.4]), (Attacks.Il76, [2.6, 4.2, 6.8]), (Attacks.Ac130, [2.4, 4.6, 7.4]), (Attacks.Shahed, [3.0, 4.4, 6.5]), (Attacks.A10, [2.0, 3.4, 5.5]), (Attacks.Su25, [2.0, 3.4, 5.5]) })
            {
                var sc = new Scene(icons, homes, iconSize, screens, primary, true, 64, attack: attack);
                Pass(attack + "-einschlaege-vorhanden", sc.Raid.Count >= 1);
                // jedes Symbol: ein Einschlag höchstens 2,2 Symbolgrößen entfernt (keine Treffer ins Leere), Il-76 trägt nur weg
                if (attack != Attacks.Il76) Pass(attack + "-jedes-symbol-getroffen", icons.All(i => sc.Raid.Any(b => (b.P - i.Center).Length <= iconSize * 2.2)));
                if (attack != Attacks.Il76) Pass(attack + "-kein-einschlag-ins-leere", sc.Raid.All(b => icons.Any(i => (b.P - i.Center).Length <= iconSize * 1.2)));
                Pass(attack + "-alle-symbole-weg-vor-dume", sc.IconCenters(sc.RobotStart - 0.05).Values.All(v => v is null));
                Pass(attack + "-am-ende-alle-zuhause", sc.IconCenters(sc.Duration).All(e => e.Value is { } v && (v - homes[e.Key]).Length < 0.5));
                double limit = attack == Attacks.Ka52 ? 110 : attack == Attacks.Ac130 ? 140 : attack is Attacks.A10 or Attacks.Su25 ? 130 : 100;   // AC-130: Show je Gruppe    // Ka-52 greift jedes Symbol einzeln an : dauert mit der Symbolzahl länger
                Pass($"{attack}-dauer-unter-{limit:0}-s", sc.Duration < limit);
                results[attack + "-dauer-s"] = Math.Round(sc.Duration, 1);
                var (mean, p95) = DrawCost(sc, sc.RobotStart);
                results[attack + "-bild-ms"] = mean; results[attack + "-bild-ms-p95"] = p95;
                var (soft, softMax) = RenderCost(sc, sc.RobotStart);
                results[attack + "-render-ms"] = soft; results[attack + "-render-ms-max"] = softMax;
                // Deckkraftebenen je brennendem Symbol und je Funkenregen kosteten 60 % bzw. 30 % der Bildzeit (Tu-22M3 149 ms → 17 ms)
                Pass($"{attack}-rendern-unter-60-ms", soft < 60);
                for (int f = 0; f < frames.Length; f++) Save(Frame(sc, all, icons, frames[f], 0.5), Path.Combine(output, $"{attack}-{f + 1}.png"));
                if (sc.Attack is GunshipRaid gunship)
                {
                    var kills = gunship.Shots.Where(s => s.Kill).ToList();
                    Pass("ac130-kreist-auf-dem-bildschirm", kills.All(s => screens.Any(r => r.Contains(gunship.PlaneAt(s.Fire)))));
                    Pass("ac130-gruppe-haelt-erst-durch", kills.Count > 0 && gunship.Shots.Any(s => !s.Kill && s.Fire < kills.Min(k => k.Fire)));
                    results["ac130-kreise"] = gunship.Orbits;
                    Pass("ac130-alle-drei-waffen", kills.Select(s => s.Gun).Distinct().Count() == 3);
                }
                if (sc.Attack is HelicopterRaid heli)
                {   // Ka-52 : jedes Symbol einzeln, Waffe nach Entfernung
                    var plan = heli.Plan;
                    var byCenter = icons.ToDictionary(i => i.Center, i => i.Id);
                    bool each = plan.All(e => byCenter.TryGetValue(e.Target, out var id)
                        && sc.IconCenters(e.Hit - 0.05)[id] is not null && sc.IconCenters(e.Hit + 0.2)[id] is null);
                    Pass("ka52-jedes-symbol-erst-beim-eigenen-treffer", plan.Count > 0 && each);
                    Pass("ka52-ziele-nacheinander", plan.Zip(plan.Skip(1)).All(p => p.Second.Hit > p.First.Hit - 0.3) && plan[^1].Hit - plan[0].Hit > plan.Count * 0.2);
                    Pass("ka52-mehrere-waffen", plan.Select(e => e.Weapon).Distinct().Count() >= 2); Pass("ka52-alle-symbole-angeflogen", plan.Count == icons.Count);
                    results["ka52-waffen"] = string.Join(" ", plan.GroupBy(e => e.Weapon).Select(g => g.Key + "=" + g.Count()));
                    results["ka52-schwebepunkte"] = plan.Select(e => e.Hover).Distinct().Count();
                    double fight = plan[^1].Hit - plan[0].Start;
                    results["ka52-angriff-s"] = Math.Round(fight, 1);
                    Pass("ka52-je-symbol-unter-1-s", fight / plan.Count < 1.0);
                    if (plan.FirstOrDefault(e => e.Hover != plan[0].Hover) is { } next)
                    {   // Umsetzen und Angriff am nächsten Schwebepunkt (anderer Bildschirm)
                        Save(Frame(sc, all, icons, next.Start - 0.9, 0.5), Path.Combine(output, "ka52-umsetzen.png"));
                        Save(Frame(sc, all, icons, next.Start + 0.15, 0.5), Path.Combine(output, "ka52-zweiter-schwebepunkt.png"));
                    }
                    foreach (var w in plan.GroupBy(e => e.Weapon))
                    {
                        var e = w.First();
                        double at = w.Key == HelicopterRaid.Weapon.Gun ? e.Start + 0.12 : w.Key == HelicopterRaid.Weapon.Rockets ? e.Start + 0.2 : (e.Start + e.Hit) / 2;
                        Save(Frame(sc, all, icons, at, 0.5), Path.Combine(output, $"ka52-waffe-{w.Key}.png".ToLowerInvariant()));
                    }
                }
            }
            // Weitere Helfer : nach einer Szene aus dem Lager, beim Wiederherstellen von dort, wo Windows die Symbole hingelegt hat
            foreach (var helper in new[] { Helpers.Forklift, Helpers.Crane, Helpers.Drones, Helpers.Chinook, Helpers.Mi26 })
            {
                Helpers.Current = helper;
                try
                {
                    var after = new Scene(icons, homes, iconSize, screens, primary, true, 64, attack: Attacks.Tu160);
                    Pass($"helfer-{helper}-nach-szene-alle-zuhause", after.IconCenters(after.Duration).All(e => e.Value is { } v && (v - homes[e.Key]).Length < 0.5));
                    Pass($"helfer-{helper}-traegt-alle", after.CarriedCount == icons.Count);
                    results[$"helfer-{helper}-dauer-s"] = Math.Round(after.Duration - after.RobotStart, 1);
                    double mid = after.RobotStart + (after.Duration - after.RobotStart) * 0.35;
                    Save(Frame(after, all, icons, mid, 0.5), Path.Combine(output, $"helfer-{helper}-1.png"));
                    Save(Frame(after, all, icons, after.RobotStart + (after.Duration - after.RobotStart) * 0.7, 0.5), Path.Combine(output, $"helfer-{helper}-2.png"));
                    var moved = icons.Select((i, n) => i with { Center = i.Center + new Vector(300 + n * 13, 40) }).ToList();
                    var back = new Scene(moved, homes, iconSize, screens, primary, false, 64);
                    Pass($"helfer-{helper}-wiederherstellen-alle-zuhause", back.IconCenters(back.Duration).All(e => e.Value is { } v && (v - homes[e.Key]).Length < 0.5));
                    results[$"helfer-{helper}-wiederherstellen-s"] = Math.Round(back.Duration, 1);
                    Pass($"helfer-{helper}-unter-120-s", back.Duration < 120 && after.Duration - after.RobotStart < 120);
                }
                finally { Helpers.Current = Helpers.Dume; }
            }

            {   // Maßstabstafel : alle Modelle im gemeinsamen Maßstab, ein Symbol = 6 m, Nase nach oben
                const double pm = 5;   // Pixel je Meter
                var v = new DrawingVisual();
                using (var dc = v.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x2B, 0x3A, 0x50)), null, new Rect(0, 0, 1800, 1100));
                    double up = -Math.PI / 2;
                    Art.Bomber(dc, new Point(190, 200), up, 20, false, 0, 54.1 * pm / 100, 0.3);
                    Planes.Tu95(dc, new Point(520, 200), up, 44.7 * pm / 100, 0.5, 0.3);
                    Planes.Il76(dc, new Point(840, 200), up, 45.3 * pm / 100, 0.5, false);
                    Planes.Tu22(dc, new Point(1130, 200), up, 39.5 * pm / 100, 0.5, 20);
                    Planes.AC130(dc, new Point(1420, 200), up, 40.4 * pm / 137, 0.5, 0.3);
                    LiftHelper.DrawMi26(dc, new Point(190, 600), up, pm, 0.5, 0.3);
                    Planes.Su34(dc, new Point(470, 600), up, 20.1 * pm / 100, 0.5, 0.3);
                    LiftHelper.DrawChinook(dc, new Point(700, 600), up, pm, 0.5, 0.3);
                    Planes.Ka52(dc, new Point(930, 600), up, 13.8 * pm / 100, 0.5, 0.3);
                    Planes.A10(dc, new Point(1130, 600), up, 17.53 * pm / 107.8, 0.5, 0.3);
                    Planes.Su25(dc, new Point(1320, 600), up, 14.36 * pm / 93.4, 0.5, 0.3);
                    Planes.Shahed(dc, new Point(1480, 600), up, 2.5 * pm / 71.6, 0.3, 0.3);
                    var folder = new SolidColorBrush(Color.FromRgb(0xE7, 0xB4, 0x7B));
                    for (int i = 0; i < 12; i++) dc.DrawRoundedRectangle(folder, null, new Rect(1560 + (i % 4) * 36, 520 + (i / 4) * 36, 30, 26), 4, 4);   // Symbole: je 6 m
                }
                var bmp = new RenderTargetBitmap(1800, 1100, 96, 96, PixelFormats.Pbgra32); bmp.Render(v);
                Save(bmp, Path.Combine(output, "0-massstab.png"));
            }

            {   // Typentafel der neuen Modelle : Shahed-136, A-10C, Su-25, CH-47F, Mi-26
                var v = new DrawingVisual();
                using (var dc = v.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x2B, 0x3A, 0x50)), null, new Rect(0, 0, 1800, 1100));
                    Planes.Shahed(dc, new Point(200, 230), -Math.PI / 2, 3.2, 0.5, 0.3);
                    Planes.Shahed(dc, new Point(470, 230), -Math.PI / 2, 3.2, 0.2, 0.31, 0.7);
                    Planes.A10(dc, new Point(900, 260), -Math.PI / 2, 3.6, 0.6, 0.5);
                    Planes.Su25(dc, new Point(1450, 260), -Math.PI / 2, 4.0, 0.6, 0.5);
                    LiftHelper.DrawChinook(dc, new Point(450, 800), -Math.PI / 2, 150 * 2.9 / 15.9, 0.6, 0.3);
                    LiftHelper.DrawMi26(dc, new Point(1300, 780), -Math.PI / 2, 105 * 5.6 / 33.7, 0.6, 0.3);
                }
                var bmp = new RenderTargetBitmap(1800, 1100, 96, 96, PixelFormats.Pbgra32); bmp.Render(v);
                Save(bmp, Path.Combine(output, "0-neue-modelle.png"));
            }

            {   // Typentafel: alle fünf Draufsichten groß, Rotoren und Propeller in zwei Stellungen
                var v = new DrawingVisual();
                using (var dc = v.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x2B, 0x3A, 0x4E)), null, new Rect(0, 0, 1800, 1750));
                    Planes.Tu22(dc, new Point(230, 220), 0, 3.6, 0.6);
                    Planes.Tu22(dc, new Point(230, 1130), 0, 3.6, 0.6, 65);   // Flügel ganz zurückgeschwenkt
                    Planes.Tu22(dc, new Point(640, 1130), 0, 3.6, 0.6, 20);   // ganz ausgeschwenkt (Start, Landung)
                    Planes.Tu22(dc, new Point(1000, 1130), 0, 3.6, 0.6, 45);
                    Planes.AC130(dc, new Point(560, 1530), 0, 2.9, 0.6, 0.07);
                    Planes.Su34(dc, new Point(640, 220), 0, 3.6, 0.4, 0.2);
                    Planes.Ka52(dc, new Point(1070, 230), 0, 3.0, 0.3, 0.05);
                    Planes.Ka52(dc, new Point(1520, 230), -Math.PI / 2, 3.0, 0.3, 0.19);
                    Planes.Ka52(dc, new Point(1500, 1130), 0, 2.6, 0.3, 0, rotors: false);   // Rumpf ohne Rotoren zum Vergleich mit der Vorlage
                    Planes.Tu95(dc, new Point(330, 700), 0, 3.6, 0.6, 0.03);
                    Planes.Il76(dc, new Point(860, 700), 0, 3.6, 0.6, true);
                    Planes.Bomb(dc, new Point(1250, 600), 0, 6, 1);
                    Planes.GuidedBomb(dc, new Point(1420, 600), 0, 6, 1);
                    Planes.Rocket(dc, new Point(1250, 700), new Point(1480, 700), 3, 0);
                    Planes.Parachute(dc, new Point(1300, 860), 70, 1, 0, 0.2);
                    Planes.Carrier(dc, new Point(1520, 860), 0, 70, 1.2, 1);
                }
                var bmp = new RenderTargetBitmap(1800, 1750, 96, 96, PixelFormats.Pbgra32); bmp.Render(v);
                Save(bmp, Path.Combine(output, "0-flugzeuge.png"));
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
        public void ApplyQuick(Dictionary<string, int[]> layout) => Apply(layout);
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
        // b lag zuletzt auf dem Notebook, das fehlt: kommt in derselben Ecke auf den Hauptbildschirm (Spiegeln, 19.11.2026)
        yield return ("plan-notebook-zu-symbol-bleibt-auf-seinem-bildschirm", plan1["a"] == new Point(100, 120) && plan1["b"] == new Point(50, 50) && n1 == 2);
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
        // Schnappschuss einer anderen Anordnung, Gerät fehlt: anteilig auf den Hauptbildschirm
        var frac = new Placement("L", 0, 0, 0.5, 0.5, -1, -1);
        var plan6 = Keeper.Plan(new Dictionary<string, Point> { ["a"] = new(10, 10) }, null, [new Dictionary<string, Placement> { ["a"] = frac }],
            new List<MonitorInfo> { new("L", new Rect(0, 0, 1920, 1080), true) }, 100, out _);
        yield return ("plan-fremder-schnappschuss-anteilig", plan6["a"] == new Point(960, 540));

        // Nur Notebook (1920×1080, 125 %): Symbole des fehlenden Widescreens (3440×1440, 100 %) in derselben Anordnung
        var wide = new Dictionary<string, Placement> { ["links-oben"] = new("G", 35, 40, 35 / 3440.0, 40 / 1440.0, 3440, 1440) { Dpi = 96 },
            ["links-unten"] = new("G", 35, 1276, 35 / 3440.0, 1276 / 1440.0, 3440, 1440) { Dpi = 96 }, ["rechts-oben"] = new("G", 3339, 40, 3339 / 3440.0, 40 / 1440.0, 3440, 1440) { Dpi = 96 },
            ["links-oben-2"] = new("G", 35, 138, 35 / 3440.0, 138 / 1440.0, 3440, 1440) { Dpi = 96 } };
        var notebook = new List<MonitorInfo> { new("N", new Rect(0, 0, 1920, 1080), true) { Dpi = 120 } };
        var piled = wide.Keys.ToDictionary(k => k, k => new Point(60, 60), StringComparer.OrdinalIgnoreCase);   // Windows hat alles links oben gestapelt
        var mirrored = Keeper.Plan(piled, null, [wide], notebook, 147, out int nm, 122);
        yield return ("plan-notebook-wie-widescreen", nm == 4 && mirrored["links-oben"].X < 150 && mirrored["links-oben"].Y < 150
            && mirrored["links-oben-2"].X < 150 && mirrored["links-oben-2"].Y > mirrored["links-oben"].Y + 100
            && mirrored["links-unten"].X < 150 && mirrored["links-unten"].Y is > 800 and < 1000 && mirrored["rechts-oben"].X > 1700 && mirrored["rechts-oben"].Y < 150);

        // SMs Fall (19.11.2026): Spalte links unten (5 Symbole) bleibt eine Spalte unten, Reihenfolge gleich; Gruppe in der Mitte unten bleibt mittig
        var column = new Dictionary<string, Placement>();
        string[] order = ["vs", "lm", "db", "creo1", "creo2"];
        for (int i = 0; i < 5; i++) column[order[i]] = new("G", 92, 928 + 99 * i, 92 / 3440.0, (928 + 99 * i) / 1440.0, 3440, 1440) { Dpi = 96 };
        foreach (var (id, x) in new[] { ("lunar", 1716.0), ("minecraft", 1832.0), ("stardew", 1948.0), ("steam", 2064.0) })
            column[id] = new("G", x, 1324, x / 3440.0, 1324 / 1440.0, 3440, 1440) { Dpi = 96 };
        var flat = new List<MonitorInfo> { new("N", new Rect(0, 0, 1920, 1080), true) { Dpi = 96, Work = new Rect(0, 0, 1858, 1032) } };
        var mixed = column.Keys.ToDictionary(k => k, k => new Point(500, 500), StringComparer.OrdinalIgnoreCase);
        var sm = Keeper.Plan(mixed, null, [column], flat, 116, out _, 100);
        bool sameColumn = order.All(k => Math.Abs(sm[k].X - sm["vs"].X) < 1) && order.Zip(order.Skip(1)).All(pq => sm[pq.Second].Y > sm[pq.First].Y + 90);
        bool middle = new[] { "lunar", "minecraft", "stardew", "steam" }.All(k => sm[k].Y > 900 && sm[k].X is > 700 and < 1400);
        yield return ("plan-spalte-bleibt-spalte-unten", sameColumn && sm["vs"].Y > 450 && sm["creo2"].Y < 1000 && middle);

        // Erweitern nach „Nur PC-Bildschirm“: der jüngere Ein-Bildschirm-Stand darf die Zwei-Bildschirm-Anordnung nicht schlagen 
        var onlyLaptop = new Dictionary<string, Placement> { ["a"] = new("N", 100, 100, 0.05, 0.09, 1920, 1080) };
        var extended = new Dictionary<string, Placement> { ["a"] = new("G", 3000, 200, 0.87, 0.14, 3440, 1440) };
        var both = new List<MonitorInfo> { new("G", new Rect(0, 0, 3440, 1440), true), new("N", new Rect(778, 1440, 1920, 1080), false) };
        var ranked = Keeper.Rank([(onlyLaptop, new HashSet<string> { "N" }), (extended, new HashSet<string> { "G", "N" })], both);
        var back = Keeper.Plan(new Dictionary<string, Point> { ["a"] = new(40, 40) }, null, ranked, both, 100, out _);
        yield return ("plan-zwei-bildschirme-vor-juengerem-einzelstand", back["a"] == new Point(3000, 200));

        // Gespiegelte Ziele zwischen den Zellen: aufs Raster der jetzigen Symbole (Versatz 0/1, Abstand 116×99)
        var snapped = RealWorld.Snap(new Dictionary<string, int[]> { ["a"] = [977, 113], ["b"] = [233, 199] }, [[0, 1], [116, 100], [232, 199]], 116, 99);
        yield return ("raster-ziele-einrasten", snapped["a"][0] == 928 && snapped["a"][1] == 100 && snapped["b"][0] == 232 && snapped["b"][1] == 199);

        // Monitor aus und wieder an; Windows mischt nach dem Zurückstellen noch einmal: Nachwache stellt wieder zurück 
        var w7 = new FakeWorld(); var k7 = new Keeper(w7); double t7 = 0;
        void Run7(double seconds) { for (double end = t7 + seconds; t7 < end; t7 += 3) k7.Tick(t7); }
        Run7(15);
        w7.Screens = "B"; w7.Desk["x"] = [5, 5]; w7.Desk["y"] = [5, 90]; Run7(12);
        w7.Screens = "A"; w7.Desk["x"] = [7, 7]; Run7(9);
        int firstRestore = w7.Applied;
        w7.Desk["x"] = [600, 600]; w7.Desk["y"] = [700, 700]; Run7(9);
        yield return ("hueter-nachwache-stellt-spaete-umordnung-zurueck", firstRestore == 1 && w7.Applied == 2 && w7.Desk["x"][0] == 10 && w7.Stored["A"]["x"][0] == 10 && w7.Stored["A"]["y"][0] == 100);
        Run7(24); w7.Desk["x"] = [333, 333]; Run7(9);
        yield return ("hueter-nach-nachwache-normal-speichern", w7.Stored["A"]["x"][0] == 333 && w7.Applied == 2);

        // Doppelgänger aus der DPI-Virtualisierung (Seitenleisten-Ausgabe mit System-DPI, am Notebook gestartet)
        yield return ("dpi-doppelgaenger-erkannt", Store.ScaledTwinKey("0,0,4300,1800,1,120", "0,0,3440,1440,1,96") && Store.ScaledTwinKey("0,0,1536,864,1,96", "0,0,1920,1080,1,120")
            && Store.ScaledTwinKey("0,0,3440,1440,1,96;3440,0,1536,864,0,96", "0,0,3440,1440,1,96;3440,0,1920,1080,0,120"));
        yield return ("dpi-echte-anordnungen-bleiben", !Store.ScaledTwinKey("0,0,3440,1440,1,120", "0,0,3440,1440,1,96") && !Store.ScaledTwinKey("0,0,1920,1080,1,120", "0,0,3440,1440,1,96")
            && !Store.ScaledTwinKey("0,0,3440,1440,1,96", "0,0,3440,1440,1,96") && !Store.ScaledTwinKey("0,0,3440,1440,1,96;3440,0,1920,1080,0,120", "0,0,3440,1440,1,96"));
        // echte Pixel: in diesem Prozess (pro Monitor) genau wie Windows; auch aus einem Thread mit System-DPI
        var forms = System.Windows.Forms.Screen.AllScreens.Select(s => new Rect(s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height)).OrderBy(r => r.X).ToList();
        var mine = Physical.Displays().Select(d => d.Bounds).OrderBy(r => r.X).ToList();
        yield return ("physisch-wie-windows", forms.SequenceEqual(mine));
        var old = SetThreadDpiAwarenessContext(new IntPtr(-2));   // SYSTEM_AWARE wie Seitenleisten-Ausgabe
        List<Rect> asSystem;
        try { asSystem = Physical.Displays().Select(d => d.Bounds).OrderBy(r => r.X).ToList(); }
        finally { SetThreadDpiAwarenessContext(old); }
        yield return ("physisch-auch-mit-system-dpi", asSystem.SequenceEqual(mine) && AreDpiAwarenessContextsEqual(GetThreadDpiAwarenessContext(), old));
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetThreadDpiAwarenessContext();
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool AreDpiAwarenessContextsEqual(IntPtr a, IntPtr b);

    /// <summary>Rechenzeit eines Szenenbilds (Zeichenliste aufbauen, wie Stage.Actors.OnRender) über die Angriffsphase: Mittel und 95-%-Wert in ms.</summary>
    static (double Mean, double P95) DrawCost(Scene sc, double until)
    {
        var times = new List<double>();
        var watch = new System.Diagnostics.Stopwatch();
        for (int k = 0; k < 300; k++)
        {
            double at = until * k / 300;
            var v = new DrawingVisual();
            watch.Restart();
            using (var dc = v.RenderOpen()) sc.Draw(dc, at);
            times.Add(watch.Elapsed.TotalMilliseconds);
        }
        times.Sort();
        return (Math.Round(times.Skip(10).Average(), 3), Math.Round(times[(int)(times.Count * 0.95)], 3));
    }

    /// <summary>Software-Rendern 1920×1080 über die Angriffsphase (Richtwert für Füllaufwand: Glühen, Rauch, Explosionen): Mittel und Maximum in ms.</summary>
    static (double Mean, double Max) RenderCost(Scene sc, double until)
    {
        var target = new RenderTargetBitmap(1920, 1080, 96, 96, PixelFormats.Pbgra32);
        var times = new List<double>();
        for (double tt = 0.3; tt < until; tt += Math.Max(0.15, until / 60))
        {
            var v = new DrawingVisual();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using (var dc = v.RenderOpen()) sc.Draw(dc, tt);
            target.Clear(); target.Render(v);
            times.Add(sw.Elapsed.TotalMilliseconds);
        }
        return (Math.Round(times.Average(), 1), Math.Round(times.Max(), 1));
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

    /// <summary>Jedes Modell einzeln, Nase oben, auf Weiß und groß (Vergleich mit echten Draufsichten). Nur Bilder, keine Desktop-Änderung.</summary>
    internal static void Models(string output)
    {
        Directory.CreateDirectory(output);
        double up = -Math.PI / 2;
        // Pixel je Modelleinheit so, dass die Länge (ohne Rotoren) etwa 900 px ergibt
        var all = new (string Name, double Span, Action<DrawingContext, Point> Draw)[]
        {
            ("tu160", 1000, (dc, c) => Art.Bomber(dc, c, up, 20, false, 0, 8.6, 0.3)),
            ("tu160-65", 1000, (dc, c) => Art.Bomber(dc, c, up, 65, false, 0, 8.6, 0.3)),
            ("tu22m3", 1000, (dc, c) => Planes.Tu22(dc, c, up, 8.2, 0, 20)),
            ("su34", 1000, (dc, c) => Planes.Su34(dc, c, up, 7.5, 0, 0.3)),
            ("ka52", 1000, (dc, c) => Planes.Ka52(dc, c, up, 9, 0, 0.3, false)),
            ("tu95", 1000, (dc, c) => Planes.Tu95(dc, c, up, 8.2, 0, 0.3)),
            ("il76", 1000, (dc, c) => Planes.Il76(dc, c, up, 8.2, 0, false)),
            ("ac130", 1000, (dc, c) => Planes.AC130(dc, c, up, 6.6, 0, 0.3)),
            ("a10", 1000, (dc, c) => Planes.A10(dc, c, up, 8.4, 0, 0.3)),
            ("su25", 1000, (dc, c) => Planes.Su25(dc, c, up, 9, 0, 0.3)),
            ("shahed", 1000, (dc, c) => Planes.Shahed(dc, c, up, 9, 0, 0.3)),
            ("chinook", 1000, (dc, c) => LiftHelper.DrawChinook(dc, c, up, 44, 0, 0.3)),
            ("mi26", 1000, (dc, c) => LiftHelper.DrawMi26(dc, c, up, 26, 0, 0.3)),
            ("kran", 1000, (dc, c) => CraneHelper.DrawTruck(dc, c + new Vector(0, 150), up, 210, 1, 0.75, 210 * 2.6, 0.4)),
        };
        foreach (var (name, size, draw) in all)
        {
            var v = new DrawingVisual();
            using (var dc = v.RenderOpen()) { dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, size, 1200)); draw(dc, new Point(size / 2, 600)); }
            var bmp = new RenderTargetBitmap((int)size, 1200, 96, 96, PixelFormats.Pbgra32); bmp.Render(v);
            Save(bmp, Path.Combine(output, name + ".png"));
        }
    }

    static void Save(BitmapSource image, string path)
    {
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(path);
        png.Save(file);
    }
}
