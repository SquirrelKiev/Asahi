using Microsoft.EntityFrameworkCore;
using Seigen.Database;

namespace Seigen.Modules.Autocompletes;

public class TrackableAutocompleteHandler : AutocompleteHandler
{
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter, IServiceProvider services)
    {
        var dbService = services.GetRequiredService<DbService>();

        await using var dbContext = dbService.GetDbContext();

        var trackables = await dbContext.GetScopedTrackables(context.Guild.Id).ToArrayAsync();

        var autocompletes = new List<AutocompleteResult>();

        foreach (var trackable in trackables)
        {
            var monitoredGuild = await context.Client.GetGuildAsync(trackable.MonitoredGuild);
            var monitoredRole = monitoredGuild.GetRole(trackable.MonitoredRole);
            var assignableGuild = await context.Client.GetGuildAsync(trackable.AssignableGuild);
            var assignableRole = monitoredGuild.GetRole(trackable.AssignableRole);

            autocompletes.Add(new AutocompleteResult(
                $"{trackable.Id}: Monitoring {monitoredRole.Name} in {monitoredGuild.Name}. Assign {assignableRole.Name} in {assignableGuild.Name}. {trackable.Limit} slot limit.", trackable.Id.ToString()));
        }

        return AutocompletionResult.FromSuccess(autocompletes.Where(x =>
                x.Name.Contains((string)autocompleteInteraction.Data.Current.Value, StringComparison.InvariantCultureIgnoreCase))
            .Take(25));
    }
}