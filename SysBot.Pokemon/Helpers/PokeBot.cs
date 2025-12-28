using PKHeX.Core;

namespace SysBot.Pokemon;

public static class PokeBot
{
    public const string Attribution = "https://github.com/Taku1991/ZE-FusionBot";

    public const string ConfigPath = "config.json";

    public const string Version = "v7.0.7d";

    /// <summary>
    /// Checks if a user can use AutoOT functionality.
    /// This is now simplified to check only the IgnoreAutoOT flag, as role checking
    /// is handled at the Discord command level.
    /// </summary>
    /// <param name="poke">The trade detail containing user information</param>
    /// <returns>True if the user can use AutoOT, false otherwise</returns>
    public static bool CanUseAutoOT<T>(PokeTradeDetail<T> poke) where T : PKM, new()
    {
        // If IgnoreAutoOT is set, it means either:
        // 1. User explicitly specified OT/TID/SID in their request
        // 2. User doesn't have AutoOT role permission (set by Discord module)
        return !poke.IgnoreAutoOT;
    }
}
