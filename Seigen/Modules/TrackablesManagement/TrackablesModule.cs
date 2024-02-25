using Seigen.Database;
using Seigen.Database.Models;
using Seigen.Modules.Autocompletes;
using Microsoft.EntityFrameworkCore;
using Seigen.Modules.RoleManagement;

namespace Seigen.Modules.TrackablesManagement;

[Group("trackables", "Commands relating to managing trackables and their users.")]
public class TrackablesModule(DbService dbService, RoleManagementService roleManagement) : BotModule
{
    public const string MONITORED_GUILD_PARAM_NAME = "monitored-guild";
    public const string ASSIGNABLE_GUILD_PARAM_NAME = "assignable-guild";
    public const string ID_PARAM_NAME = "id";


    [SlashCommand("add", "Add a trackable.")]
    public async Task AddTrackable(
        [Autocomplete(typeof(GuildAutocompleteHandler)), Summary(MONITORED_GUILD_PARAM_NAME)] string monitoredGuild,
        [Autocomplete(typeof(MonitoredRoleAutocompleteHandler))] string monitoredRole,
        [Autocomplete(typeof(GuildAutocompleteHandler)), Summary(ASSIGNABLE_GUILD_PARAM_NAME)] string assignableGuild,
        [Autocomplete(typeof(MonitoredRoleAutocompleteHandler))] string assignableRole,
        uint limit = 0)
    {
        await DeferAsync();

        var trackable = GetTrackable(monitoredGuild, monitoredRole, assignableGuild, assignableRole, limit);

        if (trackable == null)
        {
            await FollowupAsync(new MessageContents(new EmbedBuilder().WithDescription("One of the parameters is not a valid ID.")));
            return;
        }

        var isValid = await GetIsValid(trackable.MonitoredGuild, trackable.MonitoredRole, trackable.AssignableGuild, trackable.AssignableRole);
        if (isValid != null)
            await FollowupAsync(isValid.Value);

        await using var context = dbService.GetDbContext();

        context.Add(trackable);

        await context.SaveChangesAsync();

        await roleManagement.CacheAndResolve();

        await FollowupAsync(new MessageContents(new EmbedBuilder().WithDescription("Added trackable!")));
    }

    private Trackable? GetTrackable(string monitoredGuild, string monitoredRole, string assignableGuild, string assignableRole, uint? limit, Trackable? trackable = null)
    {
        if (!ulong.TryParse(monitoredGuild, out ulong monitoredGuildId) ||
            !ulong.TryParse(monitoredRole, out ulong monitoredRoleId) ||
            !ulong.TryParse(assignableGuild, out ulong assignableGuildId) ||
            !ulong.TryParse(assignableRole, out ulong assignableRoleId))
        {
            return null;
        }

        trackable ??= new Trackable();

        if(assignableGuildId != 0)
            trackable.AssignableGuild = assignableGuildId;
        if(assignableRoleId != 0)
            trackable.AssignableRole = assignableRoleId;
        if(monitoredGuildId != 0)
            trackable.MonitoredGuild = monitoredGuildId;
        if(monitoredRoleId != 0)
            trackable.MonitoredRole = monitoredRoleId;
        if(limit.HasValue)
            trackable.Limit = limit.Value;

        return trackable;
    }

    private async Task<MessageContents?> GetIsValid(ulong monitoredGuild, ulong monitoredRole, ulong assignableGuild, ulong assignableRole)
    {
        var monitoredGuildInstance = await Context.Client.GetGuildAsync(monitoredGuild);
        var assignedGuildInstance = await Context.Client.GetGuildAsync(assignableGuild);

        if (!await TrackablesUtility.IsGuildValid(monitoredGuildInstance, Context.User.Id) ||
            !await TrackablesUtility.IsGuildValid(assignedGuildInstance, Context.User.Id))
        {
            return new MessageContents(new EmbedBuilder().WithDescription("One of the specified guilds could not be found or is not allowed."));
        }

        var monitoredRoleInstance = monitoredGuildInstance.GetRole(monitoredRole);
        var assignableRoleInstance = monitoredGuildInstance.GetRole(assignableRole);

        if (monitoredRoleInstance == null || assignableRoleInstance == null)
        {
            return
                new MessageContents(new EmbedBuilder().WithDescription("One of the specified roles could not be found."));
        }

        return null;
    }

    private async Task<MessageContents?> GetIsValidPermissionCheckOnly(ulong monitoredGuild, ulong assignableGuild)
    {
        var monitoredGuildInstance = await Context.Client.GetGuildAsync(monitoredGuild);
        var assignedGuildInstance = await Context.Client.GetGuildAsync(assignableGuild);

        if ((!await TrackablesUtility.IsGuildValidPermissionCheckOnly(monitoredGuildInstance, Context.User.Id) ||
             !await TrackablesUtility.IsGuildValidPermissionCheckOnly(assignedGuildInstance, Context.User.Id))
            && monitoredGuildInstance != null && assignedGuildInstance != null)
        {
            return new MessageContents(new EmbedBuilder().WithDescription("One of the specified guilds is not allowed."));
        }

        return null;
    }

    [SlashCommand("modify", "Modify/edit an existing trackable.")]
    public async Task ModifyTrackable(
        [Autocomplete(typeof(TrackableAutocompleteHandler)), Summary(ID_PARAM_NAME)] string idStr,
        [Autocomplete(typeof(GuildAutocompleteHandler)), Summary(MONITORED_GUILD_PARAM_NAME)] string monitoredGuild = "0",
        [Autocomplete(typeof(MonitoredRoleAutocompleteHandler))] string monitoredRole = "0",
        [Autocomplete(typeof(GuildAutocompleteHandler)), Summary(ASSIGNABLE_GUILD_PARAM_NAME)] string assignableGuild = "0",
        [Autocomplete(typeof(AssignableRoleAutocompleteHandler))] string assignableRole = "0",
        int limit = -1)
    {
        await DeferAsync();

        if(!uint.TryParse(idStr, out uint id))
        {
            await FollowupAsync(
                new MessageContents(
                    new EmbedBuilder().WithDescription("ID is not valid.")));
            return;
        }

        await using var context = dbService.GetDbContext();

        var entry = context.Trackables.FirstOrDefault(x => x.Id == id);

        if (entry == null)
        {
            await FollowupAsync(
                new MessageContents(
                    new EmbedBuilder().WithDescription($"Trackable with ID {id} could not be found.")));
            return;
        }

        // do we have permission from both guilds to edit this trackable?
        // (checking only permissions in case one of the guilds/roles no longer exists to the bot, which might be the case if the
        //  bot was kicked or the guild/role was deleted.)
        var isValid = await GetIsValidPermissionCheckOnly(entry.MonitoredGuild, entry.AssignableGuild);
        if (isValid != null)
        {
            await FollowupAsync(isValid.Value);
            return;
        }

        // is the trackable we're about to push to the database valid?
        uint? uintLimit = limit >= 0 ? (uint)limit : null;
        var trackable = GetTrackable(monitoredGuild, monitoredRole, assignableGuild, assignableRole, uintLimit, entry);

        if (trackable == null)
        {
            await FollowupAsync(new MessageContents(new EmbedBuilder().WithDescription("One of the parameters is not a valid ID. (somehow)")));
            return;
        }

        isValid = await GetIsValid(trackable.MonitoredGuild, trackable.MonitoredRole, trackable.AssignableGuild, trackable.AssignableRole);
        if (isValid != null)
        {
            await FollowupAsync(isValid.Value);
            return;
        }

        // looks good, push
        await context.SaveChangesAsync();

        await FollowupAsync(new MessageContents(new EmbedBuilder().WithDescription("Updated!")));
    }

    [SlashCommand("remove", "Remove an existing trackable.")]
    public async Task RemoveTrackable(
        [Autocomplete(typeof(TrackableAutocompleteHandler)), Summary(ID_PARAM_NAME)] string idStr)
    {
        await DeferAsync();

        if (!uint.TryParse(idStr, out uint id))
            await FollowupAsync(
                new MessageContents(
                    new EmbedBuilder().WithDescription("ID is not valid.")));

        await using var context = dbService.GetDbContext();

        var trackable = await context.Trackables.FirstOrDefaultAsync(x => x.Id == id);

        if(trackable == null)
        {
            await FollowupAsync(
                new MessageContents(
                    new EmbedBuilder().WithDescription("Trackable not found.")));
            return;
        }

        var isValid = await GetIsValidPermissionCheckOnly(trackable.MonitoredGuild, trackable.AssignableGuild);
        if (isValid != null)
        {
            await FollowupAsync(isValid.Value);
            return;
        }

        context.Trackables.Remove(trackable);
        await context.SaveChangesAsync();

        await FollowupAsync(
            new MessageContents(
                new EmbedBuilder().WithDescription("Removed trackable.")));
    }

    [SlashCommand("list", "Lists all the trackables related to the current guild.")]
    public async Task ListTrackables()
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var trackables = await context.GetScopedTrackables(Context.Guild.Id).ToArrayAsync();

        var embed = new EmbedBuilder();

        var wasTrackables = false;
        foreach (var trackable in trackables)
        {
            wasTrackables = true;

            var guild = await Context.Client.GetGuildAsync(trackable.MonitoredGuild);

            var desc = 
                $"**Monitored Guild:** {guild?.Name} ({trackable.MonitoredGuild})\n" +
                $"**Monitored Role:** {guild?.GetRole(trackable.MonitoredRole)?.Name} ({trackable.MonitoredRole})\n" + 
                $"**Assignable Guild:** {guild?.Name} ({trackable.AssignableGuild})\n" +
                $"**Assignable Role:** {guild?.GetRole(trackable.AssignableRole)?.Name} ({trackable.AssignableRole})\n" +
                $"**Limit**: {trackable.Limit}";

            embed.AddField(trackable.Id.ToString(), desc);
        }

        if (!wasTrackables)
        {
            embed.WithDescription("No trackables!");
        }

        await FollowupAsync(new MessageContents(embed));
    }
}