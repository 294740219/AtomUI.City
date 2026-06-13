namespace AtomUI.City.Testing;

public sealed class FakeUiWorkItem
{
    private readonly Func<CancellationToken, ValueTask> _callback;
    private readonly CancellationToken _cancellationToken;

    internal FakeUiWorkItem(
        long id,
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken = default)
    {
        Id = id;
        _callback = callback;
        _cancellationToken = cancellationToken;
    }

    public long Id { get; }

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
        if (IsCanceled || IsCompleted)
        {
            return;
        }

        if (_cancellationToken.IsCancellationRequested)
        {
            IsCanceled = true;
            IsCompleted = true;

            return;
        }

        try
        {
            _callback(_cancellationToken).GetAwaiter().GetResult();
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
