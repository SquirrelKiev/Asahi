using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace Asahi.Modules;

public sealed class RedditAuthTokenProvider(IAnonymousRedditApi redditApi, BotConfig config, IClock clock, ILogger<RedditAuthTokenProvider> logger) : IAuthTokenProvider<IRedditApi>, IDisposable
{
    private string? token;
    private Instant lastFetched;
    private Duration expiresIn;
    
    private readonly SemaphoreSlim semaphore = new(1, 1);
    
    public async Task<string> GetToken(CancellationToken cancellationToken = default)
    {
        if (TokenIsValid())
        {
            return token;
        }
        
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            if (!TokenIsValid())
            {
                await FetchNewToken(cancellationToken);
            }

            return token;
        }
        catch(Exception e)
        {
            logger.LogError(e, "An exception occurred while fetching the Reddit token");
            throw;
        }
        finally
        {
            semaphore.Release();
        }
    }

    [MemberNotNullWhen(true, nameof(token))]
    private bool TokenIsValid()
    {
        if(token == null)
            return false;
        
        var expiryTime = lastFetched.Plus(expiresIn - Duration.FromSeconds(60));
        return clock.GetCurrentInstant() >= expiryTime;
    }

    [MemberNotNull(nameof(token))]
    private async Task FetchNewToken(CancellationToken cancellationToken = default)
    {
        var tokenResponse = await redditApi.GetAccessToken($"{config.RedditApiCredentials.BasicAuthenticationSecret}", cancellationToken);
        
        await tokenResponse.EnsureSuccessfulAsync();

        if (string.IsNullOrEmpty(tokenResponse.Content.AccessToken))
        {
            throw new InvalidDataException("Token is empty");
        }

        token = tokenResponse.Content.AccessToken;
        lastFetched = clock.GetCurrentInstant();
        expiresIn = Duration.FromSeconds(tokenResponse.Content.ExpiresIn);
    }

    public void Dispose()
    {
        semaphore.Dispose();
    }
}
