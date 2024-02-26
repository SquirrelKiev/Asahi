using Discord;
using Seigen.Modules.TrackablesManagement;

namespace Seigen.Modules.Autocompletes;

public class GuildAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        var autocompletes = new List<AutocompleteResult>();
        var trackablesUtility = services.GetRequiredService<TrackablesUtility>();

        foreach (var guild in await context.Client.GetGuildsAsync())
        {
            if(!await trackablesUtility.IsGuildValid(guild, context.User.Id))
                continue;

            autocompletes.Add(new AutocompleteResult($"{guild.Name} ({guild.Id})", guild.Id.ToString()));
        }

        return AutocompletionResult.FromSuccess(autocompletes.Where(x => 
            x.Name.Contains((string)autocompleteInteraction.Data.Current.Value, StringComparison.InvariantCultureIgnoreCase))
            .Take(25));
    }
}