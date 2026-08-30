using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Core.Lifecycle;

public sealed class LifecycleScope : IDisposable, IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly List<LifecycleScope> _children = [];
    private readonly IHostDiagnostics? _diagnostics;
    private readonly object _syncRoot = new();
    private Task? _disposeTask;
    private bool _disposed;
    private LifecycleScopeState _state;
    private Task? _stopTask;

    private LifecycleScope(
        LifecycleScopeKind kind,
        string id,
        LifecycleScope? parent,
        CancellationTokenSource cancellationTokenSource,
        IHostDiagnostics? diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Kind = kind;
        Id = id;
        Parent = parent;
        _cancellationTokenSource = cancellationTokenSource;
        _diagnostics = diagnostics;
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
        return new LifecycleScope(
            kind,
            id,
            parent: null,
            new CancellationTokenSource(),
            diagnostics: null);
    }

    public static LifecycleScope CreateRoot(
        LifecycleScopeKind kind,
        string id,
        IHostDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        return new LifecycleScope(
            kind,
            id,
            parent: null,
            new CancellationTokenSource(),
            diagnostics);
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
                CancellationTokenSource.CreateLinkedTokenSource(CancellationToken),
                _diagnostics);

            child.Disposed += OnChildDisposed;
            _children.Add(child);

            return child;
        }
    }

    public ValueTask StopAsync()
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();

            if (_stopTask is not null)
            {
                return new ValueTask(_stopTask);
            }

            if (_state == LifecycleScopeState.Stopped)
            {
                return ValueTask.CompletedTask;
            }

            _state = LifecycleScopeState.Stopping;
            _stopTask = StopCoreAsync();

            return new ValueTask(_stopTask);
        }
    }

    public void Dispose()
    {
        Task.Run(async () => await DisposeAsync().ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }

    public ValueTask DisposeAsync()
    {
        lock (_syncRoot)
        {
            if (_disposeTask is not null)
            {
                return new ValueTask(_disposeTask);
            }

            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposeTask = DisposeCoreAsync();

            return new ValueTask(_disposeTask);
        }
    }

    private async Task StopCoreAsync()
    {
        var failures = new List<Exception>();
        LifecycleScope[] children;

        lock (_syncRoot)
        {
            children = _children.ToArray();
        }

        try
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel(throwOnFirstException: false);
            }
        }
        catch (Exception exception)
        {
            RecordCleanupFailure(exception, child: null, "Cancel");
            AddFailure(failures, exception);
        }

        for (var index = children.Length - 1; index >= 0; index--)
        {
            try
            {
                await children[index].StopAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                RecordCleanupFailure(exception, children[index], "StopChild");
                AddFailure(failures, exception);
            }
        }

        lock (_syncRoot)
        {
            if (!_disposed)
            {
                _state = failures.Count == 0
                    ? LifecycleScopeState.Stopped
                    : LifecycleScopeState.Faulted;
            }
        }

        ThrowIfFailures(failures, "Lifecycle scope stop failed.");
    }

    private async Task DisposeCoreAsync()
    {
        var failures = new List<Exception>();

        try
        {
            await StopAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AddFailure(failures, exception);
        }

        LifecycleScope[] children;

        lock (_syncRoot)
        {
            _disposed = true;
            _state = LifecycleScopeState.Disposing;
            children = _children.ToArray();
        }

        for (var index = children.Length - 1; index >= 0; index--)
        {
            try
            {
                await children[index].DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                RecordCleanupFailure(exception, children[index], "DisposeChild");
                AddFailure(failures, exception);
            }
        }

        try
        {
            _cancellationTokenSource.Dispose();
        }
        catch (Exception exception)
        {
            RecordCleanupFailure(exception, child: null, "DisposeCancellationSource");
            AddFailure(failures, exception);
        }

        lock (_syncRoot)
        {
            _state = LifecycleScopeState.Disposed;
        }

        try
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            RecordCleanupFailure(exception, child: null, "DisposedNotification");
            AddFailure(failures, exception);
        }

        ThrowIfFailures(failures, "Lifecycle scope disposal failed.");
    }

    private void RecordCleanupFailure(
        Exception exception,
        LifecycleScope? child,
        string operation)
    {
        if (_diagnostics is null)
        {
            return;
        }

        try
        {
            _diagnostics.Write(new HostDiagnosticRecord(
                HostDiagnosticIds.LifecycleScopeCleanupFailed,
                "Lifecycle scope cleanup failed.",
                HostDiagnosticSeverity.Error,
                ScopeId: Id)
            {
                Context = new Dictionary<string, string?>
                {
                    ["scopeKind"] = Kind.ToString(),
                    ["childScopeId"] = child?.Id,
                    ["childScopeKind"] = child?.Kind.ToString(),
                    ["operation"] = operation,
                    ["exceptionType"] = exception.GetType().FullName,
                },
            });
        }
        catch
        {
            // Diagnostics must not interrupt lifecycle cleanup.
        }
    }

    private void OnChildDisposed(object? sender, EventArgs eventArgs)
    {
        if (sender is not LifecycleScope child)
        {
            return;
        }

        child.Disposed -= OnChildDisposed;

        lock (_syncRoot)
        {
            _children.Remove(child);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LifecycleScope));
        }
    }

    private static void AddFailure(ICollection<Exception> failures, Exception exception)
    {
        if (exception is AggregateException aggregateException)
        {
            foreach (var innerException in aggregateException.Flatten().InnerExceptions)
            {
                failures.Add(innerException);
            }

            return;
        }

        failures.Add(exception);
    }

    private static void ThrowIfFailures(IReadOnlyCollection<Exception> failures, string message)
    {
        if (failures.Count > 0)
        {
            throw new AggregateException(message, failures);
        }
    }
}
