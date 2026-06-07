using Newtonsoft.Json;

namespace Asahi.Modules;

public interface IAuthTokenProvider<T>
{
    public Task<string> GetToken(CancellationToken cancellationToken = default);
}

public readonly record struct OauthResponse(
    [property: JsonRequired, JsonProperty("access_token")] string AccessToken,
    [property: JsonProperty("token_type")] string TokenType,
    [property: JsonRequired, JsonProperty("expires_in")] int ExpiresIn,
    [property: JsonProperty("scope")] string Scope);
