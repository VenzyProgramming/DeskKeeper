using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DeskKeeper;

/// <summary>
/// Öffentliche Ausgabe: eigenständiges Fenster im Stil der Windows-Einstellungen (Windows 10 oder 11, hell oder dunkel),
/// Deutsch/Englisch/Russisch. Seiten: Bildschirme (Geräteansicht, Anordnungen, Schnappschüsse – ohne Scrollen),
/// Szene, Werkzeuge, Einstellungen. Alle Bedienelemente selbst gezeichnet (keine WPF-Standardoptik).
/// </summary>
sealed class PublicWindow : Window, IDeskWindow
{
    readonly Look L = Look.Current;
    static string T(string key) => Loc.T(key);

    readonly Background? host;
    readonly bool test;
    public bool AllowClose { get; set; }
    readonly ScreensView screens = new();
    readonly StackPanel legend = new(), layoutsList = new(), snapshotsList = new();
    readonly TextBlock status = new(), pageTitle = new(), layoutsCount = new(), snapshotsCount = new();
    readonly Popup legendPopup = new();
    readonly Button save, restore, start;
    ToggleButton pickJericho = null!, pickNuke = null!, pickTu = null!;
    static readonly string[] Weapons = ["jericho", "nuke", Attacks.Tu160, Attacks.Tu22, Attacks.Su34, Attacks.Ka52, Attacks.Tu95, Attacks.Il76, Attacks.Ac130, Attacks.Shahed, Attacks.A10, Attacks.Su25, Attacks.F35, Attacks.Su57, Attacks.Duel, Attacks.B2, Attacks.MiG31, Attacks.Hunter, Attacks.Su35, Attacks.MQ9, Attacks.SR71, Attacks.Vacuum, Attacks.Gravity, Attacks.Cat, Attacks.Pug, Attacks.Shepherd];
    readonly Dictionary<string, ToggleButton> weaponTiles = [];
    string Weapon() => weaponTiles.FirstOrDefault(w => w.Value.IsChecked == true).Key ?? "jericho";
    readonly List<ToggleButton> skinButtons = [];
    readonly Dictionary<string, Image> scenePictures = [];
    readonly List<Image> skinPictures = [];
    readonly Dictionary<string, Image> helperPictures = [];
    readonly List<ToggleButton> helperButtons = [];
    string HelperId() => helperButtons.FirstOrDefault(b => b.IsChecked == true)?.Tag as string ?? Helpers.Dume;
    readonly FrameworkElement[] pages = new FrameworkElement[4];
    readonly Border[] navItems = new Border[4];
    readonly Grid root = new();
    readonly List<Action> refreshers = [];
    string? selectedKey;
    Store.Choice choice = Store.LoadChoice();
    static readonly string[] PageKeys = ["screens", "scene", "tools", "settings"];

    public PublicWindow(Background? host, bool test)
    {
        this.host = host; this.test = test;
        Title = "DeskKeeper";
        Width = 1180; Height = 790; MinWidth = 980; MinHeight = 680;
        Background = L.Bg; Foreground = L.Text;
        FontFamily = L.Font; FontSize = 14;
        UseLayoutRounding = true; SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var options = Store.LoadOptions();
        if (test) { WindowStartupLocation = WindowStartupLocation.Manual; Left = -20000; Top = -20000; ShowActivated = false; ShowInTaskbar = false; }
        else if (options.RememberWindow && options.Window is { Length: 4 } w && OnScreen(new Rect(w[0], w[1], w[2], w[3])))
        { WindowStartupLocation = WindowStartupLocation.Manual; Left = w[0]; Top = w[1]; Width = w[2]; Height = w[3]; }
        Resources.MergedDictionaries.Add(Styles.For(L.ThumbHex, L.ThumbHoverHex, L.TipFillHex, L.TipLineHex));
        screens.AccentColor = L.AccentColor;
        screens.ActiveLabel = T("active");
        var follow = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        follow.Tick += (_, _) => { if (IsVisible && WindowState != WindowState.Minimized) screens.ActiveId = selectedKey == Store.ScreenKey() && GetCursorPos(out var c) ? Monitors.Current().FirstOrDefault(m => m.Bounds.Contains(new Point(c.X, c.Y)))?.Id : null; };
        follow.Start();

        save = WinButton(T("save"), false, Save);
        restore = WinButton(T("restore"), false, () => Run(false));
        start = WinButton(T("start"), true, () => Run(true));

        // ------------------------------------------------ Navigation links
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(272) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var nav = new DockPanel { Background = L.Nav, LastChildFill = false };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 20, 16, 22) };
        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(new TextBlock { Text = "DeskKeeper", FontSize = 15, FontWeight = FontWeights.SemiBold });
        names.Children.Add(new TextBlock { Text = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(2) ?? "", FontSize = 12, Foreground = L.Tertiary });
        brand.Children.Add(names);
        DockPanel.SetDock(brand, Dock.Top); nav.Children.Add(brand);
        string[] glyphs = ["", "", "", ""];
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            navItems[i] = NavItem(glyphs[i], T(PageKeys[i]), () => Go(index));
            DockPanel.SetDock(navItems[i], i == 3 ? Dock.Bottom : Dock.Top);
        }
        nav.Children.Add(navItems[0]); nav.Children.Add(navItems[1]); nav.Children.Add(navItems[2]);
        status.FontSize = 12; status.Foreground = L.Tertiary; status.TextWrapping = TextWrapping.Wrap; status.Margin = new Thickness(22, 0, 18, 14);
        DockPanel.SetDock(status, Dock.Bottom);
        navItems[3].Margin = new Thickness(L.Win10 ? 0 : 10, 0, L.Win10 ? 0 : 10, 14);
        nav.Children.Add(navItems[3]); nav.Children.Add(status);
        root.Children.Add(nav);

        // ------------------------------------------------ Inhalt
        var content = new Grid { Margin = new Thickness(36, 24, 36, 0) };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var header = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        pageTitle.FontSize = 28; pageTitle.FontWeight = L.Win10 ? FontWeights.Light : FontWeights.SemiBold; pageTitle.FontFamily = L.Display;
        header.Children.Add(pageTitle);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(save); actions.Children.Add(restore); actions.Children.Add(start);
        Grid.SetColumn(actions, 1); header.Children.Add(actions);
        content.Children.Add(header);

        pages[0] = ScreensPage();
        pages[1] = Scroll(ScenePage());   // 20.11.2026: alle Szenen erreichbar
        pages[2] = Scroll(ToolsPage());
        pages[3] = Scroll(SettingsPage());
        foreach (var p in pages) { Grid.SetRow(p, 1); content.Children.Add(p); }
        Grid.SetColumn(content, 1);
        root.Children.Add(content);
        Content = root;

        SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            int dark = L.Dark ? 1 : 0, caption = ToColorRef(((SolidColorBrush)L.Bg).Color), text = ToColorRef(((SolidColorBrush)L.Text).Color), backdrop = 2;
            DwmSetWindowAttribute(h, 20, ref dark, sizeof(int));
            if (!L.Win10) { DwmSetWindowAttribute(h, 35, ref caption, sizeof(int)); DwmSetWindowAttribute(h, 36, ref text, sizeof(int)); DwmSetWindowAttribute(h, 38, ref backdrop, sizeof(int)); }
        };
        Closing += (_, e) =>
        {
            RememberPlace();
            if (AllowClose || this.host is null) return;
            e.Cancel = true;
            if (Store.LoadOptions().CloseToTray) Hide(); else this.host.Exit();
        };
        Loaded += (_, _) =>
        {
            status.Text = host?.Recovered ?? "";
            Go(0);
            Dispatcher.BeginInvoke(LoadPictures, System.Windows.Threading.DispatcherPriority.Background);
        };
    }

    // ================================================================ Seiten
    /// <summary>Bildschirme: Geräteansicht mit i-Knopf (Legende), darunter Anordnungen und Schnappschüsse nebeneinander.</summary>
    FrameworkElement ScreensPage()
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 260 });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(250) });
        // Geräteansicht füllt die Karte; der i-Knopf blendet die Geräteliste ein
        var stage = new Grid();
        screens.Margin = new Thickness(10, 12, 10, 12);
        stage.Children.Add(screens);
        var info = IconButton("", T("info"), () => legendPopup.IsOpen = !legendPopup.IsOpen);
        info.HorizontalAlignment = HorizontalAlignment.Right; info.VerticalAlignment = VerticalAlignment.Top; info.Margin = new Thickness(0, 10, 10, 0);
        stage.Children.Add(info);
        legendPopup.PlacementTarget = info; legendPopup.Placement = PlacementMode.Left; legendPopup.StaysOpen = false; legendPopup.AllowsTransparency = true;
        legendPopup.HorizontalOffset = -6;
        legendPopup.Child = LegendCard();
        grid.Children.Add(CardBox(stage, new Thickness(0, 0, 0, 14)));

        var lists = new Grid();
        lists.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        lists.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        lists.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        lists.Children.Add(ListColumn(T("layoutsTitle"), layoutsCount, layoutsList));
        var right = ListColumn(T("snapsTitle"), snapshotsCount, snapshotsList); Grid.SetColumn(right, 2); lists.Children.Add(right);
        Grid.SetRow(lists, 1); grid.Children.Add(lists);
        return grid;
    }

    Border LegendCard() => new()
    {
        Child = new StackPanel { Children = { new TextBlock { Text = T("info"), FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = L.Text, Margin = new Thickness(0, 0, 0, 8) }, legend } },
        Background = L.Card, BorderBrush = L.ButtonStroke, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(L.Radius), Padding = new Thickness(14, 12, 16, 10), Margin = new Thickness(8),
        Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.35, Color = Colors.Black },
    };

    FrameworkElement ListColumn(string title, TextBlock count, StackPanel list)
    {
        var dock = new DockPanel();
        var head = SectionLabel(title, count); head.Margin = new Thickness(2, 0, 0, 8);
        DockPanel.SetDock(head, Dock.Top); dock.Children.Add(head);
        dock.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 0, 12) });
        return dock;
    }

    FrameworkElement ScenePage()
    {
        var p = new StackPanel();
        var scenes = new UniformGrid { Columns = 4, Margin = new Thickness(16) };
        string known = Weapons.Contains(choice.Weapon) ? choice.Weapon : "jericho";
        foreach (var id in Weapons) weaponTiles[id] = Tile(id, T("w." + id), known == id, 16.0 / 9, scenePictures);
        pickJericho = weaponTiles["jericho"]; pickNuke = weaponTiles["nuke"]; pickTu = weaponTiles["tu160"];
        var weapons = weaponTiles.Values.ToArray();
        foreach (var w in weapons)
        {
            var me = w;
            me.Checked += (_, _) => { foreach (var o in weapons) if (o != me) o.IsChecked = false; Remember(); };
            me.Unchecked += (_, _) => { if (!weapons.Any(o => o.IsChecked == true)) me.IsChecked = true; };
            me.Margin = new Thickness(0, 0, 10, 10);
            scenes.Children.Add(me);
        }
        var first = SectionLabel(T("destruction"), null); first.Margin = new Thickness(2, 0, 0, 8);
        p.Children.Add(first);
        p.Children.Add(CardBox(scenes, new Thickness(0)));
        var skins = new UniformGrid { Columns = Art.Skins.Length, Margin = new Thickness(16) };
        int chosen = Math.Max(0, Array.FindIndex(Art.Skins, s => s.Id == choice.Skin));
        Art.Current = Art.Skins[chosen];
        for (int i = 0; i < Art.Skins.Length; i++)
        {
            int index = i;
            var chip = Tile(Art.Skins[i].Id, Art.SkinName(Art.Skins[i]), i == chosen, 4.0 / 3, null);
            chip.Margin = new Thickness(0, 0, i == Art.Skins.Length - 1 ? 0 : 10, 0);
            chip.Checked += (_, _) => { foreach (var o in skinButtons) if (o != chip) o.IsChecked = false; Art.Current = Art.Skins[index]; Remember(); };
            chip.Unchecked += (_, _) => { if (!skinButtons.Any(b => b.IsChecked == true)) chip.IsChecked = true; };
            skinButtons.Add(chip); skins.Children.Add(chip);
        }
        p.Children.Add(SectionLabel(T("robot"), null));
        p.Children.Add(CardBox(skins, new Thickness(0)));
        var helpers = new UniformGrid { Columns = 3, Margin = new Thickness(16) };
        string helperKnown = Helpers.All.Contains(choice.Helper) ? choice.Helper : Helpers.Dume;
        foreach (var kind in Helpers.All)
        {
            var chip = Tile(kind, T("h." + kind), kind == helperKnown, 16.0 / 9, helperPictures);
            chip.Tag = kind;
            chip.Margin = new Thickness(0, 0, Array.IndexOf(Helpers.All, kind) % 3 == 2 ? 0 : 10, Array.IndexOf(Helpers.All, kind) < 3 ? 10 : 0);
            chip.Checked += (_, _) => { foreach (var o in helperButtons) if (o != chip) o.IsChecked = false; Remember(); };
            chip.Unchecked += (_, _) => { if (!helperButtons.Any(b => b.IsChecked == true)) chip.IsChecked = true; };
            helperButtons.Add(chip); helpers.Children.Add(chip);
        }
        p.Children.Add(SectionLabel(T("helper"), null));
        p.Children.Add(CardBox(helpers, new Thickness(0)));
        return p;
    }

    FrameworkElement ToolsPage()
    {
        var p = new StackPanel { Margin = new Thickness(0, 0, 0, 24) };
        var first = SectionLabel(T("t.windows"), null); first.Margin = new Thickness(2, 0, 0, 8); p.Children.Add(first);
        p.Children.Add(Row("", T("t.minimize"), WinButton(T("t.do"), false, DeskTools.MinimizeAll)));
        p.Children.Add(SectionLabel(T("t.desktop"), null));
        p.Children.Add(Row("", T("t.hide"), SwitchControl(() => DeskTools.IconsShown, v => DeskTools.IconsShown = v)));
        p.Children.Add(Row("", T("t.size"), Segments([(T("t.small"), "32"), (T("t.medium"), "48"), (T("t.large"), "96")],
            () => DeskTools.IconSize switch { <= 36 => "32", >= 80 => "96", _ => "48" }, v => DeskTools.IconSize = int.Parse(v))));
        p.Children.Add(Row("", T("t.grid"), SwitchControl(() => DeskTools.Grid, v => DeskTools.Grid = v)));
        p.Children.Add(Row("", T("t.arrange"), SwitchControl(() => DeskTools.Arrange, v => DeskTools.Arrange = v)));
        p.Children.Add(Row("", T("t.keepGrid"), SwitchControl(() => Store.LoadOptions().KeepGrid, v => Store.SaveOptions(Store.LoadOptions() with { KeepGrid = v }))));
        p.Children.Add(Row("", T("t.sort"), WinButton(T("t.sortGo"), false, () => _ = SortByCategory())));
        p.Children.Add(SectionLabel(T("t.file"), null));
        p.Children.Add(Row("", T("t.export"), WinButton(T("t.choose"), false, ExportFile)));
        p.Children.Add(Row("", T("t.import"), WinButton(T("t.choose"), false, ImportFile)));
        return p;
    }

    FrameworkElement SettingsPage()
    {
        var p = new StackPanel { Margin = new Thickness(0, 0, 0, 24) };
        static Store.Options O() => Store.LoadOptions();
        void Set(Func<Store.Options, Store.Options> change) { Store.SaveOptions(change(O())); }
        var first = SectionLabel(T("s.restore"), null); first.Margin = new Thickness(2, 0, 0, 8); p.Children.Add(first);
        p.Children.Add(Row("", T("s.watch"), SwitchControl(() => O().Watch, v => { if (host is not null) host.SetWatch(v); else Set(o => o with { Watch = v }); })));
        p.Children.Add(Row("", T("s.mode"), Segments([(T("s.instant"), "instant"), (T("s.withRobot"), "robot")], () => O().RestoreMode, v => Set(o => o with { RestoreMode = v }))));
        p.Children.Add(Row("", T("s.atLogin"), SwitchControl(() => O().RestoreAtLogin, v => Set(o => o with { RestoreAtLogin = v }))));
        p.Children.Add(Row("", T("s.atStart"), SwitchControl(() => O().RestoreAtStart, v => Set(o => o with { RestoreAtStart = v }))));
        p.Children.Add(SectionLabel(T("s.saving"), null));
        p.Children.Add(Row("", T("s.autosnap"), Segments([(T("s.off"), "0"), (string.Format(T("s.min"), 5), "5"), (string.Format(T("s.min"), 15), "15"), (string.Format(T("s.min"), 60), "60")],
            () => O().AutoSnapshotMinutes.ToString(), v => Set(o => o with { AutoSnapshotMinutes = int.Parse(v) }))));
        p.Children.Add(Row("", T("s.onShutdown"), SwitchControl(() => O().SaveOnShutdown, v => Set(o => o with { SaveOnShutdown = v }))));
        p.Children.Add(Row("", T("s.onSwitch"), SwitchControl(() => O().SaveOnUserSwitch, v => Set(o => o with { SaveOnUserSwitch = v }))));
        p.Children.Add(Row("", T("s.onExit"), SwitchControl(() => O().SaveOnExit, v => Set(o => o with { SaveOnExit = v }))));
        p.Children.Add(SectionLabel(T("s.program"), null));
        var auto = SwitchControl(() => Autostart.Enabled, v => host?.SetAutostart(v)); auto.IsEnabled = host is not null;
        p.Children.Add(Row("", T("s.autostart"), auto));
        p.Children.Add(Row("", T("s.minimized"), SwitchControl(() => O().StartMinimized, v => Set(o => o with { StartMinimized = v }))));
        p.Children.Add(Row("", T("s.toTray"), SwitchControl(() => O().CloseToTray, v => Set(o => o with { CloseToTray = v }))));
        p.Children.Add(Row("", T("s.window"), SwitchControl(() => O().RememberWindow, v => Set(o => o with { RememberWindow = v }))));
        p.Children.Add(Row("", T("s.theme"), Segments([(T("s.system"), "system"), (T("s.dark"), "dark"), (T("s.light"), "light")], () => O().Theme,
            v => { Set(o => o with { Theme = v }); Look.Reset(); host?.Rebuild(); })));
        p.Children.Add(Row("", T("s.language"), Segments([(T("s.automatic"), "auto"), ("Deutsch", "de"), ("English", "en"), ("Русский", "ru")], () => O().Language,
            v => { Set(o => o with { Language = v }); host?.Rebuild(); })));
        return p;
    }

    // ================================================================ Ablauf
    public void Open()
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Refresh();
        Activate();
    }

    void Go(int index)
    {
        for (int i = 0; i < pages.Length; i++) pages[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
        pageTitle.Text = T(PageKeys[index]);
        save.Visibility = restore.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        start.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        for (int i = 0; i < navItems.Length; i++) StyleNav(navItems[i], i == index);
        Refresh();
    }

    public void Refresh()
    {
        string key = Store.ScreenKey();
        restore.IsEnabled = Store.LoadProfile() is not null && !Runner.Busy;
        foreach (var r in refreshers) r();
        var known = Store.Known();
        if (selectedKey is null || !known.Any(x => x.Key == selectedKey)) selectedKey = key;
        if (status.Text.Length == 0 && Store.LoadAuto(key) is { } cur) status.Text = $"{Store.Describe(key)} · {Loc.Count(cur.Count, "icons")}";
        var list = Store.ScreensOf(selectedKey);
        var desk = ViewData.Icons(selectedKey, key, list);
        screens.IconPixels = ViewData.IconSize;
        ShowScreens(list, desk);
        layoutsCount.Text = known.Count.ToString();
        layoutsList.Children.Clear();
        foreach (var (k, count, when) in known.OrderByDescending(x => x.Key == key).ThenByDescending(x => x.Saved))
            layoutsList.Children.Add(LayoutRow(k, count, when, k == key));
        if (known.Count == 0) layoutsList.Children.Add(EmptyRow(T("nothing")));
        var snaps = Store.Snapshots(selectedKey);
        snapshotsCount.Text = snaps.Count.ToString();
        snapshotsList.Children.Clear();
        foreach (var (file, time, count) in snaps) snapshotsList.Children.Add(SnapshotRow(file, time, count, selectedKey ?? key));
        if (snaps.Count == 0) snapshotsList.Children.Add(EmptyRow(T("noSnaps")));
    }

    public void KeeperActed(string what)
    {
        if (what == "restored") status.Text = Store.LoadOptions().RestoreMode == "robot" ? T("m.restoring") : T("m.restored");
        Refresh();
    }

    void ShowScreens(IReadOnlyList<MonitorInfo> list, IReadOnlyList<DeskIcon> desk)
    {
        screens.Show(list, desk);
        legend.Children.Clear();
        foreach (var m in list.OrderBy(m => m.Number))
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            row.Children.Add(new Border
            {
                Width = 22, Height = 22, CornerRadius = new CornerRadius(L.Win10 ? 0 : 11), Margin = new Thickness(0, 0, 10, 0), Background = m.Primary ? L.Accent : L.Selected,
                Child = new TextBlock { Text = m.Number.ToString(), FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = m.Primary ? L.AccentText : L.Text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            });
            var texts = new StackPanel();
            texts.Children.Add(new TextBlock { Text = ViewText.Device(m), FontSize = 13, Foreground = L.Text });
            if (m.Primary) texts.Children.Add(new TextBlock { Text = T("primary"), FontSize = 12, Foreground = L.Tertiary });
            row.Children.Add(texts);
            legend.Children.Add(row);
        }
    }

    void RememberPlace()
    {
        if (test || WindowState != WindowState.Normal || !Store.LoadOptions().RememberWindow) return;
        Store.SaveOptions(Store.LoadOptions() with { Window = [Left, Top, Width, Height] });
    }

    static bool OnScreen(Rect r) => System.Windows.Forms.Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(new System.Drawing.Rectangle((int)r.X, (int)r.Y, (int)Math.Max(40, r.Width), 40)));

    // ---------------------------------------------------------------- Vorschaubilder, Prüfung
    void LoadPictures()
    {
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var queue = new Queue<Action>();
        foreach (var (id, image) in scenePictures) { var (i, img) = (id, image); queue.Enqueue(() => img.Source = Preview.SceneImage(i, (int)Math.Ceiling(Math.Max(img.ActualWidth, 220) * dpi))); }
        for (int k = 0; k < skinPictures.Count; k++)
        {
            int index = k; var img = skinPictures[k];
            queue.Enqueue(() => { int w = (int)Math.Ceiling(Math.Max(img.ActualWidth, 110) * dpi); img.Source = Preview.SkinImage(Art.Skins[index], w, (int)Math.Round(w * 3.0 / 4)); });
        }
        foreach (var (id, image) in helperPictures)
        {
            var (kind, img) = (id, image);
            queue.Enqueue(() => { int w = (int)Math.Ceiling(Math.Max(img.ActualWidth, 90) * dpi); img.Source = Preview.HelperImage(kind, w, (int)Math.Round(w * 9.0 / 16)); });
        }
        void Next()
        {
            if (queue.Count == 0) return;
            try { queue.Dequeue()(); } catch (Exception error) { Fault.Report(error); }
            Dispatcher.BeginInvoke(Next, System.Windows.Threading.DispatcherPriority.Background);
        }
        if (!test) Next(); else while (queue.Count > 0) queue.Dequeue()();
    }

    /// <summary>Werbevideo (--showcase): Teile der Oberfläche, die der Film anzeigt und umschaltet. Nur Darstellung: Seitenwechsel,
    /// Auswahl von Szene und Helfer (gespeichert in der Datenkopie), Scrollen – nie Speichern, Start oder Werkzeuge auslösen.</summary>
    internal sealed record ShowParts(Grid Root, Action<int> Go, IReadOnlyDictionary<string, ToggleButton> Scenes, IReadOnlyList<ToggleButton> HelperChips,
        Button Start, Button Save, Border[] Nav, ScrollViewer ScenePage);

    internal ShowParts ForShowcase()
    {
        Go(1); UpdateLayout(); LoadPictures(); Go(0); UpdateLayout();
        return new ShowParts(root, Go, weaponTiles, helperButtons, start, save, navItems, (ScrollViewer)pages[1]);
    }

    public void UiTest(string png)
    {
        string Name(string suffix) => System.IO.Path.ChangeExtension(png, null) + suffix + ".png";
        Go(1); UpdateLayout(); LoadPictures();
        foreach (var chip in skinButtons) { chip.IsChecked = true; Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background); }
        if (skinButtons.Count(b => b.IsChecked == true) != 1 || Art.Current != Art.Skins[^1]) throw new InvalidOperationException("Skin-Auswahl uneindeutig");
        pickNuke.IsChecked = true; pickTu.IsChecked = true; pickJericho.IsChecked = true;
        if (pickTu.IsChecked == true || pickNuke.IsChecked == true) throw new InvalidOperationException("Waffenwahl uneindeutig");
        if (scenePictures.Values.Any(i => i.Source is null) || skinPictures.Any(i => i.Source is null)) throw new InvalidOperationException("Vorschaubild fehlt");
        skinButtons[1].IsChecked = true; pickTu.IsChecked = true;
        Snapshot(Name("-szene"));
        Go(2); Snapshot(Name("-werkzeuge"));
        Go(3); Snapshot(Name("-einstellungen"));
        Go(0); Snapshot(png);
        var stageCard = (FrameworkElement)((Grid)pages[0]).Children[0];
        var at = stageCard.TransformToAncestor(root).Transform(new Point(stageCard.ActualWidth, 0));
        System.IO.File.WriteAllText(System.IO.Path.ChangeExtension(png, null) + "-rand.txt", $"fenster {root.ActualWidth:0} karte-rechts {at.X:0} abstand {root.ActualWidth - at.X:0}");
        if (Store.ScreensOf(Store.ScreenKey()).FirstOrDefault(m => m.Primary) is { } main)
        { screens.Zoom(main); screens.UpdateLayout(); screens.HoverFirst(); Snapshot(Name("-gross")); screens.Zoom(null); }
        var (demo, dots) = ViewData.Demo();
        ShowScreens(demo, dots);
        Snapshot(Name("-drei"));
        // Legende (Inhalt des i-Knopfs) einzeln
        var card = (Border)legendPopup.Child; legendPopup.Child = null;
        var effect = card.Effect; card.Effect = null;
        var holder = new Border { Child = card, Background = L.Bg };
        holder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); holder.Arrange(new Rect(holder.DesiredSize)); holder.UpdateLayout();
        SnapshotOf(holder, Name("-legende"));
        holder.Child = null; card.Effect = effect; legendPopup.Child = card;
    }

    void Snapshot(string png) => SnapshotOf(root, png);

    void SnapshotOf(FrameworkElement element, string png)
    {
        element.UpdateLayout();
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * scale), (int)Math.Ceiling(element.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        // Hintergrund, dann das Element selbst (ohne VisualBrush: der dehnt auf die Inhaltsgrenzen und verzerrt die Ränder)
        var v = new DrawingVisual();
        using (var dc = v.RenderOpen()) dc.DrawRectangle(L.Bg, null, new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        bitmap.Render(v);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = System.IO.File.Create(png); encoder.Save(file);
    }

    // ---------------------------------------------------------------- Aktionen
    void Remember()
    {
        choice = new Store.Choice(Weapon(), Art.Current.Id) { Helper = HelperId() };
        Helpers.Current = choice.Helper;
        Store.SaveChoice(choice);
    }

    void Save()
    {
        try { using var d = new ShellDesktop(); var snap = d.Snapshot(); Store.SaveSnapshot(Store.ScreenKey(), snap); status.Text = string.Format(T("m.saved"), snap.Count); Refresh(); }
        catch (Exception error) { status.Text = error.Message; }
    }

    /// <summary>Wiederherstellen: laut Einstellung sofort oder mit Дубина; „Start“: Zerstörungsszene.</summary>
    async void Run(bool jericho)
    {
        if (!jericho && Store.LoadOptions().RestoreMode != "robot")
        {
            if (Store.LoadProfile() is { } layout) { RealWorld.Restore(layout); status.Text = T("m.restored"); }
            Refresh(); return;
        }
        await Play(jericho, Store.LoadProfile());
    }

    async System.Threading.Tasks.Task SortByCategory()
    {
        if (Runner.Busy) return;
        try { status.Text = await Sorter.Run(async layout => await Runner.Play(false, layout, showDesktop: true)); }
        catch (Exception error) { status.Text = error.Message; }
    }

    async System.Threading.Tasks.Task Play(bool jericho, Dictionary<string, int[]>? layout)
    {
        save.IsEnabled = restore.IsEnabled = start.IsEnabled = false;
        var state = WindowState;
        WindowState = WindowState.Minimized;
        try { string weapon = Weapon(); status.Text = await Runner.Play(jericho, layout, jericho && weapon == "nuke", showDesktop: true, attack: jericho && Attacks.All.Contains(weapon) ? weapon : null); }
        finally { save.IsEnabled = start.IsEnabled = true; WindowState = state; Refresh(); Activate(); }
    }

    void ExportFile()
    {
        try
        {
            using var d = new ShellDesktop();
            var dialog = new Microsoft.Win32.SaveFileDialog { FileName = "DeskKeeper " + DateTime.Now.ToString("yyyy-MM-dd HH-mm"), DefaultExt = ".deskkeeper", Filter = "DeskKeeper|*.deskkeeper" };
            if (dialog.ShowDialog(this) != true) return;
            Store.Export(dialog.FileName, Store.ScreenKey(), d.Snapshot());
            status.Text = string.Format(T("m.exported"), System.IO.Path.GetFileName(dialog.FileName));
        }
        catch (Exception error) { status.Text = error.Message; }
    }

    async void ImportFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "DeskKeeper|*.deskkeeper" };
        if (dialog.ShowDialog(this) != true) return;
        if (Store.Import(dialog.FileName) is not { } file) { status.Text = T("m.badFile"); return; }
        if (Store.LoadOptions().RestoreMode == "robot") await Play(false, file.Icons);
        else { RealWorld.Restore(file.Icons); status.Text = T("m.restored"); Refresh(); }
    }

    // ================================================================ Bausteine (Windows-Einstellungen nachempfunden, selbst gezeichnet)
    Border NavItem(string glyph, string label, Action click)
    {
        var g = new Grid { Height = 38 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(L.Win10 ? 4 : 3) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var pill = new Border { Width = L.Win10 ? 4 : 3, Height = L.Win10 ? 38 : 16, CornerRadius = new CornerRadius(L.Win10 ? 0 : 1.5), Background = L.Accent, VerticalAlignment = VerticalAlignment.Center };
        var icon = new TextBlock { Text = glyph, FontFamily = L.Icons, FontSize = 16, Margin = new Thickness(13, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
        var text = new TextBlock { Text = label, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(icon, 1); Grid.SetColumn(text, 2);
        g.Children.Add(pill); g.Children.Add(icon); g.Children.Add(text);
        var b = new Border { Child = g, CornerRadius = new CornerRadius(L.Win10 ? 0 : 5), Margin = new Thickness(L.Win10 ? 0 : 10, 0, L.Win10 ? 0 : 10, 4), Cursor = Cursors.Hand, Background = Brushes.Transparent, Tag = pill };
        b.MouseEnter += (_, _) => { if (b.Background != L.Selected) b.Background = L.Hover; };
        b.MouseLeave += (_, _) => { if (b.Background != L.Selected) b.Background = Brushes.Transparent; };
        b.MouseLeftButtonUp += (_, _) => click();
        return b;
    }

    void StyleNav(Border b, bool on) { b.Background = on ? L.Selected : Brushes.Transparent; ((Border)b.Tag).Visibility = on ? Visibility.Visible : Visibility.Hidden; }

    Border CardBox(UIElement child, Thickness margin) => new()
    {
        Child = child, Background = L.Card, BorderBrush = L.Stroke, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(L.Radius), Margin = margin,
    };

    FrameworkElement SectionLabel(string title, TextBlock? count)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(2, 24, 0, 8) };
        row.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold });
        if (count is not null) { count.FontSize = 13; count.Foreground = L.Tertiary; count.Margin = new Thickness(8, 1, 0, 0); row.Children.Add(count); }
        return row;
    }

    static ScrollViewer Scroll(FrameworkElement content) => new() { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 16, 0) };

    /// <summary>Einstellungszeile: Symbol, Titel, rechts das Bedienelement.</summary>
    Border Row(string glyph, string title, FrameworkElement control)
    {
        var g = new Grid { MinHeight = 62 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(new TextBlock { Text = glyph, FontFamily = L.Icons, FontSize = 18, Margin = new Thickness(20, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center });
        var t = new TextBlock { Text = title, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 16, 0) }; Grid.SetColumn(t, 1); g.Children.Add(t);
        control.Margin = new Thickness(0, 0, 18, 0); control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 2); g.Children.Add(control);
        var card = CardBox(g, new Thickness(0, 0, 0, L.Win10 ? 2 : 3));
        card.MouseEnter += (_, _) => card.Background = L.CardHover; card.MouseLeave += (_, _) => card.Background = L.Card;
        return card;
    }

    /// <summary>Schalter mit Zustandstext („Ein/Aus“) links davon, wie in den Windows-Einstellungen.</summary>
    FrameworkElement SwitchControl(Func<bool> get, Action<bool> set)
    {
        var state = new TextBlock { FontSize = 14, Foreground = L.Secondary, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        var toggle = Switch(get());
        void Show() { bool v = get(); toggle.IsChecked = v; state.Text = v ? T("on") : T("off"); }
        toggle.Click += (_, _) => { set(toggle.IsChecked == true); Show(); };
        refreshers.Add(Show); Show();
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(state); row.Children.Add(toggle);
        row.IsEnabledChanged += (_, _) => toggle.IsEnabled = row.IsEnabled;
        return row;
    }

    /// <summary>Auswahl aus wenigen Werten als Knopfgruppe (statt Aufklappliste).</summary>
    FrameworkElement Segments((string Label, string Value)[] items, Func<string> get, Action<string> set)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var buttons = new List<(Border B, TextBlock T, string V)>();
        void Show()
        {
            string v = get();
            foreach (var (b, t, value) in buttons) { bool on = value == v; b.Background = on ? L.Accent : L.ButtonFill; t.Foreground = on ? L.AccentText : L.Text; b.BorderBrush = on ? L.Accent : L.ButtonStroke; }
        }
        for (int i = 0; i < items.Length; i++)
        {
            var (label, value) = items[i];
            var t = new TextBlock { Text = label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            double r = L.ButtonRadius;
            var b = new Border
            {
                Child = t, Height = 32, MinWidth = 72, Padding = new Thickness(14, 0, 14, 1), BorderThickness = new Thickness(1, 1, i == items.Length - 1 ? 1 : 0, 1), Cursor = Cursors.Hand,
                CornerRadius = new CornerRadius(i == 0 ? r : 0, i == items.Length - 1 ? r : 0, i == items.Length - 1 ? r : 0, i == 0 ? r : 0),
            };
            b.MouseLeftButtonUp += (_, _) => { set(value); Show(); };
            buttons.Add((b, t, value)); panel.Children.Add(b);
        }
        refreshers.Add(Show); Show();
        return panel;
    }

    Border LayoutRow(string key, int count, DateTime when, bool current)
    {
        var g = new Grid { Height = 58 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(new TextBlock { Text = "", FontFamily = L.Icons, FontSize = 17, Margin = new Thickness(18, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center });
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(new TextBlock { Text = Store.Describe(key), FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis });
        texts.Children.Add(new TextBlock { Text = $"{Loc.Count(count, "icons")} · {Loc.Ago(when)}", FontSize = 12, Foreground = L.Tertiary, Margin = new Thickness(0, 2, 0, 0) });
        Grid.SetColumn(texts, 1); g.Children.Add(texts);
        if (current)
        {
            var tag = new Border { CornerRadius = new CornerRadius(L.Win10 ? 0 : 4), Background = L.AccentSoft, Padding = new Thickness(8, 3, 8, 4), Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = T("current"), FontSize = 12, Foreground = L.Accent } };
            Grid.SetColumn(tag, 2); g.Children.Add(tag);
        }
        bool selected = key == selectedKey;
        var card = CardBox(g, new Thickness(0, 0, 0, L.Win10 ? 2 : 3));
        card.Background = selected ? L.CardHover : L.Card;
        if (selected) card.BorderBrush = L.ButtonStroke;
        card.Cursor = Cursors.Hand;
        card.MouseEnter += (_, _) => card.Background = L.CardHover;
        card.MouseLeave += (_, _) => { if (key != selectedKey) card.Background = L.Card; };
        card.MouseLeftButtonUp += (_, _) => { selectedKey = key; Refresh(); };
        return card;
    }

    Border SnapshotRow(string file, DateTime time, int count, string from)
    {
        var g = new Grid { Height = 58 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bool auto = file.EndsWith("-auto.json", StringComparison.OrdinalIgnoreCase);
        g.Children.Add(new TextBlock { Text = auto ? "" : "", FontFamily = L.Icons, FontSize = 16, Margin = new Thickness(18, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center });
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(new TextBlock { Text = time.ToString(Loc.Lang == "en" ? "MM/dd/yyyy  h:mm tt" : "dd.MM.yyyy  HH:mm", System.Globalization.CultureInfo.InvariantCulture), FontSize = 14 });
        texts.Children.Add(new TextBlock { Text = Loc.Count(count, "icons") + (auto ? " · " + T("auto") : "") + " · " + Loc.Ago(time), FontSize = 12, Foreground = L.Tertiary, Margin = new Thickness(0, 2, 0, 0) });
        Grid.SetColumn(texts, 1); g.Children.Add(texts);
        var back = WinButton(T("restore"), false, async () =>
        {
            if (Store.LoadSnapshot(file) is not { } saved) return;
            var layout = RealWorld.Translate(from, saved);
            if (Store.LoadOptions().RestoreMode == "robot") await Play(false, layout);
            else { RealWorld.Restore(layout); status.Text = T("m.restored"); Refresh(); }
        });
        back.MinWidth = 0; back.IsEnabled = !Runner.Busy;
        Grid.SetColumn(back, 2); g.Children.Add(back);
        var remove = IconButton("", T("remove"), () => { Store.RemoveSnapshot(file); Refresh(); });
        remove.Margin = new Thickness(6, 0, 12, 0);
        Grid.SetColumn(remove, 3); g.Children.Add(remove);
        return CardBox(g, new Thickness(0, 0, 0, L.Win10 ? 2 : 3));
    }

    Border EmptyRow(string text) => CardBox(new TextBlock { Text = text, Foreground = L.Tertiary, Margin = new Thickness(18, 18, 18, 18) }, new Thickness(0, 0, 0, 3));

    Button WinButton(string label, bool accent, Action click)
    {
        var template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
            "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>" +
            "<Border x:Name='b' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='" + (L.Win10 ? "2" : "1") + "' CornerRadius='" + L.ButtonRadius + "' Padding='{TemplateBinding Padding}'>" +
            "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>" +
            "<ControlTemplate.Triggers><Trigger Property='IsEnabled' Value='False'><Setter TargetName='b' Property='Opacity' Value='0.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
        var idle = accent ? L.Accent : L.ButtonFill;
        var b = new Button
        {
            Content = label, Template = template, Height = 34, MinWidth = 120, Padding = new Thickness(18, 0, 18, 1), Margin = new Thickness(8, 0, 0, 0), FontSize = 14,
            Background = idle, BorderBrush = accent ? L.Accent : L.ButtonStroke, Foreground = accent ? L.AccentText : L.Text, Cursor = Cursors.Hand,
        };
        b.MouseEnter += (_, _) => b.Background = accent ? L.AccentHover : L.ButtonHover;
        b.MouseLeave += (_, _) => b.Background = idle;
        b.Click += (_, _) => click();
        return b;
    }

    Button IconButton(string glyph, string name, Action click)
    {
        var b = WinButton(glyph, false, click);
        b.FontFamily = L.Icons; b.FontSize = 14; b.MinWidth = 0; b.Width = 34; b.Height = 34; b.Padding = new Thickness(0); b.Margin = new Thickness(0);
        b.ToolTip = name;
        System.Windows.Automation.AutomationProperties.SetName(b, name);
        return b;
    }

    /// <summary>Schalter wie im jeweiligen Windows: 11 rund mit wachsendem Knopf, 10 mit kräftigem Rand.</summary>
    ToggleButton Switch(bool on)
    {
        string accent = ((SolidColorBrush)L.Accent).Color.ToString(), off = ((SolidColorBrush)L.ToggleOff).Color.ToString(),
            knobOff = ((SolidColorBrush)L.ToggleKnobOff).Color.ToString(), knobOn = ((SolidColorBrush)L.ToggleKnobOn).Color.ToString();
        string border = L.Win10 ? "2" : "1", small = L.Win10 ? "10" : "12", big = L.Win10 ? "10" : "14", onMargin = L.Win10 ? "29,0,0,0" : "23,0,0,0";
        var template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
            "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ToggleButton'>" +
            $"<Grid Width='{(L.Win10 ? 44 : 40)}' Height='20' Background='Transparent'><Border x:Name='track' CornerRadius='10' BorderThickness='{border}' BorderBrush='{off}' Background='#00000000'/>" +
            $"<Ellipse x:Name='knob' Width='{small}' Height='{small}' Fill='{knobOff}' HorizontalAlignment='Left' Margin='{(L.Win10 ? "5,0,0,0" : "4,0,0,0")}'/></Grid>" +
            "<ControlTemplate.Triggers>" +
            $"<Trigger Property='IsChecked' Value='True'><Setter TargetName='track' Property='Background' Value='{accent}'/><Setter TargetName='track' Property='BorderBrush' Value='{accent}'/>" +
            $"<Setter TargetName='knob' Property='Fill' Value='{knobOn}'/><Setter TargetName='knob' Property='Width' Value='{big}'/><Setter TargetName='knob' Property='Height' Value='{big}'/><Setter TargetName='knob' Property='Margin' Value='{onMargin}'/></Trigger>" +
            "<Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger>" +
            "</ControlTemplate.Triggers></ControlTemplate>");
        return new ToggleButton { Template = template, IsChecked = on, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center };
    }

    ToggleButton Tile(string id, string label, bool on, double aspect, Dictionary<string, Image>? register)
    {
        var template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
            "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ToggleButton'>" +
            $"<Border Background='{{TemplateBinding Background}}' BorderBrush='{{TemplateBinding BorderBrush}}' BorderThickness='2' CornerRadius='{L.Radius}' Padding='4'>" +
            "<ContentPresenter/></Border></ControlTemplate>");
        var picture = new Image { Stretch = Stretch.UniformToFill };
        RenderOptions.SetBitmapScalingMode(picture, BitmapScalingMode.HighQuality);
        double r = Math.Max(0, L.Radius - 3);
        var frame = new Border { Background = L.Selected, CornerRadius = new CornerRadius(r), ClipToBounds = true, Child = picture };
        frame.SizeChanged += (_, e) => { frame.Height = e.NewSize.Width / aspect; frame.Clip = new RectangleGeometry(new Rect(0, 0, e.NewSize.Width, e.NewSize.Width / aspect), r, r); };
        var name = new TextBlock { Text = label, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 4), TextTrimming = TextTrimming.CharacterEllipsis };
        var b = new ToggleButton { Content = new StackPanel { Children = { frame, name } }, Template = template, Margin = new Thickness(0, 0, 12, 0), IsChecked = on, Cursor = Cursors.Hand };
        System.Windows.Automation.AutomationProperties.SetName(b, label);
        if (register is not null) register[id] = picture; else skinPictures.Add(picture);
        void Paint() { bool c = b.IsChecked == true; b.Background = b.IsMouseOver ? L.CardHover : Brushes.Transparent; b.BorderBrush = c ? L.Accent : Brushes.Transparent; name.Foreground = c ? L.Text : L.Secondary; }
        b.Checked += (_, _) => Paint(); b.Unchecked += (_, _) => Paint(); b.MouseEnter += (_, _) => Paint(); b.MouseLeave += (_, _) => Paint(); Paint();
        return b;
    }

    static Color Mix(Color a, Color b, double t) => Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
    static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [StructLayout(LayoutKind.Sequential)] struct CursorPoint { public int X, Y; }
    [DllImport("user32.dll")] static extern bool GetCursorPos(out CursorPoint p);
}
