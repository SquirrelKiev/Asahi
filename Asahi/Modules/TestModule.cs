#if DEBUG
using Discord.Interactions;

namespace Asahi.Modules;

public class TestModule(
    //ILogger<TestModule> logger
    ) : BotModule
{
    [SlashCommand("scratch-pad", "testy test test")]
    public async Task ScratchPadSlash()
    {
        await DeferAsync();

        await RespondAsync("<@667762724654022678>");
    }
}
#endif