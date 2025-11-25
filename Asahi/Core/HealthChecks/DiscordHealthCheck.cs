using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Asahi.HealthChecks;

public class DiscordHealthCheck(IDiscordClient discordClient) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
    {
        if (discordClient.ConnectionState == ConnectionState.Connected)
        {
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        
        return Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, $"Currently {discordClient.ConnectionState}."));
    }
}
