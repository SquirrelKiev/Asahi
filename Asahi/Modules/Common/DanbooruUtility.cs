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
            "jpeg",
            "png",
            "gif",
            "bmp",
            "webp",
        ];

        private static readonly HashSet<string> KnownVideoExtensions =
        [
            "zip",
            "mp4",
            "webm"
        ];

        [Pure]
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
                    ? post.TagStringCharacter.Split(' ').Select(x => x.Titleize()).HumanizeStringArrayWithTruncation()
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
                    + $"embed is {bestVariant.Variant.Type} quality{(bestVariant.Variant.Type != "original" ? $" ({bestVariant.Variant.FileExt.ToUpperInvariant()} file)" : "")}"
                );
            }

            var components = new ComponentBuilder();


            if (!string.IsNullOrWhiteSpace(post.Source))
            {
                if (Uri.TryCreate(post.Source, UriKind.Absolute, out var sourceUri) &&
                    sourceUri.Scheme is "http" or "https")
                {
                    IEmote? buttonEmote = null;
                    string platformName = sourceUri.Host;
                    string sourceUrl = post.Source;

                    if (post.PixivId != null)
                    {
                        buttonEmote = emotes.Pixiv;

                        sourceUrl = $"https://www.pixiv.net/artworks/{post.PixivId}";
                        platformName = "Pixiv";
                    }
                    else if (CompiledRegex.TwitterStatusIdRegex().IsMatch(post.Source))
                    {
                        buttonEmote = emotes.Twitter;

                        platformName = "Twitter";
                    }
                    else if (CompiledRegex.IsAFanboxLinkRegex().IsMatch(post.Source))
                    {
                        buttonEmote = emotes.FanboxCc;

                        platformName = "fanbox.cc";
                    }
                    else if (CompiledRegex.FantiaPostIdRegex().IsMatch(post.Source))
                    {
                        var id = CompiledRegex.FantiaPostIdRegex().Match(post.Source).Groups[1].Value;

                        buttonEmote = emotes.Fantia;

                        sourceUrl = $"https://fantia.jp/posts/{id}";
                        platformName = "Fantia";
                    }
                    else if (sourceUri.Host is "baraag.net")
                    {
                        buttonEmote = emotes.Baraag;

                        platformName = "Baraag";
                    }
                    else if (sourceUri.Host is "arca.live")
                    {
                        buttonEmote = emotes.ArcaLive;

                        platformName = "arca.live";
                    }
                    else if (CompiledRegex.MisskeyNoteRegex().IsMatch(post.Source))
                    {
                        buttonEmote = emotes.Misskey;

                        platformName = "Misskey.io";
                    }

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
                    embeds: extrasForMultiImage.Prepend(eb).Select(x => x.Build()).ToArray(), components: components);
            }

            if (videoUrl != null)
                yield return new MessageContents(videoUrl);
        }

        [Pure]
        private static DanbooruVariant? GetBestVariant(DanbooruVariant[]? variants)
        {
            if (variants == null)
                return null;

            // we only want embeddable variants
            var validVariants = variants
                .Where(v => KnownImageExtensions.Contains(v.FileExt.ToLower()) ||
                            KnownVideoExtensions.Contains(v.FileExt.ToLower()))
                .ToArray();

            // original is the ideal pick here
            var originalVariant = validVariants.FirstOrDefault(v => v.Type == "original");

            // to force GetBestVariantOrFallback's ugoria handling
            if (originalVariant is { FileExt: "zip" })
            {
                return null;
            }

            if (originalVariant != null)
            {
                return originalVariant;
            }

            // original doesn't exist/work, let's hope the rest of the options are ok
            var worseResFallback = validVariants.MaxBy(v => v.Width * v.Height);

            return worseResFallback;
        }

        [Pure]
        public ValueTask<DanbooruVariantWithExtras?> GetBestVariantOrFallback(DanbooruPost post)
        {
            var bestVariant = GetBestVariant(post.MediaAsset.Variants);

            if (bestVariant != null)
                return ValueTask.FromResult<DanbooruVariantWithExtras?>(new DanbooruVariantWithExtras(bestVariant));

            return GetFallbackVariant(post.Source);
        }

        [Pure, PublicAPI]
        public async ValueTask<DanbooruVariantWithExtras?> GetFallbackVariant(string sourceUrl)
        {
            // TODO: Migrate this to use https://danbooru.donmai.us/source
            var fallbackPixivMatch = CompiledRegex.ValidPixivDirectImageUrlRegex().Match(sourceUrl);
            if (fallbackPixivMatch.Success)
            {
                var postId = fallbackPixivMatch.Groups[1].Value;
                var extension = fallbackPixivMatch.Groups[2].Value;
                if (extension == "zip")
                {
                    return new DanbooruVariantWithExtras(new DanbooruVariant
                    {
                        FileExt = "mp4", Height = 0, Width = 0, Type = "fallback (pixiv)",
                        Url = $"https://www.phixiv.net/i/ugoira/{postId}.mp4"
                    });
                }

                // dont think videos are returned by pixiv (they have ugoria) but
                if (KnownImageExtensions.Contains(extension) || KnownVideoExtensions.Contains(extension))
                {
                    var fallbackUrl = config.ProxyUrl.Replace("{{URL}}",
                        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(sourceUrl)));

                    return new DanbooruVariantWithExtras(new DanbooruVariant
                        { FileExt = extension, Height = 0, Width = 0, Type = "fallback (pixiv)", Url = fallbackUrl });
                }
            }

            if (CompiledRegex.FantiaPostIdRegex().IsMatch(sourceUrl))
            {
                var extension = sourceUrl.Split('.')[^1];
                if (KnownImageExtensions.Contains(extension))
                {
                    var fallbackUrl = config.ProxyUrl.Replace("{{URL}}",
                        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(sourceUrl)));

                    return new DanbooruVariantWithExtras(new DanbooruVariant
                        { FileExt = extension, Height = 0, Width = 0, Type = "fallback (fantia)", Url = fallbackUrl });
                }
            }

            var danbooruFallback = await danbooruApi.GetSource(sourceUrl);

            if (danbooruFallback.Error != null)
                throw danbooruFallback.Error;

            if (!danbooruFallback.IsSuccessful || danbooruFallback.Content.IsMostLikelyUseless(sourceUrl))
                return null;

            var variant = new DanbooruVariantWithExtras(new DanbooruVariant()
            {
                FileExt = "???",
                Height = 0,
                Width = 0,
                Type = "fallback (danbooru source)",
                Url = danbooruFallback.Content.ImageUrls.First()
            })
            {
                ExtraUrls = danbooruFallback.Content.ImageUrls.Skip(1).ToArray()
            };

            return variant;
        }
    }
}
