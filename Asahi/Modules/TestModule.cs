#if DEBUG
using Asahi.Modules.Models;
using Discord.Interactions;
using Newtonsoft.Json;

namespace Asahi.Modules;

[CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
[IntegrationType(ApplicationIntegrationType.GuildInstall, ApplicationIntegrationType.UserInstall)]
public class TestModule(
    HttpClient client,
    DanbooruUtility danbooruUtility
    //ILogger<TestModule> logger
) : BotModule
{
    [SlashCommand("cat", "testy test test")]
    public async Task ScratchPadSlash()
    {
        await DeferAsync();
        
        var res = await client.GetAsync(
            "https://danbooru.donmai.us/posts/random.json?tags=animal_focus+cat+rating%3Ageneral");

        var json = await res.Content.ReadAsStringAsync();

        var content = JsonConvert.DeserializeObject<DanbooruPost>(json)!;

        var variant = await danbooruUtility.GetBestVariantOrFallback(content);

        var components = new ComponentBuilderV2();

        components.WithMediaGallery([variant!.Variant.Url]);

        components.WithActionRow([new ButtonBuilder("say hi to the cat", ModulePrefixes.TestPrefix)]);
        
        await FollowupAsync(components: components.Build());
    }

    [ComponentInteraction(ModulePrefixes.TestPrefix)]
    [RequireCommandInvoker]
    public async Task ScratchPadComponent()
    {
        await RespondAsync($"{Context.User.Mention} says hello!", allowedMentions: AllowedMentions.None);
    }
}
#endif
