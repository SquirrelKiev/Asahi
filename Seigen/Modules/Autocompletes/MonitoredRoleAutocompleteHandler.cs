using Seigen.Modules.TrackablesManagement;

namespace Seigen.Modules.Autocompletes;

public class MonitoredRoleAutocompleteHandler : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        return CommonRoleAutocomplete.GenerateSuggestionsCommonAsync(
            context,
            autocompleteInteraction,
            parameter,
            services,
            TrackablesModule.MONITORED_GUILD_PARAM_NAME,
            trackable => trackable.MonitoredGuild);
    }

}