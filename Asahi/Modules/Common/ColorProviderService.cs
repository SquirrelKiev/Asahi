namespace Asahi.Modules;

// TODO: Move things to use this
public class ColorProviderService(IDiscordClient client) : IColorProviderService
{
    public async ValueTask<Color> GetEmbedColor(ulong guildId)
    {
        var guild = await client.GetGuildAsync(guildId);

        return QuotingHelpers.GetUserRoleColorWithFallback(await guild.GetCurrentUserAsync(), Color.Default);
    }
}
