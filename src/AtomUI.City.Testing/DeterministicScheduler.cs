namespace AtomUI.City.Testing;

public sealed class DeterministicScheduler : IDisposable
{
    private readonly PriorityQueue<DeterministicScheduledWorkItem, ScheduledWorkPriority> _scheduledWork = new();
    private readonly TestDiagnostics _diagnostics;
    private long _nextWorkItemId;
    private bool _disposed;

    public DeterministicScheduler()
        : this(new TestDiagnostics())
    {
    }

    public DeterministicScheduler(TestDiagnostics diagnostics)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public DateTimeOffset Now { get; private set; } = DateTimeOffset.UnixEpoch;

    public int ScheduledCount => _scheduledWork.Count;

    public DeterministicScheduledWorkItem Schedule(TimeSpan delay, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ThrowIfDisposed();

        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Delay cannot be negative.");
        }

        var scheduledWorkItem = new DeterministicScheduledWorkItem(
            Interlocked.Increment(ref _nextWorkItemId),
            callback,
            Now.Add(delay));

        _scheduledWork.Enqueue(
            scheduledWorkItem,
            new ScheduledWorkPriority(scheduledWorkItem.DueAt, scheduledWorkItem.Id));

        return scheduledWorkItem;
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

        while (_scheduledWork.TryPeek(out var workItem, out var priority) && priority.DueAt <= Now)
        {
            _scheduledWork.Dequeue();
            ExecuteWorkItem(workItem);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        while (_scheduledWork.TryDequeue(out var workItem, out _))
        {
            workItem.Cancel();
        }
    }

    private void ExecuteWorkItem(DeterministicScheduledWorkItem workItem)
    {
        try
        {
            workItem.Execute();
        }
        catch (Exception exception)
        {
            _diagnostics.Add(
                "AUCTEST201",
                $"Scheduled work item {workItem.Id} failed at {workItem.DueAt:o}: {exception.Message}");
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DeterministicScheduler));
        }
    }

    private readonly record struct ScheduledWorkPriority(DateTimeOffset DueAt, long Id)
        : IComparable<ScheduledWorkPriority>
    {
        public int CompareTo(ScheduledWorkPriority other)
        {
            var dueAtComparison = DueAt.CompareTo(other.DueAt);

            return dueAtComparison != 0 ? dueAtComparison : Id.CompareTo(other.Id);
        }
    }
}

public sealed class DeterministicScheduledWorkItem
{
    private readonly Action _callback;

    internal DeterministicScheduledWorkItem(long id, Action callback, DateTimeOffset dueAt)
    {
        Id = id;
        _callback = callback;
        DueAt = dueAt;
    }

    public long Id { get; }

    public DateTimeOffset DueAt { get; }

    public bool IsCanceled { get; private set; }

    public bool IsCompleted { get; private set; }

    public bool IsFaulted { get; private set; }

    public Exception? Exception { get; private set; }

    public void Cancel()
    {
        if (IsCompleted)
        {
            return;
        }

        IsCanceled = true;
    }

    internal void Execute()
    {
        if (IsCompleted)
        {
            return;
        }

        if (IsCanceled)
        {
            IsCompleted = true;

            return;
        }

        try
        {
            _callback();
            IsCompleted = true;
        }
        catch (Exception exception)
        {
            Exception = exception;
            IsFaulted = true;
            IsCompleted = true;

            throw;
        }
    }
}
