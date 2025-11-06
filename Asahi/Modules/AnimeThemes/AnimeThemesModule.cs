using System.Diagnostics;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules.AnimeThemes;

[CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
[IntegrationType(ApplicationIntegrationType.GuildInstall, ApplicationIntegrationType.UserInstall)]
public class AnimeThemesModule(
    IAnimeThemesClient atClient,
    InteractiveService interactive,
    BotConfig config,
    BotEmoteService emotes,
    BotEmoteService emoteService,
    ILogger<AnimeThemesModule> logger) : BotModule
{
    private static readonly TimeSpan ThemeSlashExpiryTime = TimeSpan.FromMinutes(5);

    [SlashCommand("theme", "Searches for anime theme songs via animethemes.moe.")]
    public async Task ThemeSlash([Summary(description: "The anime to look for the theme songs of.")] string query)
    {
        await RespondAsync($"{emotes.Loading} Please wait...",
            allowedMentions: AllowedMentions.None);

        SearchResponse searchRes;
        try
        {
            searchRes = await atClient.SearchAsync(query, new IAnimeThemesClient.SearchQueryParams());
        }
        catch (TaskCanceledException)
        {
            await ModifyOriginalResponseAsync(x =>
                x.Content = "AnimeThemes took too long to respond, please try again later.");
            return;
        }

        if (searchRes.search.anime.Length == 0)
        {
            // not sure a good design for a ComponentsV2 version of this so
            await ModifyOriginalResponseAsync(x => x.Content = "No results found!");

            return;
        }

        var state = new AnimeThemesSelectionState(searchRes);

        var paginator = new ComponentPaginatorBuilder()
            .WithUsers(Context.User)
            .WithPageCount(state.CurrentStep.TotalPages)
            .WithUserState(state)
            .WithPageFactory(p => AnimeThemesPaginatorGenerator.GeneratePage(p, config, emoteService))
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .Build();

        await interactive.SendPaginatorAsync(paginator, Context.Interaction, ThemeSlashExpiryTime,
            resetTimeoutOnInput: true, responseType: InteractionResponseType.DeferredUpdateMessage);
    }

    [ComponentInteraction(ModulePrefixes.AnimeThemes.AnimeChoiceButtonId + "*")]
    public async Task AnimeChoiceButton(string choice)
    {
        var interaction = (IComponentInteraction)Context.Interaction;
        if (!interactive.TryGetComponentPaginator(interaction.Message, out var paginator) ||
            !paginator.CanInteract(interaction.User))
        {
            await DeferAsync();
            return;
        }

        var state = paginator.GetUserState<AnimeThemesSelectionState>();

        var animeId = StateSerializer.DeserializeObject<int>(choice);

        var newStep = new AnimeThemesSelectionState.ThemeSelectionState(
            state.CurrentStep.SearchResponse,
            state.CurrentStep.SearchResponse.search.anime.First(x => x.id == animeId));

        await ChangeStep(state, paginator, interaction, newStep);
    }

    [ComponentInteraction(ModulePrefixes.AnimeThemes.ThemeChoiceButtonId + "*")]
    public async Task ThemeChoiceButton(string choiceStr)
    {
        var interaction = (IComponentInteraction)Context.Interaction;
        if (!interactive.TryGetComponentPaginator(interaction.Message, out var paginator) ||
            !paginator.CanInteract(interaction.User))
        {
            await DeferAsync();
            return;
        }

        var state = paginator.GetUserState<AnimeThemesSelectionState>();

        if (state.CurrentStep is not
            AnimeThemesSelectionState.ThemeSelectionState(var searchResponse, var selectedAnime))
        {
            logger.LogWarning(
                "ThemeChoiceButton called when not in theme selection state, falling back to anime selection.");

            state.CurrentStep = new AnimeThemesSelectionState.AnimeSelectionState(state.CurrentStep.SearchResponse);

            paginator.PageCount = state.CurrentStep.TotalPages;
            paginator.SetPage(0);

            await paginator.RenderPageAsync(interaction);
            return;
        }

        var choice = StateSerializer.DeserializeObject<AnimeThemesPaginatorGenerator.ThemeAndEntrySelection>(choiceStr);

        Debug.Assert(selectedAnime.animethemes != null);

        var selectedTheme = selectedAnime.animethemes.First(x => x.id == choice.SelectedTheme);

        Debug.Assert(selectedTheme.animeThemeEntries != null);

        var selectedEntry = selectedTheme.animeThemeEntries.First(x => x.id == choice.SelectedEntry);

        Debug.Assert(selectedEntry.videos != null);

        var selectedVideo = SelectBestVideoSource(selectedEntry.videos);

        if (selectedVideo == null)
        {
            await ChangeStep(state, paginator, interaction, state.CurrentStep);
            return;
        }

        var newStep = new AnimeThemesSelectionState.VideoDisplayState(searchResponse, selectedAnime, selectedTheme,
            selectedEntry,
            selectedVideo);

        await ChangeStep(state, paginator, interaction, newStep);
    }

    [ComponentInteraction(ModulePrefixes.AnimeThemes.RefreshVideoId)]
    public async Task RefreshVideoButton()
    {
        var interaction = (IComponentInteraction)Context.Interaction;
        if (!interactive.TryGetComponentPaginator(interaction.Message, out var paginator) ||
            !paginator.CanInteract(interaction.User))
        {
            await DeferAsync();
            return;
        }

        var state = paginator.GetUserState<AnimeThemesSelectionState>();

        if (state.CurrentStep is not AnimeThemesSelectionState.VideoDisplayState videoDisplayState)
        {
            logger.LogWarning(
                "RefreshVideoButton called when not in video display state, falling back to anime selection.");

            state.CurrentStep = new AnimeThemesSelectionState.AnimeSelectionState(state.CurrentStep.SearchResponse);

            paginator.PageCount = state.CurrentStep.TotalPages;
            paginator.SetPage(0);

            await paginator.RenderPageAsync(interaction);
            return;
        }

        videoDisplayState.CacheBustingId = Guid.NewGuid();
        await paginator.RenderPageAsync(interaction);
    }

    [ComponentInteraction(ModulePrefixes.AnimeThemes.BackButtonId)]
    public async Task BackButton()
    {
        var interaction = (IComponentInteraction)Context.Interaction;
        if (!interactive.TryGetComponentPaginator(interaction.Message, out var paginator) ||
            !paginator.CanInteract(interaction.User))
        {
            await DeferAsync();
            return;
        }

        var state = paginator.GetUserState<AnimeThemesSelectionState>();

        switch (state.CurrentStep)
        {
            case AnimeThemesSelectionState.VideoDisplayState videoStep:
                await ChangeStep(state, paginator, interaction,
                    new AnimeThemesSelectionState.ThemeSelectionState(videoStep.SearchResponse,
                        videoStep.SelectedAnime));
                return;
            case AnimeThemesSelectionState.ThemeSelectionState themeStep:
                await ChangeStep(state, paginator, interaction,
                    new AnimeThemesSelectionState.AnimeSelectionState(themeStep.SearchResponse));
                return;
            default:
                await ChangeStep(state, paginator, interaction,
                    new AnimeThemesSelectionState.AnimeSelectionState(state.CurrentStep.SearchResponse));
                return;
        }
    }

    private static async Task ChangeStep(AnimeThemesSelectionState state, IComponentPaginator paginator,
        IComponentInteraction interaction, AnimeThemesSelectionState.AnimeSelectionState newStep)
    {
        state.CurrentStep = newStep;

        paginator.PageCount = state.CurrentStep.TotalPages;
        paginator.SetPage(0);

        await paginator.RenderPageAsync(interaction);
    }

    public static VideoResource? SelectBestVideoSource(VideoResource[] videos)
    {
        // TODO: take into account stuff like creditless
        var best = videos.MaxBy(x => x.resolution);

        return best;
    }
}
