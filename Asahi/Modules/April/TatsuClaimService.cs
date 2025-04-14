using Asahi.Modules.Tatsu;
using Refit;

namespace Asahi.Modules.April;

[Inject(ServiceLifetime.Singleton)]
public class TatsuClient(ITatsuClient client)
{
    private int Remaining = 60;
    private DateTimeOffset Refills = DateTime.Now;
    
    public async Task RateLimitNonsense<T>(ApiResponse<T> response)
    {
        if (!response.Headers.TryGetValues("X-RateLimit-Remaining", out IEnumerable<string>? remainingHeaders) ||
            !response.Headers.TryGetValues("X-RateLimit-Reset", out IEnumerable<string>? resetHeaders))
            return;

        string remainingHeader = remainingHeaders.First();
        string resetHeader = resetHeaders.First();
        
        if (!int.TryParse(remainingHeader, out int remaining))
            throw new ArgumentException("remaining");

        if (!int.TryParse(resetHeader, out int reset))
            throw new ArgumentException("reset");

        Remaining = remaining;
        Refills = DateTimeOffset.FromUnixTimeSeconds(reset);
    }
}
