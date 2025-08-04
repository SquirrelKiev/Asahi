using System.Diagnostics;
using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Feeds;
using CodeHollow.FeedReader.Parser;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules.FeedsV2.FeedProviders;

public class RssFeedProvider(HttpClient client) : IFeedProvider
{
    public string? FeedSource { get; private set; }

    public virtual string DefaultFeedTitle
    {
        get
        {
            if (genericFeed == null) return "RSS Feed";

            return genericFeed.Title;
        }
    }

    protected Feed? genericFeed;

    // TODO: This should use FeedReader.ParseFeedUrlsFromHtml() or smth
    public async Task<bool> Initialize(string feedSource, CancellationToken cancellationToken = default)
    {
        FeedSource = feedSource;

        var req = await client.GetAsync(feedSource, cancellationToken);

        var reqContent = await req.Content.ReadAsStringAsync(cancellationToken);
        
        var feed = FeedReader.ReadFromString(reqContent);

        if (feed?.Type == FeedType.Unknown)
            throw new FeedTypeNotSupportedException();

        genericFeed = feed;

        return true;
    }
    public IEnumerable<int> ListArticleIds()
    {
        Debug.Assert(genericFeed != null);
        
        return genericFeed.Items.Select(x => x.Id.GetHashCode());
    }

    public IAsyncEnumerable<MessageContents> GetArticleMessageContent(int articleId, Color embedColor, string? feedTitle, CancellationToken cancellationToken = default)
    {
        Debug.Assert(genericFeed != null);
        
        var article = genericFeed.Items.First(x => x.Id.GetHashCode() == articleId);

        IEnumerable<MessageContents> enumerable = [ArticleToMessageContents(article, embedColor, feedTitle)];

        return enumerable.ToAsyncEnumerable();
    }

    protected virtual MessageContents ArticleToMessageContents(FeedItem genericItem, Color embedColor, string? feedTitle)
    {
        Debug.Assert(genericFeed != null);
        
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

                if (!string.IsNullOrWhiteSpace(feedTitle))
                {
                    footer.Text = $"{feedTitle} • {item.Id}";
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

                if (!string.IsNullOrWhiteSpace(feedTitle))
                {
                    footer.Text = $"{feedTitle} • {genericItem.Id}";
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
