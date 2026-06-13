namespace AtomUI.City.Testing;

public sealed class FakeUiDispatcher : IDisposable
{
    private readonly Queue<FakeUiWorkItem> _workItems = new();
    private bool _disposed;

    public FakeUiDispatcher()
        : this(new TestDiagnostics())
    {
    }

    public FakeUiDispatcher(TestDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
    }

    public int PendingCount => _workItems.Count;

    public FakeUiWorkItem Post(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ThrowIfDisposed();

        var workItem = new FakeUiWorkItem(callback);

        _workItems.Enqueue(workItem);

        return workItem;
    }

    public void Drain()
    {
        while (_workItems.TryDequeue(out var workItem))
        {
            workItem.Execute();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        while (_workItems.TryDequeue(out var workItem))
        {
            workItem.Cancel();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(FakeUiDispatcher));
        }
    }
}
