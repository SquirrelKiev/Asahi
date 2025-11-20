using System.Diagnostics.Contracts;

namespace Asahi.Modules.FeedsV2;

public interface IFeedProvider
{
    public string? FeedSource { get; }
    public string DefaultFeedTitle { get; }
    
    public ArticleIdScope ArticleIdScope { get; }

    // TODO: switch to a result type
    /// <summary>
    /// Gets the data from the provided feed.
    /// </summary>
    public Task<bool> Initialize(string feedSource, object? continuationToken = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Returns a token that represents the current "position" in the feed. Used by <see cref="Initialize"/> for avoiding fetching redundant data that we know has already been seen before. Optional.
    /// </summary>
    /// <returns></returns>
    public object? GetContinuationToken();

    /// <summary>
    /// Returns IDs that can be used with <see cref="GetArticleMessageContent"/>.
    /// Each ID is valid only for the current session established by the most recent call to <see cref="Initialize"/>.
    /// Expects <see cref="Initialize"/> to have been called.
    /// </summary>
    [Pure]
    public IEnumerable<int> ListArticleIds();

    /// <summary>
    /// Gets the message content for the specified article.
    /// Might be multiple messages (think Danbooru videos needing a separate embed), might be async (think Danbooru fallbacks).
    /// Expects <see cref="Initialize"/> to have been called.
    /// </summary>
    /// <param name="articleId">The article ID as provided by <see cref="ListArticleIds"/></param>
    /// <param name="embedColor">The default color to be applied to article embeds.</param>
    /// <param name="feedTitle">The title of the feed this message is being generated for.</param>
    /// <param name="cancellationToken">Cancels the iteration.</param>
    /// <returns>All the messages that article needs to represent a summary of its contents. Should be as small as possible, ideally one message.</returns>
    [Pure]
    public IAsyncEnumerable<MessageContents> GetArticleMessageContent(int articleId, Color embedColor,
        string? feedTitle, CancellationToken cancellationToken = default);
}

[Flags]
public enum ArticleIdScope
{
    FeedSource = 0,
    /// <summary>
    /// Articles are marked as read for the channel, but only for the current poll.
    /// Useful for cases where multiple similar feeds in the same channel might return the same post.
    /// A poll here means for the runtime of a single call to <see cref="FeedsProcessorService.PollFeeds"/>.
    /// </summary>
    ChannelForPoll = 1 << 0
}
