using Discord.Interactions;

namespace Asahi.Modules.BirthdayRoles;

public class MonthAutocomplete : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        throw new NotImplementedException();
    }
}