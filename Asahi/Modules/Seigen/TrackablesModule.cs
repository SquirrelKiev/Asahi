using System.Text;
using Asahi.Database;
using Asahi.Database.Models;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;

namespace Asahi.Modules.Seigen;

[CommandContextType(InteractionContextType.Guild)]
[DefaultMemberPermissions(GuildPermission.ManageGuild)]
[Group("trackables", "Commands relating to managing trackables and their users.")]
public class TrackablesModule(DbService dbService, RoleManagementService roleManagement, TrackablesUtility trackablesUtility) : BotModule
{
    public const string MONITORED_GUILD_PARAM_NAME = "monitored-guild";
    public const string ASSIGNABLE_GUILD_PARAM_NAME = "assignable-guild";
    public const string ID_PARAM_NAME = "id";
    public const string ID_PARAM_DESCRIPTION = "The ID of the trackable.";


    [SlashCommand("add", "Add a trackable.")]
    public async Task AddTrackable(
        [Autocomplete(typeof(GuildAutocompleteHandler)), Summary(MONITORED_GUILD_PARAM_NAME, "The Guild to monitor the roles of.")]
        string monitoredGuild,
        [Autocomplete(typeof(MonitoredRoleAutocompleteHandler))]
        [Summary(description: "The specific role to monitor.")]
        string monitoredRole,
        [Autocomplete(typeof(GuildAutocompleteHandler)), Summary(ASSIGNABLE_GUILD_PARAM_NAME, description: "The Guild to assign the role in.")]
        string assignableGuild,
        [Autocomplete(typeof(AssignableRoleAutocompleteHandler)), Summary(description: "The role to assign if the user has the monitored role.")]
        string assignableRole,
        [Summary(description: "The channel to log changes to.")]
        ITextChannel? logsChannel = null,
        [Summary(description: "The maximum amount of users to allow to have the role.")]
        uint limit = 0,
        [Summary(description: "Whether members already in the role should count toward the limit or not.")]
        bool includeExistingMembers = true)
    {
        await DeferAsync();

        var trackable = GetTrackable(monitoredGuild, monitoredRole, assignableGuild, assignableRole, limit, logsChannel);

        if (trackable == null)
        {
            await FollowupAsync(new MessageContents(new EmbedBuilder().WithDescription("One of the parameters is not a valid ID.")));
            return;
        }

        var isValid = await GetIsValid(trackable.MonitoredGuild, trackable.MonitoredRole, trackable.AssignableGuild, trackable.AssignableRole);
        if (isValid != null)
        {
            await FollowupAsync(isValid.Value);
            return;
        }

        await using var context = dbService.GetDbContext();

        context.Add(trackable);

        await context.SaveChangesAsync();

        if (includeExistingMembers)
            await roleManagement.CacheAndResolve();
        else
            await roleManagement.CacheUsers();

        await FollowupAsync(new MessageContents(new EmbedBuilder().WithDescription("Added trackable!")));
    }

    private static Trackable? GetTrackable(string monitoredGuild, string monitoredRole, string assignableGuild,
        string assignableRole, uint? limit, ITextChannel? loggingChannel = null, Trackable? trackable = null)
    {
        if (!ulong.TryParse(monitoredGuild, out ulong monitoredGuildId) ||
            !ulong.TryParse(monitoredRole, out ulong monitoredRoleId) ||
            !ulong.TryParse(assignableGuild, out ulong assignableGuildId) ||
            !ulong.TryParse(assignableRole, out ulong assignableRoleId))
        {
            return null;
        }

        trackable ??= new Trackable();

        if (assignableGuildId != 0)
            trackable.AssignableGuild = assignableGuildId;
        if (assignableRoleId != 0)
            trackable.AssignableRole = assignableRoleId;
        if (monitoredGuildId != 0)
            trackable.MonitoredGuild = monitoredGuildId;
        if (monitoredRoleId != 0)
            trackable.MonitoredRole = monitoredRoleId;
        if (limit.HasValue)
            trackable.Limit = limit.Value;
        if (loggingChannel != null)
            trackable.LoggingChannel = loggingChannel.Id;

        return trackable;
    }

    private async Task<MessageContents?> GetIsValid(ulong monitoredGuild, ulong monitoredRole, ulong assignableGuild, ulong assignableRole)
    {
        var monitoredGuildInstance = await Context.Client.GetGuildAsync(monitoredGuild);
        var assignedGuildInstance = await Context.Client.GetGuildAsync(assignableGuild);

        if (!await trackablesUtility.IsGuildValid(monitoredGuildInstance, Context.User.Id) ||
            !await trackablesUtility.IsGuildValid(assignedGuildInstance, Context.User.Id))
        {
            return new MessageContents(new EmbedBuilder().WithDescription("One of the specified guilds could not be found or is not allowed."));
        }

        var monitoredRoleInstance = monitoredGuildInstance.GetRole(monitoredRole);
        var assignableRoleInstance = assignedGuildInstance.GetRole(assignableRole);

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

        if ((!await trackablesUtility.IsGuildValidIgnoreNull(monitoredGuildInstance, Context.User.Id) ||
             !await trackablesUtility.IsGuildValidIgnoreNull(assignedGuildInstance, Context.User.Id))
            && monitoredGuildInstance != null && assignedGuildInstance != null)
        {
            return new MessageContents(new EmbedBuilder().WithDescription("One of the specified guilds is not allowed."));
        }

        return null;
    }

    [SlashCommand("modify", "Modify/edit an existing trackable.")]
    public async Task ModifyTrackable(
        [Autocomplete(typeof(TrackableAutocompleteHandler))]
        [Summary(ID_PARAM_NAME, description: ID_PARAM_DESCRIPTION)]
        string idStr,
        [Autocomplete(typeof(GuildAutocompleteHandler))]
        [Summary(MONITORED_GUILD_PARAM_NAME, "The Guild to monitor the roles of.")]
        string monitoredGuild = "0",
        [Autocomplete(typeof(MonitoredRoleAutocompleteHandler))]
        [Summary(description: "The specific role to monitor.")]
        string monitoredRole = "0",
        [Autocomplete(typeof(GuildAutocompleteHandler))]
        [Summary(ASSIGNABLE_GUILD_PARAM_NAME, "The Guild to assign the role in.")]
        string assignableGuild = "0",
        [Autocomplete(typeof(AssignableRoleAutocompleteHandler))]
        [Summary(description: "The role to assign if the user has the monitored role.")]
        string assignableRole = "0",
        [Summary(description: "The channel to log changes to.")]
        ITextChannel? logsChannel = null,
        [Summary(description: "The maximum amount of users to allow to have the role.")]
        int limit = -1)
    {
        await DeferAsync();

        if (!uint.TryParse(idStr, out uint id))
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
        var trackable = GetTrackable(monitoredGuild, monitoredRole, assignableGuild, assignableRole, uintLimit, logsChannel, entry);

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
        [Autocomplete(typeof(TrackableAutocompleteHandler))]
        [Summary(ID_PARAM_NAME, description: ID_PARAM_DESCRIPTION)]
        string idStr)
    {
        await DeferAsync();

        if (!uint.TryParse(idStr, out uint id))
            await FollowupAsync(
                new MessageContents(
                    new EmbedBuilder().WithDescription("ID is not valid.")));

        await using var context = dbService.GetDbContext();

        var trackable = await context.Trackables.FirstOrDefaultAsync(x => x.Id == id);

        if (trackable == null)
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

            var monitoredGuild = await Context.Client.GetGuildAsync(trackable.MonitoredGuild);
            var assignableGuild = await Context.Client.GetGuildAsync(trackable.AssignableGuild);

            var desc = trackable.ToDisplayableString(monitoredGuild, assignableGuild);

            embed.AddField(trackable.Id.ToString(), desc);
        }

        if (!wasTrackables)
        {
            embed.WithDescription("No trackables!");
        }

        await FollowupAsync(new MessageContents(embed));
    }

    [SlashCommand("track-user", "Force adds a user to the specified trackable.")]
    public async Task TrackUser(
        [Autocomplete(typeof(TrackableAutocompleteHandler))]
        [Summary(ID_PARAM_NAME, ID_PARAM_DESCRIPTION)]
        string idStr,
        [Summary(description: "The user to forcefully add/track.")]
        IUser user)
    {
        await DeferAsync();

        if (!uint.TryParse(idStr, out uint id))
            await FollowupAsync(
                new MessageContents(
                    new EmbedBuilder().WithDescription("ID is not valid.")));

        await using var context = dbService.GetDbContext();

        var trackable = await context.Trackables.FirstOrDefaultAsync(x => x.Id == id);

        if (trackable == null)
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

        var alreadyTracked = await context.TrackedUsers.FirstOrDefaultAsync(x => x.UserId == user.Id && x.Trackable.Id == x.TrackableId) != null;
        if (alreadyTracked)
        {
            await FollowupAsync(
                new MessageContents(
                    new EmbedBuilder().WithDescription("User already tracked!")));
            return;
        }

        var newTrackedUser = new TrackedUser()
        {
            Trackable = trackable,
            UserId = user.Id
        };

        await roleManagement.TrackUser(context, newTrackedUser, trackable);

        await context.SaveChangesAsync();

        await FollowupAsync(
            new MessageContents(
                new EmbedBuilder().WithDescription("Tracking user.")));
    }

    [SlashCommand("untrack-user", "Force adds a user to the specified trackable.")]
    public async Task UntrackUser(
        [Autocomplete(typeof(TrackableAutocompleteHandler))]
        [Summary(ID_PARAM_NAME)]
        string idStr, 
        [Summary(description: "The user to forcefully untrack.")]
        IUser user)
    {
        await DeferAsync();

        if (!uint.TryParse(idStr, out uint id))
            await FollowupAsync(
                new MessageContents(
                    new EmbedBuilder().WithDescription("ID is not valid.")));

        await using var context = dbService.GetDbContext();

        var trackable = await context.Trackables.FirstOrDefaultAsync(x => x.Id == id);

        if (trackable == null)
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

        var trackedUser = await context.TrackedUsers.FirstOrDefaultAsync(x => x.UserId == user.Id && x.Trackable.Id == x.TrackableId);
        if (trackedUser == null)
        {
            await FollowupAsync(
                new MessageContents(
                    new EmbedBuilder().WithDescription("User isn't tracked already!")));
            return;
        }

        var guild = await Context.Client.GetGuildAsync(trackable.AssignableGuild);

        await roleManagement.UntrackUser(context, trackedUser, trackable);

        await context.SaveChangesAsync();

        await FollowupAsync(
            new MessageContents(
                new EmbedBuilder().WithDescription("Stopped tracking user.")));
    }

    [SlashCommand("list-users", "Lists all the users for the current trackable.")]
    public async Task ListUsers([Autocomplete(typeof(TrackableAutocompleteHandler)), Summary(ID_PARAM_NAME)] string idStr)
    {
        await DeferAsync();

        if (!uint.TryParse(idStr, out uint id))
            await FollowupAsync(
                new MessageContents(
                    new EmbedBuilder().WithDescription("ID is not valid.")));

        await using var context = dbService.GetDbContext();

        var trackable = await context.Trackables.FirstOrDefaultAsync(x => x.Id == id);

        if (trackable == null)
        {
            await FollowupAsync(
                new MessageContents(
                    new EmbedBuilder().WithDescription("Trackable not found.")));
            return;
        }

        var embed = new EmbedBuilder();

        var wasUsers = false;
        var desc = new StringBuilder();
        foreach (var trackedUser in await context.TrackedUsers.Where(x => x.Trackable.Id == trackable.Id).ToArrayAsync())
        {
            wasUsers = true;

            var user = await Context.Client.GetUserAsync(trackedUser.UserId);

            desc.Append(user.Username);
            if (user.Discriminator != "0000")
                desc.Append('#')
                    .Append(user.Discriminator);

            desc.AppendLine();
        }

        embed.WithDescription(desc.ToString());

        if (!wasUsers)
        {
            embed.WithDescription("No users!");
        }

        await FollowupAsync(new MessageContents(embed));
    }
}