using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord.Commands.Bots.Autocomplete;

/// <summary>
/// Autocomplete handler for Pokemon natures that shows the stat changes in the label
/// while keeping the raw nature name as the value passed to the command.
/// </summary>
public class NatureAutocompleteHandler : AutocompleteHandler
{
    private static readonly IReadOnlyList<AutocompleteResult> NatureOptions = new List<AutocompleteResult>
    {
        new("Adamant (Atk+ / Sp. Atk-)", "Adamant"),
        new("Bashful (Neutral)", "Bashful"),
        new("Bold (Def+ / Atk-)", "Bold"),
        new("Brave (Atk+ / Speed-)", "Brave"),
        new("Calm (Sp. Def+ / Atk-)", "Calm"),
        new("Careful (Sp. Def+ / Sp. Atk-)", "Careful"),
        new("Docile (Neutral)", "Docile"),
        new("Gentle (Sp. Def+ / Def-)", "Gentle"),
        new("Hardy (Neutral)", "Hardy"),
        new("Hasty (Speed+ / Def-)", "Hasty"),
        new("Impish (Def+ / Sp. Atk-)", "Impish"),
        new("Jolly (Speed+ / Sp. Atk-)", "Jolly"),
        new("Lax (Def+ / Sp. Def-)", "Lax"),
        new("Lonely (Atk+ / Def-)", "Lonely"),
        new("Mild (Sp. Atk+ / Def-)", "Mild"),
        new("Modest (Sp. Atk+ / Atk-)", "Modest"),
        new("Naive (Speed+ / Sp. Def-)", "Naive"),
        new("Naughty (Atk+ / Sp. Def-)", "Naughty"),
        new("Quiet (Sp. Atk+ / Speed-)", "Quiet"),
        new("Quirky (Neutral)", "Quirky"),
        new("Rash (Sp. Atk+ / Sp. Def-)", "Rash"),
        new("Relaxed (Def+ / Speed-)", "Relaxed"),
        new("Sassy (Sp. Def+ / Speed-)", "Sassy"),
        new("Serious (Neutral)", "Serious"),
        new("Timid (Speed+ / Atk-)", "Timid"),
    };

    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        try
        {
            var userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? string.Empty;

            IEnumerable<AutocompleteResult> results = NatureOptions;

            if (!string.IsNullOrWhiteSpace(userInput))
            {
                results = NatureOptions
                    .Where(option =>
                        option.Name.Contains(userInput, StringComparison.OrdinalIgnoreCase) ||
                        (option.Value?.ToString()?.Contains(userInput, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var finalResults = results.Take(25).ToList();

            if (finalResults.Count == 0)
            {
                finalResults.Add(new AutocompleteResult("No matches found", "Adamant"));
            }

            return Task.FromResult(AutocompletionResult.FromSuccess(finalResults));
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                AutocompletionResult.FromError(InteractionCommandError.Unsuccessful, ex.Message)
            );
        }
    }
}
