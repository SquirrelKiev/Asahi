using System.Diagnostics;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;

namespace Asahi.Modules.Lookup;

public static class AniListMediaPaginatorGenerator
{
    public static IPage GeneratePage(IComponentPaginator paginator, BotEmoteService emotes)
    {
        var state = paginator.GetUserState<AniListMediaSelectionState>();

        return state.CurrentStep switch
        {
            AniListMediaSelectionState.MediaInfoDisplayState infoDisplayState => GenerateMediaInfoPage(paginator,
                infoDisplayState),
            _ => GenerateMediaSelectionPage(paginator, state.CurrentStep)
        };
    }

    private static IPage GenerateMediaSelectionPage(IComponentPaginator p,
        AniListMediaSelectionState.MediaSelectionState state)
    {
        Debug.Assert(state.SearchResponse.Page?.Media != null);

        var chunk = state.SearchResponse.Page.Media.Chunk(AniListMediaSelectionState.MaxMediaPerPage)
            .ElementAt(p.CurrentPageIndex);
        var container = new ContainerBuilder();

        for (var i = 0; i < chunk.Length; i++)
        {
            var media = chunk[i]!;

            var titleComponent = new SectionBuilder();
            var mediaFormat = media.Format.GetValueOrDefault() switch
            {
                MediaFormat.Tv => "TV",
                MediaFormat.TvShort => "TV Short",
                MediaFormat.Movie => "Movie",
                MediaFormat.Special => "Special",
                MediaFormat.Ova => "OVA",
                MediaFormat.Ona => "ONA",
                MediaFormat.Music => "Music",
                // these shouldn't show up but no harm in having them
                MediaFormat.Manga => "Manga",
                MediaFormat.Novel => "Novel",
                MediaFormat.OneShot => "One Shot",
                _ => throw new NotSupportedException()
            };
            string? season = null;
            switch (media)
            {
                case { SeasonYear: not null, Season: not null }:
                    season = $"{media.Season} {media.SeasonYear}";
                    break;
                case { SeasonYear: not null }:
                    season = media.SeasonYear.ToString();
                    break;
            }

            if (season == null && media.StartDate?.Year != null)
            {
                season = media.StartDate.Year.ToString();
            }

            var mediaTitle = GetTitle(media);
            string title = $"### {i + 1}. {mediaTitle}\n{mediaFormat}";

            if (season != null)
            {
                title += $" • {season}";
            }

            titleComponent.WithTextDisplay(title);

            var image = GetMediaThumbnail(media);

            titleComponent.WithAccessory(new ThumbnailBuilder().WithMedia(image));

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
                StateSerializer.SerializeObject(x!.Id, ModulePrefixes.Lookup.AniList.MediaChoiceButtonId),
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

    private static IPage GenerateMediaInfoPage(IComponentPaginator p,
        AniListMediaSelectionState.MediaInfoDisplayState state)
    {
        var container = new ContainerBuilder();

        if (state.MediaInfo.BannerImage != null)
        {
            container.WithMediaGallery([state.MediaInfo.BannerImage]);
        }

        container.WithSeparator(SeparatorSpacingSize.Small, false);

        var section = new SectionBuilder();

        section.WithAccessory(new ThumbnailBuilder(state.MediaInfo.CoverImage!.Large));

        section.WithTextDisplay($"## [{GetTitle(state.MediaInfo)}]({state.MediaInfo.SiteUrl})");
        section.WithTextDisplay($"### ❓ **{state.MediaInfo.AverageScore!}%**\n" +
                                $"{state.MediaInfo.Description}");

        container.WithSection(section);

        var components = new ComponentBuilderV2().WithContainer(container);
        var builtComponents = components.Build();

        return new PageBuilder()
            .WithComponents(builtComponents)
            .Build();
    }

    private static string GetMediaThumbnail(IMediaInfo media)
    {
        return media.CoverImage?.Large ?? "https://cubari.onk.moe/404.png";
    }

    private static string GetTitle(IMediaInfo media)
    {
        return media.Title!.English ?? media.Title!.Romaji ?? media.Title!.Native!;
    }
}

public class AniListMediaSelectionState(IGetMediaResult data)
{
    public const int MaxMediaPerPage = 4;

    public MediaSelectionState CurrentStep = new(data);

    public record MediaSelectionState(IGetMediaResult SearchResponse)
    {
        public virtual int TotalPages =>
            (int)Math.Ceiling((double)SearchResponse.Page!.Media!.Count / MaxMediaPerPage);
    }

    public record MediaInfoDisplayState(IGetMediaResult SearchResponse, IMediaInfo MediaInfo)
        : MediaSelectionState(SearchResponse)
    {
        public override int TotalPages => 1;
    }
}
