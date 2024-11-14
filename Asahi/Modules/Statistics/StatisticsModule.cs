using System.Text;
using Asahi.Database;
using Asahi.Modules.Highlights;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules.Statistics;

[Group("stats", "Commands relating to statistics")]
public class StatisticsModule(IDbService dbService, ILogger<StatisticsModule> logger) : BotModule
{
    // One day you shall work...
    // .GroupBy(x => x.OriginalMessageId)
    // .Select(x => x.MaxBy(y => y.TotalUniqueReactions)!)

    public class IdToReactionCount
    {
        public ulong Id { get; set; }
        public int TotalReactions { get; set; }
    }

    public class EmoteToReactionCount
    {
        public string DisplayEmote { get; set; } = "";
        public int TotalReactions { get; set; }
    }

    public class MessageIdToReactionCount
    {
        public ulong MessageId { get; set; }
        public ulong ChannelId { get; set; }
        public int TotalReactions { get; set; }
    }

    [SlashCommand(
        "user-most-reacts",
        "Lists the users who over time have received the most reactions."
    )]
    public async Task UsersWithMostReactionsSlash(
        [Summary(description: "Optional emote to filter results by.")]
        string emoteName = "",
        [Summary(description: "Optional channel to filter results by.")]
        IChannel? channel = null,
        [Summary(description: "Optional board to filter results by.")]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string? board = ""
    )
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var postgresHappyChannelId = channel?.Id ?? 0;

        var results = await context.Database
            .SqlQuery<IdToReactionCount>($"""
                                          WITH UniqueMessages AS (
                                              SELECT DISTINCT ON (CHM."OriginalMessageId")
                                                  "AuthorId",
                                                  CASE
                                                      WHEN {emoteName} = '' THEN CHM."TotalUniqueReactions"
                                                      ELSE cmr."Count"
                                                      END as "ReactionCount"
                                              FROM "CachedHighlightedMessages" CHM
                                                       INNER JOIN "CachedMessageReactions" CMR
                                                                  ON CHM."Id" = CMR."HighlightedMessageId"
                                                                      AND ({emoteName} = '' OR CMR."EmoteName" = {emoteName})
                                              WHERE "HighlightBoardGuildId" = {Context.Guild.Id}
                                                AND ({board} = '' OR "HighlightBoardName" = {board})
                                                AND ({postgresHappyChannelId} = 0 OR "OriginalMessageChannelId" = {postgresHappyChannelId})
                                              ORDER BY "OriginalMessageId", "ReactionCount" DESC
                                          )
                                                          SELECT "AuthorId" as Id, SUM("ReactionCount") as TotalReactions FROM UniqueMessages
                                                          GROUP BY "AuthorId"
                                                          ORDER BY TotalReactions DESC
                                                          LIMIT 10;
                                          """).ToArrayAsync();

        var topStr = results
            .Aggregate(
                new StringBuilder(),
                (x, y) => x.AppendLine($"<@{y.Id}> - {y.TotalReactions} total {(emoteName ?? "reactions")}")
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

        var postgresHappyBoard = board ?? "";

        var results = await context.Database
            .SqlQuery<IdToReactionCount>($"""
                                          WITH UniqueMessages AS (
                                              SELECT DISTINCT ON ("OriginalMessageId")
                                                  "OriginalMessageChannelId",
                                                  "TotalUniqueReactions"
                                              FROM "CachedHighlightedMessages"
                                              WHERE "HighlightBoardGuildId" = {Context.Guild.Id}
                                              AND ({postgresHappyBoard} = '' OR "HighlightBoardName" = {postgresHappyBoard})
                                              ORDER BY "OriginalMessageId", "TotalUniqueReactions" DESC
                                          )
                                          SELECT "OriginalMessageChannelId" AS Id, SUM("TotalUniqueReactions") as TotalReactions FROM UniqueMessages
                                          GROUP BY "OriginalMessageChannelId"
                                          ORDER BY TotalReactions DESC
                                          LIMIT 10;
                                          """).ToArrayAsync();

        var topStr = results
            .Aggregate(
                new StringBuilder(),
                (x, y) => x.AppendLine($"<#{y.Id}> - {y.TotalReactions} total reactions")
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

    [SlashCommand("message-most-reacts", "Lists the messages with the most reacted messages.")]
    public async Task MessageWithMostReactionsSlash(
        [Summary(description: "Optional channel to filter results by.")]
        IChannel? channel = null,
        [Summary(description: "Optional board to filter results by.")]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string board = "",
        [Summary(description: "Optional emote to filter results by")]
        string emoteName = "",
        [Summary(description: "Optional user to filter results by")]
        IUser? author = null
    )
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var postgresHappyChannelId = channel?.Id ?? 0;
        var postgresHappyAuthorId = author?.Id ?? 0;

        var results = await context.Database.SqlQuery<MessageIdToReactionCount>($"""
             WITH UniqueMessages AS (
                 SELECT DISTINCT ON (CHM."OriginalMessageId")
                     "OriginalMessageId",
                     "OriginalMessageChannelId",
                     CASE
                         WHEN {emoteName} = '' THEN CHM."TotalUniqueReactions"
                         ELSE cmr."Count"
                         END as "ReactionCount"
                 FROM "CachedHighlightedMessages" CHM
                          INNER JOIN "CachedMessageReactions" CMR
                                     ON CHM."Id" = CMR."HighlightedMessageId"
                                         AND ({emoteName} = '' OR CMR."EmoteName" = {emoteName})
                 WHERE "HighlightBoardGuildId" = {Context.Guild.Id}
                   AND ({board} = '' OR "HighlightBoardName" = {board})
                   AND ({postgresHappyChannelId} = 0 OR "OriginalMessageChannelId" = {postgresHappyChannelId})
                   AND ({postgresHappyAuthorId} = 0 OR "AuthorId" = {postgresHappyAuthorId})
                 ORDER BY "OriginalMessageId", "ReactionCount" DESC
             )
             SELECT "OriginalMessageId" as MessageId, "OriginalMessageChannelId" as ChannelId, SUM("ReactionCount") as TotalReactions
             FROM UniqueMessages
             GROUP BY "OriginalMessageId", "OriginalMessageChannelId"
             ORDER BY TotalReactions DESC
             LIMIT 10;
             """).ToArrayAsync();

        var topStr = results
            .Aggregate(
                new StringBuilder(),
                (x, y) =>
                    x.AppendLine(
                        $"https://discord.com/channels/{Context.Guild.Id}/{y.ChannelId}/{y.MessageId} - {y.TotalReactions} total reactions"
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

    [SlashCommand("emote-most-reacts", "Lists the emotes with the most summed reacted messages.")]
    public async Task EmoteWithMostReactsSlash(
        [Summary(description: "Optional channel to filter results by.")]
        IChannel? channel = null,
        [Summary(description: "Optional board to filter results by.")]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string board = "",
        [Summary(description: "Optional user to filter results by")]
        IUser? author = null
    )
    {
        await DeferAsync();

        await using var context = dbService.GetDbContext();

        var postgresHappyChannelId = channel?.Id ?? 0;
        var postgresHappyAuthorId = author?.Id ?? 0;

        var results = await context.Database.SqlQuery<EmoteToReactionCount>($"""
                                                                             WITH UniqueReactions AS (
                                                                                 SELECT DISTINCT ON (CHM."OriginalMessageId", CMR."EmoteName", CMR."EmoteId")
                                                                                     CMR."EmoteName",
                                                                                     CMR."EmoteId",
                                                                                     CMR."IsAnimated",
                                                                                     CMR."Count"
                                                                                 FROM "CachedMessageReactions" CMR
                                                                                          INNER JOIN "CachedHighlightedMessages" CHM
                                                                                                     ON CHM."Id" = CMR."HighlightedMessageId"
                                                                                 WHERE "HighlightBoardGuildId" = {Context.Guild.Id}
                                                                                   AND ({board} = '' OR "HighlightBoardName" = {board})
                                                                                   AND ({postgresHappyChannelId} = 0 OR "OriginalMessageChannelId" = {postgresHappyChannelId})
                                                                                   AND ({postgresHappyAuthorId} = 0 OR "AuthorId" = {postgresHappyAuthorId})
                                                                             ),
                                                                                  NormalizedEmotes AS (
                                                                                      SELECT
                                                                                          CASE
                                                                                              WHEN EA."EmoteReplacement" IS NOT NULL THEN EA."EmoteReplacement"
                                                                                              WHEN UR."EmoteId" = 0 THEN UR."EmoteName"
                                                                                              WHEN UR."IsAnimated" THEN '<a:' || UR."EmoteName" || ':' || UR."EmoteId" || '>'
                                                                                              ELSE '<:' || UR."EmoteName" || ':' || UR."EmoteId" || '>'
                                                                                              END as "DisplayEmote",
                                                                                          UR."Count"
                                                                                      -- rider complains if i dont do it as an alias
                                                                                      FROM UniqueReactions UR
                                                                                               LEFT JOIN "EmoteAliases" EA
                                                                                                         ON EA."GuildId" = {Context.Guild.Id}
                                                                                                             AND EA."EmoteName" = UR."EmoteName"
                                                                                  )
                                                                             SELECT
                                                                                 "DisplayEmote",
                                                                                 SUM("Count") as "TotalReactions"
                                                                             FROM NormalizedEmotes
                                                                             GROUP BY "DisplayEmote"
                                                                             ORDER BY "TotalReactions" DESC
                                                                             LIMIT 10;
                                                                             """).ToArrayAsync();

        var topStr = results
            .Aggregate(
                new StringBuilder(),
                (x, y) => x.AppendLine($"{y.DisplayEmote} - {y.TotalReactions} total reactions")
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
