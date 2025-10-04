using System.Buffers.Text;
using System.Text;
using Asahi.Modules.FeedsV2;
using Asahi.Modules.Models;
using Humanizer;
using JetBrains.Annotations;

namespace Asahi.Modules
{
    [Inject(ServiceLifetime.Singleton)]
    public class DanbooruUtility(BotConfig config, BotEmoteService emotes, IDanbooruApi danbooruApi)
    {
        private static readonly HashSet<string> KnownImageExtensions =
        [
            "jpg",
            "png",
            "webp",
        ];

        private static readonly HashSet<string> KnownVideoExtensions =
        [
            "zip",
            "mp4",
            "gif",
            "webm"
        ];

        [Pure]
        public async Task<MessageComponent> GetComponent(DanbooruPost post, Color embedColor, string feedTitle,
            bool extraInfoMode = false, bool forceFullSizeImage = false, ulong deletedByUserId = 0ul,
            CancellationToken cancellationToken = default)
        {
            var components = new ComponentBuilderV2();

            var container = new ContainerBuilder();

            // Do I do this as the same color as the rating icon?
            container.WithAccentColor(embedColor);

            // "Post by Author" text
            string? authors = null;
            if (!string.IsNullOrWhiteSpace(post.TagStringArtist))
            {
                authors = post.TagStringArtist.Split(' ')
                    .Select(x => $"[{x}](https://danbooru.donmai.us/posts?tags={x})")
                    .HumanizeStringArrayWithTruncation();
            }

            var characters = post.TagStringCharacter.Split(' ').Select(x => x.Titleize())
                .HumanizeStringArrayWithTruncation();

            var ratingEmote = post.Rating switch
            {
                DanbooruRating.General => emotes.DanbooruGeneral,
                DanbooruRating.Suggestive => emotes.DanbooruSuggestive,
                DanbooruRating.Questionable => emotes.DanbooruQuestionable,
                DanbooruRating.Explicit => emotes.DanbooruExplicit,
                _ => throw new NotSupportedException()
            };

            var postUrl = $"https://danbooru.donmai.us/posts/{post.Id}/";
            var titleString =
                $"### {ratingEmote}  [{(string.IsNullOrWhiteSpace(characters) ? "Post" : characters)}]({postUrl})";
            if (!string.IsNullOrWhiteSpace(authors))
            {
                titleString += $" by {authors}";
            }

            // Image/Video/Whatever

            // TODO: better handling around failing to find a variant
            var bestVariant = await GetBestVariantOrFallback(post, cancellationToken);
            if (bestVariant != null)
            {
                var footerText = new StringBuilder($"{post.MediaAsset.FileExtension.ToUpperInvariant()} file");
                if (bestVariant.Variant.Type != DanbooruVariantType.Original)
                {
                    footerText.Append(
                        $" • embed is {bestVariant.Variant.Type.ToReadableString()} quality ({bestVariant.Variant.FileExt.ToUpperInvariant()} file)");
                }

                if (extraInfoMode)
                {
                    footerText.Append("\n**Feed title:** ");
                }
                else
                {
                    footerText.Append(" • ");
                }

                footerText.Append($"{feedTitle}");

                // Will see if this is annoying or not
                var shouldSpoiler = post.Rating is DanbooruRating.Explicit or DanbooruRating.Questionable;
                if (extraInfoMode)
                {
                    var userRes = await danbooruApi.GetUser(post.UploaderId, cancellationToken);

                    var user = userRes.IsSuccessful ? userRes.Content : null;

                    var userInfo =
                        $"\n-# Uploaded by [{(user != null ? user.Name : post.UploaderId.ToString())}](https://danbooru.donmai.us/users/{post.UploaderId})";

                    if (user != null)
                    {
                        userInfo += $" ({user.PostUploadCount} posts)";
                    }

                    if (!forceFullSizeImage)
                    {
                        var extraInfoText = $"{footerText}\n**Posted on:** <t:{post.CreatedAt.ToUnixTimeSeconds()}>";

                        if (Uri.TryCreate(post.Source, UriKind.Absolute, out var sourceUri) &&
                            sourceUri.Scheme is "http" or "https")
                        {
                            var (platformName, sourceUrl, emote) = GetPlatformButtonInfo(post, sourceUri);
                            
                            extraInfoText += $"\n**Source:** {emote} [{platformName}]({sourceUrl})";
                        }

                        var section = new SectionBuilder()
                            .WithTextDisplay(titleString + userInfo)
                            .WithTextDisplay(extraInfoText)
                            .WithAccessory(new ThumbnailBuilder(bestVariant.Variant.Url, isSpoiler: shouldSpoiler));

                        container.WithSection(section);
                    }
                    else
                    {
                        var extraInfoText =
                            $"{titleString}{userInfo}\n{footerText}\n**Posted on:** <t:{post.CreatedAt.ToUnixTimeSeconds()}>";
                        
                        if (Uri.TryCreate(post.Source, UriKind.Absolute, out var sourceUri) &&
                            sourceUri.Scheme is "http" or "https")
                        {
                            var (platformName, sourceUrl, emote) = GetPlatformButtonInfo(post, sourceUri);
                            
                            extraInfoText += $"\n**Source:** {emote} [{platformName}]({sourceUrl})";
                        }

                        if (deletedByUserId != 0ul)
                        {
                            extraInfoText += $"\n-# Message deleted by <@{deletedByUserId}>";
                        }

                        container.WithTextDisplay(extraInfoText);
                    }
                }
                else
                {
                    container.WithTextDisplay(titleString);
                    container.WithSeparator(isDivider: false);
                }

                if (!extraInfoMode || forceFullSizeImage)
                {
                    if (bestVariant.ExtraUrls == null)
                    {
                        container.WithMediaGallery([
                            new MediaGalleryItemProperties(new UnfurledMediaItemProperties(bestVariant.Variant.Url),
                                isSpoiler: shouldSpoiler, description: footerText.ToString())
                        ]);
                    }
                    else
                    {
                        container.WithMediaGallery([
                            new MediaGalleryItemProperties(new UnfurledMediaItemProperties(bestVariant.Variant.Url),
                                isSpoiler: shouldSpoiler, description: footerText.ToString()),
                            ..bestVariant.ExtraUrls.Select(x =>
                                new MediaGalleryItemProperties(new UnfurledMediaItemProperties(x)))
                        ]);
                    }
                }
            }
            else
            {
                container.WithTextDisplay(titleString);
                container.WithSeparator(isDivider: false);
            }

            // Footer
            // container.WithSeparator();
            //
            // var footerText = $"-# ";
            //
            // footerText += $"{emotes.DanbooruLogo} {post.MediaAsset.FileExtension.ToUpperInvariant()} file";
            // if (bestVariant != null && bestVariant.Variant.Type != "original")
            // {
            //     footerText +=
            //         $" • embed is {bestVariant.Variant.Type} quality ({bestVariant.Variant.FileExt.ToUpperInvariant()} file)\n-# ";
            // }
            // else
            // {
            //     footerText += " • ";
            // }
            //
            // footerText += $"{feedTitle} • <t:{post.CreatedAt.ToUnixTimeSeconds()}>";
            //
            // container.WithTextDisplay(footerText);

            // container.WithTextDisplay($"-# <t:{post.CreatedAt.ToUnixTimeSeconds()}>");

            components.WithContainer(container);

            if (!extraInfoMode)
            {
                var sourceButton = CreatePlatformButton(post);

                var moreInfoData = new DanbooruModule.DanbooruExtraInfoData(
                    (uint)post.Id,
                    feedTitle, embedColor.RawValue);

                var moreInfoButton =
                    new ButtonBuilder()
                        .WithCustomId(StateSerializer.SerializeObject(moreInfoData,
                            ModulePrefixes.Danbooru.MoreInfoButton))
                        .WithEmote(emotes.DanbooruMoreInfo)
                        .WithStyle(ButtonStyle.Secondary);

                var deleteButton = new ButtonBuilder()
                    .WithCustomId(StateSerializer.SerializeObject(moreInfoData,
                        ModulePrefixes.Danbooru.DeleteButton))
                    .WithEmote(emotes.DanbooruDeletePost)
                    .WithStyle(ButtonStyle.Secondary);

                if (sourceButton != null)
                    components.WithActionRow([sourceButton, moreInfoButton, deleteButton]);
                else
                    components.WithActionRow([moreInfoButton, deleteButton]);
            }

            return components.Build();
        }

        private ButtonBuilder? CreatePlatformButton(DanbooruPost post)
        {
            if (!string.IsNullOrWhiteSpace(post.Source))
            {
                if (Uri.TryCreate(post.Source, UriKind.Absolute, out var sourceUri) &&
                    sourceUri.Scheme is "http" or "https")
                {
                    var (platformName, sourceUrl, buttonEmote) = GetPlatformButtonInfo(post, sourceUri);

                    if (platformName.Length >= 80)
                        platformName = "Source";

                    if (sourceUrl.Length < 512)
                    {
                        return new ButtonBuilder(platformName, url: sourceUrl, emote: buttonEmote,
                            style: ButtonStyle.Link);
                    }
                }
            }

            return null;
        }

        [Pure, Obsolete($"Use {nameof(GetComponent)} instead.")]
        public async IAsyncEnumerable<MessageContents> GetEmbeds(DanbooruPost post, Color embedColor,
            string feedTitle)
        {
            var eb = new EmbedBuilder();
            EmbedBuilder[]? extrasForMultiImage = null;
            string? videoUrl = null;

            eb.WithColor(embedColor);
            if (!string.IsNullOrWhiteSpace(post.TagStringArtist))
            {
                eb.WithAuthor(post.TagStringArtist.Split(' ').Humanize());
            }

            var footer = new EmbedFooterBuilder();
            footer.WithIconUrl(
                "https://danbooru.donmai.us/packs/static/danbooru-logo-128x128-ea111b6658173e847734.png"
            );
            if (!string.IsNullOrWhiteSpace(feedTitle))
            {
                footer.WithText($"{feedTitle} • Rating: {post.Rating}");
            }

            eb.WithFooter(footer);
            eb.WithTimestamp(post.CreatedAt);

            eb.WithTitle(
                !string.IsNullOrWhiteSpace(post.TagStringCharacter)
                    ? post.TagStringCharacter.Split(' ').Select(x => x.Titleize())
                        .HumanizeStringArrayWithTruncation()
                    : "Danbooru"
            );

            var url = $"https://danbooru.donmai.us/posts/{post.Id}/";
            eb.WithUrl(url);

            var bestVariant = await GetBestVariantOrFallback(post);
            if (bestVariant != null)
            {
                if (KnownVideoExtensions.Contains(bestVariant.Variant.FileExt))
                {
                    videoUrl = bestVariant.Variant.Url;
                }
                else
                {
                    eb.WithImageUrl(bestVariant.Variant.Url);
                }

                if (bestVariant.ExtraUrls != null)
                {
                    extrasForMultiImage =
                        bestVariant.ExtraUrls.Select(x => new EmbedBuilder().WithUrl(url).WithImageUrl(x))
                            .ToArray();
                }
            }

            if (bestVariant == null)
            {
                eb.WithDescription("No known image link found.");
            }
            else
            {
                eb.WithDescription(
                    $"{post.MediaAsset.FileExtension.ToUpperInvariant()} file | "
                    + $"embed is {bestVariant.Variant.Type} quality{(bestVariant.Variant.Type != DanbooruVariantType.Original ? $" ({bestVariant.Variant.FileExt.ToUpperInvariant()} file)" : "")}"
                );
            }

            var components = new ComponentBuilder();


            if (!string.IsNullOrWhiteSpace(post.Source))
            {
                if (Uri.TryCreate(post.Source, UriKind.Absolute, out var sourceUri) &&
                    sourceUri.Scheme is "http" or "https")
                {
                    var (platformName, sourceUrl, buttonEmote) = GetPlatformButtonInfo(post, sourceUri);

                    if (platformName.Length >= 80)
                        platformName = "Source";

                    if (sourceUrl.Length < 512)
                        components.WithButton(platformName, url: sourceUrl, emote: buttonEmote,
                            style: ButtonStyle.Link);
                }
            }

            if (extrasForMultiImage == null)
            {
                yield return new MessageContents(eb, components);
            }
            else
            {
                yield return new MessageContents("",
                    embeds: extrasForMultiImage.Prepend(eb).Select(x => x.Build()).ToArray(),
                    components: components);
            }

            if (videoUrl != null)
                yield return new MessageContents(videoUrl);
        }

        private (string PlatformName, string SourceUrl, IEmote? Emote) GetPlatformButtonInfo(DanbooruPost post,
            Uri sourceUri)
        {
            if (post.PixivId != null)
                return ("Pixiv", $"https://www.pixiv.net/artworks/{post.PixivId}", emotes.Pixiv);

            if (CompiledRegex.IsAFanboxLinkRegex().IsMatch(post.Source))
                return ("fanbox.cc", post.Source, emotes.FanboxCc);

            if (CompiledRegex.IsALofterLinkRegex().IsMatch(post.Source))
                return ("Lofter", post.Source, emotes.Lofter);

            var fantiaMatch = CompiledRegex.FantiaPostIdRegex().Match(post.Source);
            if (fantiaMatch.Success)
            {
                var id = fantiaMatch.Groups[1].Value;
                return ("Fantia", $"https://fantia.jp/posts/{id}", emotes.Fantia);
            }

            var host = sourceUri.Host.StartsWith("www.") ? sourceUri.Host[4..] : sourceUri.Host;

            return host switch
            {
                "x.com" or "twitter.com" => ("Twitter", post.Source, emotes.Twitter),
                "misskey.io" => ("Misskey.io", post.Source, emotes.Misskey),
                "baraag.net" => ("Baraag", post.Source, emotes.Baraag),
                "arca.live" => ("arca.live", post.Source, emotes.ArcaLive),
                "weibo.com" => ("Weibo", post.Source, emotes.Weibo),
                "yande.re" or "files.yande.re" => ("yande.re", post.Source, emotes.YandereLogo),
                "bilibili.com" or "t.bilibili.com" => ("bilibili", post.Source, emotes.Bilibili),
                "youtube.com" or "youtu.be" => ("YouTube", post.Source, emotes.YouTube),
                _ => (host, post.Source, null)
            };
        }

        [Pure]
        private DanbooruVariant? GetBestVariant(DanbooruVariant[]? variants)
        {
            if (variants == null)
                return null;

            // we only want embeddable variants
            var validVariants = variants
                .Where(v => KnownImageExtensions.Contains(v.FileExt.ToLower()) ||
                            KnownVideoExtensions.Contains(v.FileExt.ToLower()))
                .ToArray();

            // original is the ideal pick here
            var originalVariant = validVariants.FirstOrDefault(v => v.Type == DanbooruVariantType.Original);

            // to force GetBestVariantOrFallback's ugoria handling
            if (originalVariant is { FileExt: "zip" })
            {
                return null;
            }

            if (originalVariant != null)
            {
                if (KnownVideoExtensions.Contains(originalVariant.FileExt))
                {
                    originalVariant.Url = config.VideoProxyUrl.Replace("{{URL}}",
                        Base64Url.EncodeToString(System.Text.Encoding.UTF8.GetBytes(originalVariant.Url)));
                }
                
                return originalVariant;
            }

            // original doesn't exist/work, let's hope the rest of the options are ok
            var worseResFallback = validVariants.MaxBy(v => v.Width * v.Height);

            return worseResFallback;
        }

        [Pure]
        public ValueTask<DanbooruVariantWithExtras?> GetBestVariantOrFallback(DanbooruPost post,
            CancellationToken cancellationToken = default)
        {
            var bestVariant = GetBestVariant(post.MediaAsset.Variants);

            if (bestVariant != null)
                return ValueTask.FromResult<DanbooruVariantWithExtras?>(new DanbooruVariantWithExtras(bestVariant));

            return GetFallbackVariant(post.Source, cancellationToken);
        }

        [Pure, PublicAPI]
        public async ValueTask<DanbooruVariantWithExtras?> GetFallbackVariant(string sourceUrl,
            CancellationToken cancellationToken = default)
        {
            var fallbackPixivMatch = CompiledRegex.ValidPixivDirectImageUrlRegex().Match(sourceUrl);
            if (fallbackPixivMatch.Success)
            {
                var postId = fallbackPixivMatch.Groups[1].Value;
                var extension = fallbackPixivMatch.Groups[2].Value;
                if (extension == "zip")
                {
                    return new DanbooruVariantWithExtras(new DanbooruVariant
                    {
                        FileExt = "mp4", Height = 0, Width = 0, Type = DanbooruVariantType.FallbackPixiv,
                        Url = $"https://www.phixiv.net/i/ugoira/{postId}.mp4"
                    });
                }

                // dont think videos are returned by pixiv (they have ugoria) but
                if (KnownImageExtensions.Contains(extension) || KnownVideoExtensions.Contains(extension))
                {
                    var fallbackUrl = config.ProxyUrl.Replace("{{URL}}",
                        Base64Url.EncodeToString(System.Text.Encoding.UTF8.GetBytes(sourceUrl)));

                    return new DanbooruVariantWithExtras(new DanbooruVariant
                    {
                        FileExt = extension, Height = 0, Width = 0, Type = DanbooruVariantType.FallbackPixiv,
                        Url = fallbackUrl
                    });
                }
            }

            if (CompiledRegex.FantiaPostIdRegex().IsMatch(sourceUrl))
            {
                var extension = sourceUrl.Split('.')[^1];
                if (KnownImageExtensions.Contains(extension))
                {
                    var fallbackUrl = config.ProxyUrl.Replace("{{URL}}",
                        Base64Url.EncodeToString(System.Text.Encoding.UTF8.GetBytes(sourceUrl)));

                    return new DanbooruVariantWithExtras(new DanbooruVariant
                    {
                        FileExt = extension, Height = 0, Width = 0, Type = DanbooruVariantType.FallbackFantia,
                        Url = fallbackUrl
                    });
                }
            }

            var danbooruFallback = await danbooruApi.GetSource(sourceUrl, cancellationToken);

            if (danbooruFallback.Error != null)
                throw danbooruFallback.Error;

            if (!danbooruFallback.IsSuccessful || danbooruFallback.Content.IsMostLikelyUseless(sourceUrl))
                return null;

            var variant = new DanbooruVariantWithExtras(new DanbooruVariant()
            {
                FileExt = "???",
                Height = 0,
                Width = 0,
                Type = DanbooruVariantType.FallbackDanbooru,
                Url = danbooruFallback.Content.ImageUrls.First()
            })
            {
                ExtraUrls = danbooruFallback.Content.ImageUrls.Skip(1).ToArray()
            };

            return variant;
        }
    }
}
