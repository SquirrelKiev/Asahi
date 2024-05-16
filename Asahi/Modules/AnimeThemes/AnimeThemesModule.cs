using BotBase.Modules;
using Discord.Interactions;
using Fergun.Interactive;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules.AnimeThemes;

public class AnimeThemesModule(IAnimeThemesClient atClient, InteractiveService interactive, ILogger<AnimeThemesModule> logger) : BotModule
{
    private static readonly TimeSpan ThemeSlashExpiryTime = TimeSpan.FromSeconds(60);
    private const string BACK_BUTTON = "at-bb:";
    private const string PREVIOUS_PAGE_BUTTON = "at-pp:";
    private const string NEXT_PAGE_BUTTON = "at-np:";
    private const string SELECT_PREFIX = "at-s:";

    // HACK: interactive.NextMessageComponentAsync doesn't register anything with CommandHandler,
    // causing issues as it thinks nothing is able to respond to the components. This makes it happy.
    // Has the added bonus of giving a response to people interacting with it who shouldn't be (people who didn't trigger the command)
    // honestly I'm blown away by my genius sometimes
    [ComponentInteraction($"({BACK_BUTTON}|{PREVIOUS_PAGE_BUTTON}|{NEXT_PAGE_BUTTON}|{SELECT_PREFIX})", TreatAsRegex = true)]
    public Task DummyComponent() => Task.CompletedTask;

    [SlashCommand("theme", "Searches for anime theme songs via animethemes.moe.")]
    public async Task ThemeSlash(string query)
    {
        const int maxPageLength = 5;
        await DeferAsync();

        var searchRes = await atClient.SearchAsync(query, new IAnimeThemesClient.SearchQueryParams());

    anime_selection:
        logger.LogTrace("anime selection");

        var selectedAnime = await SelectItemAsync(
            searchRes.search.anime,
            maxPageLength,
            SELECT_PREFIX,
            x => x.name,
            x => x.id.ToString(),
            "select anime",
            showBackButton: false);

        switch (selectedAnime.responseType)
        {
            case ResultType.Success:
                break;
            case ResultType.HandledAlreadyDontWorryAboutIt:
                return;
            case ResultType.Back:
                throw new NotSupportedException();
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (selectedAnime.response?.animethemes == null)
        {
            return;
        }

        logger.LogTrace("theme selection");

        EmbedBuilder AnimeRichInfo(EmbedBuilder x)
        {
            x.WithTitle(selectedAnime.response.name.Truncate(256));
            var thumbUrl = selectedAnime.response.images?.MinBy(y => y.facet.HasValue ? (int)y.facet.Value : int.MaxValue)
                ?.link;

            if (thumbUrl != null)
            {
                x.WithThumbnailUrl(thumbUrl);
            }

            return x;
        }

        var selectedTheme = await SelectItemAsync(
            selectedAnime.response.animethemes.Order(new AnimeThemeResourceComparer()).ToArray(),
            maxPageLength,
            SELECT_PREFIX,
            x => $"{x.slug}{(x.song != null ? $" - {x.song.title} by {x.song.artists.Select(z => z.artistsong?.character != null ? $"{z.artistsong.character} (CV: {z.name})" : z.name).Humanize()}" : "")}",
            x => x.id.ToString(),
            "select theme",
            AnimeRichInfo
                );

        switch (selectedTheme.responseType)
        {
            case ResultType.Success:
                break;
            case ResultType.HandledAlreadyDontWorryAboutIt:
                return;
            case ResultType.Back:
                // instantly get banished to the depths of hell with this one easy trick
                goto anime_selection;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (selectedTheme.response?.animeThemeEntries == null)
        {
            return;
        }

        logger.LogTrace("theme entry selection");

        var selectedAnimeThemeEntry = await SelectItemAsync(
             selectedTheme.response.animeThemeEntries,
             maxPageLength,
             SELECT_PREFIX,
             x => $"v{(x.version.HasValue ? x.version.Value : "1")} - episodes {x.episodes}",
             x => x.id.ToString(),
             "select theme entry",
             AnimeRichInfo);

        switch (selectedAnimeThemeEntry.responseType)
        {
            case ResultType.Success:
                break;
            case ResultType.HandledAlreadyDontWorryAboutIt:
                return;
            case ResultType.Back:
                goto anime_selection;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (selectedAnimeThemeEntry.response?.videos == null)
        {
            return;
        }

        logger.LogTrace("video selection");

        var selectedVideo = await SelectItemAsync(
            selectedAnimeThemeEntry.response.videos,
            maxPageLength,
            SELECT_PREFIX,
            x => $"{x}",
            x => x.id.ToString(),
            "select video",
            AnimeRichInfo);

        switch (selectedVideo.responseType)
        {
            case ResultType.Success:
                break;
            case ResultType.HandledAlreadyDontWorryAboutIt:
                return;
            case ResultType.Back:
                goto anime_selection;
            default:
                throw new ArgumentOutOfRangeException();
        }

        var msg = await ModifyOriginalResponseAsync(new MessageContents($"(this might take a little time to embed)\n{selectedVideo.response.link}",
            components: new ComponentBuilder().WithButton("Back", BACK_BUTTON, ButtonStyle.Secondary).WithRedButton()));

        var selectInteraction = await interactive.NextMessageComponentAsync(
            x => msg.Id == x.Message.Id &&
                 (x.Data.CustomId == BACK_BUTTON || x.Data.CustomId == BaseModulePrefixes.RED_BUTTON),
            timeout: ThemeSlashExpiryTime);

        if (!selectInteraction.IsSuccess)
        {
            logger.LogTrace("button check failed for reason {Reason}", selectInteraction.Status);
            await ModifyOriginalResponseAsync(TimeOutEdit);
            return;
        }

        await selectInteraction.Value.DeferAsync();

        if (selectInteraction.Value.Data.Type == ComponentType.Button)
        {
            if (selectInteraction.Value.Data.CustomId == BACK_BUTTON)
            {
                goto anime_selection;
            }
            if (selectInteraction.Value.Data.CustomId == BaseModulePrefixes.RED_BUTTON)
            { }
        }
    }

    private enum ResultType
    {
        Success,
        HandledAlreadyDontWorryAboutIt,
        Back
    }

    private async Task<(T? response, ResultType responseType)> SelectItemAsync<T>(
            ICollection<T> items,
            int maxPageLength,
            string prefix,
            Func<T, string> labelSelector,
            Func<T, string> valueSelector,
            string logContext,
            Func<EmbedBuilder, EmbedBuilder>? editPageEmbed = null,
            bool showBackButton = true)
    {
        if (items.Count == 1)
        {
            return (items.First(), ResultType.Success);
        }

        editPageEmbed ??= x => x;

        var pages = items.Chunk(maxPageLength)
            .Select(x => editPageEmbed(new EmbedBuilder()
                .WithDescription(string.Join('\n', x.Select(y => $"- **{labelSelector(y)}**"))))).ToArray();
        var selectMenus = items.Chunk(maxPageLength)
            .Select((x, i) => new SelectMenuBuilder()
                .WithCustomId($"{prefix}")
                .WithOptions(items
                    .Skip(i * maxPageLength)
                    .Take(maxPageLength)
                    .Select(y => new SelectMenuOptionBuilder()
                        .WithLabel(labelSelector(y).Truncate(100))
                        .WithValue(valueSelector(y)))
                    .ToList())).ToArray();

        int pageIndex = 0;
        int maxPage = pages.Length - 1;
        while (true)
        {
            var cb = new ComponentBuilder()
                .WithSelectMenu(selectMenus[pageIndex])
                .WithButton("Back", BACK_BUTTON, ButtonStyle.Secondary, disabled: !showBackButton)
                .WithButton("<", PREVIOUS_PAGE_BUTTON, ButtonStyle.Secondary, disabled: pageIndex == 0)
                .WithButton(">", NEXT_PAGE_BUTTON, ButtonStyle.Secondary, disabled: pageIndex == maxPage)
                .WithRedButton()
                ;

            var msg = await ModifyOriginalResponseAsync(
                new MessageContents("", pages[pageIndex].WithFooter($"Page {pageIndex + 1}/{maxPage + 1}").Build(), cb));

            var selectInteraction = await interactive.NextMessageComponentAsync(
            x => msg.Id == x.Message.Id && x.User.Id == Context.User.Id &&
                 (x.Data.CustomId == prefix ||
                  x.Data.CustomId == BACK_BUTTON ||
                  x.Data.CustomId == BaseModulePrefixes.RED_BUTTON ||
                  x.Data.CustomId == PREVIOUS_PAGE_BUTTON ||
                  x.Data.CustomId == NEXT_PAGE_BUTTON),
            timeout: ThemeSlashExpiryTime);

            if (!selectInteraction.IsSuccess)
            {
                logger.LogTrace("{LogContext} (component listener) failed for reason {Reason}", logContext, selectInteraction.Status);
                await ModifyOriginalResponseAsync(TimeOutEdit);
                return (default, ResultType.HandledAlreadyDontWorryAboutIt);
            }


            if(selectInteraction.Value.Data.CustomId != BaseModulePrefixes.RED_BUTTON)
                await selectInteraction.Value.DeferAsync();

            if (selectInteraction.Value.Data.Type == ComponentType.Button)
            {
                switch (selectInteraction.Value.Data.CustomId)
                {
                    case BACK_BUTTON:
                        return (default, ResultType.Back);
                    case BaseModulePrefixes.RED_BUTTON:
                        return (default, ResultType.HandledAlreadyDontWorryAboutIt);
                    case PREVIOUS_PAGE_BUTTON:
                        pageIndex--;
                        continue;
                    case NEXT_PAGE_BUTTON:
                        pageIndex++;
                        continue;
                    default:
                        return (default, ResultType.HandledAlreadyDontWorryAboutIt);
                }
            }

            if (selectInteraction.Value.Data.Type != ComponentType.SelectMenu ||
                selectInteraction.Value.Data.Values.Count != 1 ||
                !int.TryParse(selectInteraction.Value.Data.Values.First(), out var selectedItemId))
            {
                logger.LogError("{LogContext} failed because component response was invalid, type was {Type}, values were {Values}",
                    logContext, selectInteraction.Value.Data.Type, selectInteraction.Value.Data.Values);
                await ModifyOriginalResponseAsync(new MessageContents("Interaction was invalid?"));

                return (default, ResultType.HandledAlreadyDontWorryAboutIt);
            }

            return (items.First(x => valueSelector(x) == selectedItemId.ToString()), ResultType.Success);
        }
    }

    private void TimeOutEdit(MessageProperties obj)
    {
        obj.Components = new Optional<MessageComponent>(new ComponentBuilder().Build());
    }
}
