using BotBase.Modules;
using DibariBot.Modules.ConfigCommand;
using Microsoft.EntityFrameworkCore;
using Seigen.Database;
using Seigen.Database.Models;

namespace Seigen.Modules.ConfigCommand.Pages;

public class TrackablesPage(ConfigCommandService configCommandService, DbService dbService) : ConfigPage
{
    public class TrackableSerializable
    {
        public ulong GuildToMonitor;

        public ulong GuildToAssignIn;

        public ulong RoleToMonitor;

        public ulong RoleToAssign;

        /// <remarks>0 == No limit</remarks>
        public uint Limit;
    }

    public override Page Id => Page.Trackables;

    public override string Label => "Trackables";

    public override string Description => "Configure the Guild's trackables.";

    public override async Task<MessageContents> GetMessageContents(ConfigCommandService.State state)
    {
        var embed = new EmbedBuilder();

        await using var context = dbService.GetDbContext();

        var scopedTrackables = await context.GetScopedTrackables(Context.Guild.Id).ToArrayAsync();

        bool hasTrackables = scopedTrackables.Length > 0;

        if (hasTrackables)
        {
            foreach (var scopedTrackable in scopedTrackables)
            {
                var description = $"Monitored Guild: {(await Context.Client.GetGuildAsync(scopedTrackable.GuildToMonitor))?.Name}\n" +
                                  $"Role to monitor: <&{scopedTrackable.RoleToMonitor}>\n" +
                                  $"Guild to assign in: {(await Context.Client.GetGuildAsync(scopedTrackable.GuildToAssignIn))?.Name}" +
                                  $"Role to assign: <&{scopedTrackable.RoleToAssign}>";
                embed.AddField(scopedTrackable.Id.ToString(), description);
            }
        }
        else
        {
            embed.WithDescription("No trackables!");
        }

        var components = new ComponentBuilder()
            .WithSelectMenu(GetPageSelectDropdown(configCommandService.ConfigPages, Id, IsDm()))
            .WithButton("Add", ModulePrefixes.CONFIG_TRACKABLES_ADD, ButtonStyle.Secondary)
            .WithButton("Modify", ModulePrefixes.CONFIG_TRACKABLES_MODIFY, ButtonStyle.Secondary, disabled: !hasTrackables)
            .WithButton("Remove", ModulePrefixes.CONFIG_TRACKABLES_REMOVE, ButtonStyle.Secondary, disabled: !hasTrackables)
            .WithRedButton();

        return new MessageContents(embed, components);
    }

    [ComponentInteraction(ModulePrefixes.CONFIG_TRACKABLES_ADD)]
    public async Task AddTrackableButton()
    {
        await DeferAsync();

        await ModifyOriginalResponseAsync(await UpsertConfirmation(null, Context.User.Id));
    }

    [ComponentInteraction(ModulePrefixes.CONFIG_TRACKABLES_MODIFY)]
    public async Task ModifyTrackableButton()
    {
        throw new NotImplementedException();
    }

    [ComponentInteraction(ModulePrefixes.CONFIG_TRACKABLES_REMOVE)]
    public async Task RemoveTrackableButton()
    {
        throw new NotImplementedException();
    }

    [ComponentInteraction(ModulePrefixes.CONFIG_TRACKABLES_CONFIRMATION_ASSIGNABLE_GUILD + "*")]
    public async Task AssignableGuildSelected(string id, string selectId)
    {
        await DeferAsync();

        var trackable = StateSerializer.DeserializeObject<TrackableSerializable>(id) ??
                        throw new ArgumentNullException(nameof(id));

        trackable.GuildToAssignIn = ulong.Parse(selectId);

        await ModifyOriginalResponseAsync(await UpsertConfirmation(trackable, Context.User.Id));
    }

    [ComponentInteraction(ModulePrefixes.CONFIG_TRACKABLES_CONFIRMATION_ASSIGNABLE_ROLE + "*")]
    public async Task AssignableRoleSelected(string id, string selectId)
    {
        await DeferAsync();

        var trackable = StateSerializer.DeserializeObject<TrackableSerializable>(id) ??
                        throw new ArgumentNullException(nameof(id));

        trackable.RoleToAssign = ulong.Parse(selectId);

        await ModifyOriginalResponseAsync(await UpsertConfirmation(trackable, Context.User.Id));
    }

    [ComponentInteraction(ModulePrefixes.CONFIG_TRACKABLES_CONFIRMATION_MONITORED_GUILD + "*")]
    public async Task MonitoredGuildSelected(string id, string selectId)
    {
        await DeferAsync();
        
        var trackable = StateSerializer.DeserializeObject<TrackableSerializable>(id) ??
                        throw new ArgumentNullException(nameof(id));

        trackable.GuildToMonitor = ulong.Parse(selectId);

        await ModifyOriginalResponseAsync(await UpsertConfirmation(trackable, Context.User.Id));
    }

    [ComponentInteraction(ModulePrefixes.CONFIG_TRACKABLES_CONFIRMATION_MONITORED_ROLE + "*")]
    public async Task MonitoredRoleSelected(string id, string selectId)
    {
        await DeferAsync();

        var trackable = StateSerializer.DeserializeObject<TrackableSerializable>(id) ??
                        throw new ArgumentNullException(nameof(id));

        trackable.RoleToMonitor = ulong.Parse(selectId);

        await ModifyOriginalResponseAsync(await UpsertConfirmation(trackable, Context.User.Id));
    }

    private async Task<MessageContents> UpsertConfirmation(TrackableSerializable? trackable, ulong userId)
    {
        var embed = new EmbedBuilder();

        trackable ??= new TrackableSerializable();
        var trackableSerialized = StateSerializer.SerializeObject(trackable);

        embed.AddField("Monitored Guild", trackable.GuildToMonitor)
            .AddField("Monitored Role", trackable.RoleToMonitor)
            .AddField("Guild to Assign In", trackable.GuildToAssignIn)
            .AddField("Role to Assign", trackable.RoleToAssign);

        var components = new ComponentBuilder();

        var monitoredGuilds = new List<SelectMenuOptionBuilder>();
        var assignableGuilds = new List<SelectMenuOptionBuilder>();

        foreach (var guild in await Context.Client.GetGuildsAsync())
        {
            var user = await guild.GetUserAsync(userId);

            if (user == null || !user.GuildPermissions.Has(GuildPermission.ManageGuild)) continue;

            AddGuildToSelectMenu(monitoredGuilds, guild, x => x.Id == trackable.GuildToMonitor);
            AddGuildToSelectMenu(assignableGuilds, guild, x => x.Id == trackable.GuildToAssignIn);
        }

        bool anyMonitorableRoles = true;
        bool anyAssignableRoles = true;

        var monitoredRolesSelection = await GetGuildRolesSelection(trackable.GuildToMonitor);
        var assignableRolesSelection = await GetGuildRolesSelection(trackable.GuildToAssignIn);

        if (monitoredRolesSelection == null)
        {
            monitoredRolesSelection = [new SelectMenuOptionBuilder("No Guild selected!", "none")];
            anyMonitorableRoles = false;
        }
        if (assignableRolesSelection == null)
        {
            assignableRolesSelection = [new SelectMenuOptionBuilder("No Guild selected!", "none")];
            anyAssignableRoles = false;
        }

        components
            .WithSelectMenu(ModulePrefixes.CONFIG_TRACKABLES_CONFIRMATION_MONITORED_GUILD + trackableSerialized, monitoredGuilds, "Guild to monitor")
            .WithSelectMenu(ModulePrefixes.CONFIG_TRACKABLES_CONFIRMATION_MONITORED_ROLE + trackableSerialized, monitoredRolesSelection, "Role to monitor", disabled: !anyMonitorableRoles)
            .WithSelectMenu(ModulePrefixes.CONFIG_TRACKABLES_CONFIRMATION_ASSIGNABLE_GUILD + trackableSerialized, assignableGuilds, "Guild to assign role in")
            .WithSelectMenu(ModulePrefixes.CONFIG_TRACKABLES_CONFIRMATION_ASSIGNABLE_ROLE + trackableSerialized, assignableRolesSelection, "Role to assign", disabled: !anyAssignableRoles);

        // TODO: Implement
        var submitDisabled = true;
        components.WithButton("Submit", ModulePrefixes.CONFIG_TRACKABLES_CONFIRMATION_ADD_BUTTON + trackableSerialized, ButtonStyle.Success, disabled: submitDisabled)
            .WithButton("Back", BaseModulePrefixes.CONFIG_PAGE_SELECT_PAGE_BUTTON +
                                StateSerializer.SerializeObject(StateSerializer.SerializeObject(Id)));

        return new MessageContents(string.Empty, embed.Build(), components);
    }

    private static void AddGuildToSelectMenu(List<SelectMenuOptionBuilder> guildSelection, IGuild guild, Func<IGuild, bool> isDefault)
    {
        guildSelection.Add(new SelectMenuOptionBuilder(guild.Name, guild.Id.ToString(),
            $"{guild.PremiumSubscriptionCount} Boosts, Tier {(int)guild.PremiumTier}.", isDefault: isDefault(guild)));
    }

    private async Task<List<SelectMenuOptionBuilder>?> GetGuildRolesSelection(ulong guildId)
    {
        var roles = new List<SelectMenuOptionBuilder>();

        if (guildId != 0ul)
        {
            var guild = await Context.Client.GetGuildAsync(guildId);
            if (guild != null)
            {
                roles.AddRange(guild.Roles.Select(role =>
                    new SelectMenuOptionBuilder(role.Name, role.Id.ToString(), $"Color {role.Color}")));
            }
        }

        return roles.Count == 0 ? null : roles;
    }
}
