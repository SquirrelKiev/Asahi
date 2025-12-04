using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Text;
using Asahi.Database.Models;
using Discord.WebSocket;
using Markdig;
using Markdig.Parsers;
using Markdig.Parsers.Inlines;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules;

public static class QuotingHelpers
{
    // Not the nuclear kind (I guess some of them could be considered pretty nuclear?)
    public const string ReactionsFieldName = "Reactors";
    public const string ReplyingTo = "Replying to ";

    // TODO: Make this more readable
    [Pure]
    public static List<MessageContents> QuoteMessage(IMessage message, Color embedColor, ILogger logger,
        bool showAuthor, HashSet<string> forbiddenUrls, bool spoilerAll = false, string? spoilerContext = "",
        IMessage? replyMessage = null, Action<EmbedBuilder>? modifyQuoteEmbed = null)
    {
        string constantUrl = "";
        while (constantUrl == "" || forbiddenUrls.Contains(constantUrl)) // TODO: this but without the loop
        {
            constantUrl = CatboxQts.GetRandomQtUrl();
        }

        forbiddenUrls.Add(constantUrl);

        //var channelName = message.Channel is SocketThreadChannel threadChannel ? $"#{threadChannel.ParentChannel.Name} • " : "";

        string? channelName = message.Channel != null ? $"#{message.Channel.Name}" : null;

        List<MessageContents>? replyMessages = null;
        if (replyMessage != null)
        {
            showAuthor = true;
            replyMessages = QuoteMessage(replyMessage, embedColor, logger, showAuthor, forbiddenUrls, spoilerAll,
                modifyQuoteEmbed:
                eb =>
                {
                    eb.WithFooter(new EmbedFooterBuilder());
                    eb.Timestamp = null;
                });
        }

        if (message.Channel is SocketThreadChannel threadChannel)
        {
            channelName += $" • #{threadChannel.ParentChannel.Name}";
        }

        var messageContent = message.Content;

        if (spoilerAll)
        {
            messageContent = messageContent.SpoilerMessage(spoilerContext);
        }

        var link = message.Channel != null ? message.GetJumpUrl() : null;
        var firstEmbed = new EmbedBuilder()
            .WithDescription(messageContent)
            .WithTimestamp(message.Timestamp)
            .WithOptionalColor(embedColor)
            .WithUrl(constantUrl);

        if (channelName != null)
            firstEmbed.WithFooter(channelName);

        List<MessageContents> forwardedMessages = [];
        if (message is IUserMessage userMessage)
        {
            foreach (var forwardedMessage in userMessage.ForwardedMessages)
            {
                forwardedMessages.AddRange(QuoteMessage(forwardedMessage.Message, embedColor, logger, showAuthor,
                    forbiddenUrls, spoilerAll, modifyQuoteEmbed:
                    eb =>
                    {
                        eb.WithFooter(new EmbedFooterBuilder());
                        eb.Timestamp = null;
                    }));
            }
        }

        if (showAuthor && message.Author != null)
        {
            if (message.Author is not IGuildUser user)
            {
                firstEmbed.WithAuthor(message.Author);
            }
            else
            {
                var username = user is IWebhookUser webhookUser ? webhookUser.Username : user.DisplayName;
                var avatar = user.GetDisplayAvatarUrl();

                firstEmbed.WithAuthor(username, avatar);
            }
        }

        List<EmbedBuilder> embeds = [firstEmbed];

        var queuedMessages = new List<MessageContents>();

        bool attachedImage = false;
        logger.LogTrace("There are {embedCount} embeds in this message.", message.Embeds.Count);

        bool embedsShouldSpoiler = false;
        if (message.Content != null && embeds.Count > 0)
        {
            var pipelineBuilder = new MarkdownPipelineBuilder();
            
            pipelineBuilder.BlockParsers.Clear();
            pipelineBuilder.InlineParsers.Clear();

            var emphasis = new EmphasisInlineParser();
            emphasis.EmphasisDescriptors.Clear();
            emphasis.EmphasisDescriptors.Add(new EmphasisDescriptor('|', 2, 2, true));
            
            pipelineBuilder.InlineParsers.Add(emphasis);

            pipelineBuilder.BlockParsers.Add(new ParagraphBlockParser());
            
            var pipeline = pipelineBuilder.Build();

            // HACK: commonmark doesn't count emphasis with spaces after the delimiter as emphasis. e.g. ** hello ** is not a valid emphasis.
            // discord however does not care about commonmark and counts it anyway.
            // afaik they use this parser for spoilers, which means naturally spoilers also don't care about spaces.
            // doing some replacing to make the message something that is happy to be parsed
            var fixedUpContent = CompiledRegex.HackDiscordSpoilerReplacer().Replace(message.Content, "||");
            var res = Markdown.Parse(fixedUpContent, pipeline);

            foreach (var descendant in res.Descendants<EmphasisInline>())
            {
                if (descendant.Descendants<LiteralInline>()
                    .Any(x => CompiledRegex.BadLinkFinder().IsMatch(x.Content.AsSpan())))
                {
                    logger.LogTrace("Message contains spoiler-tagged link, marking all embeds as spoilers.");
                    embedsShouldSpoiler = true;
                    break;
                }
            }
        }

        int spoilerRichEmbeds = 0;
        bool addedSpoilerMessage = false;
        foreach (var embed in message.Embeds)
        {
            //logger.LogTrace("embed: {embedJson}", JsonConvert.SerializeObject(embed, Formatting.Indented));

            switch (embed.Type)
            {
                case EmbedType.Image:
                    logger.LogTrace("Adding image embed.");

                    if (spoilerAll || embedsShouldSpoiler)
                    {
                        if (!addedSpoilerMessage)
                        {
                            firstEmbed.Description += $"\n\n**Spoiler attachments**";
                            addedSpoilerMessage = true;
                        }

                        firstEmbed.Description += $"\n{embed.Url}";
                    }
                    else
                    {
                        HandleImageEmbed(embed.Url, embeds, embedColor, ref attachedImage, constantUrl);
                    }

                    break;
                case EmbedType.Video:
                case EmbedType.Gifv:
                    logger.LogTrace("Queued video link message.");
                    if (spoilerAll || embedsShouldSpoiler)
                    {
                        if (!addedSpoilerMessage)
                        {
                            firstEmbed.Description += $"\n\n**Spoiler attachments**";
                            addedSpoilerMessage = true;
                        }

                        firstEmbed.Description += $"\n{embed.Url}";
                    }
                    else
                    {
                        queuedMessages.Add(new MessageContents(embed.Url, embed: null));
                    }

                    break;
                case EmbedType.Link:
                case EmbedType.Article:
                case EmbedType.Rich:
                    logger.LogTrace("Adding rich embed.");
                    if (spoilerAll || embedsShouldSpoiler)
                        spoilerRichEmbeds++;
                    else
                        embeds.Add(embed.ToEmbedBuilderForce());
                    break;
                default:
                    logger.LogTrace("skipping unknown embed type {embedType}", embed.Type);
                    break;
            }
        }

        if (spoilerRichEmbeds > 0)
        {
            firstEmbed.Description += $"\n[Skipped **{spoilerRichEmbeds}** embed(s) in case of spoilers]";
        }

        var attachmentsValueContents = new StringBuilder();
        var i = 1;
        var tooManyAttachments = false;
        foreach (var attachment in message.Attachments)
        {
            logger.LogTrace("Found attachment {index}.", i);

            if (attachment.ContentType.StartsWith("image"))
            {
                logger.LogTrace("attachment is image");

                if (spoilerAll || attachment.IsSpoiler())
                {
                    if (!addedSpoilerMessage)
                    {
                        firstEmbed.Description += $"\n\n**Spoiler attachments**";
                        addedSpoilerMessage = true;
                    }

                    firstEmbed.Description += $"\n{attachment.Url}";
                }
                else
                {
                    HandleImageEmbed(attachment.Url, embeds, embedColor, ref attachedImage, constantUrl);
                }
            }
            else if (attachment.ContentType.StartsWith("video"))
            {
                if (spoilerAll || attachment.IsSpoiler())
                {
                    logger.LogTrace("attachment is spoiler video");

                    if (!addedSpoilerMessage)
                    {
                        firstEmbed.Description += $"\n\n**Spoiler attachments**";
                        addedSpoilerMessage = true;
                    }

                    firstEmbed.Description += $"\nSpoiler Video{attachment.Url}";
                }
                else
                {
                    logger.LogTrace("attachment is video, sending as queued message");
                    queuedMessages.Add(new MessageContents(attachment.Url, embed: null));
                }
            }
            else
            {
                var txt = $"[File {i} ({attachment.Filename})]({attachment.Url})";
                var tooManyAttachmentsText = $"Plus {message.Attachments.Count - i + 1} more.";
                if (!tooManyAttachments &&
                    attachmentsValueContents.Length + txt.Length > 1024 - tooManyAttachmentsText.Length)
                {
                    attachmentsValueContents.AppendLine(tooManyAttachmentsText);
                    tooManyAttachments = true;
                }
                else if (!tooManyAttachments)
                {
                    attachmentsValueContents.AppendLine(txt);
                }
            }

            i++;
        }

        if (attachmentsValueContents.Length != 0)
        {
            firstEmbed.AddField("Attachments", attachmentsValueContents.ToString().Truncate(1024, false), true);
        }

        if (!spoilerAll)
        {
            foreach (var sticker in message.Stickers)
            {
                logger.LogTrace("found sticker, sending as image.");

                var stickerUrl = CDN.GetStickerUrl(sticker.Id, sticker.Format);

                HandleImageEmbed(stickerUrl, embeds, embedColor, ref attachedImage, constantUrl);
            }
        }

        if (string.IsNullOrWhiteSpace(embeds[0].Description) && string.IsNullOrWhiteSpace(embeds[0].ImageUrl))
        {
            firstEmbed.WithDescription("*No content.*");
        }

        modifyQuoteEmbed?.Invoke(firstEmbed);

        queuedMessages.Insert(0, new MessageContents(link ?? "", embeds.Take(10).Select(x => x.Build()).ToArray(), null));

        if (replyMessages != null)
        {
            var firstReplyMessage = replyMessages[0];
            var replyMessageAuthor = firstReplyMessage.embeds![0].Author!.Value;
            firstReplyMessage.embeds![0] = firstReplyMessage.embeds![0].ToEmbedBuilder()
                .WithAuthor($"{ReplyingTo}{replyMessageAuthor.Name}", replyMessageAuthor.IconUrl,
                    replyMessageAuthor.Url).Build();

            if (replyMessages.Count == 1 && queuedMessages[0].embeds?.Length < 10)
            {
                replyMessages[0] = firstReplyMessage;

                var firstMessage = queuedMessages[0];
                firstMessage.embeds = [.. replyMessages[0].embeds!, .. queuedMessages[0].embeds!];
                queuedMessages[0] = firstMessage;
            }
            else
            {
                var firstQueuedMessage = queuedMessages[0];

                firstReplyMessage.body = firstQueuedMessage.body;
                firstQueuedMessage.body = "";

                replyMessages[0] = firstReplyMessage;
                queuedMessages[0] = firstQueuedMessage;

                queuedMessages.InsertRange(0, replyMessages);
            }
        }

        if(forwardedMessages.Count != 0)
        {
            var firstForwardedMessage = forwardedMessages[0];
            firstForwardedMessage.embeds![0] = firstForwardedMessage.embeds![0].ToEmbedBuilder()
                .WithAuthor("Forwarded message").Build();
            
            if (forwardedMessages.Count == 1 && queuedMessages[0].embeds?.Length < 10)
            {
                forwardedMessages[0] = firstForwardedMessage;

                var firstMessage = queuedMessages[0];
                firstMessage.embeds = [.. forwardedMessages[0].embeds!, .. queuedMessages[0].embeds!];
                queuedMessages[0] = firstMessage;
            }
            else
            {
                var firstQueuedMessage = queuedMessages[0];

                firstForwardedMessage.body = firstQueuedMessage.body;
                firstQueuedMessage.body = "";

                forwardedMessages[0] = firstForwardedMessage;
                queuedMessages[0] = firstQueuedMessage;

                queuedMessages.AddRange(forwardedMessages);
            }
        }

        return queuedMessages;
    }

    [Pure]
    public static string SpoilerMessage(this string messageContent, string? spoilerContext)
    {
        var msg = $"{spoilerContext}{(string.IsNullOrWhiteSpace(spoilerContext) ? "" : " ")}";
        if (!string.IsNullOrWhiteSpace(messageContent))
        {
            msg += $"|| {messageContent.Replace("|", "\\|")} ||";
        }

        return msg;
    }

    private static void HandleImageEmbed(string imageUrl,
        List<EmbedBuilder> embedBuilders,
        Color embedColor,
        ref bool isFirstImageAdded,
        string constantUrl)
    {
        if (!isFirstImageAdded)
        {
            embedBuilders[0].WithImageUrl(imageUrl);
            isFirstImageAdded = true;
        }
        else
        {
            var eb = new EmbedBuilder().WithImageUrl(imageUrl);
            if (!isFirstImageAdded)
            {
                eb.WithOptionalColor(embedColor);
            }

            eb.WithUrl(constantUrl);

            embedBuilders.Add(eb);
        }
    }

    public static async ValueTask<Color> GetQuoteEmbedColor(EmbedColorSource colorSource, Color fallbackColor,
        IGuildUser? embedAuthor, IDiscordClient client)
    {
        Color embedColor = fallbackColor;

        var user = embedAuthor;

        switch (colorSource)
        {
            case EmbedColorSource.UsersRoleColor:
                embedColor = GetUserRoleColorWithFallback(user, embedColor);
                break;
            case EmbedColorSource.BotsRoleColor:
                if (embedAuthor != null)
                {
                    user = await embedAuthor.Guild.GetCurrentUserAsync();
                    goto case EmbedColorSource.UsersRoleColor;
                }

                break;
            case EmbedColorSource.AlwaysUseFallbackColor:
            default:
                break;
        }

        return embedColor;
    }

    public static Color GetUserRoleColorWithFallback(IGuildUser? embedAuthor, Color fallbackColor)
    {
        return embedAuthor?.RoleIds.Select(x => embedAuthor.Guild.GetRole(x))
            .OrderByDescending(x => x.Position)
            .FirstOrDefault(x => x.Color != Color.Default)?.Color ?? fallbackColor;
    }

    // TODO: Spoiler handling
    public static EmbedBuilder ToEmbedBuilderForce(this IEmbed embed)
    {
        var imageUrl = embed.Image?.Url;
        var thumbnailUrl = embed.Thumbnail?.Url;
        if (embed is { Type: EmbedType.Article, Thumbnail.Url: not null })
        {
            imageUrl = embed.Thumbnail?.Url;
            thumbnailUrl = null;
        }

        var builder = new EmbedBuilder
        {
            Author = new EmbedAuthorBuilder
            {
                Name = embed.Author?.Name,
                IconUrl = embed.Author?.IconUrl,
                Url = embed.Author?.Url
            },
            Color = embed.Color,
            Description = embed.Description,
            Footer = new EmbedFooterBuilder
            {
                Text = embed.Footer?.Text,
                IconUrl = embed.Footer?.IconUrl
            },
            ImageUrl = imageUrl,
            ThumbnailUrl = thumbnailUrl,
            Timestamp = embed.Timestamp,
            Title = embed.Title,
            Url = embed.Url
        };

        foreach (var field in embed.Fields)
            builder.AddField(field.Name, field.Value, field.Inline);

        return builder;
    }

    public static bool TryParseEmote(string text, [NotNullWhen(true)] out IEmote? emote)
    {
        if (Emote.TryParse(text, out var result))
        {
            emote = result;
            return true;
        }

        if (Emoji.TryParse(text, out var result2))
        {
            emote = result2;
            return true;
        }

        emote = null;
        return false;
    }
}
