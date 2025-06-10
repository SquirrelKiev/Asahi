using Asahi.BotEmoteManagement;

namespace Asahi.Modules;

public class BotEmoteService(IDiscordClient discordClient, BotEmoteManagerConfig config) :
    BotEmoteManagerService<BotEmotesSpecification, BotEmotes>(discordClient, config)
{
}
