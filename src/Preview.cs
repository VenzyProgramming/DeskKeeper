using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskKeeper;

/// <summary>Vorschaubilder für die Karte: echte Szenenbilder auf einem Testschreibtisch und Дубина je Skin.</summary>
static class Preview
{
    static readonly string[] Names = ["Проекты", "Заметки", "Корзина", "Картинки", "Документы", "Музыка", "Игры", "Загрузки"];
    static readonly Rect Desk = new(0, 0, 1600, 900);

    static List<IconInfo> Icons()
    {
        var icons = new List<IconInfo>();
        for (int i = 0; i < 21; i++) icons.Add(new IconInfo("p" + i, Names[i % Names.Length], new Point(60 + (i / 7) * 96, 70 + (i % 7) * 112)));
        for (int i = 0; i < 6; i++) icons.Add(new IconInfo("q" + i, Names[(i + 2) % Names.Length], new Point(1500 - (i / 3) * 96, 70 + (i % 3) * 112)));
        return icons;
    }

    /// <summary>scene: jericho | nuke | tu160; Bild 16:9 in der gegebenen Breite (physische Pixel).</summary>
    static readonly Dictionary<string, BitmapSource> cache = [];

    public static BitmapSource SceneImage(string scene, int width)
    {
        string id = $"{scene}:{width}:{Art.Current.Id}";
        if (cache.TryGetValue(id, out var hit)) return hit;
        return cache[id] = RenderScene(scene, width);
    }

    /// <summary>Flugzeug bzw. Rakete der Szene groß im Bild (19.11.2026: „bei Szenen sollen die jeweiligen Flugzeuge oder Raketen als
    /// Vorschaubild da sein“): Draufsicht schräg nach rechts oben, mit Schatten, über einem angedeuteten Desktop. Maßstab je Modell wie bei
    /// <c>--modelle</c> (dort etwa 900 px Länge), Anteil an der Bildbreite je Art.</summary>
    static readonly Dictionary<string, (double ModelU, double Share, Action<DrawingContext, Point, double, double> Draw)> Heroes = new()
    {
        ["jericho"] = (65, 0.24, (dc, c, h, u) => Art.Missile(dc, c, new Vector(Math.Cos(h), Math.Sin(h)), 1, true, 0.35, u, 0.3)),
        [Attacks.Tu160] = (8.6, 0.6, (dc, c, h, u) => Art.Bomber(dc, c, h, 20, false, 0.5, u, 0.3)),
        [Attacks.Tu22] = (8.2, 0.66, (dc, c, h, u) => Planes.Tu22(dc, c, h, u, 0.35, 20)),
        [Attacks.Su34] = (7.5, 0.56, (dc, c, h, u) => Planes.Su34(dc, c, h, u, 0.35, 0.3)),
        [Attacks.Ka52] = (9, 0.42, (dc, c, h, u) => Planes.Ka52(dc, c, h, u, 0.25, 0.3, true)),
        [Attacks.Tu95] = (8.2, 0.52, (dc, c, h, u) => Planes.Tu95(dc, c, h, u, 0.35, 0.3)),
        [Attacks.Il76] = (8.2, 0.52, (dc, c, h, u) => Planes.Il76(dc, c, h, u, 0.35, false)),
        [Attacks.Ac130] = (6.6, 0.5, (dc, c, h, u) => Planes.AC130(dc, c, h, u, 0.35, 0.3)),
        [Attacks.Shahed] = (9, 0.38, (dc, c, h, u) => Planes.Shahed(dc, c, h, u, 0.2, 0.3)),
        [Attacks.A10] = (8.4, 0.56, (dc, c, h, u) => Planes.A10(dc, c, h, u, 0.3, 0.3)),
        [Attacks.Su25] = (9, 0.52, (dc, c, h, u) => Planes.Su25(dc, c, h, u, 0.3, 0.3)),
        [Attacks.F35] = (8.4, 0.52, (dc, c, h, u) => Planes.F35(dc, c, h, u, 0.35, 0.3, 0)),
        [Attacks.Su57] = (8.4, 0.58, (dc, c, h, u) => Planes.Su57(dc, c, h, u, 0.35, 0.3, 0)),
        [Attacks.B2] = (9, 0.36, (dc, c, h, u) => Planes.B2(dc, c, h, u, 0.4, 0.3)),
        [Attacks.Hunter] = (8.6, 0.5, (dc, c, h, u) => Planes.S70(dc, c, h, u * 0.85, 0.35, 0.3)),
        [Attacks.Su35] = (8.4, 0.6, (dc, c, h, u) => Planes.Su35(dc, c, h, u, 0.35, 0.3)),
        [Attacks.MQ9] = (9, 0.3, (dc, c, h, u) => Planes.MQ9(dc, c, h, u, 0.35, 0.3)),
        [Attacks.SR71] = (8.4, 0.62, (dc, c, h, u) => Planes.SR71(dc, c, h, u, 0.35, 0.3, 0.5)),
        [Attacks.MiG31] = (8.4, 0.6, (dc, c, h, u) => Planes.MiG31(dc, c, h, u, 0.35, 0.3, kinzhal: true, burner: 0.5)),
    };

    static BitmapSource RenderHero(string scene, int width)
    {
        int height = (int)Math.Round(width * 9.0 / 16);
        double W = width, H = height, k = W / Desk.Width;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Wall, null, new Rect(0, 0, W, H));
            // angedeuteter Desktop: Symbole links und rechts oben, gedämpft
            dc.PushOpacity(0.55);
            foreach (var icon in Icons())
            {
                var c = new Point(icon.Center.X * k, icon.Center.Y * k);
                dc.DrawRoundedRectangle(Folder, null, new Rect(c.X - 24 * k, c.Y - 24 * k, 48 * k, 40 * k), 6 * k, 6 * k);
                dc.DrawRoundedRectangle(Label, null, new Rect(c.X - 26 * k, c.Y + 22 * k, 52 * k, 8 * k), 4 * k, 4 * k);
            }
            dc.Pop();
            const double heading = -0.55;   // schräg nach rechts oben
            if (scene == Attacks.Duel)
            {   // Su-57 hinten links jagt die F-35A vorn rechts, Rakete mit Rauchspur dazwischen
                double uF = 8.4 * (W * 0.34 / 900), uS = 8.4 * (W * 0.40 / 900);
                var f35 = new Point(W * 0.68, H * 0.36); var su57 = new Point(W * 0.34, H * 0.68);
                var dir = new Vector(Math.Cos(heading), Math.Sin(heading));
                var from = su57 + dir * W * 0.12; var to = f35 - dir * W * 0.16;
                dc.DrawLine(Soft.FadePen(Trail, W * 0.012, 0.45), from, to);
                dc.DrawLine(Soft.FadePen(Trail, W * 0.005, 0.8), from, to);
                Planes.Maverick(dc, to, heading, uF * 0.8);
                Planes.Su57(dc, su57, heading, uS, 0.35, 0.3, 0);
                Planes.F35(dc, f35, heading, uF, 0.35, 0.3, 0);
            }
            else if (Heroes.TryGetValue(scene, out var hero))
                hero.Draw(dc, new Point(W * 0.55, H * 0.47), heading, hero.ModelU * (W * hero.Share / 900));
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    static readonly Brush Trail = Frozen(new SolidColorBrush(Color.FromRgb(0xEE, 0xF0, 0xF2)));

    static BitmapSource RenderScene(string scene, int width)
    {
        if (scene == Attacks.Duel || Heroes.ContainsKey(scene)) return RenderHero(scene, width);
        var icons = Icons();
        var homes = icons.ToDictionary(i => i.Id, i => i.Center);
        var s = new Scene(icons, homes, 48, [Desk], Desk, true, 64, nuclear: scene == "nuke", attack: Attacks.All.Contains(scene) ? scene : null);
        double t = scene switch { "nuke" => DeskKeeper.Scene.Impact + 1.3, "tu160" => 1.8, "tu22m3" => 2.2, "su34" => 1.3, "ka52" => 4.1, "tu95" => 3.4, "il76" => 3.2, "ac130" => 5.0, "shahed" => 4.6, "a10" => 3.0, "su25" => 3.0, _ => DeskKeeper.Scene.Impact + 0.35 };
        if (s.Attack is GunshipRaid gunship)
        {   // Kanonenboot ganz im Bild, kurz nach einem Schuss der Haubitze
            var inner = Desk; inner.Inflate(-90, -60);
            var shot = gunship.Shots.Where(x => x.Gun == GunshipRaid.Gun.G105 && inner.Contains(gunship.PlaneAt(x.Fire + 0.1))).OrderBy(x => x.Fire).Skip(1).FirstOrDefault();
            if (shot is not null) t = shot.Fire + 0.1;
        }
        if (s.Attack is StealthRaid or SpiritRaid && s.Raid.Count > 0) t = s.Raid.Min(r => r.T) - 0.45;   // Flugzeug im Bild, Waffen unterwegs
        if (s.Attack is VacuumRaid && s.Raid.Count > 2) t = s.Raid.OrderBy(r => r.T).ElementAt(2).T - 0.15;
        if (s.Attack is GravityRaid) t = 2.9;
        if (s.Attack is DuelRaid duel && duel.Shots.Count > 1) t = duel.Shots[1].Fire + 0.55;   // Raketen unterwegs, Täuschkörper, beide Jets im Bild
        if (s.Attack is StrafeRaid strafe)
        {   // Tiefflieger ganz im Bild mitten im Feuerstoß
            var inner = Desk; inner.Inflate(-90, -60);
            var shot = strafe.Shots.Where(x => inner.Contains(strafe.PlaneAt(x.Fire))).Skip(4).FirstOrDefault();
            if (shot is not null) t = shot.Fire;
        }
        double scale = width / Desk.Width;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(scale, scale));
            dc.DrawRectangle(Wall, null, Desk);
            var centers = s.IconCenters(t);
            foreach (var icon in icons)
            {
                if (centers[icon.Id] is not { } c) continue;
                dc.DrawRoundedRectangle(Folder, null, new Rect(c.X - 24, c.Y - 24, 48, 40), 6, 6);
                dc.DrawRoundedRectangle(Label, null, new Rect(c.X - 26, c.Y + 22, 52, 8), 4, 4);
            }
            s.Draw(dc, t);
        }
        var bitmap = new RenderTargetBitmap(width, (int)Math.Round(Desk.Height * scale), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>Дубина mit Anhänger von oben im gegebenen Skin.</summary>
    public static BitmapSource SkinImage(Art.Skin skin, int width, int height)
    {
        string id = $"skin:{skin.Id}:{width}x{height}";
        if (cache.TryGetValue(id, out var hit)) return hit;
        return cache[id] = RenderSkin(skin, width, height);
    }

    static BitmapSource RenderSkin(Art.Skin skin, int width, int height)
    {
        var keep = Art.Current;
        Art.Current = skin;
        try
        {
            var one = new Scene([new IconInfo("a", "", new Point(100, 100))], new Dictionary<string, Point> { ["a"] = new Point(100, 100) }, 48,
                [new Rect(0, 0, width, height)], new Rect(0, 0, width, height), true, height * 0.34);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Wall, null, new Rect(0, 0, width, height));
                one.Dubina.DrawPortrait(dc, new Point(width * 0.52, height * 0.5), -0.4, 0.35, 1.0, 0.5, 0.2);
            }
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
        finally { Art.Current = keep; }
    }

    /// <summary>Helfer von oben (DUM-E im aktuellen Skin, sonst Fahrzeug bzw. Drohnen).</summary>
    public static BitmapSource HelperImage(string kind, int width, int height)
    {
        if (kind == Helpers.Dume) return SkinImage(Art.Current, width, height);
        string id = $"helper:{kind}:{width}x{height}";
        if (cache.TryGetValue(id, out var hit)) return hit;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Wall, null, new Rect(0, 0, width, height));
            double size = kind == Helpers.Crane ? height * 0.2 : kind == Helpers.Chinook ? height * 0.27 : kind == Helpers.Mi26 ? height * 0.16 : height * 0.3;
            var at = kind == Helpers.Crane ? new Point(width * 0.42, height * 0.6) : new Point(width * 0.5, height * 0.5);
            Helpers.DrawPortrait(dc, kind, at, size);
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return cache[id] = bitmap;
    }

    static readonly Brush Wall = Frozen(new LinearGradientBrush(Color.FromRgb(0x24, 0x3B, 0x55), Color.FromRgb(0x5A, 0x3A, 0x4E), 35));
    static readonly Brush Folder = Frozen(new SolidColorBrush(Color.FromRgb(0xE7, 0xB4, 0x7B)));
    static readonly Brush Label = Frozen(new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)));
    static Brush Frozen(Brush b) { b.Freeze(); return b; }
}
