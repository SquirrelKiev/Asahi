using Asahi.Modules.RssAtomFeed.Models;
using Refit;

namespace Asahi.Modules.RssAtomFeed;

public interface IRedditApi
{
    [Get($"/r/{{{nameof(subreddit)}}}/about.json")]
    Task<SubredditChild> GetSubredditInfo(string subreddit);
    
    [Get($"/r/{{{nameof(subreddit)}}}/new.json")]
    Task<SubredditPosts> GetSubredditPosts(string subreddit);
}
