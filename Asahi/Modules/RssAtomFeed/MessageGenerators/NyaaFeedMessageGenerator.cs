using System.Xml.Linq;
using Asahi.Database.Models.Rss;
using CodeHollow.FeedReader;

namespace Asahi.Modules.RssAtomFeed;

public class NyaaFeedMessageGenerator(Feed genericFeed, FeedItem[] feedItems) : IEmbedGenerator
{
    public IEnumerable<MessageContents> GenerateFeedItemMessages(
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

    public MessageContents GenerateFeedItemEmbed(
        FeedListener feedListener,
        FeedItem genericItem,
        Color embedColor
    )
    {
        var eb = new EmbedBuilder();

        var nyaaUrlMatch = CompiledRegex.NyaaATagRegex().Match(genericItem.Description);
        if (!nyaaUrlMatch.Groups[1].Success)
        {
            throw new InvalidDataException($"Could not find nyaa url <a> tag in description. {genericItem.Description}");
        }

        var nyaaUrl = nyaaUrlMatch.Groups[1].Value;

        eb.WithTitle(genericItem.Title.Truncate(256));
        eb.WithUrl(nyaaUrl);

        XNamespace ns = "https://nyaa.si/xmlns/nyaa";

        eb.WithFields(
            new EmbedFieldBuilder()
                .WithName("Size")
                .WithValue(genericItem.SpecificItem.Element.Element(ns + "size")!.Value)
                .WithIsInline(true),
            new EmbedFieldBuilder()
                .WithName("Category")
                .WithValue(genericItem.SpecificItem.Element.Element(ns + "category")!.Value)
                .WithIsInline(true)
        );

        var footer = new EmbedFooterBuilder();

        if (!string.IsNullOrWhiteSpace(feedListener.FeedTitle))
        {
            footer.Text = $"{feedListener.FeedTitle} • {genericItem.Id}";
        }
        else if (!string.IsNullOrWhiteSpace(genericFeed.Title))
        {
            footer.Text = $"{genericFeed.Title} • {genericItem.Id}";
        }

        if (genericItem.PublishingDate.HasValue)
        {
            eb.WithTimestamp(genericItem.PublishingDate.Value);
        }

        footer.WithIconUrl("https://nyaa.si/static/favicon.png");

        eb.WithFooter(footer);
        eb.WithOptionalColor(embedColor);

        var cb = new ComponentBuilder();
        
        cb.WithButton(label: "Torrent File", style: ButtonStyle.Link, url: genericItem.Link);
        
        return new MessageContents(eb, cb);
    }
}
