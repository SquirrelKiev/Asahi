using System.Diagnostics.Contracts;

namespace Asahi.Modules.FeedsV2;

[Inject(ServiceLifetime.Singleton)]
public class FeedsStateTracker
{
    private readonly Dictionary<int, HashSet<int>> seenArticleHashes = [];
    private readonly Dictionary<int, string> feedSourceToTitleDictionary = []; // TODO

    [Pure]
    public bool IsFirstTimeSeeingFeedSource(string feedSource)
    {
        return !seenArticleHashes.ContainsKey(feedSource.GetHashCode());
    }
    
    [Pure]
    public bool IsNewArticle(string feedSource, int articleId)
    {
        if (!seenArticleHashes.TryGetValue(feedSource.GetHashCode(), out var articleHashes))
        {
            return true;
        }

        return !articleHashes.Contains(articleId);
    }

    public void MarkArticleAsRead(string feedSource, int articleId)
    {
        var feedSourceHashCode = feedSource.GetHashCode();
        
        if (!seenArticleHashes.TryGetValue(feedSourceHashCode, out var articleHashes))
        {
            articleHashes = new HashSet<int>();
            
            seenArticleHashes.Add(feedSourceHashCode, articleHashes);
        }

        articleHashes.Add(articleId);
    }

    public void PruneMissingArticles(IFeedProvider feedProvider)
    {
        var feedSource = feedProvider.FeedSource;

        if (feedSource == null || !seenArticleHashes.TryGetValue(feedSource.GetHashCode(), out var articleHashes))
        {
            return;
        }

        var prunableHashes = feedProvider.ListArticleIds().Where(x => articleHashes.All(y => y != x)).ToArray();

        foreach (var prunableHash in prunableHashes)
        {
            articleHashes.Remove(prunableHash);
        }
    }

    public void PruneMissingFeeds(IEnumerable<string> feedSources)
    {
        var validFeedHashes = new HashSet<int>(feedSources.Select(fs => fs.GetHashCode()));

        var obsoleteFeeds = seenArticleHashes.Keys.Where(feedHash => !validFeedHashes.Contains(feedHash)).ToList();

        foreach (var feedHash in obsoleteFeeds)
        {
            seenArticleHashes.Remove(feedHash);
        }
    }
}
