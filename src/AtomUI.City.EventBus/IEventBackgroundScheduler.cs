namespace AtomUI.City.EventBus;

public interface IEventBackgroundScheduler
{
    ValueTask RunAsync(
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken = default);
}

public sealed class ThreadPoolEventBackgroundScheduler : IEventBackgroundScheduler
{
    public async ValueTask RunAsync(
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        cancellationToken.ThrowIfCancellationRequested();

        Task operation;
        if (ExecutionContext.IsFlowSuppressed())
        {
            operation = Task.Run(
                async () => await callback(cancellationToken).ConfigureAwait(false),
                cancellationToken);
        }
        else
        {
            using (ExecutionContext.SuppressFlow())
            {
                operation = Task.Run(
                    async () => await callback(cancellationToken).ConfigureAwait(false),
                    cancellationToken);
            }
        }

        await operation.ConfigureAwait(false);
    }
}
