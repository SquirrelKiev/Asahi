using System.Diagnostics;
using System.Xml.Linq;
using CodeHollow.FeedReader;

namespace Asahi.Modules.FeedsV2.FeedProviders;

public class NyaaFeedProvider(HttpClient client) : RssFeedProvider(client)
{
    public override string DefaultFeedTitle
    {
        get
        {
            if (genericFeed == null) return "Nyaa Feed";

            return genericFeed.Title;
        }
    }

    protected override MessageContents ArticleToMessageContents(FeedItem genericItem, Color embedColor, string? feedTitle)
    {
        Debug.Assert(genericFeed != null);
        
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

        footer.Text = $"{(!string.IsNullOrWhiteSpace(feedTitle) ? feedTitle : DefaultFeedTitle)} • {genericItem.Id}";

        if (genericItem.PublishingDate.HasValue)
        {
            eb.WithTimestamp(genericItem.PublishingDate.Value);
        }

        footer.WithIconUrl("https://nyaa.si/static/favicon.png");

        eb.WithFooter(footer);
        eb.WithOptionalColor(embedColor);

        var cb = new ComponentBuilder();

        if (genericItem.Link.StartsWith("https://nyaa.si/"))
        {
            cb.WithButton(label: "Torrent File", style: ButtonStyle.Link, url: genericItem.Link);
        }
        // else if (genericItem.Link.StartsWith("magnet:?"))
        // {
        //     cb.WithButton(label: "Magnet Link", style: ButtonStyle.Link, url: GenerateMagnetRedirectLink(genericItem.Link));
        // }
        
        return new MessageContents(eb, cb);
    }

    // not used because the links were too long for Discord (512 char max)
    // TODO: maybe try compressing the link?
    // private string GenerateMagnetRedirectLink(string magnetLink)
    // {
    //     var uriBuilder = new UriBuilder(config.MagnetRedirectorBaseUrl);
    //     var base64Magnet = Convert.ToBase64String(Encoding.UTF8.GetBytes(magnetLink));
    //     uriBuilder.Path = $"/v1/magnet/{base64Magnet}";
    //
    //     return uriBuilder.ToString();
    // }
}
