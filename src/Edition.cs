using System.Windows;

namespace DeskKeeper;

/// <summary>Fenster der Anwendung.</summary>
interface IDeskWindow
{
    void Open();
    void Refresh();
    void KeeperActed(string what);
    bool AllowClose { get; set; }
    void UiTest(string png);
}

static class Edition
{
    public const bool Public = true;

    /// <param name="test">Prüfung außerhalb des Bildschirms: ohne Aktivieren.</param>
    public static IDeskWindow Create(Background? host, bool test) => new PublicWindow(host, test);
}
