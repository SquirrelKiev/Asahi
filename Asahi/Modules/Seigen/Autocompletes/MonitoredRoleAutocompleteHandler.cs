using Discord.Interactions;

namespace Asahi.Modules.Seigen;

public class MonitoredRoleAutocompleteHandler : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var commonAutocompleteService = services.GetRequiredService<CommonAutocompleteService>();
        return commonAutocompleteService.GenerateSuggestionsCommonAsync(
            context,
            autocompleteInteraction,
            parameter,
            services,
            TrackablesModule.MONITORED_GUILD_PARAM_NAME,
            trackable => trackable.MonitoredGuild);
    }

}