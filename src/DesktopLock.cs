using System;
using System.Threading;

namespace DeskKeeper;

/// <summary>
/// Absprache mit dem Seitenleisten-Ausgabe-Saugroboter über benannte Mutexe (nur diese Sitzung):
/// solange der Saugroboter Dateien einsammelt („Seitenleisten-Ausgabe.Saugroboter.Aktiv“), speichert und stellt DeskKeeper nichts zurück;
/// solange eine DeskKeeper-Szene läuft („DeskKeeper.Szene.Aktiv“), startet der Saugroboter nicht.
/// </summary>
static class DesktopLock
{
    public const string RobotName = "Seitenleisten-Ausgabe.Saugroboter.Aktiv", SceneName = "DeskKeeper.Szene.Aktiv";

    public static bool RobotActive
    {
        get
        {
            if (!Mutex.TryOpenExisting(RobotName, out var m)) return false;
            m.Dispose();
            return true;
        }
    }

    /// <summary>Für die Dauer einer Szene halten (using).</summary>
    public static IDisposable Scene() => new Held(new Mutex(true, SceneName));

    sealed class Held(Mutex mutex) : IDisposable
    {
        public void Dispose() { try { mutex.ReleaseMutex(); } catch (ApplicationException) { } mutex.Dispose(); }
    }
}
