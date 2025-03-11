#if DEBUG
using Asahi.Modules.Models;
using Discord.Interactions;
using Newtonsoft.Json;

namespace Asahi.Modules;

[CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
[IntegrationType(ApplicationIntegrationType.GuildInstall, ApplicationIntegrationType.UserInstall)]
public class TestModule(
    HttpClient client,
    IFxTwitterApi fxTwitterApi,
    BotConfig config
    //ILogger<TestModule> logger
    ) : BotModule
{
    [SlashCommand("cat", "testy test test")]
    public async Task ScratchPadSlash()
    {
        await DeferAsync();

        var res = await client.GetAsync("https://danbooru.donmai.us/posts/random.json?tags=animal_focus+cat+rating%3Ageneral");

        var json = await res.Content.ReadAsStringAsync();

        var content = JsonConvert.DeserializeObject<DanbooruPost>(json)!;

        var variant = await DanbooruUtility.GetBestVariantOrFallback(content, config, fxTwitterApi);

        await FollowupAsync(variant?.Variant.Url ?? "uh oh");
    }
}
#endif
