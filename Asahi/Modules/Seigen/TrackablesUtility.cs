using Discord.WebSocket;

namespace Asahi.Modules.Seigen;

[Inject(ServiceLifetime.Singleton)]
public class TrackablesUtility
{
    public bool IsGuildValid(DiscordSocketClient client, ulong guildId, ulong userId)
    {
        return IsGuildValid(client.GetGuild(guildId), userId);
    }

    public bool IsGuildValid(SocketGuild? guild, ulong userId)
    {
        return guild != null && IsGuildValid(guild, guild.GetUser(userId));
    }

    public bool IsGuildValid(SocketGuild? guild, SocketGuildUser? user)
    {
        return guild != null && user != null && (user.GuildPermissions.Has(GuildPermission.ManageGuild));
    }

    public bool IsGuildValidIgnoreNull(DiscordSocketClient client, ulong guildId, ulong userId)
    {
        return IsGuildValidIgnoreNull(client.GetGuild(guildId), userId);
    }

    public bool IsGuildValidIgnoreNull(SocketGuild? guild, ulong userId)
    {
        return guild == null || IsGuildValidIgnoreNull(guild, guild.GetUser(userId));
    }

    public bool IsGuildValidIgnoreNull(SocketGuild? guild, SocketGuildUser? user)
    {
        if (guild == null || user == null)
        {
            return true;
        }
        return user.GuildPermissions.Has(GuildPermission.ManageGuild);
    }
}
