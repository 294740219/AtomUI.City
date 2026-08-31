namespace AtomUI.City.Core.Lifecycle;

internal sealed class DeferredLifecycleOperation
{
    private readonly TaskCompletionSource _completion = new(
        TaskCreationOptions.RunContinuationsAsynchronously);
    private int _started;

    public Task Task => _completion.Task;

    public void Start(
        object owner,
        LifecycleOperationKind operation,
        Func<Task> execute)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(execute);

        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException("Lifecycle operation has already started.");
        }

        _ = ExecuteAsync(owner, operation, execute);
    }

    private async Task ExecuteAsync(
        object owner,
        LifecycleOperationKind operation,
        Func<Task> execute)
    {
        try
        {
            using var invocation = LifecycleInvocationGuard.Enter(owner, operation);
            Task executionTask;

            using (LifecycleInvocationGuard.EnterSynchronous(owner, operation))
            {
                executionTask = execute();
            }

            await executionTask.ConfigureAwait(false);
            _completion.TrySetResult();
        }
        catch (OperationCanceledException exception)
        {
            _completion.TrySetCanceled(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            _completion.TrySetException(exception);
        }
    }
}
