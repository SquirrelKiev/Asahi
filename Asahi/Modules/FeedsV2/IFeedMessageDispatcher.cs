using Asahi.Database.Models.Rss;

namespace Asahi.Modules.FeedsV2
{
    public interface IFeedMessageDispatcher
    {
        public Task SendMessages(FeedListener listener, IAsyncEnumerable<MessageContents> messages);
    }
}
