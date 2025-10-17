using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules.FeedsV2;

[Inject(ServiceLifetime.Singleton)]
public class FeedsStateTracker(ILogger<FeedsStateTracker> logger)
{
    private readonly ConcurrentDictionary<int, HashSet<int>> seenArticleHashes = [];
    private readonly ConcurrentDictionary<ulong, HashSet<int>> seenArticleHashesChannel = [];
    private readonly ConcurrentDictionary<int, string> feedSourceToTitleDictionary = [];
    private readonly ConcurrentDictionary<int, object> feedSourceToContinuationTokenDictionary = [];

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
        return feedSourceToTitleDictionary.TryGetValue(feedSource.GetHashCode(), out var title) ? title : null;
    }

    /// <remarks>Provided for debugging purposes. Use <see cref="IsNewArticle"/> for all other cases.</remarks>
    [Pure]
    public IReadOnlyCollection<int>? GetSeenArticleIds(string feedSource)
    {
        return seenArticleHashes.TryGetValue(feedSource.GetHashCode(), out var articleHashes) ? articleHashes : null;
    }

    [Pure]
    public object? GetFeedSourceContinuationToken(string feedSource)
    {
        return feedSourceToContinuationTokenDictionary.GetValueOrDefault(feedSource.GetHashCode());
    }

    public void SetFeedSourceContinuationToken(string feedSource, object? continuationToken)
    {
        if (continuationToken == null)
            feedSourceToContinuationTokenDictionary.TryRemove(feedSource.GetHashCode(), out _);
        else
            feedSourceToContinuationTokenDictionary[feedSource.GetHashCode()] = continuationToken;
    }

    public void UpdateDefaultFeedTitleCache(string feedSource, string title)
    {
        feedSourceToTitleDictionary[feedSource.GetHashCode()] = title;
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
        var feedSourceHashCode = feedSource.GetHashCode();

        var articleHashes = seenArticleHashes.GetOrAdd(feedSourceHashCode, _ => []);

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

        if (feedSource == null || !seenArticleHashes.TryGetValue(feedSource.GetHashCode(), out var articleHashes))
        {
            return;
        }

        var prunableHashes = feedProvider.ListArticleIds().Where(x => articleHashes.All(y => y != x)).ToArray();

        if (prunableHashes.Length != 0)
        {
            logger.LogTrace("Pruning {prunableCount} articles from feed {feedSource} - {prunedArticles}",
                prunableHashes.Length, feedSource, prunableHashes);
        }

        foreach (var prunableHash in prunableHashes)
        {
            articleHashes.Remove(prunableHash);
        }
    }

    public void PruneMissingFeeds(IEnumerable<string> feedSources)
    {
        var validFeedHashes = new HashSet<int>(feedSources.Select(fs => fs.GetHashCode()));

        var obsoleteFeeds = seenArticleHashes.Keys.Where(feedHash => !validFeedHashes.Contains(feedHash)).ToList();

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
