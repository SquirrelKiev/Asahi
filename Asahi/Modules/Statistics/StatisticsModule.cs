using System.Text;
using Asahi.Database;
using Asahi.Modules.Highlights;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;

namespace Asahi.Modules.Statistics;

// TODO: Something about sqlite. just disable for this?
[Group("stats", "Commands relating to statistics")]
[CommandContextType(InteractionContextType.Guild)]
[IntegrationType(ApplicationIntegrationType.GuildInstall)]
public class StatisticsModule(IDbContextFactory<BotDbContext> dbService) : BotModule
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
        string emoteName = "", // TODO: Parse this in case its emote markup
        [Summary(description: "Optional channel to filter results by.")]
        IChannel? channel = null,
        [Summary(description: "Optional board to filter results by.")]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string? board = "",
        [Summary(description: "Optional user to filter results by. Useful for checking your own stats.")]
        IGuildUser? user = null
    )
    {
        await DeferAsync();

        await using var context = await dbService.CreateDbContextAsync();

        // if (context is not PostgresContext)
        // {
        //     await FollowupAsync(
        //         $"This is not supported on this instance. (Database is `{context.GetType().Name}`, should be `{nameof(PostgresContext)}`.)");
        //     return;
        // }

        var postgresHappyChannelId = channel?.Id ?? 0;
        var postgresHappyUserId = user?.Id ?? 0;

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
                                                AND ({postgresHappyUserId} = 0 OR "AuthorId" = {postgresHappyUserId})
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
                (x, y) => x.AppendLine($"<@{y.Id}> - {y.TotalReactions} total {emoteName}")
            )
            .ToString();


        if (string.IsNullOrEmpty(topStr))
            topStr = "No results.";

        var eb = new EmbedBuilder()
            .WithColor(QuotingHelpers.GetUserRoleColorWithFallback(await Context.Guild.GetCurrentUserAsync(),
                Color.Green))
            .WithDescription(topStr);

        var fields = new List<EmbedFieldBuilder>();
        if (emoteName != "")
            fields.Add(new EmbedFieldBuilder().WithName("Emote").WithValue(emoteName).WithIsInline(true));
        if (channel != null)
            fields.Add(new EmbedFieldBuilder().WithName("Channel").WithValue($"<#{channel.Id}>").WithIsInline(true));
        if (board != "")
        {
            var boardChannelId = await context.HighlightBoards
                .Where(x => x.GuildId == Context.Guild.Id && x.Name == board)
                .Select(x => x.LoggingChannelId).FirstOrDefaultAsync();

            fields.Add(new EmbedFieldBuilder().WithName("Board").WithValue($"`{board}` (<#{boardChannelId}>)")
                .WithIsInline(true));
        }

        if (user != null)
        {
            eb.WithAuthor($"Highlights by {user.DisplayName}", user.GetAvatarUrl());
        }
        else
        {
            eb.WithAuthor(Context.Guild.Name, Context.Guild.IconUrl);
        }

        eb.WithFields(fields);

        await FollowupAsync(new MessageContents(eb));
    }

    [SlashCommand(
        "channel-most-reacts",
        "Lists the channels with the most summed reacted messages."
    )]
    public async Task ChannelsWithMostReactionsSlash(
        [Summary(description: "Optional board to filter results by.")]
        [Autocomplete(typeof(HighlightsNameAutocomplete))]
        string board = "",
        [Summary(description: "Optional user to filter results by.")]
        IGuildUser? user = null
    )
    {
        await DeferAsync();

        await using var context = await dbService.CreateDbContextAsync();

        // if (context is not PostgresContext)
        // {
        //     await FollowupAsync(
        //         $"This is not supported on this instance. (Database is `{context.GetType().Name}`, should be `{nameof(PostgresContext)}`.)");
        //     return;
        // }

        var postgresHappyUserId = user?.Id ?? 0;

        var results = await context.Database
            .SqlQuery<IdToReactionCount>($"""
                                          WITH UniqueMessages AS (
                                              SELECT DISTINCT ON ("OriginalMessageId")
                                                  "OriginalMessageChannelId",
                                                  "TotalUniqueReactions"
                                              FROM "CachedHighlightedMessages"
                                              WHERE "HighlightBoardGuildId" = {Context.Guild.Id}
                                              AND ({board} = '' OR "HighlightBoardName" = {board})
                                              AND ({postgresHappyUserId} = 0 OR "AuthorId" = {postgresHappyUserId})
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
            .WithColor(
                QuotingHelpers.GetUserRoleColorWithFallback(
                    await Context.Guild.GetCurrentUserAsync(),
                    Color.Green
                )
            )
            .WithDescription(topStr);

        var fields = new List<EmbedFieldBuilder>();
        if (board != "")
        {
            var boardChannelId = await context.HighlightBoards
                .Where(x => x.GuildId == Context.Guild.Id && x.Name == board)
                .Select(x => x.LoggingChannelId).FirstOrDefaultAsync();

            fields.Add(new EmbedFieldBuilder().WithName("Board").WithValue($"`{board}` (<#{boardChannelId}>)")
                .WithIsInline(true));
        }

        if (user != null)
        {
            eb.WithAuthor($"Highlights by {user.DisplayName}", user.GetAvatarUrl());
        }
        else
        {
            eb.WithAuthor(Context.Guild.Name, Context.Guild.IconUrl);
        }

        eb.WithFields(fields);

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
        IGuildUser? user = null
    )
    {
        await DeferAsync();

        await using var context = await dbService.CreateDbContextAsync();

        // if (context is not PostgresContext)
        // {
        //     await FollowupAsync(
        //         $"This is not supported on this instance. (Database is `{context.GetType().Name}`, should be `{nameof(PostgresContext)}`.)");
        //     return;
        // }

        var postgresHappyChannelId = channel?.Id ?? 0;
        var postgresHappyAuthorId = user?.Id ?? 0;

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
            .WithColor(
                QuotingHelpers.GetUserRoleColorWithFallback(
                    await Context.Guild.GetCurrentUserAsync(),
                    Color.Green
                )
            )
            .WithDescription(topStr);

        var fields = new List<EmbedFieldBuilder>();
        if (emoteName != "")
            fields.Add(new EmbedFieldBuilder().WithName("Emote").WithValue(emoteName).WithIsInline(true));
        if (channel != null)
            fields.Add(new EmbedFieldBuilder().WithName("Channel").WithValue($"<#{channel.Id}>").WithIsInline(true));
        if (board != "")
        {
            var boardChannelId = await context.HighlightBoards
                .Where(x => x.GuildId == Context.Guild.Id && x.Name == board)
                .Select(x => x.LoggingChannelId).FirstOrDefaultAsync();

            fields.Add(new EmbedFieldBuilder().WithName("Board").WithValue($"`{board}` (<#{boardChannelId}>)")
                .WithIsInline(true));
        }

        if (user != null)
        {
            eb.WithAuthor($"Highlights by {user.DisplayName}", user.GetAvatarUrl());
        }
        else
        {
            eb.WithAuthor(Context.Guild.Name, Context.Guild.IconUrl);
        }

        eb.WithFields(fields);

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
        IGuildUser? user = null,
        [Summary(description: "Optional emote to filter results by")]
        string emoteName = ""
    )
    {
        await DeferAsync();

        await using var context = await dbService.CreateDbContextAsync();

        // if (context is not PostgresContext)
        // {
        //     await FollowupAsync(
        //         $"This is not supported on this instance. (Database is `{context.GetType().Name}`, should be `{nameof(PostgresContext)}`.)");
        //     return;
        // }

        var postgresHappyChannelId = channel?.Id ?? 0;
        var postgresHappyAuthorId = user?.Id ?? 0;

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
                                                                                                        AND ({emoteName} = '' OR CMR."EmoteName" = {emoteName})
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
                                                                                      -- rider complains if I don't do it as an alias
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
            .WithColor(
                QuotingHelpers.GetUserRoleColorWithFallback(
                    await Context.Guild.GetCurrentUserAsync(),
                    Color.Green
                )
            )
            .WithDescription(topStr);

        var fields = new List<EmbedFieldBuilder>();
        if (emoteName != "")
            fields.Add(new EmbedFieldBuilder().WithName("Emote").WithValue(emoteName).WithIsInline(true));
        if (channel != null)
            fields.Add(new EmbedFieldBuilder().WithName("Channel").WithValue($"<#{channel.Id}>").WithIsInline(true));
        if (board != "")
        {
            var boardChannelId = await context.HighlightBoards
                .Where(x => x.GuildId == Context.Guild.Id && x.Name == board)
                .Select(x => x.LoggingChannelId).FirstOrDefaultAsync();

            fields.Add(new EmbedFieldBuilder().WithName("Board").WithValue($"`{board}` (<#{boardChannelId}>)")
                .WithIsInline(true));
        }

        if (user != null)
        {
            eb.WithAuthor($"Highlights by {user.DisplayName}", user.GetAvatarUrl());
        }
        else
        {
            eb.WithAuthor(Context.Guild.Name, Context.Guild.IconUrl);
        }

        eb.WithFields(fields);

        await FollowupAsync(new MessageContents(eb));
    }
}
