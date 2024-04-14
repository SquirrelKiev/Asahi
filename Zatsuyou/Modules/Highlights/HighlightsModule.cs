using System.Text.RegularExpressions;
using Discord.Interactions;
using Fergun.Interactive.Pagination;
using Fergun.Interactive;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Zatsuyou.Database;
using Zatsuyou.Database.Models;

namespace Zatsuyou.Modules.Highlights;

[Group("highlights", "Commands relating to the highlights system.")]
[InteractionsModCommand]
public partial class HighlightsModule(DbService dbService, InteractiveService interactiveService) : BotModule
{
    [SlashCommand("create", "Creates a new highlight board.")]
    public async Task CreateSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")] 
        [MaxLength(HighlightBoard.MaxNameLength)] 
        string name,
        [Summary(description: "The channel to log highlights to. Can be changed later.")]
        ITextChannel channel)
    {
        name = name.ToLowerInvariant();

        if (!IsValidId().IsMatch(name))
        {
            await FollowupAsync($"`{name}` is not valid.");
            return;
        }

        await DeferAsync();

        await using var context = dbService.GetDbContext();

        if (await context.HighlightBoards.AnyAsync(x => x.Name == name))
        {
            await FollowupAsync($"`{name}` already exists.");
            return;
        }

        context.HighlightBoards.Add(new HighlightBoard()
        {
            GuildId = Context.Guild.Id,
            Name = name,
            LoggingChannelId = channel.Id
        });

        await context.SaveChangesAsync();

        await FollowupAsync($"Added board `{name}`.");
    }

    [SlashCommand("remove", "Removes a highlight board.")]
    public async Task RemoveSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name)
    {
        name = name.ToLowerInvariant();

        if (!IsValidId().IsMatch(name))
        {
            await FollowupAsync($"`{name}` is not valid.");
            return;
        }

        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var board = await context.HighlightBoards.FirstOrDefaultAsync(x => x.Name == name);
        if (board == null)
        {
            await FollowupAsync($"`{name}` does not exist already.");
            return;
        }

        context.HighlightBoards.Remove(board);

        await context.SaveChangesAsync();

        await FollowupAsync($"Removed board `{name}`.");
    }

    [SlashCommand("threshold", "Sets the minimum required reactions needed to put that message in the starboard.")]
    public async Task ThresholdSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The minimum required reactions.")]
        uint threshold)
    {
        name = name.ToLowerInvariant();

        if (!IsValidId().IsMatch(name))
        {
            await FollowupAsync($"`{name}` is not valid.");
            return;
        }

        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var board = await context.HighlightBoards.FirstOrDefaultAsync(x => x.Name == name);
        if (board == null)
        {
            await FollowupAsync($"`{name}` does not exist.");
            return;
        }

        board.Threshold = threshold;

        await context.SaveChangesAsync();

        await FollowupAsync($"(`{name}`) Threshold set to {threshold}.");
    }

    [SlashCommand("max-message-age",
        "The maximum age (in seconds) a message is allowed to be to be added as a highlight. 0 = any age.")]
    public async Task SetMessageAgeSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The maximum age of a message, in seconds. 0 = any age.")]
        uint maxAge)
    {
        name = name.ToLowerInvariant();

        if (!IsValidId().IsMatch(name))
        {
            await FollowupAsync($"`{name}` is not valid.");
            return;
        }

        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var board = await context.HighlightBoards.FirstOrDefaultAsync(x => x.Name == name);
        if (board == null)
        {
            await FollowupAsync($"`{name}` does not exist.");
            return;
        }

        board.MaxMessageAgeSeconds = maxAge;

        await context.SaveChangesAsync();

        await FollowupAsync($"(`{name}`) Maximum age set to {maxAge}s.");
    }

    [SlashCommand("add-filtered-channel",
        "Adds the channel to the channel filter. Channel filter is blocklist by default.")]
    public async Task AddFilterChannelSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        ITextChannel channel)
    {
        name = name.ToLowerInvariant();

        if (!IsValidId().IsMatch(name))
        {
            await FollowupAsync($"`{name}` is not valid.");
            return;
        }

        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var board = await context.HighlightBoards.FirstOrDefaultAsync(x => x.Name == name);
        if (board == null)
        {
            await FollowupAsync($"`{name}` does not exist.");
            return;
        }

        if (board.FilteredChannels.Contains(channel.Id))
        {
            await FollowupAsync($"Channel already in filtered channels.");
            return;
        }

        board.FilteredChannels.Add(channel.Id);

        await context.SaveChangesAsync();

        await FollowupAsync($"(`{name}`) Channel <#{channel.Id}> added to filtered channels.");
    }

    [SlashCommand("remove-filtered-channel",
        "Removes the channel from the channel filter. Channel filter is blocklist by default.")]
    public async Task RemoveFilterChannelSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        ITextChannel channel)
    {
        name = name.ToLowerInvariant();

        if (!IsValidId().IsMatch(name))
        {
            await FollowupAsync($"`{name}` is not valid.");
            return;
        }

        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var board = await context.HighlightBoards.FirstOrDefaultAsync(x => x.Name == name);
        if (board == null)
        {
            await FollowupAsync($"`{name}` does not exist.");
            return;
        }

        if (!board.FilteredChannels.Contains(channel.Id))
        {
            await FollowupAsync($"Channel not in filtered channels anyway.");
            return;
        }

        board.FilteredChannels.Remove(channel.Id);

        await context.SaveChangesAsync();

        await FollowupAsync($"(`{name}`) Channel <#{channel.Id}> removed from filtered channels.");
    }

    public enum AllowBlockList
    {
        BlockList,
        AllowList
    }

    [SlashCommand("set-channel-filter-type",
        "Sets the channel filter type.")]
    public async Task SetFilterChannelTypeSlash(
        [Summary(description: "The name/ID of the board. Case insensitive.")]
        [MaxLength(HighlightBoard.MaxNameLength)]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string name,
        [Summary(description: "The filter type.")]
        AllowBlockList filterType)
    {
        name = name.ToLowerInvariant();

        if (!IsValidId().IsMatch(name))
        {
            await FollowupAsync($"`{name}` is not valid.");
            return;
        }

        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var board = await context.HighlightBoards.FirstOrDefaultAsync(x => x.Name == name);
        if (board == null)
        {
            await FollowupAsync($"`{name}` does not exist.");
            return;
        }

        board.FilteredChannelsIsBlockList = filterType == AllowBlockList.BlockList;

        await context.SaveChangesAsync();

        await FollowupAsync($"(`{name}`) Set channel filter type as {(board.FilteredChannelsIsBlockList ? "Blocklist" : "Allowlist")}.");
    }

    [SlashCommand("list-boards", "Lists all the boards in the guild.")]
    public async Task ListBoardsSlash()
    {
        const int maxPerPage = 2;

        await using var context = dbService.GetDbContext();

        var boards = await context.HighlightBoards.Where(x => x.GuildId == Context.Guild.Id).ToArrayAsync();
        var paginator = new LazyPaginatorBuilder()
            .AddUser(Context.User)
            .WithPageFactory(PageFactory)
            .WithFooter(PaginatorFooter.PageNumber)
            .WithMaxPageIndex(boards.Length / maxPerPage)
            .WithDefaultEmotes()
            .WithActionOnCancellation(ActionOnStop.DeleteInput)
            .Build();

        await interactiveService.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(1));
        return;

        Task<PageBuilder> PageFactory(int page)
        {
            var embed = new PageBuilder()
                .WithTitle("Boards");

            foreach (var board in boards.Skip(page * maxPerPage).Take(maxPerPage))
            {
                embed.AddField(board.Name, GetDisplayableBoardConfig(board));
            }

            return Task.FromResult(embed);
        }
    }

    private static string GetDisplayableBoardConfig(HighlightBoard board)
    {
        return $"```json\n{JsonConvert.SerializeObject(board, Formatting.Indented).Replace("```", @"\`\`\`")}\n```";
    }

#if DEBUG
    [SlashCommand("debug-clear-highlights", "[DEBUG] Clear all cached highlighted messages from db.")]
    public async Task DebugClearHighlights()
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        await context.CachedHighlightedMessages.ExecuteDeleteAsync();

        await FollowupAsync("done'd");
    }
#endif

    [GeneratedRegex(@"^[\w-]+$")]
    private static partial Regex IsValidId();
}
