using System.Diagnostics;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Microsoft.Extensions.Logging;
using StrawberryShake;

namespace Asahi.Modules.AnimeThemes;

[CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
[IntegrationType(ApplicationIntegrationType.GuildInstall, ApplicationIntegrationType.UserInstall)]
public class AnimeThemesModule(
    IAnimeThemesClient atClient,
    InteractiveService interactive,
    AnimeThemesPaginatorGenerator animeThemesPaginatorGenerator,
    BotEmoteService emotes,
    WebServicesUrlSignerService urlSigner,
    HttpClient httpClient,
    BotConfig config,
    ILogger<AnimeThemesModule> logger) : BotModule
{
    private static readonly TimeSpan ThemeSlashExpiryTime = TimeSpan.FromMinutes(5);

    [SlashCommand("theme", "Searches for anime theme songs via animethemes.moe.")]
    public async Task ThemeSlash([Summary(description: "The anime to look for the theme songs of.")] string query)
    {
        await RespondAsync($"{emotes.Loading} Please wait...",
            allowedMentions: AllowedMentions.None);

        IOperationResult<IGetThemesWithDataResult> searchRes;
        try
        {
            searchRes = await atClient.GetThemesWithData.ExecuteAsync(query);
        }
        catch (TaskCanceledException)
        {
            await ModifyOriginalResponseAsync(x =>
                x.Content = "AnimeThemes took too long to respond, please try again later.");
            return;
        }

        searchRes.EnsureNoErrors();
        Debug.Assert(searchRes.Data != null);

        if (searchRes.Data.AnimePagination.Data.Count == 0)
        {
            // not sure a good design for a ComponentsV2 version of this so
            await ModifyOriginalResponseAsync(x => x.Content = "No results found!");

            return;
        }

        var state = new AnimeThemesSelectionState(searchRes.Data);

        var paginator = new ComponentPaginatorBuilder()
            .WithUsers(Context.User)
            .WithPageCount(state.CurrentStep.TotalPages)
            .WithUserState(state)
            .WithPageFactory(animeThemesPaginatorGenerator.GeneratePage)
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
            state.CurrentStep.SearchResponse.AnimePagination.Data.First(x => x.Id == animeId));

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

        Debug.Assert(selectedAnime.Animethemes != null);

        var selectedTheme = selectedAnime.Animethemes.First(x => x.Id == choice.SelectedTheme);

        Debug.Assert(selectedTheme.Animethemeentries != null);

        var selectedEntry = selectedTheme.Animethemeentries.First(x => x.Id == choice.SelectedEntry);

        Debug.Assert(selectedEntry.Videos != null);

        var selectedVideo = SelectBestVideoSource(selectedEntry.Videos);

        if (selectedVideo == null)
        {
            await ChangeStep(state, paginator, interaction, state.CurrentStep);
            return;
        }

        var loadingComponents = new ComponentBuilderV2().WithTextDisplay($"{emotes.Loading} Please wait...").Build();

        var respond = interaction.UpdateAsync(x => x.Components = loadingComponents);
        var sig = urlSigner.Sign(UrlSignature.UrlSignaturePurposes.ThemeProxy, selectedVideo.Filename);

        var videoUrl = $"{config.AsahiWebServicesBaseUrl}/api/theme/{selectedVideo.Filename}.webm?sig={sig}";
        // firing this off to make sure the cache is warm
        httpClient.Timeout = TimeSpan.FromSeconds(60);
        await httpClient.GetAsync(videoUrl, HttpCompletionOption.ResponseHeadersRead);

        await respond;

        var newStep = new AnimeThemesSelectionState.VideoDisplayState(searchResponse, selectedAnime, selectedTheme, selectedEntry, videoUrl);

        await ChangeStep(state, paginator, interaction, newStep, InteractionResponseType.DeferredUpdateMessage);
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
        IComponentInteraction interaction, AnimeThemesSelectionState.AnimeSelectionState newStep, InteractionResponseType responseType = InteractionResponseType.UpdateMessage)
    {
        state.CurrentStep = newStep;

        paginator.PageCount = state.CurrentStep.TotalPages;
        paginator.SetPage(0);

        await paginator.RenderPageAsync(interaction, responseType, false);
    }

    public static IAnimeThemeVideoInfo? SelectBestVideoSource(IVideosQueryInfo videos)
    {
        // TODO: take into account stuff like creditless
        var best = videos.Edges.MaxBy(x => x.Node.Resolution);

        return best?.Node;
    }
}
