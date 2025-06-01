using System.Buffers.Text;
using System.Diagnostics;
using System.Text;
using Discord.Interactions;
using Fergun.Interactive;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules.AnimeThemes;

[CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
[IntegrationType(ApplicationIntegrationType.GuildInstall, ApplicationIntegrationType.UserInstall)]
public class AnimeThemesModule(
    IAnimeThemesClient atClient,
    InteractiveService interactive,
    BotConfig config,
    ILogger<AnimeThemesModule> logger) : BotModule
{
    private static readonly TimeSpan ThemeSlashExpiryTime = TimeSpan.FromMinutes(10);
    private const string BACK_BUTTON = "atv2-bb:";
    private const string PREVIOUS_PAGE_BUTTON = "atv2-pp:";
    private const string NEXT_PAGE_BUTTON = "atv2-np:";
    private const string CHOICE_BUTTON = "atv2-choice:";

    // TODO: Migrate to the new fergun component paginator
    [SlashCommand("theme", "Searches for anime theme songs via animethemes.moe.")]
    public async Task ThemeSlash([Summary(description: "The anime to look for the theme songs of.")] string query)
    {
        IComponentInteraction? discordInteraction = null;

        await RespondAsync($"{config.LoadingEmote} Please wait...",
            allowedMentions: AllowedMentions.None);

        // TODO: is there a better way?
        var msgId = (await GetOriginalResponseAsync()).Id;

        var searchRes = await atClient.SearchAsync(query, new IAnimeThemesClient.SearchQueryParams());

        animeSelection:
        int pageIndex = 0;
        AnimeResource? selectedAnime = null;
        while (selectedAnime == null)
        {
            var components = new ComponentBuilderV2();

            var pages = searchRes.search.anime.Chunk(4).ToArray();
            var componentPages = new List<List<IMessageComponentBuilder>>();

            for (int pg = 0; pg < pages.Length; pg++)
            {
                var chunk = pages[pg];
                var pageComponents = new List<IMessageComponentBuilder>();

                for (var i = 0; i < chunk.Length; i++)
                {
                    var anime = chunk[i];

                    var totalThemes = anime.animethemes?.Length ?? 0;

                    var titleComponent = new SectionBuilder();
                    titleComponent.WithTextDisplay(
                        $"### {i + 1}. {anime.name}\n{anime.media_format.GetValueOrDefault()} • {anime.season} {anime.year} • {totalThemes} {(totalThemes == 1 ? "theme" : "themes")}");

                    var image = GetAnimeThumbnail(anime);
                    var media = new UnfurledMediaItemProperties(image);

                    titleComponent.WithAccessory(new ThumbnailBuilder().WithMedia(media));

                    // ---

                    pageComponents.Add(titleComponent);

                    if (chunk.Length - 1 != i)
                    {
                        var separator = new SeparatorBuilder().WithIsDivider(true);
                        pageComponents.Add(separator);
                    }
                }

                pageComponents.Add(new SeparatorBuilder().WithIsDivider(true).WithSpacing(SeparatorSpacingSize.Small));
                pageComponents.Add(new ActionRowBuilder().WithComponents(chunk.Select((x, i) =>
                    new ButtonBuilder((i + 1).ToString(), $"{CHOICE_BUTTON}{x.id}", ButtonStyle.Success))));
                // pageComponents.Add(new TextDisplayBuilder($"-# Page {pageIndex + 1}/{pages.Length}"));

                pageComponents.Add(new SeparatorBuilder().WithIsDivider(false).WithSpacing(SeparatorSpacingSize.Small));

                pageComponents.Add(new ActionRowBuilder().WithComponents(
                [
                    new ButtonBuilder("<", PREVIOUS_PAGE_BUTTON, ButtonStyle.Secondary, isDisabled: pg == 0),
                    new ButtonBuilder($"{pg + 1} / {pages.Length}", "page-indicator", ButtonStyle.Secondary,
                        isDisabled: true),
                    new ButtonBuilder(">", NEXT_PAGE_BUTTON, ButtonStyle.Secondary,
                        isDisabled: pg >= pages.Length - 1)
                ]));

                componentPages.Add(
                [
                    new ContainerBuilder().WithComponents(pageComponents),
                    // new ActionRowBuilder().WithComponents(chunk.Select((x, i) => new ButtonBuilder((i+1).ToString(), x.id.ToString(), ButtonStyle.Success))),
                ]);
            }

            components.WithComponents(componentPages[pageIndex]);
            var builtComponents = components.Build();

            if (discordInteraction == null)
            {
                await Context.Interaction.ModifyOriginalResponseAsync(x =>
                {
                    x.Flags = MessageFlags.ComponentsV2;
                    x.Content = "";
                    x.Embeds = new Optional<Embed[]>([]);
                    x.Components = builtComponents;
                });
            }
            else
            {
                await discordInteraction.UpdateAsync(x =>
                {
                    x.Flags = MessageFlags.ComponentsV2;
                    x.Content = "";
                    x.Embeds = new Optional<Embed[]>([]);
                    x.Components = builtComponents;
                });
            }

            var interaction = await interactive.NextMessageComponentAsync(x =>
                msgId == x.Message.Id && x.User.Id == Context.User.Id &&
                (x.Data.CustomId is PREVIOUS_PAGE_BUTTON
                    or NEXT_PAGE_BUTTON || x.Data.CustomId.StartsWith(CHOICE_BUTTON)), timeout: ThemeSlashExpiryTime);

            if (!interaction.IsSuccess)
            {
                logger.LogTrace("(component listener) failed for reason {Reason}", interaction.Status);
                if (discordInteraction == null)
                {
                    await Context.Interaction.ModifyOriginalResponseAsync(x => TimeOutEdit(x, components));
                }
                else
                {
                    await discordInteraction.ModifyOriginalResponseAsync(x => TimeOutEdit(x, components));
                }

                return;
            }

            discordInteraction = interaction.Value;

            // await interaction.Value.DeferAsync();

            if (!interaction.Value.Data.CustomId.StartsWith(CHOICE_BUTTON))
            {
                switch (interaction.Value.Data.CustomId)
                {
                    case PREVIOUS_PAGE_BUTTON:
                        pageIndex--;
                        continue;
                    case NEXT_PAGE_BUTTON:
                        pageIndex++;
                        continue;
                    default:
                        logger.LogWarning("Unknown component {componentId} at anime selection stage.",
                            interaction.Value.Data.CustomId);
                        await discordInteraction.DeferAsync();
                        continue;
                }
            }

            var animeId = int.Parse(interaction.Value.Data.CustomId[CHOICE_BUTTON.Length..]);

            selectedAnime = searchRes.search.anime.First(x => x.id == animeId);
        }

        Debug.Assert(discordInteraction != null);

        themeSelection:
        pageIndex = 0;
        AnimeThemeResource? selectedTheme = null;
        AnimeThemeEntryResource? selectedEntry = null;
        while (selectedTheme == null || selectedEntry == null)
        {
            var components = new ComponentBuilderV2();

            Debug.Assert(selectedAnime.animethemes != null);

            var pages = selectedAnime.animethemes.Order(new AnimeThemeResourceComparer()).ToArray().Chunk(3)
                .ToArray();
            var componentPages = new List<List<IMessageComponentBuilder>>();

            for (int pg = 0; pg < pages.Length; pg++)
            {
                var chunk = pages[pg];
                var pageComponents = new List<IMessageComponentBuilder>();

                for (var i = 0; i < chunk.Length; i++)
                {
                    var theme = chunk[i];
                    Debug.Assert(theme.animeThemeEntries != null);

                    var titleText = ThemeToString(theme);
                    var titleComponent = new TextDisplayBuilder(titleText);

                    var videos = theme.animeThemeEntries.First().videos;
                    
                    Debug.Assert(videos != null);
                    
                    var thumbnailVideo = SelectBestVideoSource(videos);
                    var thumbnailVideoLink = thumbnailVideo.link;
                    
                    Debug.Assert(thumbnailVideoLink != null);

                    var titleSectionComponent = new SectionBuilder().WithTextDisplay(titleComponent)
                        .WithAccessory(
                            new ThumbnailBuilder(
                                new UnfurledMediaItemProperties(GetAnimeVideoThumbnailUrl(thumbnailVideoLink))));

                    pageComponents.Add(titleSectionComponent);

                    foreach (var entryChunk in theme.animeThemeEntries.Chunk(5))
                    {
                        var actionRow = new ActionRowBuilder();

                        // actionRow.AddComponent(new SelectMenuBuilder()
                        //     .WithPlaceholder($"{entryChunk.Length} {(entryChunk.Length == 1 ? "theme" : "themes")}")
                        //     .WithOptions(entryChunk
                        //         .Select(x => new SelectMenuOptionBuilder().WithLabel(x.ToString()).WithValue(
                        //             StateSerializer.SerializeObject(new ThemeAndEntrySelection
                        //                 { SelectedEntry = x.id, SelectedTheme = theme.id }, CHOICE_BUTTON)))
                        //         .ToList()).WithCustomId(theme.id.ToString()));

                        foreach (var entry in entryChunk)
                        {
                            var button = new ButtonBuilder(entry.ToString(),
                                StateSerializer.SerializeObject(new ThemeAndEntrySelection
                                    { SelectedEntry = entry.id, SelectedTheme = theme.id }, CHOICE_BUTTON),
                                ButtonStyle.Success);

                            actionRow.AddComponent(button);
                        }

                        pageComponents.Add(actionRow);
                    }

                    if (i != chunk.Length - 1)
                        pageComponents.Add(new SeparatorBuilder().WithIsDivider(true)
                            .WithSpacing(SeparatorSpacingSize.Large));
                }

                pageComponents.Add(new SeparatorBuilder().WithIsDivider(true).WithSpacing(SeparatorSpacingSize.Small));
                pageComponents.Add(new ActionRowBuilder().WithComponents(
                [
                    new ButtonBuilder("<", PREVIOUS_PAGE_BUTTON, ButtonStyle.Secondary, isDisabled: pg == 0),
                    new ButtonBuilder($"{pg + 1} / {pages.Length}", "page-indicator", ButtonStyle.Secondary,
                        isDisabled: true),
                    new ButtonBuilder(">", NEXT_PAGE_BUTTON, ButtonStyle.Secondary,
                        isDisabled: pg >= pages.Length - 1),
                    new ButtonBuilder("Back", BACK_BUTTON, ButtonStyle.Danger)
                ]));

                componentPages.Add(
                [
                    new ContainerBuilder().WithComponents(pageComponents)
                    // new ActionRowBuilder().WithComponents(chunk.Select((x, i) => new ButtonBuilder((i+1).ToString(), x.id.ToString(), ButtonStyle.Success))),
                ]);
            }

            components.WithComponents(componentPages[pageIndex]);
            var builtComponents = components.Build();

            await discordInteraction.UpdateAsync(x =>
            {
                x.Flags = MessageFlags.ComponentsV2;
                x.Content = "";
                x.Embeds = new Optional<Embed[]>([]);
                x.Components = builtComponents;
            });

            var interaction = await interactive.NextMessageComponentAsync(x =>
                    msgId == x.Message.Id && x.User.Id == Context.User.Id &&
                    (x.Data.CustomId is PREVIOUS_PAGE_BUTTON
                        or NEXT_PAGE_BUTTON or BACK_BUTTON || x.Data.CustomId.StartsWith(CHOICE_BUTTON)),
                timeout: ThemeSlashExpiryTime);

            if (!interaction.IsSuccess)
            {
                logger.LogTrace("(component listener) failed for reason {Reason}", interaction.Status);
                await discordInteraction.ModifyOriginalResponseAsync(x => TimeOutEdit(x, components));
                return;
            }

            discordInteraction = interaction.Value;

            if (!interaction.Value.Data.CustomId.StartsWith(CHOICE_BUTTON))
            {
                switch (interaction.Value.Data.CustomId)
                {
                    case PREVIOUS_PAGE_BUTTON:
                        pageIndex--;
                        continue;
                    case NEXT_PAGE_BUTTON:
                        pageIndex++;
                        continue;
                    case BACK_BUTTON:
                        goto animeSelection;
                    default:
                        logger.LogWarning("Unknown component {componentId} at anime selection stage.",
                            interaction.Value.Data.CustomId);
                        await discordInteraction.DeferAsync();
                        continue;
                }
            }

            var themeEntryId =
                StateSerializer.DeserializeObject<ThemeAndEntrySelection>(
                    interaction.Value.Data.CustomId[CHOICE_BUTTON.Length..]);
            selectedTheme = selectedAnime.animethemes.First(x => x.id == themeEntryId.SelectedTheme);

            Debug.Assert(selectedTheme.animeThemeEntries != null);

            selectedEntry = selectedTheme.animeThemeEntries.First(x => x.id == themeEntryId.SelectedEntry);
        }

        {
            Debug.Assert(selectedEntry.videos != null);

            var selectedVideo = SelectBestVideoSource(selectedEntry.videos);

            var videoEmbedComponents = new ComponentBuilderV2().WithComponents([
                new ContainerBuilder().WithComponents([
                    new MediaGalleryBuilder([
                        new MediaGalleryItemProperties(new UnfurledMediaItemProperties(selectedVideo.link),
                            isSpoiler: selectedEntry.spoiler.GetValueOrDefault())
                    ]),
                    new SectionBuilder()
                        .WithComponents([
                            new TextDisplayBuilder(
                                $"{ThemeToString(selectedTheme, $" • {selectedEntry}")}\nfrom *{selectedAnime.name}*")
                        ]).WithAccessory(
                            new ThumbnailBuilder(new UnfurledMediaItemProperties(GetAnimeThumbnail(selectedAnime)))),
                    new SeparatorBuilder().WithIsDivider(true).WithSpacing(SeparatorSpacingSize.Large),
                    new SectionBuilder().WithComponents([new TextDisplayBuilder("\u200b")])
                        .WithAccessory(new ButtonBuilder("Back", BACK_BUTTON, ButtonStyle.Danger))
                ])
            ]);

            var builtComponents = videoEmbedComponents.Build();

            await discordInteraction.UpdateAsync(x =>
            {
                x.Flags = MessageFlags.ComponentsV2;
                x.Content = "";
                x.Embeds = new Optional<Embed[]>([]);
                x.Components = builtComponents;
            });

            var interaction = await interactive.NextMessageComponentAsync(x =>
                msgId == x.Message.Id && x.User.Id == Context.User.Id &&
                x.Data.CustomId is BACK_BUTTON, timeout: ThemeSlashExpiryTime);

            if (!interaction.IsSuccess)
            {
                logger.LogTrace("(component listener) failed for reason {Reason}", interaction.Status);
                await discordInteraction.ModifyOriginalResponseAsync(x => TimeOutEdit(x, videoEmbedComponents));
                return;
            }

            discordInteraction = interaction.Value;

            // await interaction.Value.DeferAsync();

            if (interaction.Value.Data.CustomId == BACK_BUTTON)
            {
                goto themeSelection;
            }
        }
    }

    private void TimeOutEdit(MessageProperties obj, ComponentBuilderV2 components)
    {
        components.DisableAllComponents();

        obj.Flags = MessageFlags.ComponentsV2;
        obj.Content = "";
        obj.Embeds = new Optional<Embed[]>([]);
        obj.Components = components.Build();
    }

    private string GetAnimeVideoThumbnailUrl(string url)
    {
        var base64EncodedUrl = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(url));
        
        return $"{config.AsahiWebServicesBaseUrl}/api/thumb/{base64EncodedUrl}.png";
    }

    private static string GetAnimeThumbnail(AnimeResource anime)
    {
        return anime.images?.FirstOrDefault(x => x.facet == ImageResource.Facet.SmallCover)?.link ??
               "https://cubari.onk.moe/404.png";
    }

    private static string ThemeToString(AnimeThemeResource theme, string entryInformation = "")
    {
        var songInfo = "";

        if (theme.song != null)
        {
            var artistInfo = "";
            if (theme.song.artists != null && theme.song.artists.Length != 0)
            {
                artistInfo = $"\nby *{theme.song.artists.ToStringNice()}*";
            }

            songInfo = $"**{theme.song.title}**{artistInfo}";
        }

        return $"-# {theme.slug}{entryInformation}\n{songInfo}";
    }

    private static VideoResource SelectBestVideoSource(VideoResource[] videos)
    {
        // TODO: take into account stuff like creditless
        var best = videos.MaxBy(x => x.resolution);

        Debug.Assert(best != null);

        return best;
    }

    private struct ThemeAndEntrySelection
    {
        public required int SelectedTheme;
        public required int SelectedEntry;
    }
}
