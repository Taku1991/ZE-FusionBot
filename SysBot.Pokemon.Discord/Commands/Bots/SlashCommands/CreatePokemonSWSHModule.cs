using Discord.Interactions;
using Discord.WebSocket;
using PKHeX.Core;
using SysBot.Pokemon.Discord.Commands.Bots.Autocomplete;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord.Commands.Bots.SlashCommands;

/// <summary>
/// Slash command module for creating Sword/Shield (PK8) Pokemon with Gigantamax support
/// </summary>
public class CreatePokemonSWSHModule<T> : InteractionModuleBase<SocketInteractionContext> where T : PKM, new()
{
    [SlashCommand("create-swsh", "Create a Sword/Shield Pokemon with Gigantamax support")]
    public async Task CreatePokemonSWSHAsync(
        [Summary("pokemon", "Pokemon species")]
        [Autocomplete(typeof(PokemonAutocompleteSWSHHandler))]
        string pokemon,

        [Summary("shiny", "Should the Pokemon be shiny?")]
        bool shiny = false,

        [Summary("item", "Held item (optional)")]
        [Autocomplete(typeof(ItemAutocompleteSWSHHandler))]
        string? item = null,

        [Summary("ball", "Poke Ball (optional)")]
        [Autocomplete(typeof(BallAutocompleteHandler))]
        string? ball = null,

        [Summary("gigantamax", "Can Gigantamax?")]
        bool gigantamax = false,

        [Summary("level", "Pokemon level (1-100)")]
        [MinValue(1)]
        [MaxValue(100)]
        int level = 100,

        [Summary("nature", "Pokemon nature (optional)")]
        string? nature = null
    )
    {
        await DeferAsync(ephemeral: false).ConfigureAwait(false);

        try
        {
            // Build Gigantamax feature string (will be added to Showdown format)
            string specialFeature = gigantamax ? "Gigantamax: Yes" : string.Empty;

            await CreatePokemonHelper.ExecuteCreatePokemonAsync<T>(
                Context,
                pokemon,
                shiny,
                item,
                ball,
                level,
                nature,
                specialFeature,
                null // No post-processing needed for Gigantamax (handled by Showdown)
            ).ConfigureAwait(false);
        }
        catch (System.Exception ex)
        {
            await FollowupAsync($"❌ An error occurred: {ex.Message}", ephemeral: true).ConfigureAwait(false);
        }
    }
}
