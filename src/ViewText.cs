namespace DeskKeeper;

/// <summary>Beschriftung eines Geräts: Art, Modell (sonst Hersteller), Auflösung, Diagonale.</summary>
static class ViewText
{
    /// <summary>Russische Mehrzahl: 1 значок, 2–4 значка, 5+ значков (11–14 immer viele).</summary>
    public static string Plural(int n, string one, string few, string many)
    {
        int a = System.Math.Abs(n) % 100, b = a % 10;
        return a is >= 11 and <= 14 ? many : b == 1 ? one : b is >= 2 and <= 4 ? few : many;
    }
    public static string Icons(int n) => Loc.Count(n, "icons");

    public static string Device(MonitorInfo m)
    {
        string model = m.Model.Length > 0 ? m.Model : m.Maker is { Length: > 0 } mk && mk != "Встроенный" ? mk : "";   // „Встроенный“ = interne Kennung für eingebaut
        string size = m.Diagonal > 0 ? $" · {m.Diagonal:0.#}\u2033" : "";
        return DeviceFacts.Name(m.Kind) + (model.Length > 0 ? " · " + model : "") + $" · {m.Bounds.Width:0}×{m.Bounds.Height:0}" + size;
    }
}
