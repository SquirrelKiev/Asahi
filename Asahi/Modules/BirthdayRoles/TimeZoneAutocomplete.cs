using Discord.Interactions;
using Microsoft.Extensions.Logging;
using NodaTime.TimeZones;

namespace Asahi.Modules.BirthdayRoles;

public class TimeZoneAutocomplete : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<TimeZoneAutocomplete>>();

        var ids = TzdbDateTimeZoneSource.Default.GetIds();

        var userInput = autocompleteInteraction.Data.Current.Value.ToString()!.ToLowerInvariant();

        return AutocompletionResult.FromSuccess(ids
            .Where(x => x.Contains(userInput, StringComparison.InvariantCultureIgnoreCase))
            .Take(25)
            .Select(x => new AutocompleteResult(x, x)));
    }
}