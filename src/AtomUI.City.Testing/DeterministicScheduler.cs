namespace AtomUI.City.Testing;

public sealed class DeterministicScheduler : IDisposable
{
    private readonly PriorityQueue<ScheduledWorkItem, DateTimeOffset> _scheduledWork = new();
    private bool _disposed;

    public DeterministicScheduler()
        : this(new TestDiagnostics())
    {
    }

    public DeterministicScheduler(TestDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
    }

    public DateTimeOffset Now { get; private set; } = DateTimeOffset.UnixEpoch;

    public int ScheduledCount => _scheduledWork.Count;

    public void Schedule(TimeSpan delay, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ThrowIfDisposed();

        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Delay cannot be negative.");
        }

        var dueAt = Now.Add(delay);
        _scheduledWork.Enqueue(new ScheduledWorkItem(callback, dueAt), dueAt);
    }

    public void AdvanceBy(TimeSpan duration)
    {
        ThrowIfDisposed();

        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Duration cannot be negative.");
        }

        Now = Now.Add(duration);
        RunDueWork();
    }

    public void RunDueWork()
    {
        ThrowIfDisposed();

        while (_scheduledWork.TryPeek(out var workItem, out var dueAt) && dueAt <= Now)
        {
            _scheduledWork.Dequeue();
            workItem.Callback();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scheduledWork.Clear();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DeterministicScheduler));
        }
    }

    private sealed record ScheduledWorkItem(Action Callback, DateTimeOffset DueAt);
}
