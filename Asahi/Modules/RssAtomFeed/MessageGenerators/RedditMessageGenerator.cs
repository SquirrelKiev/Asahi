using Asahi.Database.Models.Rss;
using Asahi.Modules.RssAtomFeed.Models;

namespace Asahi.Modules.RssAtomFeed;

public class RedditMessageGenerator(List<PostChild> posts) : IEmbedGeneratorAsync
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
        foreach (var post in posts.Select(x => x.Data))
        {
            processedArticles.Add(post.Id.GetHashCode());

            if (seenArticles.Contains(post.Id.GetHashCode()))
                continue;
            if (!shouldCreateEmbeds)
                continue;

            yield return GenerateFeedItemMessage(feedListener, post, embedColor);
        }
    }

    private MessageContents GenerateFeedItemMessage(
        FeedListener feedListener,
        Post post,
        Color embedColor
    )
    {
        // TODO: this is uber lazy, don't do this
        if (post.Spoiler)
            return new MessageContents($"|| https://www.rxddit.com{post.Permalink} ||");
        else
            return new MessageContents($"https://www.rxddit.com{post.Permalink}");

        //var eb = new EmbedBuilder();

        //eb.WithColor(embedColor);

        //var footer = new EmbedFooterBuilder();
        //// TODO: Customisable per feed :chatting:
        //footer.WithIconUrl("https://www.redditstatic.com/icon.png");
        //if (!string.IsNullOrWhiteSpace(feedListener?.FeedTitle))
        //{
        //    footer.WithText($"{feedListener.FeedTitle}");
        //}

        //eb.WithFooter(footer);
        //eb.WithTimestamp(DateTimeOffset.FromUnixTimeSeconds(post.CreatedUtc));

        //return new MessageContents();
    }
}
