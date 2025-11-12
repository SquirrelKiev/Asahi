using System.Buffers.Text;
using System.Text;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Humanizer;
using JetBrains.Annotations;

namespace Asahi.Modules.AnimeThemes;

public static class AnimeThemesPaginatorGenerator
{
    public static IPage GeneratePage(IComponentPaginator paginator, BotConfig config, BotEmoteService emoteService)
    {
        var state = paginator.GetUserState<AnimeThemesSelectionState>();

        return state.CurrentStep switch
        {
            AnimeThemesSelectionState.VideoDisplayState videoDisplayState =>
                GenerateVideoDisplayPage(paginator, videoDisplayState, emoteService),

            AnimeThemesSelectionState.ThemeSelectionState themeSelectionState =>
                GenerateThemeSelectionPage(paginator, themeSelectionState, config),

            _ => GenerateAnimeSelectionPage(paginator, state.CurrentStep)
        };
    }

    private static Page GenerateAnimeSelectionPage(IComponentPaginator p,
        AnimeThemesSelectionState.AnimeSelectionState state)
    {
        var chunk = state.SearchResponse.AnimePagination.Data.Chunk(AnimeThemesSelectionState.MaxAnimePerPage)
            .ElementAt(p.CurrentPageIndex);
        var container = new ContainerBuilder();

        for (var i = 0; i < chunk.Length; i++)
        {
            var anime = chunk[i];

            var totalThemes = anime.Animethemes?.Count ?? 0;

            var titleComponent = new SectionBuilder();
            var mediaFormat = anime.MediaFormat.GetValueOrDefault() switch
            {
                AnimeMediaFormat.Tv => "TV",
                AnimeMediaFormat.TvShort => "TV Short",
                AnimeMediaFormat.Ova => "OVA",
                AnimeMediaFormat.Movie => "Movie",
                AnimeMediaFormat.Special => "Special",
                AnimeMediaFormat.Ona => "ONA",
                _ => "N/A"
            };
            titleComponent.WithTextDisplay(
                $"### {i + 1}. {anime.Name}\n{mediaFormat} • {anime.Season} {anime.Year} • {totalThemes} {(totalThemes == 1 ? "theme" : "themes")}");

            var image = GetAnimeThumbnail(anime);
            var media = new UnfurledMediaItemProperties(image);

            titleComponent.WithAccessory(new ThumbnailBuilder().WithMedia(media));

            // ---

            container.WithSection(titleComponent);

            if (chunk.Length - 1 != i)
            {
                var separator = new SeparatorBuilder().WithIsDivider(true);
                container.WithSeparator(separator);
            }
        }

        container.WithSeparator(new SeparatorBuilder().WithIsDivider(true).WithSpacing(SeparatorSpacingSize.Small));
        container.WithActionRow(new ActionRowBuilder().WithComponents(chunk.Select((x, i) =>
            new ButtonBuilder((i + 1).ToString(),
                StateSerializer.SerializeObject(x.Id, ModulePrefixes.AnimeThemes.AnimeChoiceButtonId),
                ButtonStyle.Success,
                isDisabled: p.ShouldDisable()))));

        container.WithSeparator(new SeparatorBuilder().WithIsDivider(false).WithSpacing(SeparatorSpacingSize.Small));

        container.WithActionRow(new ActionRowBuilder()
            .AddPreviousButton(p, "<", ButtonStyle.Secondary)
            .AddPageIndicatorButton(p)
            .AddNextButton(p, ">", ButtonStyle.Secondary));

        var components = new ComponentBuilderV2()
            .WithContainer(container);
        var builtComponents = components.Build();

        return new PageBuilder()
            .WithComponents(builtComponents)
            .Build();
    }

    private static Page GenerateThemeSelectionPage(IComponentPaginator p,
        AnimeThemesSelectionState.ThemeSelectionState state, BotConfig config)
    {
        var chunk = state.SelectedAnime.Animethemes.Order(AnimeThemeInfoComparer.Instance)
            .Cast<IAnimeThemeInfoWithEntries>()
            .Chunk(AnimeThemesSelectionState.MaxThemesPerPage)
            .ElementAt(p.CurrentPageIndex);

        var container = new ContainerBuilder();

        for (var i = 0; i < chunk.Length; i++)
        {
            var theme = chunk[i];

            var titleText = ThemeToString(theme);
            var titleComponent = new TextDisplayBuilder(titleText);

            if (!TryAddThumbnail(config, theme, titleComponent, container))
            {
                container.WithTextDisplay(titleComponent);
            }

            if (theme.Animethemeentries.Count != 0)
            {
                foreach (var entryChunk in
                         theme.Animethemeentries.Chunk(4)) // 4 buttons is where discord seems to wrap buttons
                {
                    var actionRow = new ActionRowBuilder();

                    foreach (var entry in entryChunk)
                    {
                        var button = new ButtonBuilder(entry.ToStringNice(),
                            StateSerializer.SerializeObject(new ThemeAndEntrySelection
                                    { SelectedEntry = entry.Id, SelectedTheme = theme.Id },
                                ModulePrefixes.AnimeThemes.ThemeChoiceButtonId),
                            ButtonStyle.Success, isDisabled: p.ShouldDisable());

                        actionRow.WithButton(button);
                    }

                    container.WithActionRow(actionRow);
                }
            }

            var isLastElement = i == chunk.Length - 1;
            if (!isLastElement)
                container.WithSeparator(new SeparatorBuilder().WithIsDivider(true)
                    .WithSpacing(SeparatorSpacingSize.Large));
        }

        container.WithSeparator(new SeparatorBuilder().WithIsDivider(true).WithSpacing(SeparatorSpacingSize.Small));
        container.WithActionRow(new ActionRowBuilder()
            .AddPreviousButton(p, "<", ButtonStyle.Secondary)
            .AddPageIndicatorButton(p)
            .AddNextButton(p, ">", ButtonStyle.Secondary)
            .WithButton("Back", ModulePrefixes.AnimeThemes.BackButtonId, ButtonStyle.Danger,
                disabled: p.ShouldDisable()));

        var components = new ComponentBuilderV2()
            .WithContainer(container);
        var builtComponents = components.Build();

        return new PageBuilder()
            .WithComponents(builtComponents)
            .Build();
    }

    private static IPage GenerateVideoDisplayPage(IComponentPaginator p,
        AnimeThemesSelectionState.VideoDisplayState state, BotEmoteService emoteService)
    {
        var videoUrl = state.SelectedVideo.Link;
        if (state.CacheBustingId != Guid.Empty)
        {
            videoUrl += $"?cache-bust={state.CacheBustingId}";
        }

        var videoEmbedComponents = new ComponentBuilderV2().WithComponents([
            new ContainerBuilder().WithComponents([
                new MediaGalleryBuilder([
                    new MediaGalleryItemProperties(new UnfurledMediaItemProperties(videoUrl),
                        isSpoiler: state.SelectedThemeEntry.Spoiler)
                ]),
                new SectionBuilder()
                    .WithComponents([
                        new TextDisplayBuilder(
                            $"{ThemeToString(state.SelectedTheme, $" • {state.SelectedThemeEntry.ToStringNice()}")}\nfrom *{state.SelectedAnime.Name}*")
                    ]).WithAccessory(
                        new ThumbnailBuilder(new UnfurledMediaItemProperties(GetAnimeThumbnail(state.SelectedAnime)))),
                // new SectionBuilder().WithComponents([new TextDisplayBuilder("\u200b")])
                //     .WithAccessory(new ButtonBuilder("Refresh Video", RefreshVideoId, ButtonStyle.Secondary,
                //         emote: emoteService.Refresh, isDisabled: p.ShouldDisable())),
                new SeparatorBuilder().WithIsDivider(true).WithSpacing(SeparatorSpacingSize.Large),
                new ActionRowBuilder().WithComponents([
                    new ButtonBuilder("Back", ModulePrefixes.AnimeThemes.BackButtonId, ButtonStyle.Danger,
                        isDisabled: p.ShouldDisable()),
                    new ButtonBuilder("Refresh Video", ModulePrefixes.AnimeThemes.RefreshVideoId, ButtonStyle.Secondary,
                        emote: emoteService.Refresh, isDisabled: p.ShouldDisable()),
                ])
                // new SectionBuilder().WithComponents([new TextDisplayBuilder("\u200b")])
                // .WithAccessory(new ButtonBuilder("Back", BackButtonId, ButtonStyle.Danger,
                // isDisabled: p.ShouldDisable())),
            ])
        ]);

        var builtComponents = videoEmbedComponents.Build();

        return new PageBuilder()
            .WithComponents(builtComponents)
            .Build();
    }

    #region Utility methods

    private static bool TryAddThumbnail(BotConfig config, IAnimeThemeInfoWithEntries theme,
        TextDisplayBuilder titleComponent,
        ContainerBuilder container)
    {
        // we only care about the first version so we can get the thumbnail
        var videos = theme.Animethemeentries.Count > 0 ? theme.Animethemeentries[0].Videos : null;

        if (videos == null)
        {
            return false;
        }

        var thumbnailVideo = AnimeThemesModule.SelectBestVideoSource(videos);
        var thumbnailVideoLink = thumbnailVideo?.Link;

        if (thumbnailVideoLink == null)
        {
            return false;
        }

        var titleSectionComponent = new SectionBuilder().WithTextDisplay(titleComponent)
            .WithAccessory(
                new ThumbnailBuilder(
                    new UnfurledMediaItemProperties(GetAnimeVideoThumbnailUrl(thumbnailVideoLink,
                        config))));

        container.WithSection(titleSectionComponent);

        return true;
    }

    private static string GetAnimeVideoThumbnailUrl(string url, BotConfig config)
    {
        var base64EncodedUrl = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(url));

        return $"{config.AsahiWebServicesBaseUrl}/api/thumb/{base64EncodedUrl}.png";
    }

    private static string GetAnimeThumbnail(IAnimeInfo anime)
    {
        return anime.Images?.Edges.FirstOrDefault(x => x.Node.Facet == ImageFacet.SmallCover)?.Node.Link ??
               "https://cubari.onk.moe/404.png";
    }

    private static string ThemeToString(IAnimeThemeInfo theme, string entryInformation = "")
    {
        var songInfo = "";

        if (theme.Song != null)
        {
            var artistInfo = "";
            if (theme.Song.Performances.Count != 0)
            {
                artistInfo = $"\nby *{theme.Song.Performances.Select(x => x.ToStringNice()).Humanize()}*";
            }

            songInfo = $"**{theme.Song.Title}**{artistInfo}";
        }

        return $"-# {theme.Slug}{entryInformation}\n{songInfo}";
    }

    [ProtoBuf.ProtoContract]
    public struct ThemeAndEntrySelection
    {
        [ProtoBuf.ProtoMember(1)] public required int SelectedTheme;
        [ProtoBuf.ProtoMember(2)] public required int SelectedEntry;
    }

    #endregion
}

public class AnimeThemesSelectionState(IGetThemesWithDataResult searchResponse)
{
    public const int MaxAnimePerPage = 4;
    public const int MaxThemesPerPage = 3;

    public AnimeSelectionState CurrentStep = new(searchResponse);

    public record AnimeSelectionState(IGetThemesWithDataResult SearchResponse)
    {
        public virtual int TotalPages =>
            (int)Math.Ceiling((double)SearchResponse.AnimePagination.Data.Count / MaxAnimePerPage);
    }

    public record ThemeSelectionState(IGetThemesWithDataResult SearchResponse, IAnimeInfoWithThemes SelectedAnime)
        : AnimeSelectionState(SearchResponse)
    {
        public override int TotalPages =>
            (int)Math.Ceiling((double)SelectedAnime!.Animethemes!.Count / MaxThemesPerPage);
    }

    public record VideoDisplayState(
        IGetThemesWithDataResult SearchResponse,
        IAnimeInfoWithThemes SelectedAnime,
        IAnimeThemeInfoWithEntries SelectedTheme,
        IAnimeThemeEntryInfoWithVideos SelectedThemeEntry,
        IAnimeThemeVideoInfo SelectedVideo) : ThemeSelectionState(SearchResponse, SelectedAnime)
    {
        public override int TotalPages => 1;
        public Guid CacheBustingId = Guid.Empty;
    }
}

public class AnimeThemeInfoComparer : IComparer<IAnimeThemeInfo>
{
    public static readonly AnimeThemeInfoComparer Instance = new();

    [Pure]
    public int Compare(IAnimeThemeInfo? x, IAnimeThemeInfo? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        if (x.Type != y.Type)
        {
            return x.Type.CompareTo(y.Type);
        }

        if (x.Sequence != y.Sequence)
        {
            if (x.Sequence == null) return -1;
            if (y.Sequence == null) return 1;
            return x.Sequence.Value.CompareTo(y.Sequence.Value);
        }

        return 0;
    }
}

public static class AnimeThemeModelExtensions
{
    public static string ToStringNice(this IAnimeThemeEntryInfoWithVideos themeEntryInfo)
    {
        List<string> labels = [];

        if (themeEntryInfo.Nsfw)
        {
            labels.Add("NSFW");
        }

        if (themeEntryInfo.Spoiler)
        {
            labels.Add("Spoiler");
        }

        var warnings = "";

        if (labels.Count != 0)
        {
            warnings = $"({labels.Humanize()}) ";
        }

        return $"{warnings}v{themeEntryInfo.Version ?? 1} • episodes {themeEntryInfo.Episodes ?? "??"}";
    }

    public static string ToStringNice(this IAnimeThemeInfoWithEntries themeInfo, bool includeSlug = true,
        bool discordRichText = false)
    {
        List<string> labels = [];
        if (themeInfo.Animethemeentries.All(x => x.Nsfw))
        {
            labels.Add("NSFW");
        }
        else if (themeInfo.Animethemeentries.Any(x => x.Nsfw))
        {
            labels.Add("May contain NSFW");
        }

        if (themeInfo.Animethemeentries.All(x => x.Spoiler))
        {
            labels.Add("spoilers");
        }
        else if (themeInfo.Animethemeentries.Any(x => x.Spoiler))
        {
            labels.Add("may contain spoilers");
        }

        var warnings = "";
        if (labels.Count != 0)
        {
            warnings = $"({labels.Humanize()}) ";
        }

        var songInfo = "";

        if (themeInfo.Song != null)
        {
            var artistInfo = "";
            if (themeInfo.Song.Performances.Count != 0)
            {
                var formattedArtists = themeInfo.Song.Performances.Select(x => x.ToStringNice()).Humanize();
                artistInfo = discordRichText ? $" by **{formattedArtists}**" : $" by {formattedArtists}";
            }

            songInfo = discordRichText
                ? $"**{themeInfo.Song.Title}**{artistInfo}"
                : $"{themeInfo.Song.Title}{artistInfo}";
        }

        return $"{warnings}{(includeSlug ? $"**{themeInfo.Slug}** • {songInfo}" : songInfo)}";
    }

    public static string ToStringNice(this IPerformanceInfo performanceInfo)
    {
        // const string linkBase = "https://animethemes.moe/artist";

        var character = performanceInfo.As;
        var stageName = performanceInfo.Alias;

        switch (performanceInfo.Artist)
        {
            case IArtistInfo artistInfo:
            {
                var artistName = artistInfo.Name;

                var displayName = stageName ?? artistName;
                // var hyperlinkedDisplayName = $"[{displayName}]({linkBase}/{artistInfo.Slug})";
                if (character != null)
                {
                    return $"{character} (CV: {displayName})";
                }
                else
                {
                    return displayName;
                }
            }
            case IMembershipInfo membershipInfo:
            {
                string displayName;
                // string hyperlinkedDisplayName;

                if (stageName != null)
                {
                    // hyperlinkedDisplayName = $"[{stageName}]({linkBase}/{membershipInfo.Group.Slug})";
                    displayName = stageName;
                }
                else
                {
                    var artistName = membershipInfo.Member.Name;
                    // var hyperlinkedArtistName = $"[{artistName}]({linkBase}/{membershipInfo.Member.Slug})";
                    var groupName = membershipInfo.Group.Name;
                    // var hyperlinkedGroupName = $"[{groupName}]({linkBase}/{membershipInfo.Group.Slug})";

                    displayName = $"{artistName}* from *{groupName}";
                    // hyperlinkedDisplayName = $"{hyperlinkedArtistName} from {hyperlinkedGroupName}";
                }
                
                if (character != null)
                {
                    return $"{character} (CV: {displayName})";
                }
                else
                {
                    return displayName;
                }
            }

            default:
                return character ?? stageName ?? "Unknown!";
        }
    }
}
