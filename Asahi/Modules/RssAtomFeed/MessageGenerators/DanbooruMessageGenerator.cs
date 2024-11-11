using Asahi.Database.Models.Rss;
using Asahi.Modules.RssAtomFeed.Models;
using Humanizer;

namespace Asahi.Modules.RssAtomFeed
{
    public class DanbooruMessageGenerator(DanbooruPost[] posts, BotConfig config) : IEmbedGenerator
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

        public IEnumerable<MessageContents> GenerateFeedItemMessages(
            FeedListener feedListener,
            HashSet<int> seenArticles,
            HashSet<int> processedArticles,
            Color embedColor,
            bool shouldCreateEmbeds
        )
        {
            foreach (var post in posts)
            {
                processedArticles.Add(post.Id);

                if (seenArticles.Contains(post.Id))
                    continue;
                if (!shouldCreateEmbeds)
                    continue;

                yield return GenerateFeedItemMessage(feedListener, post, embedColor);
            }
        }

        private MessageContents GenerateFeedItemMessage(
            FeedListener? feedListener,
            DanbooruPost post,
            Color embedColor
        )
        {
            var eb = new EmbedBuilder();

            eb.WithColor(embedColor);
            if (!string.IsNullOrWhiteSpace(post.TagStringArtist))
            {
                eb.WithAuthor(post.TagStringArtist.Split(' ').Humanize());
            }

            var footer = new EmbedFooterBuilder();
            footer.WithIconUrl(
                "https://danbooru.donmai.us/packs/static/danbooru-logo-128x128-ea111b6658173e847734.png"
            );
            if (!string.IsNullOrWhiteSpace(feedListener?.FeedTitle))
            {
                footer.WithText($"{feedListener.FeedTitle} • Rating: {post.Rating}");
            }

            eb.WithFooter(footer);
            eb.WithTimestamp(post.CreatedAt);

            eb.WithTitle(
                !string.IsNullOrWhiteSpace(post.TagStringCharacter)
                    ? post.TagStringCharacter.Split(' ').Select(x => x.Titleize()).HumanizeStringArrayWithTruncation()
                    : "Danbooru"
            );

            eb.WithUrl($"https://danbooru.donmai.us/posts/{post.Id}/");

            var bestVariant = GetBestVariant(post.MediaAsset.Variants);
            if (bestVariant != null)
            {
                eb.WithImageUrl(bestVariant.Url);
            }

            eb.WithDescription(
                $"{post.MediaAsset.FileExtension.ToUpperInvariant()} file | "
                + $"embed is {bestVariant?.Type} quality{(bestVariant?.Type != "original" ? $" ({bestVariant?.FileExt.ToUpperInvariant()} file)" : "")}"
            );

            var components = new ComponentBuilder();

            if (post.PixivId != null)
            {
                QuotingHelpers.TryParseEmote(config.PixivEmote, out var pixivEmote);

                var pixivUrl = $"https://www.pixiv.net/artworks/{post.PixivId}";
                components.WithButton(
                    "Pixiv",
                    emote: pixivEmote,
                    url: pixivUrl,
                    style: ButtonStyle.Link
                );
            }
            else if (
                !string.IsNullOrWhiteSpace(post.Source)
                && CompiledRegex.GenericLinkRegex().IsMatch(post.Source)
            )
            {
                components.WithButton("Source", url: post.Source, style: ButtonStyle.Link);
            }

            return new MessageContents(eb, components);
        }

        public static DanbooruVariant? GetBestVariant(DanbooruVariant[]? variants)
        {
            if (variants == null)
                return null;
            
            // we only want embeddable variants
            var validVariants = variants
                .Where(v => KnownImageExtensions.Contains(v.FileExt.ToLower()))
                .ToArray();

            // original is the ideal pick here
            var originalVariant = validVariants.FirstOrDefault(v => v.Type == "original");

            if (originalVariant != null)
            {
                return originalVariant;
            }

            // original doesn't exist/work oh god lets just hope the rest of the options are ok
            return validVariants.MaxBy(v => v.Width * v.Height);
        }
    }
}
