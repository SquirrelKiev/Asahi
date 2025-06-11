using Asahi.BotEmoteManagement;

namespace Asahi.Modules;

public class BotEmoteService(IDiscordClient discordClient, IInternalEmoteSource internalEmoteSource) :
    ReflectionBasedBotEmoteManagerService<BotEmotesSpecification, BotEmotes>(discordClient, internalEmoteSource)
{
}
