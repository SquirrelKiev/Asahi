#if DEBUG
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules;

public class TestModule(
    //ILogger<TestModule> logger
    ) : BotModule
{
    [SlashCommand("scratch-pad", "testy test test")]
    public async Task ScratchPadSlash()
    {
        await RespondAsync("nothing here");
    }
}
#endif