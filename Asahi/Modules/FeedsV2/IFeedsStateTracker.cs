namespace Asahi.Modules.FeedsV2
{
    public interface IFeedsStateTracker
    {
        bool IsFirstTimeSeeingFeedSource(string feedSource);
        bool IsNewArticle(string feedSource, int articleId);
        bool IsNewArticle(ulong channelId, int articleId);
        string GetBestDefaultFeedTitle(string feedSource);
        string? GetCachedDefaultFeedTitle(string feedSource);
        object? GetFeedSourceContinuationToken(string feedSource);
        void SetFeedSourceContinuationToken(string feedSource, object? continuationToken);
        void UpdateDefaultFeedTitleCache(string feedSource, string title);
        void MarkArticleAsRead(ulong channelId, int articleId);
        void MarkArticleAsRead(string feedSource, int articleId);
        void PruneMissingArticles(IFeedProvider feedProvider);
        void PruneMissingFeeds(IEnumerable<string> feedSources);
        void ClearChannelArticleList();
    }
}
