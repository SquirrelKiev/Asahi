using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Asahi.HealthChecks;

public class WebServicesHealthCheck(HttpClient client, BotConfig config) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
    {
        try
        {
            var res = await client.GetAsync($"{config.AsahiWebServicesBaseUrl}/api/health", cancellationToken);

            if (res.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy();
            }

            return new HealthCheckResult(context.Registration.FailureStatus);
        }
        catch (Exception e)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, e.Message, e);
        }
    }
}
