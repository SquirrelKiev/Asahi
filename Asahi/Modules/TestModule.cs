#if DEBUG
using Asahi.Modules.RssAtomFeed;
using Asahi.Modules.RssAtomFeed.Models;
using Discord.Interactions;
using Newtonsoft.Json;

namespace Asahi.Modules;

public class TestModule(
    HttpClient client
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

        var variant = DanbooruMessageGenerator.GetBestVariant(content.MediaAsset.Variants);

        await FollowupAsync(variant?.Url ?? "uh oh");
    }
}
#endif