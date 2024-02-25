namespace Seigen.Modules.TrackablesManagement;

public class TrackablesUtility
{
    public static async Task<bool> IsGuildValid(IDiscordClient client, ulong guildId, ulong userId)
    {
        return await IsGuildValid(await client.GetGuildAsync(guildId), userId);
    }

    public static async Task<bool> IsGuildValid(IGuild? guild, ulong userId)
    {
        return guild != null && IsGuildValid(guild, await guild.GetUserAsync(userId));
    }

    public static bool IsGuildValid(IGuild? guild, IGuildUser? user)
    {
        return guild != null && user != null && user.GuildPermissions.Has(GuildPermission.ManageGuild);
    }

    public static async Task<bool> IsGuildValidPermissionCheckOnly(IDiscordClient client, ulong guildId, ulong userId)
    {
        return await IsGuildValidPermissionCheckOnly(await client.GetGuildAsync(guildId), userId);
    }

    public static async Task<bool> IsGuildValidPermissionCheckOnly(IGuild? guild, ulong userId)
    {
        return guild == null || IsGuildValidPermissionCheckOnly(guild, await guild.GetUserAsync(userId));
    }

    public static bool IsGuildValidPermissionCheckOnly(IGuild? guild, IGuildUser? user)
    {
        if (guild == null || user == null)
        {
            return true;
        }
        return user.GuildPermissions.Has(GuildPermission.ManageGuild);
    }
}