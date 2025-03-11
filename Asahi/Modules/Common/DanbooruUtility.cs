using Asahi.Modules.Models;
using Humanizer;

namespace Asahi.Modules
{
    [Inject(ServiceLifetime.Singleton)]
    public class DanbooruUtility(BotConfig config, IFxTwitterApi fxTwitterApi)
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
            "mp4"
        ];

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

            var bestVariant = await GetBestVariantOrFallback(post, config, fxTwitterApi);
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

            if (!string.IsNullOrWhiteSpace(post.Source) &&
                (post.Source.StartsWith("http://") || post.Source.StartsWith("https://")))
            {
                IEmote? buttonEmote = null;
                string platformName = "Source";
                string sourceUrl = post.Source;

                if (post.PixivId != null)
                {
                    QuotingHelpers.TryParseEmote(config.PixivEmote, out var pixivEmote);

                    sourceUrl = $"https://www.pixiv.net/artworks/{post.PixivId}";
                    buttonEmote = pixivEmote;
                    platformName = "Pixiv";
                }
                else if (CompiledRegex.TwitterStatusIdRegex().IsMatch(post.Source))
                {
                    QuotingHelpers.TryParseEmote(config.TwitterEmote, out var emote);

                    buttonEmote = emote;
                    platformName = "Twitter";
                }
                else if (CompiledRegex.IsAFanboxLinkRegex().IsMatch(post.Source))
                {
                    QuotingHelpers.TryParseEmote(config.FanboxCcEmote, out var emote);

                    buttonEmote = emote;
                    platformName = "fanbox.cc";
                }
                else if (CompiledRegex.FantiaPostIdRegex().IsMatch(post.Source))
                {
                    var id = CompiledRegex.FantiaPostIdRegex().Match(post.Source).Groups[1].Value;

                    QuotingHelpers.TryParseEmote(config.FantiaEmote, out var emote);

                    sourceUrl = $"https://fantia.jp/posts/{id}";
                    buttonEmote = emote;
                    platformName = "Fantia";
                }
                else if (post.Source.StartsWith("https://baraag.net"))
                {
                    QuotingHelpers.TryParseEmote(config.BaraagEmote, out var emote);

                    buttonEmote = emote;
                    platformName = "Baraag";
                }
                else if (post.Source.StartsWith("https://arca.live/"))
                {
                    QuotingHelpers.TryParseEmote(config.ArcaLiveEmote, out var emote);

                    buttonEmote = emote;
                    platformName = "arca.live";
                }

                components.WithButton(platformName, url: sourceUrl, emote: buttonEmote, style: ButtonStyle.Link);
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

        private static DanbooruVariant? GetBestVariant(DanbooruVariant[]? variants, int? pixivId = null)
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
            if (originalVariant != null && originalVariant.FileExt == "zip")
            {
                return null;
            }

            if (originalVariant != null)
            {
                return originalVariant;
            }

            // original doesn't exist/work oh god lets just hope the rest of the options are ok
            var worseResFallback = validVariants.MaxBy(v => v.Width * v.Height);

            return worseResFallback;
        }

        public static async ValueTask<DanbooruVariantWithFallback?> GetBestVariantOrFallback(DanbooruPost post,
            BotConfig config, IFxTwitterApi fxTwitterApi)
        {
            var bestVariant = GetBestVariant(post.MediaAsset.Variants);

            if (bestVariant != null)
                return new DanbooruVariantWithFallback(bestVariant);

            var fallbackPixivMatch = CompiledRegex.ValidPixivDirectImageUrlRegex().Match(post.Source);
            if (fallbackPixivMatch.Success)
            {
                var postId = fallbackPixivMatch.Groups[1].Value;
                var extension = fallbackPixivMatch.Groups[2].Value;
                if (extension == "zip")
                {
                    return new DanbooruVariantWithFallback(new DanbooruVariant
                    {
                        FileExt = "mp4", Height = 0, Width = 0, Type = "fallback (pixiv)",
                        Url = $"https://www.phixiv.net/i/ugoira/{postId}.mp4"
                    });
                }

                // not sure if videos are returned by pixiv but
                if (KnownImageExtensions.Contains(extension) || KnownVideoExtensions.Contains(extension))
                {
                    var fallbackUrl = config.ProxyUrl.Replace("{{URL}}",
                        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(post.Source)));

                    return new DanbooruVariantWithFallback(new DanbooruVariant
                        { FileExt = extension, Height = 0, Width = 0, Type = "fallback (pixiv)", Url = fallbackUrl });
                }
            }

            var fallbackTwitterMatch = CompiledRegex.TwitterStatusIdRegex().Match(post.Source);
            if (fallbackTwitterMatch.Success)
            {
                var id = ulong.Parse(fallbackTwitterMatch.Groups[1].Value);

                var status = await fxTwitterApi.GetStatusInfo(id);

                if (status is { Code: 200, Tweet: not null } && status.Tweet.Media.Photos.Length != 0)
                {
                    var firstImg = status.Tweet.Media.Photos[0];

                    var extraUrls = status.Tweet.Media.Photos.Length > 1
                        ? status.Tweet.Media.Photos.Skip(1).Select(x => x.Url + "?name=orig").ToArray()
                        : null;

                    return new DanbooruVariantWithFallback(new DanbooruVariant()
                    {
                        FileExt = firstImg.Url.Split('.')[^1],
                        Height = firstImg.Height,
                        Width = firstImg.Width,
                        Type = "fallback (twitter)",
                        Url = firstImg.Url + "?name=orig"
                    })
                    {
                        ExtraUrls = extraUrls
                    };
                }
            }

            if (CompiledRegex.FantiaPostIdRegex().IsMatch(post.Source))
            {
                var extension = post.Source.Split('.')[^1];
                if (KnownImageExtensions.Contains(extension))
                {
                    var fallbackUrl = config.ProxyUrl.Replace("{{URL}}",
                        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(post.Source)));

                    return new DanbooruVariantWithFallback(new DanbooruVariant
                        { FileExt = extension, Height = 0, Width = 0, Type = "fallback (fantia)", Url = fallbackUrl });
                }
            }

            // even the fallbacks has failed us
            return null;
        }
    }
}
