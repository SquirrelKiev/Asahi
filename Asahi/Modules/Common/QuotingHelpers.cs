using System.Diagnostics.Contracts;
using System.Text;
using Asahi.Database.Models;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules;

public static class QuotingHelpers
{
    // Not the nuclear kind (I guess some of them could be considered pretty nuclear?)
    public const string ReactionsFieldName = "Reactors";

    public static List<MessageContents> QuoteMessage(IMessage message, Color embedColor, ILogger logger,
        bool webhookMode, bool spoilerAll = false, string spoilerContext = "")
    {
        var constantUrl = CatboxQts.GetRandomQtUrl();

        //var channelName = message.Channel is SocketThreadChannel threadChannel ? $"#{threadChannel.ParentChannel.Name} • " : "";

        var channelName = $"#{message.Channel.Name}";

        if (message.Channel is SocketThreadChannel threadChannel)
        {
            channelName += $" • #{threadChannel.ParentChannel.Name}";
        }

        var messageContent = message.Content;

        if (spoilerAll)
        {
            messageContent = messageContent.SpoilerMessage(spoilerContext);
        }

        var link = message.GetJumpUrl();
        var firstEmbed = new EmbedBuilder()
            .WithDescription(messageContent)
            .WithFooter(channelName)
            .WithTimestamp(message.Timestamp)
            .WithOptionalColor(embedColor)
            .WithUrl(constantUrl);

        if (!webhookMode)
        {
            firstEmbed.WithAuthor(message.Author);
        }

        List<EmbedBuilder> embeds = [firstEmbed];

        var queuedMessages = new List<MessageContents>();

        bool attachedImage = false;
        logger.LogTrace("There are {embedCount} embeds in this message.", message.Embeds.Count);

        int spoilerRichEmbeds = 0;
        foreach (var embed in message.Embeds)
        {
            //logger.LogTrace("embed: {embedJson}", JsonConvert.SerializeObject(embed, Formatting.Indented));

            switch (embed.Type)
            {
                case EmbedType.Image:
                    logger.LogTrace("Adding image embed.");

                    if (spoilerAll)
                    {
                        firstEmbed.Description += $"\n[[Spoiler Image]]({embed.Url})";
                    }
                    else
                    {
                        HandleImageEmbed(embed.Url, embeds, embedColor, ref attachedImage, constantUrl);
                    }
                    break;
                case EmbedType.Video:
                case EmbedType.Gifv:
                    logger.LogTrace("Queued video link message.");
                    if (spoilerAll)
                    {
                        firstEmbed.Description += $"\n[[Spoiler Video]]({embed.Url})";
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
                    if (!spoilerAll)
                        embeds.Add(embed.ToEmbedBuilderForce());
                    else
                        spoilerRichEmbeds++;
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
                    firstEmbed.Description += $"\n[[Spoiler Image]]({attachment.Url})";
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
                    firstEmbed.Description += $"\n[[Spoiler Video]]({attachment.Url})";
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
                if (!tooManyAttachments && attachmentsValueContents.Length + txt.Length > 1024 - tooManyAttachmentsText.Length)
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

        queuedMessages.Insert(0, new MessageContents(link, embeds.Take(10).Select(x => x.Build()).ToArray(), null));

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

    public static async ValueTask<Color> GetQuoteEmbedColor(EmbedColorSource colorSource, Color fallbackColor, IGuildUser? embedAuthor, DiscordSocketClient client)
    {
        Color embedColor = fallbackColor;

        switch (colorSource)
        {
            case EmbedColorSource.UsersRoleColor:
                embedColor = embedAuthor?.RoleIds.Select(x => embedAuthor.Guild.GetRole(x))
                    .OrderByDescending(x => x.Position)
                    .FirstOrDefault(x => x.Color != Color.Default)?.Color ?? embedColor;
                break;
            case EmbedColorSource.UsersBannerColor:
                if (embedAuthor != null)
                {
                    var restUser = await client.Rest.GetUserAsync(embedAuthor.Id);

                    embedColor = restUser.BannerColor ?? embedColor;
                }
                break;
            case EmbedColorSource.UsersAccentColor:
                if (embedAuthor != null)
                {
                    var restUser = await client.Rest.GetUserAsync(embedAuthor.Id);

                    embedColor = restUser.AccentColor ?? embedColor;
                }
                break;
            case EmbedColorSource.AlwaysUseFallbackColor:
            default:
                break;
        }

        return embedColor;
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

        var builder = new EmbedBuilder();
        builder.Author = new EmbedAuthorBuilder
        {
            Name = embed.Author?.Name,
            IconUrl = embed.Author?.IconUrl,
            Url = embed.Author?.Url
        };
        builder.Color = embed.Color;
        builder.Description = embed.Description;
        builder.Footer = new EmbedFooterBuilder
        {
            Text = embed.Footer?.Text,
            IconUrl = embed.Footer?.IconUrl
        };
        builder.ImageUrl = imageUrl;
        builder.ThumbnailUrl = thumbnailUrl;
        builder.Timestamp = embed.Timestamp;
        builder.Title = embed.Title;
        builder.Url = embed.Url;

        foreach (var field in embed.Fields)
            builder.AddField(field.Name, field.Value, field.Inline);

        return builder;
    }
}