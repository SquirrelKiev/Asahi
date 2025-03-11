using Asahi.Database.Models.Rss;
using Microsoft.Extensions.Logging;

namespace Asahi.Modules.FeedsV2
{
    public class LogFeedMessageDispatcher(ILogger<LogFeedMessageDispatcher> logger) : IFeedMessageDispatcher
    {
        public async Task SendMessages(FeedListener listener, IAsyncEnumerable<MessageContents> messages)
        {
            await foreach (var message in messages)
            {
                logger.LogInformation("New article, body: {body}. embed title: {embedTitle}.", message.body, message.embeds?.FirstOrDefault()?.Title);
            }
        }
    }
}
