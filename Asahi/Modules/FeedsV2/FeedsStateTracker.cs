using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules.FeedsV2;

[Inject(ServiceLifetime.Singleton)]
public class FeedsStateTracker(ILogger<FeedsStateTracker> logger)
{
    private readonly ConcurrentDictionary<string, HashSet<int>> seenArticleHashes = [];
    private readonly ConcurrentDictionary<ulong, HashSet<int>> seenArticleHashesChannel = [];
    private readonly ConcurrentDictionary<string, string> feedSourceToTitleDictionary = [];
    private readonly ConcurrentDictionary<string, object> feedSourceToContinuationTokenDictionary = [];

    [Pure]
    public bool IsFirstTimeSeeingFeedSource(string feedSource)
    {
        return !seenArticleHashes.ContainsKey(feedSource);
    }

    [Pure]
    public bool IsNewArticle(string feedSource, int articleId)
    {
        if (!seenArticleHashes.TryGetValue(feedSource, out var articleHashes))
        {
            return true;
        }

        return !articleHashes.Contains(articleId);
    }
    
    [Pure]
    public bool IsNewArticle(ulong channelId, int articleId)
    {
        if (!seenArticleHashesChannel.TryGetValue(channelId, out var articleHashes))
        {
            return true;
        }

        return !articleHashes.Contains(articleId);
    }

    [Pure]
    public string GetBestDefaultFeedTitle(string feedSource)
    {
        return GetCachedDefaultFeedTitle(feedSource) ?? feedSource;
    }

    [Pure]
    public string? GetCachedDefaultFeedTitle(string feedSource)
    {
        return feedSourceToTitleDictionary.TryGetValue(feedSource, out var title) ? title : null;
    }

    // /// <remarks>Provided for debugging purposes. Use <see cref="IsNewArticle"/> for all other cases.</remarks>
    // [Pure]
    // public IReadOnlyCollection<int>? GetSeenArticleIds(string feedSource)
    // {
    //     return seenArticleHashes.TryGetValue(feedSource.GetHashCode(), out var articleHashes) ? articleHashes : null;
    // }

    [Pure]
    public object? GetFeedSourceContinuationToken(string feedSource)
    {
        return feedSourceToContinuationTokenDictionary.GetValueOrDefault(feedSource);
    }

    public void SetFeedSourceContinuationToken(string feedSource, object? continuationToken)
    {
        if (continuationToken == null)
            feedSourceToContinuationTokenDictionary.TryRemove(feedSource, out _);
        else
            feedSourceToContinuationTokenDictionary[feedSource] = continuationToken;
    }

    public void UpdateDefaultFeedTitleCache(string feedSource, string title)
    {
        feedSourceToTitleDictionary[feedSource] = title;
    }

    public void MarkArticleAsRead(ulong channelId, int articleId)
    {
        var channelArticleHashes = seenArticleHashesChannel.GetOrAdd(channelId, _ => []);

        if (channelArticleHashes.Add(articleId))
        {
            logger.LogTrace("Marked article {articleId} as read for channel {channelId}.", articleId, channelId);
        }
        else
        {
            logger.LogTrace("Already seen article {articleId} for channel {channelId}, not marking as read.", articleId,
                channelId);
        }
    }

    public void MarkArticleAsRead(string feedSource, int articleId)
    {
        var articleHashes = seenArticleHashes.GetOrAdd(feedSource, _ => []);

        if (articleHashes.Add(articleId))
        {
            logger.LogTrace("Marked article {articleId} as read for feed {feedSource}.", articleId, feedSource);
        }
        else
        {
            logger.LogTrace("Already seen article {articleId} for feed {feedSource}, not marking as read.", articleId,
                feedSource);
        }
    }

    public void PruneMissingArticles(IFeedProvider feedProvider)
    {
        var feedSource = feedProvider.FeedSource;

        if (feedSource == null || !seenArticleHashes.TryGetValue(feedSource, out var articleHashes))
        {
            return;
        }

        var newHashes = feedProvider.ListArticleIds().ToList();
        var prunableHashes = articleHashes.Where(x => !newHashes.Contains(x)).ToList();

        if (prunableHashes.Count != 0)
        {
            logger.LogTrace("Pruning {prunableCount} articles from feed {feedSource} - {prunedArticles}",
                prunableHashes.Count, feedSource, prunableHashes);
        }

        foreach (var prunableHash in prunableHashes)
        {
            articleHashes.Remove(prunableHash);
        }
    }

    public void PruneMissingFeeds(IEnumerable<string> feedSources)
    {
        var validFeedSources = new HashSet<string>(feedSources.Select(fs => fs));

        var obsoleteFeeds = seenArticleHashes.Keys.Where(feedHash => !validFeedSources.Contains(feedHash)).ToList();

        if (obsoleteFeeds.Count != 0)
        {
            logger.LogTrace("Pruning {prunableCount} feeds - {prunedArticles}", obsoleteFeeds.Count, obsoleteFeeds);
        }

        foreach (var feedHash in obsoleteFeeds)
        {
            seenArticleHashes.Remove(feedHash, out _);
            feedSourceToTitleDictionary.Remove(feedHash, out _);
            feedSourceToContinuationTokenDictionary.Remove(feedHash, out _);
        }
    }

    public void ClearChannelArticleList()
    {
        seenArticleHashesChannel.Clear();
    }
}
