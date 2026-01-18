using System.Collections.Generic;

namespace SysBot.Pokemon.WinForms.API.Models;

public class TradeRequest
{
    /// <summary>
    /// Trade ID - set by Master when routing to slaves
    /// </summary>
    public string? TradeId { get; set; }

    /// <summary>
    /// User ID from Next.js authentication
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    /// User's email for notifications
    /// </summary>
    public string? UserEmail { get; set; }

    /// <summary>
    /// Discord username if available
    /// </summary>
    public string? DiscordUsername { get; set; }

    /// <summary>
    /// In-game trainer name
    /// </summary>
    public required string TrainerName { get; set; }

    /// <summary>
    /// Game version: SV, SWSH, BDSP, PLA, LGPE, PLZA
    /// </summary>
    public required string Game { get; set; }

    /// <summary>
    /// Pokemon in Showdown format
    /// </summary>
    public required string ShowdownSet { get; set; }

    /// <summary>
    /// Optional custom trade code (8 digits)
    /// </summary>
    public string? TradeCode { get; set; }

    /// <summary>
    /// LGPE Trade Code (3 Pokemon icons)
    /// Can be: "Pikachu,Eevee,Bulbasaur" or ["Pikachu", "Eevee", "Bulbasaur"]
    /// Valid values: Pikachu, Eevee, Bulbasaur, Charmander, Squirtle, Pidgey, Caterpie, Rattata, Jigglypuff, Diglett
    /// </summary>
    public string? LgpeTradeCode { get; set; }

    /// <summary>
    /// Trainer preferences for ID, OT, etc.
    /// </summary>
    public TrainerPreferences? Preferences { get; set; }

    /// <summary>
    /// For batch trades - multiple Pokemon
    /// </summary>
    public List<string>? BatchShowdownSets { get; set; }
}

public class TrainerPreferences
{
    public int? TrainerID { get; set; }
    public int? SecretID { get; set; }
    public string? OriginalTrainerName { get; set; }
    public string? Language { get; set; }
}
