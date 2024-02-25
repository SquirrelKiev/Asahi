namespace Seigen.Modules.Autocompletes;

public class TrackableAutocompleteHandler : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        return Task.FromResult(AutocompletionResult.FromSuccess());
    }
}