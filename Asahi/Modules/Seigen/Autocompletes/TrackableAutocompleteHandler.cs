using Asahi.Database;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;

namespace Asahi.Modules.Seigen;

public class TrackableAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        await using var dbContext = services.GetRequiredService<BotDbContext>();

        var trackables = await dbContext.GetScopedTrackables(context.Guild.Id).ToArrayAsync();

        var autocompletes = new List<AutocompleteResult>();

        foreach (var trackable in trackables)
        {
            IGuild? monitoredGuild = await context.Client.GetGuildAsync(trackable.MonitoredGuild);
            IRole? monitoredRole = monitoredGuild.GetRole(trackable.MonitoredRole);
            IGuild? assignableGuild = await context.Client.GetGuildAsync(trackable.AssignableGuild);
            IRole? assignableRole = assignableGuild.GetRole(trackable.AssignableRole);

            autocompletes.Add(new AutocompleteResult(
                $"{trackable.Id}: Monitoring {monitoredRole?.Name} in {monitoredGuild?.Name}. Assign {assignableRole?.Name} in {assignableGuild?.Name}. {trackable.Limit} slot limit.", trackable.Id.ToString()));
        }

        return AutocompletionResult.FromSuccess(autocompletes.Where(x =>
                x.Name.Contains((string)autocompleteInteraction.Data.Current.Value, StringComparison.InvariantCultureIgnoreCase))
            .Take(25));
    }
}
