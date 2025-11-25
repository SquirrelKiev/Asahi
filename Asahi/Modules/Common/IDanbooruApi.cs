using Asahi.Modules.Models;
using Refit;

namespace Asahi.Modules;

public interface IDanbooruApi
{
    // not sure if this QueryUriFormat is the default
    [Get("/posts.json"), QueryUriFormat(UriFormat.UriEscaped)]
    Task<ApiResponse<DanbooruPost[]>> GetPosts([Query] string tags, CancellationToken cancellationToken = default);
    
    [Get("/source.json"), QueryUriFormat(UriFormat.UriEscaped)]
    Task<ApiResponse<DanbooruSource>> GetSource([Query] string url, CancellationToken cancellationToken = default);
    
    [Get($"/posts/{{{nameof(id)}}}.json")]
    Task<ApiResponse<DanbooruPost>> GetPost(uint id, CancellationToken cancellationToken = default);
    
    [Get($"/users/{{{nameof(id)}}}.json")]
    Task<ApiResponse<DanbooruUser>> GetUser(int id, CancellationToken cancellationToken = default);
}
