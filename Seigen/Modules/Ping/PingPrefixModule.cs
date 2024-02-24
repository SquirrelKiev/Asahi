using BotBase;
using Discord.Commands;

namespace Seigen.Modules.Ping;

public class PingPrefixModule : PrefixModule
{
    private readonly PingService pingService;

    public PingPrefixModule(PingService pingService)
    {
        this.pingService = pingService;
    }

    [Command("ping")]
    // technically not needed as we dont have the help stuff loaded but
    [ParentModulePrefix(typeof(PingModule))]
    public async Task PingCommand()
    {
        await DeferAsync();

        await ReplyAsync(pingService.GetMessageContents(Context.Client));
    }
}