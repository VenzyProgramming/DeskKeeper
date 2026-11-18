using System;
using System.Collections.Generic;
using System.Globalization;

namespace DeskKeeper;

/// <summary>Beschriftungen in Deutsch, Englisch und Russisch. Die Seitenleisten-Ausgabe ist immer deutsch
/// (Seitenleisten-Ausgabe läuft deutsch); die öffentliche Ausgabe folgt der Einstellung oder der Windows-Sprache.</summary>
static class Loc
{
    public static string Lang { get; private set; } = "de";

    public static void Use(string setting)
    {
        if (!Edition.Public) { Lang = "de"; return; }
        string ui = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        Lang = setting is "de" or "en" or "ru" ? setting : ui is "de" or "ru" ? ui : "en";
    }

    public static string T(string key) => Texts.TryGetValue(key, out var t) ? Lang switch { "en" => t.En, "ru" => t.Ru, _ => t.De } : key;

    /// <summary>Anzahl mit Wort: Russisch mit drei Formen, Deutsch/Englisch mit zwei.</summary>
    public static string Count(int n, string key)
    {
        if (!Plurals.TryGetValue(key, out var f)) return n + " " + T(key);
        if (Lang == "ru")
        {
            int a = Math.Abs(n) % 100, b = a % 10;
            return n + " " + (a is >= 11 and <= 14 ? f.RuMany : b == 1 ? f.RuOne : b is >= 2 and <= 4 ? f.RuFew : f.RuMany);
        }
        return n + " " + (Lang == "en" ? (n == 1 ? f.EnOne : f.EnMany) : (n == 1 ? f.DeOne : f.DeMany));
    }

    public static string Ago(DateTime when)
    {
        var d = DateTime.Now - when;
        if (d.TotalMinutes < 1) return T("justNow");
        if (d.TotalHours < 1) return string.Format(T("minAgo"), (int)d.TotalMinutes);
        if (d.TotalDays < 1) return string.Format(T("hAgo"), (int)d.TotalHours);
        return when.ToString(Lang == "en" ? "MM/dd/yyyy h:mm tt" : "dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
    }

    record Plural(string DeOne, string DeMany, string EnOne, string EnMany, string RuOne, string RuFew, string RuMany);
    static readonly Dictionary<string, Plural> Plurals = new()
    {
        ["icons"] = new("Symbol", "Symbole", "icon", "icons", "значок", "значка", "значков"),
        ["screens"] = new("Bildschirm", "Bildschirme", "display", "displays", "экран", "экрана", "экранов"),
        ["layouts"] = new("Anordnung", "Anordnungen", "arrangement", "arrangements", "раскладка", "раскладки", "раскладок"),
        ["snaps"] = new("Schnappschuss", "Schnappschüsse", "snapshot", "snapshots", "снимок", "снимка", "снимков"),
    };

    record Text(string De, string En, string Ru);
    static readonly Dictionary<string, Text> Texts = new()
    {
        // Navigation und Kopf
        ["screens"] = new("Bildschirme", "Displays", "Экраны"),
        ["scene"] = new("Szene", "Scene", "Сцена"),
        ["tools"] = new("Werkzeuge", "Tools", "Инструменты"),
        ["settings"] = new("Einstellungen", "Settings", "Настройки"),
        ["save"] = new("Speichern", "Save", "Запомнить"),
        ["restore"] = new("Wiederherstellen", "Restore", "Вернуть"),
        ["start"] = new("Start", "Start", "Пуск"),
        ["close"] = new("Schließen", "Close", "Закрыть"),
        ["info"] = new("Geräte", "Devices", "Устройства"),
        ["primary"] = new("Hauptbildschirm", "Main display", "Основной"),
        ["now"] = new("jetzt", "now", "сейчас"),
        ["current"] = new("Aktuell", "Current", "Текущая"),
        ["example"] = new("Beispiel", "Example", "пример"),
        ["watching"] = new("überwacht", "watching", "следит"),
        ["notWatching"] = new("überwacht nicht", "not watching", "не следит"),
        // Listen
        ["layoutsTitle"] = new("Gespeicherte Anordnungen", "Saved arrangements", "Сохранённые раскладки"),
        ["snapsTitle"] = new("Schnappschüsse", "Snapshots", "Снимки"),
        ["nothing"] = new("Noch nichts gespeichert", "Nothing saved yet", "Пока ничего не запомнено"),
        ["noSnaps"] = new("Keine Schnappschüsse", "No snapshots", "Нет снимков"),
        ["remove"] = new("Entfernen", "Remove", "Убрать"),
        ["auto"] = new("automatisch", "automatic", "авто"),
        ["justNow"] = new("gerade eben", "just now", "только что"),
        ["minAgo"] = new("vor {0} Min.", "{0} min ago", "{0} мин назад"),
        ["hAgo"] = new("vor {0} Std.", "{0} h ago", "{0} ч назад"),
        // Geräte
        ["kind.Monitor"] = new("Monitor", "Monitor", "Монитор"),
        ["kind.Laptop"] = new("Notebook", "Laptop", "Ноутбук"),
        ["kind.Tablet"] = new("Tablet", "Tablet", "Планшет"),
        ["kind.Phone"] = new("Handy", "Phone", "Телефон"),
        ["kind.TV"] = new("Fernseher", "TV", "Телевизор"),
        ["kind.Wireless"] = new("Kabelloser Bildschirm", "Wireless display", "Беспроводной экран"),
        ["kind.Virtual"] = new("Virtueller Bildschirm", "Virtual display", "Виртуальный экран"),
        ["builtIn"] = new("Eingebaut", "Built-in", "Встроенный"),
        // Szene
        ["destruction"] = new("Zerstörung", "Destruction", "Разрушение"),
        ["robot"] = new("DUM-E", "DUM-E", "Дубина"),   // 18.11.2026: deutsch/englisch DUM-E, russisch Дубина
        ["w.jericho"] = new("Jericho", "Jericho", "Иерихон"),
        ["w.nuke"] = new("Atombombe", "Nuke", "Ядерная"),
        ["w.tu160"] = new("Tu-160M", "Tu-160M", "Ту-160М"),
        // Werkzeuge
        ["t.windows"] = new("Fenster", "Windows", "Окна"),
        ["t.minimize"] = new("Alle Fenster minimieren", "Minimize all windows", "Свернуть все окна"),
        ["t.do"] = new("Ausführen", "Run", "Выполнить"),
        ["t.desktop"] = new("Desktop", "Desktop", "Рабочий стол"),
        ["t.hide"] = new("Desktopsymbole anzeigen", "Show desktop icons", "Показывать значки"),
        ["t.size"] = new("Symbolgröße", "Icon size", "Размер значков"),
        ["t.small"] = new("Klein", "Small", "Мелкие"),
        ["t.medium"] = new("Mittel", "Medium", "Обычные"),
        ["t.large"] = new("Groß", "Large", "Крупные"),
        ["t.grid"] = new("Am Raster ausrichten", "Align to grid", "Выравнивать по сетке"),
        ["t.arrange"] = new("Automatisch anordnen", "Auto arrange", "Упорядочивать автоматически"),
        ["t.file"] = new("Datei", "File", "Файл"),
        ["t.export"] = new("Anordnung als Datei speichern", "Save arrangement to file", "Сохранить раскладку в файл"),
        ["t.import"] = new("Anordnung aus Datei laden", "Load arrangement from file", "Загрузить раскладку из файла"),
        ["t.choose"] = new("Wählen …", "Choose …", "Выбрать …"),
        // Einstellungen
        ["s.restore"] = new("Wiederherstellen", "Restore", "Восстановление"),
        ["s.watch"] = new("Bildschirmwechsel überwachen", "Watch display changes", "Следить за экранами"),
        ["s.mode"] = new("Art der Wiederherstellung", "Restore style", "Способ восстановления"),
        ["s.instant"] = new("Sofort", "Instant", "Сразу"),
        ["s.withRobot"] = new("Mit DUM-E", "With DUM-E", "С Дубиной"),
        ["s.atLogin"] = new("Beim Windows-Start wiederherstellen", "Restore at Windows startup", "Восстанавливать при запуске Windows"),
        ["s.atStart"] = new("Beim Programmstart wiederherstellen", "Restore when the app starts", "Восстанавливать при запуске программы"),
        ["s.saving"] = new("Speichern", "Saving", "Сохранение"),
        ["s.autosnap"] = new("Automatischer Schnappschuss", "Automatic snapshot", "Автоматический снимок"),
        ["s.off"] = new("Aus", "Off", "Выкл."),
        ["s.min"] = new("{0} Min.", "{0} min", "{0} мин"),
        ["s.onShutdown"] = new("Beim Herunterfahren speichern", "Save on shutdown", "Сохранять при выключении"),
        ["s.onSwitch"] = new("Beim Benutzerwechsel speichern", "Save on user switch", "Сохранять при смене пользователя"),
        ["s.onExit"] = new("Beim Beenden speichern", "Save on exit", "Сохранять при выходе"),
        ["s.program"] = new("Programm", "App", "Программа"),
        ["s.autostart"] = new("Mit Windows starten", "Start with Windows", "Запуск с Windows"),
        ["s.minimized"] = new("Immer minimiert starten", "Always start minimized", "Всегда запускать свёрнутым"),
        ["s.toTray"] = new("Beim Schließen in den Infobereich", "Close to tray", "Сворачивать в трей при закрытии"),
        ["s.window"] = new("Fensterposition merken", "Remember window position", "Запоминать положение окна"),
        ["s.theme"] = new("Design", "Theme", "Тема"),
        ["s.system"] = new("System", "System", "Системная"),
        ["s.dark"] = new("Dunkel", "Dark", "Тёмная"),
        ["s.light"] = new("Hell", "Light", "Светлая"),
        ["s.language"] = new("Sprache", "Language", "Язык"),
        ["s.automatic"] = new("Automatisch", "Automatic", "Автоматически"),
        ["on"] = new("Ein", "On", "Вкл."),
        ["off"] = new("Aus", "Off", "Откл."),
        // Infobereich und Meldungen
        ["tray.open"] = new("Öffnen", "Open", "Открыть"),
        ["tray.save"] = new("Jetzt speichern", "Save now", "Запомнить сейчас"),
        ["tray.restore"] = new("Jetzt wiederherstellen", "Restore now", "Вернуть сейчас"),
        ["tray.exit"] = new("Beenden", "Exit", "Выход"),
        ["m.saved"] = new("Anordnung gespeichert · {0}", "Arrangement saved · {0}", "Раскладка запомнена · {0}"),
        ["m.restoring"] = new("DUM-E stellt die Symbole auf", "DUM-E is placing the icons", "Дубина расставляет значки"),
        // Szene (Runner)
        ["r.autoArrange"] = new("„Automatisch anordnen“ ausschalten", "Turn off \"Auto arrange\"", "Выключите «Упорядочить значки автоматически»"),
        ["r.noIcons"] = new("Keine Symbole auf dem Desktop", "No icons on the desktop", "На рабочем столе нет значков"),
        ["r.inPlace"] = new("Symbole sind schon an ihrem Platz", "Icons are already in place", "Значки уже на местах"),
        ["r.placed"] = new("Symbole an ihrem Platz", "Icons in place", "Значки на местах"),
        ["r.aborted"] = new("Abgebrochen · Symbole an alter Stelle", "Cancelled · icons back in place", "Прервано · значки на прежних местах"),
        ["r.done"] = new("Fertig", "Done", "Готово"),
        ["r.notHidden"] = new("Symbole nicht ausgeblendet", "icons were not hidden", "значки не спрятались"),
        ["r.explorer"] = new("Explorer nicht erreichbar ({0})", "Explorer unavailable ({0})", "Проводник недоступен ({0})"),
        ["r.recovered"] = new("Symbole nach abgebrochener Szene zurückgestellt", "Icons restored after an interrupted scene", "Значки после прерванной сцены возвращены"),
        ["r.desktopLoading"] = new("Der Desktop ist noch nicht geladen.", "The desktop has not loaded yet.", "Рабочий стол ещё не загрузился."),
        ["m.restored"] = new("Symbole wiederhergestellt", "Icons restored", "Значки на местах"),
        ["m.exported"] = new("Gespeichert: {0}", "Saved: {0}", "Сохранено: {0}"),
        ["m.badFile"] = new("Datei nicht lesbar", "File cannot be read", "Файл не читается"),
    };
}
