using Asahi.Modules.Models;
using Refit;

namespace Asahi.Modules;

public interface IDanbooruApi
{
    // not sure if this QueryUriFormat is the default
    // I'll eventually do something with you im sure :clueless:
    [Get("/posts.json"), QueryUriFormat(UriFormat.UriEscaped)]
    Task<ApiResponse<DanbooruPost[]>> GetPosts([Query] string tags, CancellationToken cancellationToken = default);
    
    [Get("/source.json"), QueryUriFormat(UriFormat.UriEscaped)]
    Task<ApiResponse<DanbooruSource>> GetSource([Query] string url, CancellationToken cancellationToken = default);
}
