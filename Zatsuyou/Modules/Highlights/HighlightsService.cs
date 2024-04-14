using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;
using Zatsuyou.Database;
using Zatsuyou.Database.Models;

namespace Zatsuyou.Modules.Highlights;

[Inject(ServiceLifetime.Singleton)]
public class HighlightsTrackingService(DbService dbService, ILogger<HighlightsTrackingService> logger, DiscordSocketClient client)
{
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> messageProcessingSemaphores = [];
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> messageToBeProcessedSephamores = [];

    public async Task CheckMessageForHighlights(Cacheable<IUserMessage, ulong> cachedMessage, SocketReaction reaction)
    {
        var msgId = cachedMessage.Id;

        bool queued = false;
        var processingSemaphore = messageProcessingSemaphores.GetOrAdd(msgId, _ => new SemaphoreSlim(1, 1));
        if (!await processingSemaphore.WaitAsync(0))
        {
            var messageToBeProcessedSemaphore = messageToBeProcessedSephamores.GetOrAdd(msgId, _ => new SemaphoreSlim(1, 1));
            if (!await messageToBeProcessedSemaphore.WaitAsync(0))
            {
                return;
            }

            queued = true;
            await processingSemaphore.WaitAsync();
        }

        try
        {
            await CheckMessageForHighlights_Impl(cachedMessage, reaction);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tried to process message {message} in channel {channel} but failed, exception below", msgId, reaction.Channel.Id);
        }
        finally
        {
            processingSemaphore.Release();
            messageProcessingSemaphores.TryRemove(msgId, out _);
            if (queued)
            {
                var messageToBeProcessedSemaphore = messageToBeProcessedSephamores.GetOrAdd(msgId, _ => new SemaphoreSlim(1, 1));
                messageToBeProcessedSemaphore.Release();
                messageToBeProcessedSephamores.TryRemove(msgId, out _);
            }
        }
    }

    private async Task CheckMessageForHighlights_Impl(Cacheable<IUserMessage, ulong> cachedMessage, SocketReaction reaction)
    {
        logger.LogTrace("Checking message");

        await using var context = dbService.GetDbContext();

        if (reaction.Channel is not SocketTextChannel channel)
            return;

        if (await context.HighlightBoards.AllAsync(x => x.GuildId != channel.Guild.Id))
            return;

        var msg = await cachedMessage.GetOrDownloadAsync();

        var nonUniqueReactions = msg.Reactions.Sum(x => x.Value.ReactionCount);

        var messageAge = (DateTimeOffset.UtcNow - msg.Timestamp).TotalSeconds;

        var boards = await context.HighlightBoards.Where(x =>
                x.GuildId == channel.Guild.Id
                && x.Threshold <= nonUniqueReactions
                && (x.MaxMessageAgeSeconds == 0 || messageAge <= x.MaxMessageAgeSeconds)
                && x.HighlightedMessages.All(y => y.OriginalMessageId != msg.Id)
                && (x.FilteredChannelsIsBlockList ? x.FilteredChannels.All(y => y != channel.Id) : x.FilteredChannels.Any(y => y == channel.Id))
            )
            .ToArrayAsync();

        logger.LogTrace("Total non unique reactions is {nur}, boards length is {bl}/{bc}", nonUniqueReactions, boards.Length, await context.HighlightBoards.CountAsync());

        HashSet<HighlightBoard> completedBoards = [];
        HashSet<ulong> uniqueReactionUsers = [];

        if (boards.Length == 0)
            return;

        await foreach (var user in msg.Reactions.ToAsyncEnumerable()
                           .SelectMany(reactionMetadata => msg.GetReactionUsersAsync(reactionMetadata.Key, int.MaxValue).Flatten()))
        {
            if (client.CurrentUser.Id == user.Id)
            {
                logger.LogTrace("Skipped user {user}.", user.Id);
                continue;
            }

            uniqueReactionUsers.Add(user.Id);
            logger.LogTrace("Added unique user {user}.", user.Id);

            foreach (var board in boards.Where(x => !completedBoards.Contains(x) && x.Threshold <= uniqueReactionUsers.Count))
            {
                completedBoards.Add(board);

                await SendAndTrackHighlightMessage(board, msg);
            }

            if (completedBoards.Count == boards.Length)
                break;
        }

        await context.SaveChangesAsync();
    }

    private async Task SendAndTrackHighlightMessage(HighlightBoard board, IMessage message)
    {
        logger.LogTrace("sending highlight message to {channel}", board.LoggingChannelId);

        var textChannel = client.GetGuild(board.GuildId).GetTextChannel(board.LoggingChannelId);

        var embeds = new List<EmbedBuilder>()
        {
            new EmbedBuilder()
                .WithAuthor(message.Author)
                .WithDescription(message.Content)
                .WithTimestamp(message.Timestamp)
        };

        List<MessageContents> queuedMessages = [];

        bool attachedImage = false;
        foreach (var embed in message.Embeds)
        {
            switch (embed.Type)
            {
                case EmbedType.Image:
                    logger.LogTrace("Adding image embed.");

                    HandleImageEmbed(embed.Url, embeds, ref attachedImage);

                    break;
                case EmbedType.Video:
                    logger.LogTrace("Queued video link message.");
                    queuedMessages.Add(new MessageContents(embed.Url, embed: null, components: new ComponentBuilder()));
                    break;
                case EmbedType.Rich:
                    logger.LogTrace("Adding rich embed.");
                    embeds.Add(embed.ToEmbedBuilder());
                    break;
            }
        }

        var desc = new StringBuilder($"{message.GetJumpUrl()}");
        int i = 1;
        var tooManyAttachments = false;
        foreach (var attachment in message.Attachments)
        {
            logger.LogTrace("Found attachment {index}.", i);
            var txt = $"\n[Attachment {i}]({attachment.Url})";
            var tooManyAttachmentsText = $"\nPlus {message.Attachments.Count - i + 1} more.";
            if (!tooManyAttachments && desc.Length + txt.Length > 2000 - tooManyAttachmentsText.Length)
            {
                desc.Append(tooManyAttachmentsText);
                tooManyAttachments = true;
            }
            else if (!tooManyAttachments)
            {
                desc.Append(txt);
            }

            if (attachment.ContentType.StartsWith("image"))
            {
                logger.LogTrace("attachment is image, sending as embed");

                HandleImageEmbed(attachment.Url, embeds, ref attachedImage);
            }
            else if (attachment.ContentType.StartsWith("video"))
            {
                logger.LogTrace("attachment is video, sending as queued message");

                queuedMessages.Add(new MessageContents(attachment.Url, embed: null, components: new ComponentBuilder()));
            }

            i++;
        }

        foreach (var sticker in message.Stickers)
        {
            logger.LogTrace("found sticker, sending as image.");

            var stickerUrl = CDN.GetStickerUrl(sticker.Id, sticker.Format);

            HandleImageEmbed(stickerUrl, embeds, ref attachedImage);
        }

        if (string.IsNullOrWhiteSpace(embeds[0].Description) && string.IsNullOrWhiteSpace(embeds[0].ImageUrl))
        {
            embeds[0].WithDescription("*No content.*");
        }

        var highlightMsg = await textChannel.SendMessageAsync(desc.ToString().Truncate(2000), embeds: embeds.Take(10).Select(x => x.Build()).ToArray());
        foreach (var queuedMessage in queuedMessages)
        {
            await textChannel.SendMessageAsync(queuedMessage.body.Truncate(2000), embeds: queuedMessage.embeds, components: queuedMessage.components);
        }

        board.HighlightedMessages.Add(new CachedHighlightedMessage()
        {
            HighlightMessageId = highlightMsg.Id,
            OriginalMessageId = message.Id
        });

        logger.LogTrace("Sent and tracked highlight message {message}", highlightMsg.Id);
    }

    private static void HandleImageEmbed(string imageUrl, List<EmbedBuilder> embedBuilders, ref bool attachedImage)
    {
        if (!attachedImage)
        {
            embedBuilders[0].WithImageUrl(imageUrl);
            attachedImage = true;
        }
        else
        {
            embedBuilders.Add(new EmbedBuilder
            {
                ImageUrl = imageUrl
            });
        }
    }
}
