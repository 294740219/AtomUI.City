using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Core.Lifecycle;

/// <summary>
/// Represents lifecycle scope.
/// </summary>
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
        ValidateKind(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Kind = kind;
        Id = id;
        Parent = parent;
        _cancellationTokenSource = cancellationTokenSource;
        _diagnostics = diagnostics;
        _state = LifecycleScopeState.Running;
    }

    /// <summary>
    /// Gets the id value.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the kind value.
    /// </summary>
    public LifecycleScopeKind Kind { get; }

    /// <summary>
    /// Gets the parent value.
    /// </summary>
    public LifecycleScope? Parent { get; }

    /// <summary>
    /// Gets the children value.
    /// </summary>
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

    /// <summary>
    /// Gets the state value.
    /// </summary>
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

    /// <summary>
    /// Gets the cancellation token value.
    /// </summary>
    public CancellationToken CancellationToken => _cancellationTokenSource.Token;

    internal event EventHandler? Disposed;

    /// <summary>
    /// Executes the create root operation.
    /// </summary>
    public static LifecycleScope CreateRoot(LifecycleScopeKind kind, string id)
    {
        ValidateKind(kind);

        return new LifecycleScope(
            kind,
            id,
            parent: null,
            new CancellationTokenSource(),
            diagnostics: null);
    }

    /// <summary>
    /// Executes the create root operation.
    /// </summary>
    public static LifecycleScope CreateRoot(
        LifecycleScopeKind kind,
        string id,
        IHostDiagnostics diagnostics)
    {
        ValidateKind(kind);
        ArgumentNullException.ThrowIfNull(diagnostics);

        return new LifecycleScope(
            kind,
            id,
            parent: null,
            new CancellationTokenSource(),
            diagnostics);
    }

    /// <summary>
    /// Executes the create child operation.
    /// </summary>
    public LifecycleScope CreateChild(LifecycleScopeKind kind, string id)
    {
        ValidateKind(kind);

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

    private static void ValidateKind(LifecycleScopeKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Lifecycle scope kind must be a defined value.");
        }
    }

    /// <summary>
    /// Executes the stop async operation.
    /// </summary>
    public ValueTask StopAsync()
    {
        LifecycleInvocationGuard.ThrowIfReentrant(this, LifecycleOperationKind.Stop);

        return new ValueTask(GetOrStartStopTask(allowDisposed: false));
    }

    private ValueTask StopFromParentAsync()
    {
        return new ValueTask(GetOrStartStopTask(allowDisposed: true));
    }

    private Task GetOrStartStopTask(bool allowDisposed)
    {
        DeferredLifecycleOperation? operation = null;
        Task stopTask;

        lock (_syncRoot)
        {
            if (!allowDisposed)
            {
                ThrowIfDisposed();
            }

            if (_stopTask is not null)
            {
                return _stopTask;
            }

            if (_disposed)
            {
                return Task.CompletedTask;
            }

            if (_state == LifecycleScopeState.Stopped)
            {
                _stopTask = Task.CompletedTask;
                return _stopTask;
            }

            operation = new DeferredLifecycleOperation();
            _stopTask = operation.Task;
            _state = LifecycleScopeState.Stopping;
            stopTask = _stopTask;
        }

        operation.Start(this, LifecycleOperationKind.Stop, StopCoreAsync);

        return stopTask;
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        Task.Run(async () => await DisposeAsync().ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    /// Executes the dispose async operation.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        LifecycleInvocationGuard.ThrowIfReentrant(this, LifecycleOperationKind.Dispose);

        DeferredLifecycleOperation? operation = null;
        Task disposeTask;

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

            operation = new DeferredLifecycleOperation();
            _disposeTask = operation.Task;
            disposeTask = _disposeTask;
        }

        operation.Start(this, LifecycleOperationKind.Dispose, DisposeCoreAsync);

        return new ValueTask(disposeTask);
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
                await children[index].StopFromParentAsync().ConfigureAwait(false);
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
            await GetOrStartStopTask(allowDisposed: false).ConfigureAwait(false);
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
