using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Zatsuyou.Database;
using Zatsuyou.Database.Models;

namespace Zatsuyou.Modules.Seigen;

[Inject(ServiceLifetime.Singleton)]
public class CommonAutocompleteService(OverrideTrackerService overrideTracker)
{
    public async Task<IEnumerable<AutocompleteResult>> GetAutoCompleteOptions(IInteractionContext context, ulong guildId, ulong userId)
    {
        var guild = await context.Client.GetGuildAsync(guildId);

        if (guild == null)
            return Enumerable.Empty<AutocompleteResult>();

        var user = await guild.GetUserAsync(userId);

        if (!user.GuildPermissions.Has(GuildPermission.ManageRoles) && !await overrideTracker.HasOverride(userId))
            return Enumerable.Empty<AutocompleteResult>();

        return guild.Roles.Select(x => new AutocompleteResult($"{x.Name} ({x.Id})", x.Id.ToString()));
    }

    public async Task<AutocompletionResult> GenerateSuggestionsCommonAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services,
        string guildParamName,
        Func<Trackable, ulong> getGuildIdFromTrackable)
    {
        var idObj = autocompleteInteraction.Data.Options.FirstOrDefault(x =>
            x.Name == TrackablesModule.ID_PARAM_NAME)?.Value;

        var guildIdObj = autocompleteInteraction.Data.Options.FirstOrDefault(x =>
            x.Name == guildParamName)?.Value;

        ulong guildId = 0;

        if (guildIdObj != null && !ulong.TryParse((string)guildIdObj, out guildId))
        {
            return AutocompletionResult.FromSuccess();
        }

        if (idObj != null)
        {
            if (!uint.TryParse((string)idObj, out uint id)) return AutocompletionResult.FromSuccess();

            var dbService = services.GetRequiredService<DbService>();
            await using var dbContext = dbService.GetDbContext();

            if (guildId == 0)
            {
                var dbEntity = await dbContext.Trackables.FirstOrDefaultAsync(x => x.Id == id);
                if (dbEntity != null)
                    guildId = getGuildIdFromTrackable(dbEntity);
            }
        }

        var autocompletes = await GetAutoCompleteOptions(context, guildId, context.User.Id);

        return AutocompletionResult.FromSuccess(autocompletes.Where(x =>
                x.Name.Contains((string)autocompleteInteraction.Data.Current.Value, StringComparison.InvariantCultureIgnoreCase))
            .Take(25));
    }

}