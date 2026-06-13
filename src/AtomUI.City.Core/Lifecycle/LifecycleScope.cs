namespace AtomUI.City.Lifecycle;

public sealed class LifecycleScope : IDisposable, IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly List<LifecycleScope> _children = [];
    private readonly object _syncRoot = new();
    private bool _disposed;
    private LifecycleScopeState _state;

    private LifecycleScope(
        LifecycleScopeKind kind,
        string id,
        LifecycleScope? parent,
        CancellationTokenSource cancellationTokenSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Kind = kind;
        Id = id;
        Parent = parent;
        _cancellationTokenSource = cancellationTokenSource;
        _state = LifecycleScopeState.Running;
    }

    public string Id { get; }

    public LifecycleScopeKind Kind { get; }

    public LifecycleScope? Parent { get; }

    public IReadOnlyList<LifecycleScope> Children
    {
        get
        {
            lock (_syncRoot)
            {
                return Array.AsReadOnly(_children.ToArray());
            }
        }
    }

    public LifecycleScopeState State
    {
        get
        {
            lock (_syncRoot)
            {
                return _state;
            }
        }
    }

    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    internal event EventHandler? Disposed;

    public static LifecycleScope CreateRoot(LifecycleScopeKind kind, string id)
    {
        return new LifecycleScope(kind, id, parent: null, new CancellationTokenSource());
    }

    public LifecycleScope CreateChild(LifecycleScopeKind kind, string id)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();

            if (_state != LifecycleScopeState.Running)
            {
                throw new InvalidOperationException("Lifecycle scope can only create children while running.");
            }

            var child = new LifecycleScope(
                kind,
                id,
                this,
                CancellationTokenSource.CreateLinkedTokenSource(CancellationToken));

            _children.Add(child);

            return child;
        }
    }

    public async ValueTask StopAsync()
    {
        LifecycleScope[] children;

        lock (_syncRoot)
        {
            ThrowIfDisposed();

            if (_state is LifecycleScopeState.Stopped or LifecycleScopeState.Stopping)
            {
                return;
            }

            _state = LifecycleScopeState.Stopping;

            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }

            children = _children.ToArray();
        }

        for (var i = children.Length - 1; i >= 0; i--)
        {
            await children[i].StopAsync().ConfigureAwait(false);
        }

        lock (_syncRoot)
        {
            if (!_disposed && _state == LifecycleScopeState.Stopping)
            {
                _state = LifecycleScopeState.Stopped;
            }
        }
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        StopAsync().AsTask().GetAwaiter().GetResult();
        DisposeCoreAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        await DisposeCoreAsync().ConfigureAwait(false);
    }

    private async ValueTask DisposeCoreAsync()
    {
        LifecycleScope[] children;

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _state = LifecycleScopeState.Disposing;
            children = _children.ToArray();
        }

        for (var i = children.Length - 1; i >= 0; i--)
        {
            await children[i].DisposeAsync().ConfigureAwait(false);
        }

        _cancellationTokenSource.Dispose();

        lock (_syncRoot)
        {
            _state = LifecycleScopeState.Disposed;
        }

        Disposed?.Invoke(this, EventArgs.Empty);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LifecycleScope));
        }
    }

    private bool IsDisposed
    {
        get
        {
            lock (_syncRoot)
            {
                return _disposed;
            }
        }
    }
}
