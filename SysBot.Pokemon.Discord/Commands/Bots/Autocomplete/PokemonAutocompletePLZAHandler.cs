using Discord;
using Discord.Interactions;
using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord.Commands.Bots.Autocomplete;

/// <summary>
/// Autocomplete handler for Legends: Z-A Pokemon species
/// </summary>
public class PokemonAutocompletePLZAHandler : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        try
        {
            var userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? string.Empty;

            // Get valid Pokemon for Legends: Z-A
            var validSpecies = GetValidSpeciesForGame(GameVersion.ZA);

            // Filter based on user input
            var filteredSpecies = string.IsNullOrWhiteSpace(userInput)
                ? validSpecies.Take(25) // Show first 25 if no input
                : validSpecies
                    .Where(s => s.Contains(userInput, StringComparison.OrdinalIgnoreCase))
                    .Take(25); // Discord limit

            var results = filteredSpecies
                .Select(s => new AutocompleteResult(s, s))
                .ToList();

            return Task.FromResult(
                results.Any()
                    ? AutocompletionResult.FromSuccess(results)
                    : AutocompletionResult.FromSuccess(new[]
                    {
                        new AutocompleteResult("No matches found", "None")
                    })
            );
        }
        catch (Exception ex)
        {
            return Task.FromResult(
                AutocompletionResult.FromError(InteractionCommandError.Unsuccessful, ex.Message)
            );
        }
    }

    private static List<string> GetValidSpeciesForGame(GameVersion game)
    {
        var table = game switch
        {
            GameVersion.ZA => PersonalTable.ZA,
            _ => PersonalTable.ZA
        };

        var validSpecies = new List<string>();
        for (ushort species = 1; species < (ushort)Species.MAX_COUNT; species++)
        {
            if (table.IsPresentInGame(species, 0))
            {
                var name = ((Species)species).ToString();
                if (!name.Contains('_') &&
                    !name.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("Egg", StringComparison.OrdinalIgnoreCase))
                {
                    validSpecies.Add(name);
                }
            }
        }

        return validSpecies.OrderBy(s => s).ToList();
    }
}
