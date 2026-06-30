using Microsoft.Extensions.Hosting;

namespace Asahi;

public abstract class DiscordDependentBackgroundService(ClientReadyGate readyGate) : BackgroundService
{
    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await readyGate.WaitForReadyAsync(stoppingToken);

            await ExecuteAfterReadyAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    protected abstract Task ExecuteAfterReadyAsync(CancellationToken stoppingToken);
}
