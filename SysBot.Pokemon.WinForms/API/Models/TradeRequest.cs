using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SysBot.Pokemon.WinForms.API.Models;

public class TradeRequest
{
    /// <summary>
    /// Trade ID - set by Master when routing to slaves
    /// </summary>
    [JsonPropertyName("tradeId")]
    public string? TradeId { get; set; }

    /// <summary>
    /// User ID from Next.js authentication
    /// </summary>
    [JsonPropertyName("userId")]
    public required string UserId { get; set; }

    /// <summary>
    /// User's email for notifications
    /// </summary>
    [JsonPropertyName("userEmail")]
    public string? UserEmail { get; set; }

    /// <summary>
    /// Discord username if available
    /// </summary>
    [JsonPropertyName("discordUsername")]
    public string? DiscordUsername { get; set; }

    /// <summary>
    /// In-game trainer name
    /// </summary>
    [JsonPropertyName("trainerName")]
    public required string TrainerName { get; set; }

    /// <summary>
    /// Game version: SV, SWSH, BDSP, PLA, LGPE, PLZA
    /// </summary>
    [JsonPropertyName("game")]
    public required string Game { get; set; }

    /// <summary>
    /// Pokemon in Showdown format
    /// </summary>
    [JsonPropertyName("showdownSet")]
    public required string ShowdownSet { get; set; }

    /// <summary>
    /// Optional custom trade code (8 digits)
    /// </summary>
    [JsonPropertyName("tradeCode")]
    public string? TradeCode { get; set; }

    /// <summary>
    /// LGPE Trade Code (3 Pokemon icons)
    /// Can be: "Pikachu,Eevee,Bulbasaur" or ["Pikachu", "Eevee", "Bulbasaur"]
    /// Valid values: Pikachu, Eevee, Bulbasaur, Charmander, Squirtle, Pidgey, Caterpie, Rattata, Jigglypuff, Diglett
    /// </summary>
    [JsonPropertyName("lgpeTradeCode")]
    public string? LgpeTradeCode { get; set; }

    /// <summary>
    /// Trainer preferences for ID, OT, etc.
    /// </summary>
    [JsonPropertyName("preferences")]
    public TrainerPreferences? Preferences { get; set; }

    /// <summary>
    /// For batch trades - multiple Pokemon
    /// </summary>
    [JsonPropertyName("batchShowdownSets")]
    public List<string>? BatchShowdownSets { get; set; }
}

public class TrainerPreferences
{
    [JsonPropertyName("trainerID")]
    public int? TrainerID { get; set; }

    [JsonPropertyName("secretID")]
    public int? SecretID { get; set; }

    [JsonPropertyName("originalTrainerName")]
    public string? OriginalTrainerName { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }
}
