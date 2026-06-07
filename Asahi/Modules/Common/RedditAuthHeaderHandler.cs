using System.Net.Http.Headers;

namespace Asahi.Modules;

public class RedditAuthHeaderHandler(IAuthTokenProvider<IRedditApi> tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetToken(cancellationToken);
        
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        return await base.SendAsync(request, cancellationToken);
    }
}
