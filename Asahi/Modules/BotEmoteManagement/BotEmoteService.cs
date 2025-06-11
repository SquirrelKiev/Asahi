using Asahi.BotEmoteManagement;

namespace Asahi.Modules;

public class BotEmoteService(IDiscordClient discordClient, IInternalEmoteSource internalEmoteSource) :
    BotEmoteManagerService<BotEmotesSpecification, BotEmotes>(discordClient, internalEmoteSource)
{
}
