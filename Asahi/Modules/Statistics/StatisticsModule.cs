using System.Security.Cryptography.X509Certificates;
using System.Text;
using Asahi.Database;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;

namespace Asahi.Modules.Statistics;

[Group("stats", "Commands relating to statistics")]
public class StatisticsModule(IDbService dbService) : BotModule
{
    [SlashCommand("guild", "Bot-related statistics about the Guild.")]
    public async Task StatsSlash()
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var totalHighlightedMessages = await context.CachedHighlightedMessages
            .Where(x => x.HighlightBoard.GuildId == Context.Guild.Id)
            .CountAsync();

        var topHighlightedChannels = await context.CachedHighlightedMessages
            .Where(x => x.HighlightBoard.GuildId == Context.Guild.Id)
            .GroupBy(x => x.OriginalMessageChannelId)
            .Select(x => new { ChannelId = x.Key, Count = x.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToArrayAsync();

        var topHighlightedChannelsStr = topHighlightedChannels
            .Aggregate(new StringBuilder(), (x, y) =>
                x.AppendLine($"<#{y.ChannelId}> - {y.Count} message(s) highlighted")).ToString();

        if (topHighlightedChannelsStr.Length == 0)
        {
            topHighlightedChannelsStr = "No highlighted messages.";
        }

        var eb = new EmbedBuilder()
            .WithTitle(Context.Guild.Name)
            .WithColor(QuotingHelpers.GetUserRoleColorWithFallback(await Context.Guild.GetCurrentUserAsync(), Color.Green))
            .WithFields([
                new EmbedFieldBuilder().WithName("Highlighted Messages")
                    .WithValue(totalHighlightedMessages.ToString()),
                new EmbedFieldBuilder().WithName("\"Funniest\" channels")
                    .WithValue(topHighlightedChannelsStr),
            ]);

        await FollowupAsync(new MessageContents(eb, new ComponentBuilder().WithRedButton()));
    }

    [SlashCommand("channel", "Bot-related statistics about the specified channel.")]
    public async Task StatsSlash([Summary(description: "The channel to fetch the statistics of")] IGuildChannel channel)
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var totalHighlightedMessages = await context.CachedHighlightedMessages
            .Where(x => x.HighlightBoard.GuildId == Context.Guild.Id && x.OriginalMessageChannelId == channel.Id)
            .CountAsync();

        var eb = new EmbedBuilder()
            .WithTitle($"<#{channel.Id}>")
            .WithColor(QuotingHelpers.GetUserRoleColorWithFallback(await Context.Guild.GetCurrentUserAsync(), Color.Green))
            .WithFields([
                new EmbedFieldBuilder().WithName("Highlighted Messages")
                    .WithValue(totalHighlightedMessages.ToString()),
            ]);

        await FollowupAsync(new MessageContents(eb, new ComponentBuilder().WithRedButton()));
    }
}