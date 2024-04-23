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

[Inject(ServiceLifetime.Singleton)]
public class HighlightsTrackingService(DbService dbService, ILogger<HighlightsTrackingService> logger, DiscordSocketClient client)
{
    public struct CachedMessage
    {
        public CachedMessage(ulong messageId, ulong authorId, DateTimeOffset timestamp)
        {
            this.messageId = messageId;
            this.authorId = authorId;
            this.timestamp = timestamp;
        }

        public ulong messageId;
        public ulong authorId;
        public DateTimeOffset timestamp;
    }

    // I hate multithreading

    // safetySemaphore here is to prevent weird multithreading issues with the locks stuff.
    // had some issues where two messages would process at the same time when the reactions were sent quick enough if I didn't do this
    public readonly SemaphoreSlim safetySemaphore = new(1);

    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> messageProcessingSemaphores = [];
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> messageToBeProcessedSemaphores = [];

    private readonly ConcurrentDictionary<ulong, ConcurrentQueue<CachedMessage>> messageCaches = [];
    private readonly MemoryCache messageThresholds = new(new MemoryCacheOptions());

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

    /// <remarks>Wait on <see cref="safetySemaphore"/> before doing anything!</remarks>
    public async Task CheckMessageForHighlights(Cacheable<IUserMessage, ulong> cachedMessage, SocketTextChannel originalChannel, bool shouldAddNewHighlight)
    {
        bool freedSemaphore = false;
        var msgId = cachedMessage.Id;

        bool queued = false;
        var processingSemaphore = messageProcessingSemaphores.GetOrAdd(msgId, _ => new SemaphoreSlim(1, 1));
        if (!await processingSemaphore.WaitAsync(0))
        {
            logger.LogTrace("processing semaphore already got");
            var messageToBeProcessedSemaphore = messageToBeProcessedSemaphores.GetOrAdd(msgId, _ => new SemaphoreSlim(1, 1));
            if (!await messageToBeProcessedSemaphore.WaitAsync(0))
            {
                logger.LogTrace("message already set to be processed");
                safetySemaphore.Release();
                return;
            }

            queued = true;
            logger.LogTrace("waiting for message to be processed");
            freedSemaphore = true;

            // this is just a fancy yield right so this will mean it'll acquire the lock etc. before we free the safety semaphore (I think)
            var waitTask = processingSemaphore.WaitAsync();
            safetySemaphore.Release();
            await waitTask;
        }

        if (!freedSemaphore)
        {
            safetySemaphore.Release();
        }

        try
        {
            await CheckMessageForHighlights_Impl(cachedMessage, originalChannel, shouldAddNewHighlight);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tried to process message {message} in channel {channel} but failed, exception below",
                msgId, originalChannel.Id);
        }
        finally
        {
            processingSemaphore.Release();
            messageProcessingSemaphores.TryRemove(msgId, out _);
            if (queued)
            {
                var messageToBeProcessedSemaphore = messageToBeProcessedSemaphores.GetOrAdd(msgId, _ => new SemaphoreSlim(1, 1));
                messageToBeProcessedSemaphore.Release();
                messageToBeProcessedSemaphores.TryRemove(msgId, out _);
            }
        }
    }

    private const string ReactionsFieldName = "Reactions";
    public struct ReactionInfo(IEmote emote, int totalReactions)
    {
        public IEmote emote = emote;
        public int totalReactions = totalReactions;
    }

    // god I love linq
    private async Task CheckMessageForHighlights_Impl(Cacheable<IUserMessage, ulong> cachedMessage, SocketTextChannel channel, bool shouldAddNewHighlight)
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
                    .Any(y => y.OriginalMessageId == cachedMessage.Id || y.HighlightMessageIds.Contains(cachedMessage.Id)))
            .ToArrayAsync();

        if (boardsWithMarkedHighlight.Length != 0)
        {
            foreach (var board in boardsWithMarkedHighlight)
            {
                var cachedHighlightedMessage = board.HighlightedMessages
                    .FirstOrDefault(x =>
                        x.OriginalMessageId == cachedMessage.Id || x.HighlightMessageIds.Contains(cachedMessage.Id));

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

                Dictionary<IEmote, HashSet<SocketGuildUser>> uniqueHighlightReactions = [];

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
                            if (!uniqueHighlightReactions.TryGetValue(thing.Item2, out var hashSet))
                            {
                                hashSet = [];
                                uniqueHighlightReactions.Add(thing.Item2, hashSet);
                            }

                            hashSet.Add(user);
                        }
                    }
                }

                var reactions =
                    uniqueHighlightReactions
                        .Select(x => new ReactionInfo
                            (
                                x.Key,
                                x.Value.Count
                            )
                    );

                var webhook = await loggingChannel.GetOrCreateWebhookAsync(BotService.WebhookDefaultName, client.CurrentUser);
                var webhookClient = new DiscordWebhookClient(webhook);

                await webhookClient.ModifyMessageAsync(firstMessageId, messageProperties =>
                {
                    var embeds = firstMessage.Embeds.Select(x => x.ToEmbedBuilder()).ToArray();

                    var eb = embeds[0];

                    AddReactionsFieldToQuote(eb, reactions);

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

        var msg = await cachedMessage.GetOrDownloadAsync();

        if (msg.Author is not SocketGuildUser guildUser)
            return;

        var nonUniqueReactions = msg.Reactions.Sum(x => x.Value.ReactionCount);

        var messageAge = (DateTimeOffset.UtcNow - msg.Timestamp).TotalSeconds;

        var boardsQuery = context.HighlightBoards.Where(x =>
            x.GuildId == channel.Guild.Id
            && (x.MaxMessageAgeSeconds == 0 || messageAge <= x.MaxMessageAgeSeconds)
            && (x.FilteredChannelsIsBlockList
                ? x.FilteredChannels.All(y => y != parentChannelId)
                : x.FilteredChannels.Any(y => y == parentChannelId)
            && (!x.FilteredChannelsIsBlockList || x.FilteredChannels.All(y => y != channel.Id))
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

                await SendAndTrackHighlightMessage(board, msg, msg.Reactions);
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

    private static void AddReactionsFieldToQuote(EmbedBuilder embedBuilder, IEnumerable<ReactionInfo> reactions)
    {
        var reactionsField = embedBuilder.Fields.FirstOrDefault(x => x.Name == ReactionsFieldName);
        if (reactionsField == null)
        {
            reactionsField = new EmbedFieldBuilder().WithName(ReactionsFieldName)
                .WithIsInline(true);
            embedBuilder.AddField(reactionsField);
        }

        var sb = new StringBuilder();
        bool addedReaction = false;
        foreach (var reaction in reactions)
        {
            addedReaction = true;
            sb.Append(reaction.emote).Append(" **")
                .Append(reaction.totalReactions).Append("** ");
        }

        if (!addedReaction)
        {
            sb.Append("**None?**");
        }

        reactionsField.WithValue(sb.ToString());
    }

    private async Task SendAndTrackHighlightMessage(HighlightBoard board, IMessage message, IReadOnlyDictionary<IEmote, ReactionMetadata> reactions)
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
            AddReactionsFieldToQuote(eb, message.Reactions.Select(x => new ReactionInfo
            (
                x.Key,
                x.Value.ReactionCount
            )));
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

        if (board.AutoReactMaxAttempts != 0)
        {
            var lastMessageObj = await textChannel.GetMessageAsync(highlightMessages[^1]);
            await AutoReact(board, reactions, lastMessageObj);
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
