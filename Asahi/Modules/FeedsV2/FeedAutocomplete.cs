using Discord.Interactions;

namespace Asahi.Modules.FeedsV2
{
    public class FeedAutocomplete : AutocompleteHandler
    {
        public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
            IParameterInfo parameter, IServiceProvider services)
        {
            return Task.FromResult(AutocompletionResult.FromSuccess());
        }
    }
}
