using Asahi.Database.Models.Rss;
using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Feeds;

namespace Asahi.Modules.RssAtomFeed;

public class RssFeedMessageGenerator(Feed genericFeed, FeedItem[] feedItems) : IEmbedGeneratorAsync
{
    public IAsyncEnumerable<MessageContents> GenerateFeedItemMessages(
        FeedListener feedListener,
        HashSet<int> seenArticles,
        HashSet<int> processedArticles,
        Color embedColor,
        bool shouldCreateEmbeds
    ) => GenerateFeedItemMessagesSync(feedListener, seenArticles, processedArticles, embedColor, shouldCreateEmbeds).ToAsyncEnumerable();
    
    public IEnumerable<MessageContents> GenerateFeedItemMessagesSync(
        FeedListener feedListener,
        HashSet<int> seenArticles,
        HashSet<int> processedArticles,
        Color embedColor,
        bool shouldCreateEmbeds
    )
    {
        foreach (var feedItem in feedItems)
        {
            processedArticles.Add(feedItem.Id.GetHashCode(StringComparison.Ordinal));

            if (seenArticles.Contains(feedItem.Id.GetHashCode(StringComparison.Ordinal)))
                continue;
            if (!shouldCreateEmbeds)
                continue;

            yield return GenerateFeedItemEmbed(feedListener, feedItem, embedColor);
        }
    }

    public virtual MessageContents GenerateFeedItemEmbed(
        FeedListener feedListener,
        FeedItem genericItem,
        Color embedColor
    )
    {
        var eb = new EmbedBuilder();

        switch (genericFeed.Type)
        {
            case FeedType.Atom:
            {
                var feed = (AtomFeed)genericFeed.SpecificFeed;
                var item = (AtomFeedItem)genericItem.SpecificItem;

                var footer = new EmbedFooterBuilder();

                if (item.Author != null)
                {
                    eb.WithAuthor(
                        item.Author.ToString(),
                        url: !string.IsNullOrEmpty(item.Author.Uri) ? item.Author.Uri : null
                    );
                }

                if (!string.IsNullOrWhiteSpace(item.Summary))
                {
                    eb.WithDescription(item.Summary);
                }

                if (!string.IsNullOrWhiteSpace(item.Title))
                {
                    eb.WithTitle(item.Title);
                }

                if (!string.IsNullOrWhiteSpace(item.Link))
                {
                    eb.WithUrl(item.Link);
                }

                if (item.PublishedDate != null)
                {
                    eb.WithTimestamp(item.PublishedDate.Value);
                }
                else if (item.UpdatedDate != null)
                {
                    eb.WithTimestamp(item.UpdatedDate.Value);
                }

                // general feed stuff
                if (!string.IsNullOrWhiteSpace(feed.Icon))
                {
                    footer.IconUrl = feed.Icon;

                    // stupid ass bug
                    if (footer.IconUrl == "https://www.redditstatic.com/icon.png/")
                    {
                        footer.IconUrl = "https://www.redditstatic.com/icon.png";
                    }
                }

                if (!string.IsNullOrWhiteSpace(feedListener.FeedTitle))
                {
                    footer.Text = $"{feedListener.FeedTitle} • {item.Id}";
                }
                else if (!string.IsNullOrWhiteSpace(feed.Title))
                {
                    footer.Text = $"{feed.Title} • {item.Id}";
                }

                eb.WithFooter(footer);

                break;
            }
            case FeedType.Rss_1_0:
            case FeedType.Rss_2_0:
            case FeedType.MediaRss:
            case FeedType.Rss:
            case FeedType.Rss_0_91:
            case FeedType.Rss_0_92:
            {
                var footer = new EmbedFooterBuilder();

                if (!string.IsNullOrWhiteSpace(genericItem.Author))
                {
                    eb.WithAuthor(genericItem.Author);
                }

                if (!string.IsNullOrWhiteSpace(genericItem.Description))
                {
                    eb.WithDescription(genericItem.Description);
                }

                if (!string.IsNullOrWhiteSpace(genericItem.Title))
                {
                    eb.WithTitle(genericItem.Title);
                }

                if (!string.IsNullOrWhiteSpace(genericItem.Link))
                {
                    eb.WithUrl(genericItem.Link);
                }

                if (genericItem.PublishingDate.HasValue)
                {
                    eb.WithTimestamp(genericItem.PublishingDate.Value);
                }

                // general feed stuff
                if (!string.IsNullOrWhiteSpace(genericFeed.ImageUrl))
                {
                    eb.WithThumbnailUrl(genericFeed.ImageUrl);
                }

                if (!string.IsNullOrWhiteSpace(feedListener.FeedTitle))
                {
                    footer.Text = $"{feedListener.FeedTitle} • {genericItem.Id}";
                }
                else if (!string.IsNullOrWhiteSpace(genericFeed.Title))
                {
                    footer.Text = $"{genericFeed.Title} • {genericItem.Id}";
                }

                eb.WithFooter(footer);

                break;
            }
            case FeedType.Unknown:
            default:
                throw new NotSupportedException();
        }

        var thumbnail = genericItem
            .SpecificItem.Element.Descendants()
            .FirstOrDefault(x =>
                x.Name.LocalName == "content" && x.Attribute("type")?.Value == "xhtml"
            )
            ?.Descendants()
            .FirstOrDefault(x => x.Name.LocalName == "img")
            ?.Attributes()
            .FirstOrDefault(x => x.Name == "src")
            ?.Value;

        thumbnail ??= genericItem
            .SpecificItem.Element.Descendants()
            .FirstOrDefault(x =>
                x.Name.LocalName.Contains("thumbnail", StringComparison.InvariantCultureIgnoreCase)
            )
            ?.Attribute("url")
            ?.Value;

        if (!string.IsNullOrWhiteSpace(thumbnail))
        {
            eb.WithImageUrl(thumbnail);
        }

        eb.WithColor(embedColor);

        if (!string.IsNullOrWhiteSpace(eb.Title))
            eb.Title = eb.Title.Truncate(200);

        if (!string.IsNullOrWhiteSpace(eb.Description))
            eb.Description = eb.Description.Truncate(400);

        return new MessageContents(eb);
    }
}
