using Refit;

namespace Asahi.Modules;

public interface IAnonymousRedditApi
{
    [Post("/api/v1/access_token?grant_type=client_credentials")]
    Task<ApiResponse<OauthResponse>> GetAccessToken([Authorize("Basic")] string authorization, CancellationToken cancellationToken = default);
}
