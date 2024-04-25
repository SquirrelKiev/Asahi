using System.Net;
using Discord.Interactions;

namespace Asahi.Modules.WolframAlpha;

// limited to trusted users due to geolocation features of Wolfram. Result/Simple API doesn't support changing geolocation for some reason and
// I can't be bothered to deal with the full results api.
[HasWolframPermissions]
[CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
[IntegrationType(ApplicationIntegrationType.GuildInstall, ApplicationIntegrationType.UserInstall)]
public class WolframModule(BotConfig botConfig, HttpClient httpClient) : BotModule
{
    public enum DisplayType
    {
        Summary,
        Expanded
    }

    [SlashCommand("wolfram", "Lookup something using Wolfram Alpha.")]
    public async Task WolframSlash([Summary(description: "What do you want to ask Wolfram Alpha?")] string query,
        [Summary(description: "Specifies how to display the query.")] DisplayType displayType = DisplayType.Summary, bool ephemeral = true)
    {
        if (string.IsNullOrWhiteSpace(botConfig.WolframAppId))
        {
            await RespondAsync("Wolfram is not configured for this bot instance.", ephemeral: true);
            return;
        }

        await DeferAsync(ephemeral);

        var urlEncodedQuery = Uri.EscapeDataString(query);

        var requestType = displayType switch
        {
            DisplayType.Summary => "result",
            DisplayType.Expanded => "simple",
            _ => throw new ArgumentOutOfRangeException(nameof(displayType), displayType, null)
        };

        var requestUrl = $"https://api.wolframalpha.com/v1/{requestType}?appid={botConfig.WolframAppId}&i={urlEncodedQuery}";

        var getQuery = await httpClient.GetAsync(requestUrl);

        var contentHeader = getQuery.Content.Headers.ContentType?.MediaType;
        var dataIsImage = contentHeader != null && contentHeader.StartsWith("image");

        if (dataIsImage)
        {
            var content = await getQuery.Content.ReadAsStreamAsync();

            await FollowupWithFileAsync(content, "request.gif", allowedMentions: AllowedMentions.None);
        }
        else
        {
            var content = await getQuery.Content.ReadAsStringAsync();

            await FollowupAsync($"Query: {query}, Answer: {content}", allowedMentions: AllowedMentions.None);
        }
    }
}