using Asahi.Database;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Asahi.HealthChecks
{
    public class DatabaseHealthCheck(BotDbContext db) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new())
        {
            try
            {
                if (await db.Database.CanConnectAsync(cancellationToken))
                {
                    return HealthCheckResult.Healthy();
                }

                return HealthCheckResult.Unhealthy();
            }
            catch (Exception e)
            {
                return HealthCheckResult.Unhealthy(e.Message, e);
            }
        }
    }
}
