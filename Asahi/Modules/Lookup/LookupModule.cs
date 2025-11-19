using System.Diagnostics;
using Discord.Interactions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using StrawberryShake;

namespace Asahi.Modules.Lookup;

[Group("lookup", "Commands related to looking up stuff.")]
[CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
[IntegrationType(ApplicationIntegrationType.GuildInstall, ApplicationIntegrationType.UserInstall)]
public class LookupModule(BotEmoteService emotes, InteractiveService interactive, IAniListClient alClient) : BotModule
{
    private static readonly TimeSpan AnimeLookupSlashExpiryTime = TimeSpan.FromMinutes(5);
    
    [SlashCommand("anime", "Lookup an anime on AniList.")]
    public async Task AnimeLookupSlash([Summary(description: "The title of the anime to look up.")] string query)
    {
        await RespondAsync($"{emotes.Loading} Please wait...",
            allowedMentions: AllowedMentions.None);

        IOperationResult<IGetMediaResult> searchRes;
        try
        {
            searchRes = await alClient.GetMedia.ExecuteAsync(query, MediaType.Anime);
        }
        catch (TaskCanceledException)
        {
            await ModifyOriginalResponseAsync(x =>
                x.Content = "AniList took too long to respond, please try again later.");
            return;
        }

        searchRes.EnsureNoErrors();
        Debug.Assert(searchRes.Data?.Page?.Media != null);
        
        if (searchRes.Data.Page.Media.Count == 0)
        {
            // not sure a good design for a ComponentsV2 version of this so
            await ModifyOriginalResponseAsync(x => x.Content = "No results found!");

            return;
        }

        var state = new AniListMediaSelectionState(searchRes.Data);
        
        Debug.Assert(searchRes.Data.Page?.Media != null);

        if (searchRes.Data.Page.Media.Count == 1)
        {
            state.CurrentStep =
                new AniListMediaSelectionState.MediaInfoDisplayState(searchRes.Data, searchRes.Data.Page.Media[0]!);
        }

        var paginator = new ComponentPaginatorBuilder()
            .WithUsers(Context.User)
            .WithPageCount(state.CurrentStep.TotalPages)
            .WithUserState(state)
            .WithPageFactory(p => AniListMediaPaginatorGenerator.GeneratePage(p, emotes))
            .WithActionOnCancellation(ActionOnStop.DeleteMessage)
            .WithActionOnTimeout(ActionOnStop.DisableInput)
            .Build();

        await interactive.SendPaginatorAsync(paginator, Context.Interaction, AnimeLookupSlashExpiryTime,
            resetTimeoutOnInput: true, responseType: InteractionResponseType.DeferredUpdateMessage);
    }

    [ComponentInteraction(ModulePrefixes.Lookup.AniList.MediaChoiceButtonId + "*", ignoreGroupNames: true)]
    public async Task MediaChoiceButton(string choice)
    {
        var interaction = (IComponentInteraction)Context.Interaction;
        if (!interactive.TryGetComponentPaginator(interaction.Message, out var paginator) ||
            !paginator.CanInteract(interaction.User))
        {
            await DeferAsync();
            return;
        }

        var state = paginator.GetUserState<AniListMediaSelectionState>();

        var mediaId = StateSerializer.DeserializeObject<int>(choice);

        var newStep = new AniListMediaSelectionState.MediaInfoDisplayState(
            state.CurrentStep.SearchResponse,
            state.CurrentStep.SearchResponse.Page!.Media!.First(x => x!.Id == mediaId)!);

        await ChangeStep(state, paginator, interaction, newStep);
    }
    
    private static async Task ChangeStep(AniListMediaSelectionState state, IComponentPaginator paginator,
        IComponentInteraction interaction, AniListMediaSelectionState.MediaSelectionState newStep)
    {
        state.CurrentStep = newStep;

        paginator.PageCount = state.CurrentStep.TotalPages;
        paginator.SetPage(0);

        await paginator.RenderPageAsync(interaction);
    }
}
