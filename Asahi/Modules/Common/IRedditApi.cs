using Asahi.Modules.Models;
using Refit;

namespace Asahi.Modules;

public interface IRedditApi
{
    [Get($"/r/{{{nameof(subreddit)}}}/about.json")]
    Task<ApiResponse<SubredditChild>> GetSubredditInfo(string subreddit, CancellationToken cancellationToken = default);
    
    [Get($"/r/{{{nameof(subreddit)}}}/new.json?sort=new&limit=25")]
    Task<ApiResponse<SubredditPosts>> GetSubredditPosts(string subreddit, CancellationToken cancellationToken = default);
    
    [Get($"/r/{{{nameof(subreddit)}}}/new.json?sort=new&limit=25")]
    Task<ApiResponse<string>> GetSubredditPostsRaw(string subreddit, CancellationToken cancellationToken = default);
}
