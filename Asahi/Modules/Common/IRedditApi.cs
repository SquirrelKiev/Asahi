using Asahi.Modules.Models;
using Refit;

namespace Asahi.Modules;

public interface IRedditApi
{
    [Get($"/r/{{{nameof(subreddit)}}}/about.json")]
    Task<SubredditChild> GetSubredditInfo(string subreddit, CancellationToken cancellationToken = default);
    
    [Get($"/r/{{{nameof(subreddit)}}}/new.json")]
    Task<SubredditPosts> GetSubredditPosts(string subreddit, CancellationToken cancellationToken = default);
}
