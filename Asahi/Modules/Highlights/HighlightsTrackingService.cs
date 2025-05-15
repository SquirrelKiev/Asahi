using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Asahi.Database;
using Asahi.Database.Models;
using Discord.Net;
using Discord.Rest;
using Discord.Webhook;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules.Highlights;

// so, the highlights system here used to immediately check a message on receiving a reaction and send it to highlights if
// it passed. I had a bunch of locks checks in place to make sure it wouldn't try process the same message twice, but it
// wasn't ideal. had issues around rate limits and was occasionally getting duplicated highlight messages (still no clue
// why, I thought I had lock stuff in place to prevent this). messages are now queued and checked every second. think this
// should be a lot better and give me a lot more control, and who knows, if this bot becomes public later (right now it's
// just for the Oshi no Ko guild), maybe the processing of the queue could be changed to spin up a new task per guild or
// per channel? maybe limit to like 5 tasks in case Discord gets mad? something to look into if the need arises.
//
// one day ill be bothered enough to write tests for this but mocking everything needed for this sounds like hell
[Inject(ServiceLifetime.Singleton)]
public class HighlightsTrackingService(
    IDbContextFactory<BotDbContext> dbService,
    ILogger<HighlightsTrackingService> logger,
    IDiscordClient client,
    BotConfig botConfig,
    DiscordRestConfig webhookRestConfig
)
{
    public record struct CachedMessage(ulong MessageId, ulong AuthorId, DateTimeOffset Timestamp)
    {
        public readonly bool Equals(CachedMessage? other)
        {
            return other.HasValue && other.Value.MessageId == MessageId;
        }
    }

    public record struct QueuedMessage(ulong GuildId, ulong ChannelId, ulong MessageId)
    {
        public readonly bool Equals(QueuedMessage? other)
        {
            return other.HasValue && other.Value.MessageId == MessageId;
        }
    };

    public record struct ForcedMessage(QueuedMessage QueuedMessage, string BoardName);

    private readonly ConcurrentDictionary<ulong, ConcurrentQueue<CachedMessage>> messageCaches = [];
    private readonly MemoryCache messageThresholds = new(new MemoryCacheOptions());

    private Task? timerTask;
    private readonly object lockMessageQueue = new();
    private readonly HashSet<QueuedMessage> messageQueue = [];
    private readonly HashSet<QueuedMessage> messageQueueShouldSendHighlight = [];
    private readonly HashSet<ForcedMessage> messageQueueForceToHighlights = [];

    public void QueueMessage(QueuedMessage messageToQueue, bool shouldSendHighlight)
    {
        lock (lockMessageQueue)
        {
            messageQueue.Add(messageToQueue);

            if (shouldSendHighlight)
                messageQueueShouldSendHighlight.Add(messageToQueue);
        }
    }

    public void ForceMessageToHighlights(QueuedMessage messageToQueue, string boardName)
    {
        lock (lockMessageQueue)
        {
            QueueMessage(messageToQueue, true);

            messageQueueForceToHighlights.Add(new ForcedMessage(messageToQueue, boardName));
        }
    }

    /// <summary>
    /// Starts the background task that checks queued messages to see if they should be highlighted.
    /// </summary>
    /// <param name="cancellationToken">Indicates that the task should stop checking messages.</param>
    public void StartBackgroundTask(CancellationToken cancellationToken)
    {
        timerTask ??= Task.Run(() => TimerTask(cancellationToken), cancellationToken);
    }

    /// <remarks>Should only be one of these running!</remarks>
    private async Task TimerTask(CancellationToken cancellationToken)
    {
        await MigrateOldMessages();

        logger.LogTrace("Highlights timer task started");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                await OnFinishWaitingForTick(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception in TimerTask! {message}", ex.Message);
            }
        }
    }

    private async Task MigrateOldMessages()
    {
        await using var context = await dbService.CreateDbContextAsync();

        var outdatedMessages = await context
            .CachedHighlightedMessages.Where(x => x.Version == 0)
            .Include(cachedHighlightedMessage => cachedHighlightedMessage.HighlightBoard)
            .ToListAsync();

        if (outdatedMessages.Count == 0)
            return;

        logger.LogInformation(
            "Out of date messages in highlights! Will be migrating {outdatedMessageCount} messages.",
            outdatedMessages.Count);

        CachedHighlightedMessage? currentMessage = null;
        try
        {
            var messagesToNuke = new List<CachedHighlightedMessage>();
            var stopwatch = Stopwatch.StartNew();
            var processedMessages = new List<TimeSpan>();

            int currentMessageIndex = 0;
            foreach (var outdatedMessage in outdatedMessages)
            {
                try
                {
                    var messageStopwatch = Stopwatch.StartNew();
                    currentMessageIndex++;

                    string eta = "Calculating...";
                    if (processedMessages.Count > 0)
                    {
                        var averageTime = TimeSpan.FromTicks((long)processedMessages.Average(t => t.Ticks));
                        var remainingMessages = outdatedMessages.Count - currentMessageIndex;
                        var estimatedTimeRemaining = TimeSpan.FromTicks(averageTime.Ticks * remainingMessages);
                        eta = estimatedTimeRemaining.TotalMinutes >= 1
                            ? $"{estimatedTimeRemaining.TotalMinutes:F1} minutes"
                            : $"{estimatedTimeRemaining.TotalSeconds:F0} seconds";
                    }

                    logger.LogInformation(
                        "migrating message {messageId} (channel {channelId}) ({currentMessageIndex}/{outdatedMessagesCount}) - ETA: {eta}",
                        outdatedMessage.OriginalMessageId,
                        outdatedMessage.OriginalMessageChannelId,
                        currentMessageIndex,
                        outdatedMessages.Count,
                        eta
                    );
                    currentMessage = outdatedMessage;

                    ITextChannel? channel;

                    try
                    {
                        channel = (ITextChannel)
                            await client.GetChannelAsync(outdatedMessage.OriginalMessageChannelId);
                    }
                    catch (HttpException ex)
                    {
                        if (ex.DiscordCode != DiscordErrorCode.MissingPermissions)
                            throw;

                        logger.LogWarning(
                            "Bot does not have access to channel {channelId}! Please take a look later, or this will be in migration hell.",
                            outdatedMessage.OriginalMessageChannelId
                        );
                        continue;
                    }

                    var originalMessage = await channel.GetMessageAsync(
                        outdatedMessage.OriginalMessageId
                    );

                    if (originalMessage == null)
                    {
                        logger.LogTrace(
                            "Could not find message {messageId}, will be removing entry {highlightId} from database.",
                            outdatedMessage.OriginalMessageId,
                            outdatedMessage.Id
                        );

                        messagesToNuke.Add(outdatedMessage);
                        continue;
                    }

                    var highlightMessages = new List<IMessage>();

                    ITextChannel? loggingChannel;
                    try
                    {
                        loggingChannel = (ITextChannel)
                            await client.GetChannelAsync(outdatedMessage.HighlightBoard.LoggingChannelId);
                    }
                    catch (HttpException ex)
                    {
                        if (ex.DiscordCode != DiscordErrorCode.MissingPermissions)
                            throw;

                        logger.LogWarning(
                            "Bot does not have access to channel {channelId}! Please take a look later, or this will be in migration hell.",
                            outdatedMessage.OriginalMessageChannelId
                        );
                        continue;
                    }

                    foreach (var highlightMessageId in outdatedMessage.HighlightMessageIds)
                    {
                        var highlightMessage = await loggingChannel.GetMessageAsync(highlightMessageId);

                        if (highlightMessage == null)
                        {
                            logger.LogTrace(
                                "Could not find message {messageId} (IN LOGGING CHANNEL {loggingChannelId}), will be ignoring",
                                outdatedMessage.OriginalMessageId,
                                loggingChannel.Id
                            );
                            continue;
                        }

                        highlightMessages.Add(highlightMessage);
                    }

                    if (highlightMessages.Count == 0)
                    {
                        logger.LogTrace(
                            "Could not find a single message in logging channel {loggingChannelId} for message {messageId}, will be nuking",
                            loggingChannel.Id,
                            outdatedMessage.OriginalMessageId
                        );

                        messagesToNuke.Add(outdatedMessage);
                        continue;
                    }

                    outdatedMessage.AuthorId = originalMessage.Author.Id;
                    outdatedMessage.HighlightedMessageSendDate = originalMessage.Timestamp.UtcDateTime;

                    var reactionsCache = new Dictionary<int, IUser[]>();
                    var (uniqueReactionUsersAutoReact, uniqueReactionEmotes, emoteUserMap) =
                        await GetReactions(
                            [originalMessage, highlightMessages[^1]],
                            [originalMessage],
                            reactionsCache
                        );

                    outdatedMessage.TotalUniqueReactions = uniqueReactionUsersAutoReact.Count;

                    outdatedMessage.UpdateReactions(emoteUserMap);

                    // assist messages
                    if (
                        originalMessage.Reference
                            is
                            {
                                ReferenceType:
                                { IsSpecified: true, Value: MessageReferenceType.Default },
                                MessageId.IsSpecified: true
                            }
                        && originalMessage.Channel.Id == channel.Id
                    )
                    {
                        var assistMsg = await channel.GetMessageAsync(
                            originalMessage.Reference.MessageId.Value
                        );

                        if (assistMsg != null)
                        {
                            outdatedMessage.AssistAuthorId = assistMsg.Author.Id;
                        }
                    }

                    messageStopwatch.Stop();
                    processedMessages.Add(messageStopwatch.Elapsed);
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Message {messageId} failed to process! Please take a look manually.",
                        outdatedMessage.OriginalMessageId);
                    continue;
                }

                outdatedMessage.Version = 1;
            }

            if (messagesToNuke.Count != 0)
            {
                logger.LogTrace(
                    "Beginning nuking of {count} dead highlights",
                    messagesToNuke.Count
                );

                foreach (var messageToNuke in messagesToNuke)
                {
                    currentMessage = messageToNuke;
                    logger.LogTrace("Nuking {messageId}", messageToNuke.OriginalMessageId);
                    outdatedMessages.Remove(messageToNuke);
                }
            }

            currentMessage = null;

            stopwatch.Stop();
            logger.LogInformation("Finished migrating old messages in {totalTime}, saving", stopwatch.Elapsed);
            await context.SaveChangesAsync();
            logger.LogInformation("Saved successfully!");
        }
        catch (Exception ex)
        {
            logger.LogCritical(
                ex,
                "Failed to migrate highlights to latest! Please take a look manually. (Failed on message {messageId})",
                currentMessage?.OriginalMessageId
            );
        }
    }

    private async Task OnFinishWaitingForTick(CancellationToken cancellationToken)
    {
        ForcedMessage[] forcedMessages;
        HashSet<QueuedMessage> messages;
        HashSet<ulong> messagesShouldSendHighlight;
        lock (lockMessageQueue)
        {
            if (messageQueue.Count == 0)
            {
                return;
            }

            messages = [.. messageQueue];
            messageQueue.Clear();

            messagesShouldSendHighlight = messageQueueShouldSendHighlight
                .Select(x => x.MessageId)
                .ToHashSet();
            messageQueueShouldSendHighlight.Clear();

            forcedMessages = [.. messageQueueForceToHighlights];
        }

        List<Task> guildTasks = [];

        foreach (var groupedMessages in messages.GroupBy(x => x.GuildId))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            guildTasks.Add(
                Task.Run(
                    async () =>
                    {
                        logger.LogDebug(
                            "Checking for Guild {guildId} has begun.",
                            groupedMessages.Key
                        );

                        foreach (var queuedMessage in groupedMessages)
                        {
                            try
                            {
                                var guild = await client.GetGuildAsync(queuedMessage.GuildId);
                                var textChannel = await guild.GetTextChannelAsync(
                                    queuedMessage.ChannelId
                                );

                                var shouldAddNewHighlight = messagesShouldSendHighlight.Contains(
                                    queuedMessage.MessageId
                                );

                                await CheckMessageForHighlights(
                                    queuedMessage.MessageId,
                                    textChannel,
                                    shouldAddNewHighlight,
                                    forcedMessages
                                        .Where(x => x.QueuedMessage == queuedMessage)
                                        .Select(x => x.BoardName)
                                        .ToArray()
                                );
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(
                                    ex,
                                    "Failed to check message {messageId} in channel {channel}!",
                                    queuedMessage.MessageId,
                                    queuedMessage.ChannelId
                                );
                            }
                        }

                        logger.LogDebug(
                            "Finished processing messages for Guild {guildId}.",
                            groupedMessages.Key
                        );
                    },
                    cancellationToken
                )
            );
        }

        await Task.WhenAll(guildTasks);
    }

    /// <remarks>Not thread-safe.</remarks>
    public void AddMessageToCache(IMessage msg)
    {
        const int maxSize = 500;

        // don't think we need to lock here?
        //lock (messageCaches)
        //{
        var cache = messageCaches.GetOrAdd(
            msg.Channel.Id,
            _ => new ConcurrentQueue<CachedMessage>()
        );

        cache.Enqueue(new CachedMessage(msg.Id, msg.Author.Id, msg.CreatedAt));
        while (cache.Count > maxSize)
        {
            cache.TryDequeue(out _);
        }
        //}
    }

    public struct ReactionInfo(IEmote emote)
    {
        public IEmote emote = emote;
    }

    // god I love linq
    private async Task CheckMessageForHighlights(
        ulong messageId,
        ITextChannel channel,
        bool shouldAddNewHighlight,
        string[] forcedBoards
    )
    {
        logger.LogTrace("Checking message");

        await using var context = await dbService.CreateDbContextAsync();

        var threadChannel = channel as IThreadChannel;
        var parentChannel = threadChannel is not null
            ? await client.GetChannelAsync(threadChannel.GetParentChannelId())
            : channel;

        // could probably be merged into one request?
        if (await context.HighlightBoards.AllAsync(x => x.GuildId != channel.Guild.Id))
            return;

        #region Handling new reactions to already highlighted messages

        var reactionsCache = new Dictionary<int, IUser[]>();
        var boardsWithMarkedHighlight = await context
            .HighlightBoards.Where(x =>
                x.GuildId == channel.Guild.Id
                && x.HighlightedMessages.Any(y =>
                    y.OriginalMessageId == messageId || y.HighlightMessageIds.Contains(messageId)
                )
            )
            .Include(highlightBoard => highlightBoard.LoggingChannelOverrides)
            .ToArrayAsync();

        if (boardsWithMarkedHighlight.Length != 0)
        {
            foreach (var board in boardsWithMarkedHighlight)
            {
                try
                {
                    var cachedHighlightedMessage = await context
                        .CachedHighlightedMessages.Include(x => x.CachedMessageReactions)
                        .FirstOrDefaultAsync(x =>
                            x.HighlightBoard.GuildId == board.GuildId
                            && x.HighlightBoard.Name == board.Name
                            && (
                                x.OriginalMessageId == messageId
                                || x.HighlightMessageIds.Contains(messageId)
                            )
                        );

                    if (cachedHighlightedMessage == null)
                        continue;

                    var originalMessage = await (
                        await channel.Guild.GetTextChannelAsync(
                            cachedHighlightedMessage.OriginalMessageChannelId
                        )
                    ).GetMessageAsync(cachedHighlightedMessage.OriginalMessageId);

                    if (originalMessage == null)
                        continue;

                    var loggingChannelId = board.LoggingChannelId;
                    var loggingChannelOverride = board.LoggingChannelOverrides.FirstOrDefault(x =>
                        x.OverriddenChannelId == originalMessage.Channel.Id
                    );

                    if (loggingChannelOverride != null)
                    {
                        loggingChannelId = loggingChannelOverride.LoggingChannelId;
                    }

                    var loggingChannel = await channel.Guild.GetTextChannelAsync(loggingChannelId);

                    List<IMessage> highlightMessages = [];
                    bool bail = false;
                    foreach (var highlightMessageId in cachedHighlightedMessage.HighlightMessageIds)
                    {
                        var message = await loggingChannel.GetMessageAsync(highlightMessageId);

                        if (message == null)
                        {
                            bail = true;
                            logger.LogWarning(
                                "Could not find highlight message {messageId} in channel {channelId} (guild {guild}, board {board})",
                                highlightMessageId,
                                channel.Id,
                                board.GuildId,
                                board.Name
                            );
                            break;
                        }

                        highlightMessages.Add(message);
                    }

                    if (bail)
                        continue;

                    var (uniqueReactionUsersAutoReact, uniqueReactionEmotes, emoteUserMap_) =
                        await GetReactions(
                            [originalMessage, highlightMessages[^1]],
                            [originalMessage],
                            reactionsCache
                        );

                    var reactions = uniqueReactionEmotes.Select(x => new ReactionInfo(x));

                    IMessage? reactorsMessage = null;
                    int reactorsEmbedIndex = -1;
                    foreach (var highlightMessage in highlightMessages)
                    {
                        int i = 0;
                        foreach (var x in highlightMessage.Embeds)
                        {
                            if (
                                !(
                                    x.Author.HasValue
                                    && x.Author.Value.Name.StartsWith(QuotingHelpers.ReplyingTo)
                                ) && x.Fields.Any(y => y.Name == QuotingHelpers.ReactionsFieldName)
                            )
                            {
                                reactorsEmbedIndex = i;
                                break;
                            }

                            i++;
                        }

                        if (reactorsEmbedIndex != -1)
                        {
                            reactorsMessage = highlightMessage;
                            break;
                        }
                    }

                    if (reactorsMessage == null)
                        return;

                    var webhook = await loggingChannel.GetOrCreateWebhookAsync(
                        BotService.WebhookDefaultName
                    );

                    using var webhookClient = new DiscordWebhookClient(
                        webhook.Id,
                        webhook.Token,
                        webhookRestConfig
                    );
                    webhookClient.Log += msg => BotService.Client_Log(logger, msg);

                    await webhookClient.ModifyMessageAsync(
                        reactorsMessage.Id,
                        messageProperties =>
                        {
                            var embeds = reactorsMessage
                                .Embeds.Select(x => x.ToEmbedBuilder())
                                .ToArray();

                            var eb = embeds[reactorsEmbedIndex];

                            AddReactionsFieldToQuote(
                                eb,
                                reactions,
                                uniqueReactionUsersAutoReact.Count
                            );

                            messageProperties.Embeds = embeds.Select(x => x.Build()).ToArray();
                        }
                    );

                    cachedHighlightedMessage.TotalUniqueReactions =
                        uniqueReactionUsersAutoReact.Count;
                    cachedHighlightedMessage.UpdateReactions(emoteUserMap_);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Failed to process message reaction update for board {board}.",
                        board.Name
                    );
                }
            }

            // we don't return after this, in case another board has highlights it needs to check
            await context.SaveChangesAsync();
        }

        #endregion

        if (!shouldAddNewHighlight)
            return;

        var everyoneChannelPermissions = PermissionsForRole(channel);

        bool hasPerms =
            parentChannel is IForumChannel
                ? everyoneChannelPermissions.SendMessagesInThreads
                : everyoneChannelPermissions.SendMessages;

        if (threadChannel is not null)
        {
            if (threadChannel.IsLocked)
                hasPerms = false;
        }

        if (!hasPerms && forcedBoards.Length == 0)
        {
            logger.LogDebug("Channel locked, skipping!");
            return;
        }

        if (
            await context.HighlightBoards.AnyAsync(x =>
                x.LoggingChannelId == parentChannel.Id
                || x.LoggingChannelOverrides.Any(y => y.LoggingChannelId == parentChannel.Id)
            )
        )
            return;

        var msg = await channel.GetMessageAsync(messageId);

        if (msg == null)
            return;

        if (botConfig.BannedHighlightsUsers.Contains(msg.Author.Id))
            return;

        var nonUniqueReactions = msg.Reactions.Sum(x => x.Value.ReactionCount);

        var messageAge = (DateTimeOffset.UtcNow - msg.Timestamp).TotalSeconds;

        var boardsQuery = context.HighlightBoards.Where(x =>
            x.GuildId == channel.Guild.Id
            && (
                (
                    (x.MaxMessageAgeSeconds == 0 || messageAge <= x.MaxMessageAgeSeconds)
                    && (
                        (
                            x.FilteredChannelsIsBlockList
                                ? x.FilteredChannels.All(y => y != parentChannel.Id)
                                : x.FilteredChannels.Any(y => y == parentChannel.Id)
                        )
                        || (
                            x.FilteredChannelsIsBlockList
                                ? x.FilteredChannels.All(y => y != channel.Id)
                                : x.FilteredChannels.Any(y => y == channel.Id)
                        )
                    )
                ) || forcedBoards.Contains(x.Name)
            )
            && !x.HighlightedMessages.Any(y =>
                y.OriginalMessageId == messageId || y.HighlightMessageIds.Contains(messageId)
            )
        );

        var boards = (
                await boardsQuery
                    .Include(x => x.Thresholds)
                    .Include(x => x.SpoilerChannels)
                    .Include(x => x.LoggingChannelOverrides)
                    .ToArrayAsync()
            )
            .Where(x =>
                msg.Author is not IGuildUser
                || msg.Author is IGuildUser guildUser
                && guildUser.RoleIds.All(y => y != x.HighlightsMuteRole)
            )
            .ToArray();

        logger.LogDebug(
            "Total non unique reactions is {nur}, found {bl} boards",
            nonUniqueReactions,
            boards.Length
        );

        if (boards.Length == 0)
            return;

        var aliases = await context
            .EmoteAliases.Where(x => x.GuildId == channel.Guild.Id)
            .ToArrayAsync();

        HashSet<HighlightBoard> completedBoards = [];
        var (uniqueReactionUsers, reactionEmotes, emoteUserMap) = await GetReactions(
            [msg],
            [msg],
            reactionsCache
        );

        foreach (
            var board in boards
                .Where(x => !completedBoards.Contains(x))
                .Where(board =>
                {
                    if (forcedBoards.Contains(board.Name))
                        return true;

                    if (!hasPerms)
                        return false;

                    var threshold = messageThresholds.GetOrCreate(
                        GetThresholdKey(board, msg.Id),
                        entry =>
                        {
                            var timespan = TimeSpan.FromSeconds(
                                Math.Min(board.MaxMessageAgeSeconds, 43200)
                            ); // 43200 seconds = 12 hours

                            entry.AbsoluteExpirationRelativeToNow = timespan;

                            var threshold = board.Thresholds.FirstOrDefault(x =>
                                x.OverrideId == channel.Id
                            );
                            threshold ??= board.Thresholds.FirstOrDefault(x =>
                                x.OverrideId == parentChannel.Id
                            );
                            threshold ??= board.Thresholds.FirstOrDefault(x =>
                                x.OverrideId == channel.CategoryId
                            );
                            threshold ??= board.Thresholds.FirstOrDefault(x =>
                                x.OverrideId == channel.Guild.Id
                            );

                            if (threshold == null)
                            {
                                logger.LogError(
                                    "Could not find a threshold for {board} in {guild}! This is very bad! Defaulting to 3.",
                                    board.Name,
                                    board.GuildId
                                );

                                return 3;
                            }

                            var messages = GetCachedMessages(channel.Id);

                            var requiredReactions = CalculateThreshold(
                                threshold,
                                messages,
                                msg.CreatedAt,
                                out _
                            );

                            logger.LogDebug("threshold is {threshold}", requiredReactions);

                            return requiredReactions;
                        }
                    );

                    return threshold
                           <= uniqueReactionUsers.Count(y =>
                           {
                               logger.LogTrace(
                                   "user {user}: filter self react: {fsr}, is self react: {isr}",
                                   y,
                                   board.FilterSelfReactions,
                                   y == msg.Author.Id
                               );

                               return board.FilterSelfReactions == false || y != msg.Author.Id;
                           });
                })
        )
        {
            completedBoards.Add(board);

            await SendAndTrackHighlightMessage(
                board,
                aliases,
                msg,
                reactionEmotes.Select(x => new ReactionInfo(x)).ToArray(),
                uniqueReactionUsers.Count,
                emoteUserMap
            );
        }

        await context.SaveChangesAsync();
    }

    public int GetCachedThreshold(HighlightBoard board, ulong msgId)
    {
        if (messageThresholds.TryGetValue(GetThresholdKey(board, msgId), out int cachedThreshold))
            return cachedThreshold;
        else
            return -1;
    }

    private static string GetThresholdKey(HighlightBoard board, ulong msgId)
    {
        return $"{board.GuildId}-{board.Name}-{msgId}";
    }

    public async Task<(
        HashSet<ulong> uniqueReactionUsersAutoReact,
        HashSet<IEmote> uniqueReactionEmotes,
        Dictionary<IEmote, HashSet<ulong>> emoteUserMap
        )> GetReactions(
        IEnumerable<IMessage> messagesToCheckReactions,
        IEnumerable<IMessage> messagesToAddReactionEmotes,
        Dictionary<int, IUser[]> reactionCache
    )
    {
        HashSet<ulong> uniqueReactionUsersAutoReact = [];
        HashSet<IEmote> uniqueReactionEmotes = [];
        Dictionary<IEmote, HashSet<ulong>> emoteUserMap = [];

        var reactionEmotes = messagesToAddReactionEmotes
            .SelectMany(x => x.Reactions.Keys)
            .Distinct()
            .ToHashSet();
        foreach (var message in messagesToCheckReactions)
        {
            foreach (var reaction in message.Reactions)
            {
                if (!reactionEmotes.Contains(reaction.Key))
                    continue;

                var cacheKey = GetKey(message, reaction.Key);
                if (!reactionCache.TryGetValue(cacheKey, out var users))
                {
                    users = await message
                        .GetReactionUsersAsync(reaction.Key, int.MaxValue)
                        .Flatten()
                        .ToArrayAsync();
                    reactionCache.Add(cacheKey, users);
                }

                foreach (var user in users)
                {
                    if (user.IsBot)
                        continue;

                    uniqueReactionEmotes.Add(reaction.Key);
                    uniqueReactionUsersAutoReact.Add(user.Id);

                    if (!emoteUserMap.TryGetValue(reaction.Key, out var userSet))
                    {
                        userSet = new HashSet<ulong>();
                        emoteUserMap.Add(reaction.Key, userSet);
                    }

                    userSet.Add(user.Id);
                }
            }
        }

        return (uniqueReactionUsersAutoReact, uniqueReactionEmotes, emoteUserMap);

        static int GetKey(IMessage message, IEmote reactionEmote)
        {
            return $"{message.Id}{reactionEmote}".GetHashCode(StringComparison.Ordinal);
        }
    }

    public IReadOnlyCollection<CachedMessage> GetCachedMessages(ulong channelId)
    {
        messageCaches.TryGetValue(channelId, out var queue);
        IReadOnlyCollection<CachedMessage> messages = queue ?? [];
        return messages;
    }

    public void AddReactionsFieldToQuote(
        EmbedBuilder embedBuilder,
        IEnumerable<ReactionInfo> reactions,
        int totalUniqueReactions
    )
    {
        var reactionsField = embedBuilder.Fields.FirstOrDefault(x =>
            x.Name == QuotingHelpers.ReactionsFieldName
        );
        if (reactionsField == null)
        {
            reactionsField = new EmbedFieldBuilder()
                .WithName(QuotingHelpers.ReactionsFieldName)
                .WithIsInline(true);
            embedBuilder.AddField(reactionsField);
        }

        var sb = new StringBuilder($"**{totalUniqueReactions}** ");
        var addedReaction = false;
        foreach (var reaction in reactions)
        {
            addedReaction = true;
            sb.Append(reaction.emote).Append(' ');
        }

        if (!addedReaction)
        {
            sb.Append("None?");
        }

        reactionsField.WithValue(sb.ToString());
    }

    public async Task SendAndTrackHighlightMessage(
        HighlightBoard board,
        EmoteAlias[] aliases,
        IMessage message,
        ICollection<ReactionInfo> reactions,
        int totalUniqueReactions,
        Dictionary<IEmote, HashSet<ulong>> emoteUserMap
    )
    {
        var loggingChannelId = board.LoggingChannelId;

        var loggingChannelOverride = board.LoggingChannelOverrides.FirstOrDefault(x =>
            x.OverriddenChannelId == message.Channel.Id
        );

        if (loggingChannelOverride != null)
        {
            loggingChannelId = loggingChannelOverride.LoggingChannelId;
        }

        logger.LogDebug("Sending highlight message to {channel}", loggingChannelId);

        var loggingChannel = await (await client.GetGuildAsync(board.GuildId)).GetTextChannelAsync(
            loggingChannelId
        );
        var embedAuthor = (IGuildUser)message.Author;
        var embedColor = await QuotingHelpers.GetQuoteEmbedColor(
            board.EmbedColorSource,
            board.FallbackEmbedColor,
            embedAuthor,
            client
        );

        logger.LogTrace("Embed color will be {color}", embedColor);

        var spoilerEntry = board.SpoilerChannels.FirstOrDefault(x =>
            x.ChannelId == message.Channel.Id
        );

        IMessage? replyMessage = null;
        if (message.Reference != null &&
            message.Reference.ReferenceType.GetValueOrDefault() != MessageReferenceType.Forward &&
            message.Reference.ChannelId == message.Channel.Id && message.Reference.MessageId.IsSpecified)
        {
            replyMessage = await message.Channel.GetMessageAsync(
                message.Reference.MessageId.Value
            );
        }

        var queuedMessages = QuotingHelpers.QuoteMessage(
            message,
            embedColor,
            logger,
            false,
            [],
            spoilerEntry != null,
            spoilerEntry?.SpoilerContext ?? "",
            replyMessage,
            eb => { AddReactionsFieldToQuote(eb, reactions, totalUniqueReactions); }
        );

        var webhook = await loggingChannel.GetOrCreateWebhookAsync(BotService.WebhookDefaultName);
        using var webhookClient = new DiscordWebhookClient(
            webhook.Id,
            webhook.Token,
            webhookRestConfig
        );
        webhookClient.Log += msg => BotService.Client_Log(logger, msg);
        List<ulong> highlightMessages = [];

        var username = embedAuthor is IWebhookUser webhookUser
            ? webhookUser.Username
            : embedAuthor.DisplayName;
        var avatar = embedAuthor.GetDisplayAvatarUrl();
        foreach (var queuedMessage in queuedMessages)
        {
            highlightMessages.Add(
                await webhookClient.SendMessageAsync(
                    queuedMessage.body.Truncate(2000),
                    embeds: queuedMessage.embeds,
                    components: queuedMessage.components,
                    username: username,
                    avatarUrl: avatar,
                    allowedMentions: AllowedMentions.None
                )
            );
        }

        try
        {
            if (board.AutoReactMaxAttempts != 0)
            {
                var lastMessageObj = await loggingChannel.GetMessageAsync(highlightMessages[^1]);
                await AutoReact(board, aliases, message.Reactions, lastMessageObj);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to auto-react in channel {channel}!", loggingChannel.Id);
        }

        var highlightedMessage = new CachedHighlightedMessage()
        {
            HighlightMessageIds = highlightMessages,
            OriginalMessageChannelId = message.Channel.Id,
            OriginalMessageId = message.Id,
            HighlightedMessageSendDate = message.Timestamp.UtcDateTime,
            AssistAuthorId = replyMessage?.Author.Id,
            AuthorId = message.Author.Id,
            TotalUniqueReactions = totalUniqueReactions,
        };

        highlightedMessage.UpdateReactions(emoteUserMap);

        board.HighlightedMessages.Add(highlightedMessage);

        logger.LogDebug(
            "Sent and tracked highlight {messageCount} messages",
            highlightMessages.Count
        );
    }

    public async Task AutoReact(
        HighlightBoard board,
        EmoteAlias[] aliases,
        IReadOnlyDictionary<IEmote, ReactionMetadata> reactions,
        IMessage lastMessage
    )
    {
        IEmote? fallbackEmote = null;

        if (string.IsNullOrWhiteSpace(board.AutoReactFallbackEmoji))
        {
        }
        else if (Emoji.TryParse(board.AutoReactFallbackEmoji, out var fallbackEmoji))
        {
            fallbackEmote = fallbackEmoji;
        }
        else if (Emote.TryParse(board.AutoReactFallbackEmoji, out var tempFallbackEmote))
        {
            fallbackEmote = tempFallbackEmote;
        }

        IEnumerable<IEmote> reactEmotes = board.AutoReactEmoteChoicePreference switch
        {
            AutoReactEmoteChoicePreference.ReactionsDescendingPopularity => reactions
                .OrderByDescending(x => x.Value.ReactionCount)
                .Select(x =>
                {
                    var emoteAlias = aliases.FirstOrDefault(y =>
                        x.Key.Name.Equals(y.EmoteName, StringComparison.InvariantCultureIgnoreCase)
                    );

                    if (emoteAlias == null)
                        return x.Key;

                    return EmoteTypeConverter.TryParse(emoteAlias.EmoteReplacement, out var emote)
                        ? emote
                        : x.Key;
                })
                .Distinct(),
            _ => [],
        };

        int totalAttempts = 0;
        int successfulAttempts = 0;
        foreach (var reaction in reactEmotes)
        {
            if (
                successfulAttempts >= board.AutoReactMaxReactions
                || totalAttempts >= board.AutoReactMaxAttempts
            )
                break;

            try
            {
                await lastMessage.AddReactionAsync(reaction);
                successfulAttempts++;
                logger.LogTrace("Successfully added reaction {reaction}.", reaction);
            }
            catch (Exception ex)
            {
                logger.LogTrace(ex, "Failed to add emoji {reaction}, trying next emoji.", reaction);
            }

            totalAttempts++;
        }

        if (fallbackEmote != null && successfulAttempts == 0 && board.AutoReactMaxReactions > 0)
        {
            try
            {
                await lastMessage.AddReactionAsync(fallbackEmote);
                logger.LogTrace("Successfully added fallback reaction.");
            }
            catch (Exception ex)
            {
                logger.LogTrace(ex, "Failed to add fallback reaction.");
            }
        }
    }

    public struct ThresholdInfo
    {
        public required double CurrentThreshold { get; set; }
        public required double RawThreshold { get; set; }
        public required double WeightedUserCount { get; set; }
        public required int UnweightedUserCount { get; set; }
        public required bool IsHighActivity { get; set; }
        public required int TotalCachedMessages { get; set; }
        public required int CachedMessagesBeingConsidered { get; set; }

        public override readonly string ToString()
        {
            return $"**Important info:**\n"
                   + $"Current threshold: `{CurrentThreshold}`\n"
                   + $"Weighted users: `{WeightedUserCount}`\n"
                   + $"Activity level: {(IsHighActivity ? "`High`" : "`Normal`")}\n\n"
                   + $"**Extra debug info:**\n"
                   + $"Raw threshold: `{RawThreshold}`\n"
                   + $"Unweighted users: `{UnweightedUserCount}`\n"
                   + $"Total messages cached: `{TotalCachedMessages}`\n"
                   + $"Cached messages being considered in user count: `{CachedMessagesBeingConsidered}`";
        }
    }

    public static int CalculateThreshold(
        HighlightThreshold thresholdConfig,
        IReadOnlyCollection<CachedMessage> messages,
        DateTimeOffset messageSentAt,
        out ThresholdInfo debugInfo
    )
    {
        Dictionary<ulong, double> userWeights = [];

        var orderedMessages = messages.OrderByDescending(x => x.Timestamp).ToArray();

        var userWeightMessages = orderedMessages
            .Where(x =>
                x.Timestamp <= messageSentAt
                && x.Timestamp
                >= messageSentAt
                - TimeSpan.FromSeconds(thresholdConfig.UniqueUserMessageMaxAgeSeconds)
            )
            .ToArray();

        foreach (var message in userWeightMessages)
        {
            var userId = message.AuthorId;
            if (userWeights.ContainsKey(userId))
                continue;

            var timeSinceLastMessage = messageSentAt - message.Timestamp;
            double weight = 1f;

            if (!(timeSinceLastMessage.TotalSeconds <= thresholdConfig.UniqueUserDecayDelaySeconds))
            {
                weight =
                    1
                    - (
                        timeSinceLastMessage.TotalSeconds
                        - thresholdConfig.UniqueUserDecayDelaySeconds
                    )
                    / (
                        thresholdConfig.UniqueUserMessageMaxAgeSeconds
                        - thresholdConfig.UniqueUserDecayDelaySeconds
                    );
            }

            userWeights.TryAdd(userId, weight);
        }

        var highActivity =
            orderedMessages.Length >= thresholdConfig.HighActivityMessageLookBack
            && (
                messageSentAt
                - orderedMessages[thresholdConfig.HighActivityMessageLookBack - 1].Timestamp
            ).TotalSeconds < thresholdConfig.HighActivityMessageMaxAgeSeconds;

        var weightedUserCount = userWeights.Sum(kvp => kvp.Value);

        var highActivityMultiplier = highActivity ? thresholdConfig.HighActivityMultiplier : 1f;

        var rawThreshold =
        (
            thresholdConfig.BaseThreshold
            + weightedUserCount * thresholdConfig.UniqueUserMultiplier
        ) * highActivityMultiplier;

        var thresholdDecimal = rawThreshold % 1;
        var roundedThreshold = Math.Min(
            thresholdConfig.MaxThreshold,
            thresholdDecimal < thresholdConfig.RoundingThreshold
                ? Math.Floor(rawThreshold)
                : Math.Ceiling(rawThreshold)
        );

        debugInfo = new ThresholdInfo()
        {
            CurrentThreshold = roundedThreshold,
            RawThreshold = rawThreshold,
            WeightedUserCount = weightedUserCount,
            UnweightedUserCount = userWeights.Count,
            IsHighActivity = highActivity,
            TotalCachedMessages = orderedMessages.Length,
            CachedMessagesBeingConsidered = userWeightMessages.Length,
        };

        return (int)roundedThreshold;
    }

    public static ChannelPermissions PermissionsForRole(IGuildChannel channel)
    {
        var role = channel.Guild.EveryoneRole;

        //Start with everyone's guild permissions
        ulong resolvedPermissions = role.Permissions.RawValue;

        //Give/Take Everyone permissions
        var perms = channel.GetPermissionOverwrite(role);
        if (perms != null)
            resolvedPermissions =
                (resolvedPermissions & ~perms.Value.DenyValue) | perms.Value.AllowValue;

        return new ChannelPermissions(resolvedPermissions);
    }
}

public static class IThreadChannelExtensions
{
    public static ulong GetParentChannelId(this IThreadChannel channel)
    {
        if (channel is SocketThreadChannel socketChannel)
        {
            return socketChannel.ParentChannel.Id;
        }

        if (channel is RestThreadChannel restChannel)
        {
            return restChannel.ParentChannelId;
        }

        return 0ul;
    }
}
