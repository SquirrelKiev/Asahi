namespace Asahi;

[Inject(ServiceLifetime.Singleton)]
public class ClientReadyGate
{
    private readonly TaskCompletionSource tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitForReadyAsync(CancellationToken cancellationToken) => tcs.Task.WaitAsync(cancellationToken);

    public void SignalReady() => tcs.TrySetResult();
}
