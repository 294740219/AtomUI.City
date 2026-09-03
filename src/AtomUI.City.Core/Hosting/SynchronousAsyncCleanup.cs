namespace AtomUI.City.Core.Hosting;

internal static class SynchronousAsyncCleanup
{
    public static void Run(Func<ValueTask> cleanup, TimeSpan timeout, string operation)
    {
        ArgumentNullException.ThrowIfNull(cleanup);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        if (timeout <= TimeSpan.Zero)
        {
            var lateCleanupTask = RunCore(cleanup);
            _ = ObserveLateFailureAsync(lateCleanupTask);
            throw new AsyncCleanupTimeoutException(operation, TimeSpan.Zero, cleanupStarted: true);
        }

        // Invoke the user cleanup inside Task.Run so it cannot capture the
        // SynchronizationContext of the thread synchronously blocked in Build().
        var cleanupTask = RunCore(cleanup);
        try
        {
            cleanupTask.WaitAsync(timeout).GetAwaiter().GetResult();
        }
        catch (TimeoutException) when (!cleanupTask.IsCompleted)
        {
            _ = ObserveLateFailureAsync(cleanupTask);
            throw new AsyncCleanupTimeoutException(operation, timeout, cleanupStarted: true);
        }
    }

    private static Task RunCore(Func<ValueTask> cleanup) =>
        Task.Run(async () => await cleanup().ConfigureAwait(false));

    private static async Task ObserveLateFailureAsync(Task cleanupTask)
    {
        try
        {
            await cleanupTask.ConfigureAwait(false);
        }
        catch
        {
            // The caller has already received a timeout. Observe any later failure
            // so it cannot surface as an unobserved task exception.
        }
    }
}

internal sealed class BuildCleanupDeadline
{
    private readonly long _startedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

    public BuildCleanupDeadline(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Cleanup timeout must be greater than zero.");
        }

        Timeout = timeout;
    }

    public TimeSpan Timeout { get; }

    public TimeSpan Remaining
    {
        get
        {
            var remaining = Timeout - System.Diagnostics.Stopwatch.GetElapsedTime(_startedTimestamp);
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public void Run(Func<ValueTask> cleanup, string operation) =>
        SynchronousAsyncCleanup.Run(cleanup, Remaining, operation);
}

internal sealed class AsyncCleanupTimeoutException(
    string operation,
    TimeSpan waitTimeout,
    bool cleanupStarted) : TimeoutException(
        cleanupStarted
            ? $"Asynchronous cleanup '{operation}' did not complete within the remaining build cleanup budget of {waitTimeout}. The cleanup task may still be running."
            : $"Asynchronous cleanup '{operation}' was not started because the build cleanup budget was exhausted.")
{
    public TimeSpan WaitTimeout { get; } = waitTimeout;

    public bool CleanupStarted { get; } = cleanupStarted;
}
