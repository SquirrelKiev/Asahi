using System.Text;
using Asahi.Database;
using Asahi.Modules.Highlights;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;

namespace Asahi.Modules.Statistics;

[Group("stats", "Commands relating to statistics")]
public class StatisticsModule(IDbService dbService) : BotModule
{
    // One day you shall work...
    // .GroupBy(x => x.OriginalMessageId)
    // .Select(x => x.MaxBy(y => y.TotalUniqueReactions)!)

    [SlashCommand(
        "user-most-reacts",
        "Lists the users who over time have received the most reactions."
    )]
    public async Task UsersWithMostReactionsSlash(
        [Summary(description: "Optional emote to filter results by.")]
        string? emoteName = null,
        [Summary(description: "Optional channel to filter results by.")]
        IChannel? channel = null,
        [Summary(description: "Optional board to filter results by.")]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string? board = null
    )
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var query = context.CachedHighlightedMessagesWithStats.Where(x =>
            x.HighlightBoard.GuildId == Context.Guild.Id
        );

        if (board is not null)
            query = query.Where(x => x.HighlightBoard.Name == board);

        if (channel is not null)
            query = query.Where(x => x.OriginalMessageChannelId == channel.Id);

        string topStr;

        if (emoteName is null)
        {
            var results = await query
                // dumb hacks cuz of efcore nonsense. basically, to avoid the same message on multiple boards skewing results, im trying to
                // filter it to the message with the most reacts.
                // efcore doesn't like when I do MaxBy or OrderByDescending in a select statement however, so this is the best I can achieve.
                // if I/you knew of a better way, ***please*** say
                .GroupBy(x => new { x.OriginalMessageId, x.AuthorId })
                .Select(x => new
                {
                    x.Key.AuthorId,
                    TotalUniqueReactions = x.Max(m => m.TotalUniqueReactions)
                })
                .GroupBy(x => x.AuthorId)
                .Select(x => new { x.Key, TotalUniqueReactions = x.Sum(y => y.TotalUniqueReactions) })
                .OrderByDescending(x => x.TotalUniqueReactions)
                .Take(10)
                .ToArrayAsync();

            topStr = results
                .Aggregate(
                    new StringBuilder(),
                    (x, y) =>
                        x.AppendLine(
                            $"<@{y.Key}> - {y.TotalUniqueReactions} total reactions"
                        )
                )
                .ToString();
        }
        else
        {
            var results = await query
                .GroupBy(x => new { x.OriginalMessageId, x.AuthorId })
                .Select(x => new 
                {
                    x.Key.AuthorId,
                    Count = x.Max(y => 
                        y.CachedMessageReactions
                            .Where(z => z.EmoteName.ToLower() == emoteName.ToLower())
                            .Sum(z => z.Count))
                })
                .GroupBy(x => x.AuthorId)
                .Select(x => new
                {
                    x.Key,
                    Count = x.Sum(y => y.Count)
                })
                .Where(x => x.Count != 0)
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToArrayAsync();


            topStr = results
                .Aggregate(
                    new StringBuilder(),
                    (x, y) =>
                        x.AppendLine(
                            $"<@{y.Key}> - {y.Count} total {emoteName}"
                        )
                )
                .ToString();
        }
        
        if (string.IsNullOrEmpty(topStr))
            topStr = "No results.";

        var eb = new EmbedBuilder()
            .WithTitle(Context.Guild.Name)
            .WithColor(
                QuotingHelpers.GetUserRoleColorWithFallback(
                    await Context.Guild.GetCurrentUserAsync(),
                    Color.Green
                )
            )
            .WithDescription(topStr);

        await FollowupAsync(new MessageContents(eb));
    }

    [SlashCommand(
        "channel-most-reacts",
        "Lists the channels with the most summed reacted messages."
    )]
    public async Task ChannelsWithMostReactionsSlash(
        [Summary(description: "Optional board to filter results by.")]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string? board = null
    )
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var query = context.CachedHighlightedMessagesWithStats.Where(x =>
            x.HighlightBoard.GuildId == Context.Guild.Id
        );

        if (board is not null)
            query = query.Where(x => x.HighlightBoard.Name == board);

        var results = await query
            .GroupBy(x => new { x.OriginalMessageId, x.OriginalMessageChannelId })
            .Select(x => new
            {
                x.Key.OriginalMessageChannelId,
                TotalUniqueReactions = x.Max(m => m.TotalUniqueReactions)
            })
            .GroupBy(x => x.OriginalMessageChannelId)
            .Select(x => new { x.Key, TotalUniqueReactions = x.Sum(y => y.TotalUniqueReactions) })
            .OrderByDescending(x => x.TotalUniqueReactions)
            .Take(10)
            .ToArrayAsync();

        var topStr = results
            .Aggregate(
                new StringBuilder(),
                (x, y) =>
                    x.AppendLine(
                        $"<#{y.Key}> - {y.TotalUniqueReactions} total reactions"
                    )
            )
            .ToString();
        
        if (string.IsNullOrEmpty(topStr))
            topStr = "No results.";

        var eb = new EmbedBuilder()
            .WithTitle(Context.Guild.Name)
            .WithColor(
                QuotingHelpers.GetUserRoleColorWithFallback(
                    await Context.Guild.GetCurrentUserAsync(),
                    Color.Green
                )
            )
            .WithDescription(topStr);

        await FollowupAsync(new MessageContents(eb));
    }

    [SlashCommand(
        "message-most-reacts",
        "Lists the messages with the most reacted messages."
    )]
    public async Task MessageWithMostReactionsSlash(
        [Summary(description: "Optional channel to filter results by.")]
        IChannel? channel = null,
        [Summary(description: "Optional board to filter results by.")]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string? board = null
    )
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var query = context.CachedHighlightedMessagesWithStats.Where(x =>
            x.HighlightBoard.GuildId == Context.Guild.Id
        );
        
        if (board is not null)
            query = query.Where(x => x.HighlightBoard.Name == board);

        if (channel is not null)
            query = query.Where(x => x.OriginalMessageChannelId == channel.Id);

        var results = await query
            .GroupBy(x => new { x.OriginalMessageId, x.OriginalMessageChannelId, x.HighlightBoard.GuildId })
            .Select(x => new
            {
                x.Key.OriginalMessageId,
                x.Key.OriginalMessageChannelId,
                x.Key.GuildId,
                TotalUniqueReactions = x.Max(m => m.TotalUniqueReactions)
            })
            .OrderByDescending(x => x.TotalUniqueReactions)
            .Take(10)
            .ToArrayAsync();

        var topStr = results
            .Aggregate(
                new StringBuilder(),
                (x, y) =>
                    x.AppendLine(
                        $"https://discord.com/channels/{y.GuildId}/{y.OriginalMessageChannelId}/{y.OriginalMessageId} - {y.TotalUniqueReactions} total reactions"
                    )
            )
            .ToString();
        
        if (string.IsNullOrEmpty(topStr))
            topStr = "No results.";

        var eb = new EmbedBuilder()
            .WithTitle(Context.Guild.Name)
            .WithColor(
                QuotingHelpers.GetUserRoleColorWithFallback(
                    await Context.Guild.GetCurrentUserAsync(),
                    Color.Green
                )
            )
            .WithDescription(topStr);

        await FollowupAsync(new MessageContents(eb));
    }

    [SlashCommand(
        "emote-most-reacts",
        "Lists the emotes with the most summed reacted messages."
    )]
    public async Task EmoteWithMostReactsSlash(
        [Summary(description: "Optional channel to filter results by.")]
        IChannel? channel = null,
        [Summary(description: "Optional board to filter results by.")]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string? board = null
    )
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var query = context.CachedHighlightedMessagesWithStats.Where(x =>
            x.HighlightBoard.GuildId == Context.Guild.Id
        );

        if (board is not null)
            query = query.Where(x => x.HighlightBoard.Name == board);
        
        if (channel is not null)
            query = query.Where(x => x.OriginalMessageChannelId == channel.Id);

        var results = await query
            .GroupBy(x => new { x.OriginalMessageId, x.OriginalMessageChannelId })
            .Select(x => new
            {
                x.Key.OriginalMessageChannelId,
                TotalUniqueReactions = x.Max(m => m.TotalUniqueReactions)
            })
            .GroupBy(x => x.OriginalMessageChannelId)
            .Select(x => new { x.Key, TotalUniqueReactions = x.Sum(y => y.TotalUniqueReactions) })
            .OrderByDescending(x => x.TotalUniqueReactions)
            .Take(10)
            .ToArrayAsync();

        var topStr = results
            .Aggregate(
                new StringBuilder(),
                (x, y) =>
                    x.AppendLine(
                        $"<#{y.Key}> - {y.TotalUniqueReactions} total reactions"
                    )
            )
            .ToString();

        if (string.IsNullOrEmpty(topStr))
            topStr = "No results.";

        var eb = new EmbedBuilder()
            .WithTitle(Context.Guild.Name)
            .WithColor(
                QuotingHelpers.GetUserRoleColorWithFallback(
                    await Context.Guild.GetCurrentUserAsync(),
                    Color.Green
                )
            )
            .WithDescription(topStr);

        await FollowupAsync(new MessageContents(eb));
    }
}
