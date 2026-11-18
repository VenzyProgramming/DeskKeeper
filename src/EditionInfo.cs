namespace DeskKeeper;

/// <summary>Welche Ausgabe gebaut wird: öffentlich (GitHub) oder Seitenleiste (eigenständig bzw. in Seitenleisten-Ausgabe).</summary>
static partial class Edition
{
#if PUBLIC_EDITION
    public const bool Public = true;
#else
    public const bool Public = false;
#endif
}
