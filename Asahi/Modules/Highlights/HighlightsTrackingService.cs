using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;
using Asahi.Database;
using Asahi.Database.Models;
using Discord.Webhook;
using Microsoft.Extensions.Caching.Memory;

namespace Asahi.Modules.Highlights;

// so, the highlights system here used to immediately check a message on receiving a reaction and send it to highlights if
// it passed. I had a bunch of locks checks in place to make sure it wouldn't try process the same message twice, but it
// wasn't ideal. had issues around rate limits and was occasionally getting duplicated highlight messages (still no clue
// why, I thought I had lock stuff in place to prevent this). messages are now queued and checked every second. think this
// should be a lot better and give me a lot more control, and who knows, if this bot becomes public later (right now it's
// just for the Oshi no Ko guild), maybe the processing of the queue could be changed to spin up a new task per guild or
// per channel? maybe limit to like 5 tasks in case Discord gets mad? something to look into if the need arises.
[Inject(ServiceLifetime.Singleton)]
public class HighlightsTrackingService(DbService dbService, ILogger<HighlightsTrackingService> logger, DiscordSocketClient client, BotConfig botConfig)
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

            messagesShouldSendHighlight = messageQueueShouldSendHighlight.Select(x => x.MessageId).ToHashSet();
            messageQueueShouldSendHighlight.Clear();

            forcedMessages = [.. messageQueueForceToHighlights];
        }

        List<Task> guildTasks = [];

        foreach (var groupedMessages in messages.GroupBy(x => x.GuildId))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            guildTasks.Add(Task.Run(async () =>
            {
                logger.LogTrace("Checking for Guild {guildId} has begun.", groupedMessages.Key);

                foreach (var queuedMessage in groupedMessages)
                {
                    try
                    {
                        var guild = client.GetGuild(queuedMessage.GuildId);
                        var textChannel = guild.GetTextChannel(queuedMessage.ChannelId);

                        var shouldAddNewHighlight = messagesShouldSendHighlight.Contains(queuedMessage.MessageId);

                        await CheckMessageForHighlights(queuedMessage.MessageId, textChannel, shouldAddNewHighlight,
                            forcedMessages.Where(x => x.QueuedMessage == queuedMessage).Select(x => x.BoardName).ToArray());
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to check message {messageId} in channel {channel}!",
                            queuedMessage.MessageId, queuedMessage.ChannelId);
                    }
                }

                logger.LogTrace("Finished processing messages for Guild {guildId}.", groupedMessages.Key);
            }, cancellationToken));
        }

        await Task.WhenAll(guildTasks);
    }

    /// <remarks>Not thread-safe.</remarks>
    public void AddMessageToCache(SocketMessage msg)
    {
        const int maxSize = 500;

        // don't think we need to lock here?
        //lock (messageCaches)
        //{
        var cache = messageCaches.GetOrAdd(msg.Channel.Id, _ => new ConcurrentQueue<CachedMessage>());

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
    private async Task CheckMessageForHighlights(ulong messageId, SocketTextChannel channel, bool shouldAddNewHighlight, string[] forcedBoards)
    {
        logger.LogTrace("Checking message");

        await using var context = dbService.GetDbContext();

        var threadChannel = channel as SocketThreadChannel;
        var parentChannel = threadChannel is not null ? threadChannel.ParentChannel : channel;

        // could probably be merged into one request?
        if (await context.HighlightBoards.AllAsync(x => x.GuildId != channel.Guild.Id))
            return;

        #region Handling new reactions to already highlighted messages

        var boardsWithMarkedHighlight = await context.HighlightBoards
            .Where(x =>
                x.GuildId == channel.Guild.Id && x.HighlightedMessages
                    .Any(y => y.OriginalMessageId == messageId || y.HighlightMessageIds.Contains(messageId)))
            .Include(highlightBoard => highlightBoard.LoggingChannelOverrides)
            .ToArrayAsync();

        if (boardsWithMarkedHighlight.Length != 0)
        {
            foreach (var board in boardsWithMarkedHighlight)
            {
                var cachedHighlightedMessage = await context.CachedHighlightedMessages
                    .FirstOrDefaultAsync(x =>
                        x.OriginalMessageId == messageId || x.HighlightMessageIds.Contains(messageId));

                if (cachedHighlightedMessage == null)
                    continue;

                var originalMessage = await channel.Guild
                    .GetTextChannel(cachedHighlightedMessage.OriginalMessageChannelId)
                    .GetMessageAsync(cachedHighlightedMessage.OriginalMessageId);

                if (originalMessage == null)
                    continue;

                var loggingChannelId = board.LoggingChannelId;
                var loggingChannelOverride =
                    board.LoggingChannelOverrides.FirstOrDefault(x => x.OverriddenChannelId == originalMessage.Channel.Id);

                if (loggingChannelOverride != null)
                {
                    loggingChannelId = loggingChannelOverride.LoggingChannelId;
                }

                var loggingChannel = channel.Guild.GetTextChannel(loggingChannelId);

                List<IMessage> highlightMessages = [];
                foreach (var highlightMessageId in cachedHighlightedMessage.HighlightMessageIds)
                {
                    highlightMessages.Add(await loggingChannel.GetMessageAsync(highlightMessageId));
                }

                var (uniqueReactionUsersAutoReact, uniqueReactionEmotes) =
                    await GetReactions([originalMessage, highlightMessages[^1]], [originalMessage]);

                var reactions = uniqueReactionEmotes.Select(x => new ReactionInfo(x));

                IMessage? reactorsMessage = null;
                int reactorsEmbedIndex = -1;
                foreach (var highlightMessage in highlightMessages)
                {
                    int i = 0;
                    foreach (var x in highlightMessage.Embeds)
                    {
                        if (!(x.Author.HasValue && x.Author.Value.Name.StartsWith(QuotingHelpers.ReplyingTo)) && x.Fields.Any(y => y.Name == QuotingHelpers.ReactionsFieldName))
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

                var webhook = await loggingChannel.GetOrCreateWebhookAsync(BotService.WebhookDefaultName);
                var webhookClient = new DiscordWebhookClient(webhook);

                await webhookClient.ModifyMessageAsync(reactorsMessage.Id, messageProperties =>
                {
                    var embeds = reactorsMessage.Embeds.Select(x => x.ToEmbedBuilder()).ToArray();

                    var eb = embeds[reactorsEmbedIndex];

                    AddReactionsFieldToQuote(eb, reactions, uniqueReactionUsersAutoReact.Count);

                    messageProperties.Embeds = embeds.Select(x => x.Build()).ToArray();
                });
            }

            // we dont return after this, in case another board has highlights it needs to check
        }

        #endregion

        if (!shouldAddNewHighlight)
            return;

        var everyoneChannelPermissions = PermissionsForRole(channel);

        bool hasPerms = parentChannel is IForumChannel ? everyoneChannelPermissions.SendMessagesInThreads : everyoneChannelPermissions.SendMessages;

        if (threadChannel is not null)
        {
            if (threadChannel.IsLocked)
                hasPerms = false;
        }

        if (!hasPerms && forcedBoards.Length == 0)
        {
            logger.LogTrace("Channel locked, skipping!");
            return;
        }

        if (await context.HighlightBoards.AnyAsync
                (x => x.LoggingChannelId == parentChannel.Id || x.LoggingChannelOverrides.Any(y => y.LoggingChannelId == parentChannel.Id)))
            return;

        var msg = await channel.GetMessageAsync(messageId);

        if (botConfig.BannedHighlightsUsers.Contains(msg.Author.Id))
            return;

        var nonUniqueReactions = msg.Reactions.Sum(x => x.Value.ReactionCount);

        var messageAge = (DateTimeOffset.UtcNow - msg.Timestamp).TotalSeconds;

        var boardsQuery = context.HighlightBoards.Where(x =>
            x.GuildId == channel.Guild.Id
            && (((x.MaxMessageAgeSeconds == 0 || messageAge <= x.MaxMessageAgeSeconds)
                 && ((x.FilteredChannelsIsBlockList
                         ? x.FilteredChannels.All(y => y != parentChannel.Id)
                         : x.FilteredChannels.Any(y => y == parentChannel.Id))
                     ||
                     (x.FilteredChannelsIsBlockList
                         ? x.FilteredChannels.All(y => y != channel.Id)
                         : x.FilteredChannels.Any(y => y == channel.Id))
                 )) || forcedBoards.Contains(x.Name))
            && !x.HighlightedMessages
                .Any(y => y.OriginalMessageId == messageId || y.HighlightMessageIds.Contains(messageId)));

        var boards = (await boardsQuery
            .Include(x => x.Thresholds)
            .Include(x => x.SpoilerChannels)
            .Include(x => x.LoggingChannelOverrides)
            .ToArrayAsync()).Where(x => msg.Author is not SocketGuildUser || 
                                        msg.Author is SocketGuildUser guildUser && guildUser.Roles.All(y => y.Id != x.HighlightsMuteRole))
            .ToArray();

        logger.LogTrace("Total non unique reactions is {nur}, found {bl} boards", nonUniqueReactions, boards.Length);

        if (boards.Length == 0)
            return;

        var aliases = await context.EmoteAliases.Where(x => x.GuildId == channel.Guild.Id).ToArrayAsync();

        HashSet<HighlightBoard> completedBoards = [];
        var (uniqueReactionUsers, reactionEmotes) = await GetReactions([msg], [msg]);

        foreach (var board in boards.Where(x => !completedBoards.Contains(x)).Where(board =>
                 {
                     if (forcedBoards.Contains(board.Name))
                         return true;

                     if (!hasPerms)
                         return false;

                     var threshold = messageThresholds.GetOrCreate($"{board.GuildId}-{board.Name}-{msg.Id}", entry =>
                     {
                         entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(3);

                         var threshold = board.Thresholds.FirstOrDefault(x => x.OverrideId == channel.Id);
                         threshold ??= board.Thresholds.FirstOrDefault(x => x.OverrideId == parentChannel.Id);
                         threshold ??= board.Thresholds.FirstOrDefault(x => x.OverrideId == channel.CategoryId);
                         threshold ??= board.Thresholds.FirstOrDefault(x => x.OverrideId == channel.Guild.Id);

                         if (threshold == null)
                         {
                             logger.LogError("Could not find a threshold for {board} in {guild}! This is very bad! Defaulting to 3.", board.Name, board.GuildId);

                             return 3;
                         }

                         var messages = GetCachedMessages(channel.Id);

                         var requiredReactions = CalculateThreshold(threshold, messages, msg.CreatedAt, out _);

                         logger.LogTrace("threshold is {threshold}", requiredReactions);

                         return requiredReactions;
                     });

                     return threshold <= uniqueReactionUsers
                         .Count(y =>
                         {
                             logger.LogTrace(
                                 "user {user}: filter self react: {fsr}, is self react: {isr}",
                                 y,
                                 board.FilterSelfReactions, y == msg.Author.Id);

                             return board.FilterSelfReactions == false || y != msg.Author.Id;
                         });

                 }))
        {
            completedBoards.Add(board);

            await SendAndTrackHighlightMessage(board, aliases, msg, reactionEmotes.Select(x => new ReactionInfo(x)), uniqueReactionUsers.Count);
        }


        await context.SaveChangesAsync();
    }

    public async Task<(HashSet<ulong> uniqueReactionUsersAutoReact, HashSet<IEmote> uniqueReactionEmotes)> GetReactions(
        IEnumerable<IMessage> messagesToCheckReactions, IEnumerable<IMessage> messagesToAddReactionEmotes)
    {
        HashSet<ulong> uniqueReactionUsersAutoReact = [];
        HashSet<IEmote> uniqueReactionEmotes = [];

        var reactionEmotes = messagesToAddReactionEmotes.SelectMany(x => x.Reactions.Keys).Distinct().ToHashSet();
        foreach (var message in messagesToCheckReactions)
        {
            foreach (var reaction in message.Reactions)
            {
                if (!reactionEmotes.Contains(reaction.Key))
                    continue;

                await foreach (var user in message.GetReactionUsersAsync(reaction.Key, int.MaxValue).Flatten())
                {
                    if (user.IsBot)
                        continue;

                    uniqueReactionEmotes.Add(reaction.Key);
                    uniqueReactionUsersAutoReact.Add(user.Id);
                }
            }
        }

        return (uniqueReactionUsersAutoReact, uniqueReactionEmotes);
    }

    public IReadOnlyCollection<CachedMessage> GetCachedMessages(ulong channelId)
    {
        messageCaches.TryGetValue(channelId, out var queue);
        IReadOnlyCollection<CachedMessage> messages = queue ?? [];
        return messages;
    }

    public void AddReactionsFieldToQuote(EmbedBuilder embedBuilder, IEnumerable<ReactionInfo> reactions, int totalUniqueReactions)
    {
        var reactionsField = embedBuilder.Fields.FirstOrDefault(x => x.Name == QuotingHelpers.ReactionsFieldName);
        if (reactionsField == null)
        {
            reactionsField = new EmbedFieldBuilder().WithName(QuotingHelpers.ReactionsFieldName)
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

    public async Task SendAndTrackHighlightMessage(HighlightBoard board, EmoteAlias[] aliases, IMessage message,
        IEnumerable<ReactionInfo> reactions, int totalUniqueReactions)
    {
        var loggingChannelId = board.LoggingChannelId;

        var loggingChannelOverride =
            board.LoggingChannelOverrides.FirstOrDefault(x => x.OverriddenChannelId == message.Channel.Id);

        if (loggingChannelOverride != null)
        {
            loggingChannelId = loggingChannelOverride.LoggingChannelId;
        }

        logger.LogTrace("Sending highlight message to {channel}", loggingChannelId);

        var loggingChannel = client.GetGuild(board.GuildId).GetTextChannel(loggingChannelId);
        var embedAuthor = (IGuildUser)message.Author;
        var embedColor = await QuotingHelpers.GetQuoteEmbedColor(board.EmbedColorSource, board.FallbackEmbedColor, embedAuthor, client);

        logger.LogTrace("Embed color will be {color}", embedColor);

        var spoilerEntry = board.SpoilerChannels.FirstOrDefault(x => x.ChannelId == message.Channel.Id);

        IMessage? replyMessage = null;
        if (message.Reference != null)
        {
            if (message.Reference.ChannelId == message.Channel.Id)
            {
                if (message.Reference.MessageId.IsSpecified)
                {
                    replyMessage = await message.Channel.GetMessageAsync(message.Reference.MessageId.Value);
                }
            }
        }

        var queuedMessages =
            QuotingHelpers.QuoteMessage(message, embedColor, logger, false, spoilerEntry != null,
                spoilerEntry?.SpoilerContext ?? "", replyMessage, eb =>
                {
                    AddReactionsFieldToQuote(eb, reactions, totalUniqueReactions);
                });

        var webhook = await loggingChannel.GetOrCreateWebhookAsync(BotService.WebhookDefaultName);
        var webhookClient = new DiscordWebhookClient(webhook);
        List<ulong> highlightMessages = [];

        var username = embedAuthor is IWebhookUser webhookUser ? webhookUser.Username : embedAuthor.DisplayName;
        var avatar = embedAuthor.GetDisplayAvatarUrl();
        foreach (var queuedMessage in queuedMessages)
        {
            highlightMessages.Add(
                await webhookClient.SendMessageAsync(
                    queuedMessage.body.Truncate(2000), embeds: queuedMessage.embeds, components: queuedMessage.components,
                    username: username, avatarUrl: avatar, allowedMentions: AllowedMentions.None));
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

        board.HighlightedMessages.Add(new CachedHighlightedMessage()
        {
            HighlightMessageIds = highlightMessages,
            OriginalMessageChannelId = message.Channel.Id,
            OriginalMessageId = message.Id
        });

        logger.LogTrace("Sent and tracked highlight {messageCount} messages", highlightMessages.Count);
    }

    public async Task AutoReact(HighlightBoard board, EmoteAlias[] aliases, IReadOnlyDictionary<IEmote, ReactionMetadata> reactions, IMessage lastMessage)
    {
        IEmote? fallbackEmote = null;

        if (string.IsNullOrWhiteSpace(board.AutoReactFallbackEmoji))
        { }
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
            AutoReactEmoteChoicePreference.ReactionsDescendingPopularity =>
                reactions
                    .OrderByDescending(x => x.Value.ReactionCount)
                    .Select(x =>
                    {
                        var emoteAlias = aliases.FirstOrDefault(y =>
                            x.Key.Name.Equals(y.EmoteName, StringComparison.InvariantCultureIgnoreCase));

                        if (emoteAlias == null) return x.Key;

                        return EmoteTypeConverter.TryParse(emoteAlias.EmoteReplacement, out var emote) ? emote : x.Key;
                    })
                    .Distinct(),
            _ => []
        };

        int totalAttempts = 0;
        int successfulAttempts = 0;
        foreach (var reaction in reactEmotes)
        {
            if (successfulAttempts >= board.AutoReactMaxReactions || totalAttempts >= board.AutoReactMaxAttempts)
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

        public readonly override string ToString()
        {
            return $"**Important info:**\n" +
                   $"Current threshold: `{CurrentThreshold}`\n" +
                   $"Weighted users: `{WeightedUserCount}`\n" +
                   $"Activity level: {(IsHighActivity ? "`High`" : "`Normal`")}\n\n" +
                   $"**Extra debug info:**\n" +
                   $"Raw threshold: `{RawThreshold}`\n" +
                   $"Unweighted users: `{UnweightedUserCount}`\n" +
                   $"Total messages cached: `{TotalCachedMessages}`\n" +
                   $"Cached messages being considered in user count: `{CachedMessagesBeingConsidered}`";
        }
    }

    public static int CalculateThreshold(HighlightThreshold thresholdConfig,
        IReadOnlyCollection<HighlightsTrackingService.CachedMessage> messages,
        DateTimeOffset messageSentAt,
        out ThresholdInfo debugInfo)
    {
        Dictionary<ulong, double> userWeights = [];

        var orderedMessages = messages
            .OrderByDescending(x => x.Timestamp)
            .ToArray();

        var userWeightMessages = orderedMessages.Where(x => x.Timestamp <= messageSentAt &&
                                                            x.Timestamp >= messageSentAt - TimeSpan.FromSeconds(thresholdConfig.UniqueUserMessageMaxAgeSeconds))
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
                weight = 1 - (timeSinceLastMessage.TotalSeconds - thresholdConfig.UniqueUserDecayDelaySeconds) /
                    (thresholdConfig.UniqueUserMessageMaxAgeSeconds - thresholdConfig.UniqueUserDecayDelaySeconds);
            }

            userWeights.TryAdd(userId, weight);
        }

        var highActivity = orderedMessages.Length >= thresholdConfig.HighActivityMessageLookBack &&
                           (messageSentAt - orderedMessages[thresholdConfig.HighActivityMessageLookBack - 1].Timestamp)
                           .TotalSeconds < thresholdConfig.HighActivityMessageMaxAgeSeconds;

        var weightedUserCount = userWeights.Sum(kvp => kvp.Value);

        var highActivityMultiplier = highActivity ? thresholdConfig.HighActivityMultiplier : 1f;

        var rawThreshold = (thresholdConfig.BaseThreshold + weightedUserCount * thresholdConfig.UniqueUserMultiplier) * highActivityMultiplier;

        var thresholdDecimal = rawThreshold % 1;
        var roundedThreshold = Math.Min(thresholdConfig.MaxThreshold,
            thresholdDecimal < thresholdConfig.RoundingThreshold ? Math.Floor(rawThreshold) : Math.Ceiling(rawThreshold));

        debugInfo = new ThresholdInfo()
        {
            CurrentThreshold = roundedThreshold,
            RawThreshold = rawThreshold,
            WeightedUserCount = weightedUserCount,
            UnweightedUserCount = userWeights.Count,
            IsHighActivity = highActivity,
            TotalCachedMessages = orderedMessages.Length,
            CachedMessagesBeingConsidered = userWeightMessages.Length
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
            resolvedPermissions = (resolvedPermissions & ~perms.Value.DenyValue) | perms.Value.AllowValue;

        return new ChannelPermissions(resolvedPermissions);
    }
}
