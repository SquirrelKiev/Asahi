namespace Asahi.Modules.Seigen;

[Inject(ServiceLifetime.Singleton)]
public class TrackablesUtility(OverrideTrackerService overrideTracker)
{
    public async Task<bool> IsGuildValid(IDiscordClient client, ulong guildId, ulong userId)
    {
        return await IsGuildValid(await client.GetGuildAsync(guildId), userId);
    }

    public async Task<bool> IsGuildValid(IGuild? guild, ulong userId)
    {
        return guild != null && await IsGuildValid(guild, await guild.GetUserAsync(userId));
    }

    public async Task<bool> IsGuildValid(IGuild? guild, IGuildUser? user)
    {
        return guild != null && user != null && (user.GuildPermissions.Has(GuildPermission.ManageGuild) || await overrideTracker.HasOverride(user.Id));
    }

    public async Task<bool> IsGuildValidIgnoreNull(IDiscordClient client, ulong guildId, ulong userId)
    {
        return await IsGuildValidIgnoreNull(await client.GetGuildAsync(guildId), userId);
    }

    public async Task<bool> IsGuildValidIgnoreNull(IGuild? guild, ulong userId)
    {
        return guild == null || await IsGuildValidIgnoreNull(guild, await guild.GetUserAsync(userId));
    }

    public async Task<bool> IsGuildValidIgnoreNull(IGuild? guild, IGuildUser? user)
    {
        if (guild == null || user == null)
        {
            return true;
        }
        return user.GuildPermissions.Has(GuildPermission.ManageGuild) || await overrideTracker.HasOverride(user.Id);
    }
}