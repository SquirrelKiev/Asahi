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
    public struct CachedMessage(ulong messageId, ulong authorId, DateTimeOffset timestamp)
    {
        public ulong messageId = messageId;
        public ulong authorId = authorId;
        public DateTimeOffset timestamp = timestamp;

        public readonly override int GetHashCode() => messageId.GetHashCode();
    }

    public struct QueuedMessage(ulong guildId, ulong channelId, ulong messageId)
    {
        public ulong guildId = guildId;
        public ulong channelId = channelId;
        public ulong messageId = messageId;

        public readonly override int GetHashCode() => messageId.GetHashCode();
    }

    private readonly ConcurrentDictionary<ulong, ConcurrentQueue<CachedMessage>> messageCaches = [];
    private readonly MemoryCache messageThresholds = new(new MemoryCacheOptions());

    private Task? timerTask;
    private readonly object lockMessageQueue = new();
    private readonly HashSet<QueuedMessage> messageQueue = [];
    private readonly HashSet<QueuedMessage> messageQueueShouldSendHighlight = [];

    public void QueueMessage(QueuedMessage messageToQueue, bool shouldSendHighlight)
    {
        lock (lockMessageQueue)
        {
            messageQueue.Add(messageToQueue);
            if (shouldSendHighlight)
                messageQueueShouldSendHighlight.Add(messageToQueue);
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
        logger.LogTrace("highlights timer task started");
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
        QueuedMessage[] messages;
        HashSet<ulong> messagesShouldSendHighlight;
        lock (lockMessageQueue)
        {
            if (messageQueue.Count == 0)
            {
                return;
            }

            messages = new QueuedMessage[messageQueue.Count];
            messageQueue.CopyTo(messages);
            messageQueue.Clear();

            messagesShouldSendHighlight = messageQueueShouldSendHighlight.Select(x => x.messageId).ToHashSet();
            messageQueueShouldSendHighlight.Clear();
        }

        foreach (var queuedMessage in messages)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var guild = client.GetGuild(queuedMessage.guildId);
                var textChannel = guild.GetTextChannel(queuedMessage.channelId);

                var shouldAddNewHighlight = messagesShouldSendHighlight.Contains(queuedMessage.messageId);
                await CheckMessageForHighlights(queuedMessage.messageId, textChannel, shouldAddNewHighlight);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to check message {messageId} in channel {channel}!", queuedMessage.messageId, queuedMessage.channelId);
            }
        }
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
    private async Task CheckMessageForHighlights(ulong messageId, SocketTextChannel channel, bool shouldAddNewHighlight)
    {
        var everyoneChannelPermissions = channel.GetPermissionOverwrite(channel.Guild.EveryoneRole);
        var everyoneCategoryPermissions = channel.Category?.GetPermissionOverwrite(channel.Guild.EveryoneRole);
        if (everyoneChannelPermissions is { SendMessages: PermValue.Deny } || everyoneCategoryPermissions is { SendMessages: PermValue.Deny })
        {
            logger.LogTrace("channel is locked, skipping.");
            return;
        }

        logger.LogTrace("Checking message");

        await using var context = dbService.GetDbContext();

        var parentChannelId = channel is SocketThreadChannel threadChannel ? threadChannel.ParentChannel.Id : channel.Id;

        // could probably be merged into one request?
        if (await context.HighlightBoards.AllAsync(x => x.GuildId != channel.Guild.Id))
            return;

        #region Handling new reactions to already highlighted messages

        var boardsWithMarkedHighlight = await context.HighlightBoards
            .Include(x => x.HighlightedMessages)
            .Where(x =>
                x.GuildId == channel.Guild.Id && x.HighlightedMessages
                    .Any(y => y.OriginalMessageId == messageId || y.HighlightMessageIds.Contains(messageId)))
            .ToArrayAsync();

        if (boardsWithMarkedHighlight.Length != 0)
        {
            foreach (var board in boardsWithMarkedHighlight)
            {
                var cachedHighlightedMessage = board.HighlightedMessages
                    .FirstOrDefault(x =>
                        x.OriginalMessageId == messageId || x.HighlightMessageIds.Contains(messageId));

                if (cachedHighlightedMessage == null)
                    continue;

                var originalMessage = await channel.Guild
                    .GetTextChannel(cachedHighlightedMessage.OriginalMessageChannelId)
                    .GetMessageAsync(cachedHighlightedMessage.OriginalMessageId);

                if (originalMessage == null)
                    continue;

                var firstMessageId = cachedHighlightedMessage.HighlightMessageIds[0];
                var lastMessageId = cachedHighlightedMessage.HighlightMessageIds[^1];

                var loggingChannel = channel.Guild.GetTextChannel(board.LoggingChannelId);

                var firstMessage = await loggingChannel.GetMessageAsync(firstMessageId);
                if (firstMessage == null)
                    continue;

                var lastMessage = firstMessageId == lastMessageId ? firstMessage : await loggingChannel.GetMessageAsync(lastMessageId);
                if (lastMessage == null)
                    continue;

                HashSet<ulong> uniqueReactionUsersAutoReact = [];
                HashSet<IEmote> uniqueReactionEmotes = [];

                // tried for quite a while to get this all within one linq statement, but seemed too much of a pain unfortunately, so three foreach instead.
                // no linq one-liners today :pensive:
                foreach (var thing in originalMessage.Reactions
                                   .Select(x => new Tuple<IMessage, IEmote>(originalMessage, x.Key))
                                   .Concat(lastMessage.Reactions
                                       .Select(x => new Tuple<IMessage, IEmote>(lastMessage, x.Key))))
                {
                    await foreach (var userCollection in thing.Item1.GetReactionUsersAsync(thing.Item2, int.MaxValue))
                    {
                        foreach (var user in userCollection.Select(genericUser => channel.Guild.GetUser(genericUser.Id))
                                     .Where(user => user != null && !user.IsBot))
                        {
                            uniqueReactionUsersAutoReact.Add(user.Id);

                            // done in the loop so the IsBot applies
                            uniqueReactionEmotes.Add(thing.Item2);
                        }
                    }
                }

                var reactions = uniqueReactionEmotes.Select(x => new ReactionInfo(x));

                var webhook = await loggingChannel.GetOrCreateWebhookAsync(BotService.WebhookDefaultName, client.CurrentUser);
                var webhookClient = new DiscordWebhookClient(webhook);

                await webhookClient.ModifyMessageAsync(firstMessageId, messageProperties =>
                {
                    var embeds = firstMessage.Embeds.Select(x => x.ToEmbedBuilder()).ToArray();

                    var eb = embeds[0];

                    AddReactionsFieldToQuote(eb, reactions, uniqueReactionUsersAutoReact.Count);

                    messageProperties.Embeds = embeds.Select(x => x.Build()).ToArray();
                });
            }

            return;
        }

        #endregion

        if (!shouldAddNewHighlight)
            return;

        logger.LogTrace("at the highlight testing code");

        if (await context.HighlightBoards.AnyAsync(x => x.LoggingChannelId == parentChannelId))
            return;

        var msg = await channel.GetMessageAsync(messageId);

        if (msg.Author is not SocketGuildUser guildUser)
            return;

        if (botConfig.BannedHighlightsUsers.Contains(msg.Author.Id))
            return;

        var nonUniqueReactions = msg.Reactions.Sum(x => x.Value.ReactionCount);

        var messageAge = (DateTimeOffset.UtcNow - msg.Timestamp).TotalSeconds;

        var boardsQuery = context.HighlightBoards.Where(x =>
            x.GuildId == channel.Guild.Id
            && (x.MaxMessageAgeSeconds == 0 || messageAge <= x.MaxMessageAgeSeconds)
            && (x.FilteredChannelsIsBlockList
                ? x.FilteredChannels.All(y => y != parentChannelId)
                : x.FilteredChannels.Any(y => y == parentChannelId)
                  || (x.FilteredChannelsIsBlockList
                      ? x.FilteredChannels.All(y => y != channel.Id)
                      : x.FilteredChannels.Any(y => y == channel.Id))
        ));

        if (channel is SocketThreadChannel)
        {
            var threadId = channel.Id;

            boardsQuery = boardsQuery.Where(x => x.FilteredChannelsIsBlockList
                ? x.FilteredChannels.All(y => y != threadId)
                        : x.FilteredChannels.Any(y => y == threadId));
        }

        var boards = (await boardsQuery
            .Include(x => x.EmoteAliases)
            .Include(x => x.Thresholds)
            .ToArrayAsync()).Where(x => guildUser.Roles.All(y => y.Id != x.HighlightsMuteRole))
            .ToArray();

        logger.LogTrace("Total non unique reactions is {nur}, found {bl} boards", nonUniqueReactions, boards.Length);

        if (boards.Length == 0)
            return;

        HashSet<HighlightBoard> completedBoards = [];
        HashSet<SocketGuildUser> uniqueReactionUsers = [];

        await foreach (var userCollection in msg.Reactions.ToAsyncEnumerable()
                                   .SelectMany(reactionMetadata =>
                                       msg.GetReactionUsersAsync(reactionMetadata.Key, int.MaxValue)))
        {
            foreach (var user in userCollection.Select(genericUser => channel.Guild.GetUser(genericUser.Id)).Where(user => user != null))
            {
                if (client.CurrentUser.Id == user.Id || user.IsBot)
                {
                    logger.LogTrace("Skipped bot react.");
                    continue;
                }

                uniqueReactionUsers.Add(user);
                logger.LogTrace("Added unique user {user}.", user.Id);
            }

            // in the loop so in the case that all the boards for this guild are satisfied, we don't have to waste time processing unnecessary reactions.
            foreach (var board in boards.Where(x => !completedBoards.Contains(x)).Where(board =>
            {
                var threshold = messageThresholds.GetOrCreate($"{board.GuildId}-{board.Name}-{msg.Id}", entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(3);

                    var threshold = board.Thresholds.FirstOrDefault(x => x.OverrideId == channel.Id);
                    threshold ??= board.Thresholds.FirstOrDefault(x => x.OverrideId == parentChannelId);
                    threshold ??= board.Thresholds.FirstOrDefault(x => x.OverrideId == channel.CategoryId);
                    threshold ??= board.Thresholds.FirstOrDefault(x => x.OverrideId == channel.Guild.Id);

                    if (threshold == null)
                    {
                        logger.LogError("Could not find a threshold for {board} in {guild}! This is very bad! Defaulting to 3.", board.Name, board.GuildId);

                        return 3;
                    }

                    var messages = GetCachedMessages(channel.Id);

                    var requiredReactions = HighlightsHelpers.CalculateThreshold(threshold, messages, msg.CreatedAt, out _);

                    logger.LogTrace("threshold is {threshold}", requiredReactions);

                    return requiredReactions;
                });

                return threshold <= uniqueReactionUsers
                    .Count(y =>
                    {
                        logger.LogTrace(
                            "user {user}: filter self react: {fsr}, is self react: {isr}",
                            y.Id,
                            board.FilterSelfReactions, y.Id == msg.Author.Id);

                        return board.FilterSelfReactions == false || y.Id != msg.Author.Id;
                    });

            }))
            {
                completedBoards.Add(board);

                await SendAndTrackHighlightMessage(board, msg, uniqueReactionUsers.Count);
            }

            if (completedBoards.Count == boards.Length)
                break;
        }

        await context.SaveChangesAsync();
    }

    public IReadOnlyCollection<CachedMessage> GetCachedMessages(ulong channelId)
    {
        messageCaches.TryGetValue(channelId, out var queue);
        IReadOnlyCollection<CachedMessage> messages = queue ?? [];
        return messages;
    }

    private void AddReactionsFieldToQuote(EmbedBuilder embedBuilder, IEnumerable<ReactionInfo> reactions, int totalUniqueReactions)
    {
        var reactionsField = embedBuilder.Fields.FirstOrDefault(x => x.Name == HighlightsHelpers.ReactionsFieldName);
        if (reactionsField == null)
        {
            reactionsField = new EmbedFieldBuilder().WithName(HighlightsHelpers.ReactionsFieldName)
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

    private async Task SendAndTrackHighlightMessage(HighlightBoard board, IMessage message, int totalUniqueReactions)
    {
        logger.LogTrace("Sending highlight message to {channel}", board.LoggingChannelId);

        var textChannel = client.GetGuild(board.GuildId).GetTextChannel(board.LoggingChannelId);
        var embedAuthor = (IGuildUser)message.Author;
        var embedColor = await HighlightsHelpers.GetQuoteEmbedColor(board.EmbedColorSource, board.FallbackEmbedColor, embedAuthor, client);

        logger.LogTrace("Embed color will be {color}", embedColor);


        var queuedMessages = HighlightsHelpers.QuoteMessage(message, embedColor, logger, true);

        var eb = queuedMessages[0].embeds?[0].ToEmbedBuilder();
        if (eb != null)
        {
            AddReactionsFieldToQuote(eb, message.Reactions.Select(x => new ReactionInfo(x.Key)), totalUniqueReactions);
            queuedMessages[0].embeds![0] = eb.Build();
        }

        var webhook = await textChannel.GetOrCreateWebhookAsync(BotService.WebhookDefaultName, client.CurrentUser);
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
                var lastMessageObj = await textChannel.GetMessageAsync(highlightMessages[^1]);
                await AutoReact(board, message.Reactions, lastMessageObj);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to auto-react in channel {channel}!", textChannel.Id);
        }


        board.HighlightedMessages.Add(new CachedHighlightedMessage()
        {
            HighlightMessageIds = highlightMessages,
            OriginalMessageChannelId = message.Channel.Id,
            OriginalMessageId = message.Id
        });

        logger.LogTrace("Sent and tracked highlight {messageCount} messages", highlightMessages.Count);
    }

    private async Task AutoReact(HighlightBoard board, IReadOnlyDictionary<IEmote, ReactionMetadata> reactions, IMessage lastMessage)
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
                        var emoteAlias = board.EmoteAliases.FirstOrDefault(y =>
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
}
