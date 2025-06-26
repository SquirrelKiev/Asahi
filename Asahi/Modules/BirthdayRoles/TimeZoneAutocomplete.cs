using Discord.Interactions;
using Microsoft.Extensions.Logging;
using NodaTime.TimeZones;

namespace Asahi.Modules.BirthdayRoles;

public class TimeZoneAutocomplete : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        var ids = TzdbDateTimeZoneSource.Default.GetIds();

        var userInput = autocompleteInteraction.Data.Current.Value.ToString()!.ToLowerInvariant().Replace(' ', '_');

        return Task.FromResult(AutocompletionResult.FromSuccess(ids
            .Where(x => x.Replace(' ','_').Contains(userInput, StringComparison.InvariantCultureIgnoreCase))
            .Take(25)
            .Select(x => new AutocompleteResult(x, x))));
    }
}
