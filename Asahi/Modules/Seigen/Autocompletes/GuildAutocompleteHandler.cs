using Discord.Interactions;
using Discord.WebSocket;

namespace Asahi.Modules.Seigen;

public class GuildAutocompleteHandler : AutocompleteHandler
{
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        var autocompletes = new List<AutocompleteResult>();
        var trackablesUtility = services.GetRequiredService<TrackablesUtility>();

        foreach (var guild in ((DiscordSocketClient)context.Client).Guilds)
        {
            if (!trackablesUtility.IsGuildValid(guild, context.User.Id))
                continue;

            autocompletes.Add(new AutocompleteResult($"{guild.Name} ({guild.Id})", guild.Id.ToString()));
        }

        return Task.FromResult(AutocompletionResult.FromSuccess(autocompletes.Where(x =>
                x.Name.Contains((string)autocompleteInteraction.Data.Current.Value, StringComparison.InvariantCultureIgnoreCase))
            .Take(25)));
    }
}