using AtomUI.City.Diagnostics;

namespace AtomUI.City.State;

public sealed class ComputedState<T> : IComputedState<T>, IDisposable
{
    private readonly object _syncRoot = new();
    private readonly Func<T> _compute;
    private readonly IHostDiagnostics? _diagnostics;
    private readonly List<StateSubscription> _subscriptions = [];
    private readonly List<IStateSubscription> _dependencySubscriptions = [];
    private bool _hasComputeFailure;
    private bool _hasValue;
    private bool _isDirty = true;
    private bool _isDisposed;
    private T? _value;

    public ComputedState(Func<T> compute, params IReadOnlyState[] dependencies)
        : this(compute, diagnostics: null, dependencies)
    {
    }

    public ComputedState(
        Func<T> compute,
        IHostDiagnostics? diagnostics,
        params IReadOnlyState[] dependencies)
    {
        ArgumentNullException.ThrowIfNull(compute);
        ArgumentNullException.ThrowIfNull(dependencies);

        _compute = compute;
        _diagnostics = diagnostics;

        foreach (var dependency in dependencies)
        {
            if (dependency is null)
            {
                throw new ArgumentException("Computed state dependencies must not contain null.", nameof(dependencies));
            }
        }

        try
        {
            foreach (var dependency in dependencies)
            {
                _dependencySubscriptions.Add(dependency.OnChange(_ => InvalidateAndNotify()));
            }
        }
        catch
        {
            foreach (var subscription in _dependencySubscriptions)
            {
                DisposeSubscription(subscription);
            }

            _dependencySubscriptions.Clear();
            throw;
        }
    }

    public T Value
    {
        get
        {
            lock (_syncRoot)
            {
                ThrowIfDisposed();
                EnsureValue();

                return _value!;
            }
        }
    }

    object? IReadOnlyState.Value => Value;

    public long Version { get; private set; }

    public Type ValueType => typeof(T);

    public Exception? LastError { get; private set; }

    public IStateSubscription OnChange(Action<StateChangedEventArgs<T>> handler)
    {
        return OnChange(handler, StateSubscriptionOptions.Immediate);
    }

    public IStateSubscription OnChange(
        Action<StateChangedEventArgs<T>> handler,
        StateSubscriptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(options);

        lock (_syncRoot)
        {
            ThrowIfDisposed();
            EnsureValue();

            var subscription = new StateSubscription(
                args => handler((StateChangedEventArgs<T>)args),
                options,
                _diagnostics);

            _subscriptions.Add(subscription);

            return new RemovingStateSubscription(this, subscription);
        }
    }

    IStateSubscription IReadOnlyState.OnChange(Action<StateChangedEventArgs> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return OnChange(args => handler(args));
    }

    IStateSubscription IReadOnlyState.OnChange(
        Action<StateChangedEventArgs> handler,
        StateSubscriptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return OnChange(args => handler(args), options);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        foreach (var subscription in _dependencySubscriptions)
        {
            DisposeSubscription(subscription);
        }

        foreach (var subscription in _subscriptions)
        {
            DisposeSubscription(subscription);
        }

        _dependencySubscriptions.Clear();
        _subscriptions.Clear();
    }

    private void InvalidateAndNotify()
    {
        StateChangedEventArgs<T>? change = null;
        StateSubscription[] subscriptions;

        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDirty = true;
            if (_subscriptions.Count == 0)
            {
                return;
            }

            change = RecomputeForNotification();
            subscriptions = _subscriptions.ToArray();
        }

        if (change is null)
        {
            return;
        }

        foreach (var subscription in subscriptions)
        {
            subscription.Notify(change);
        }
    }

    private void EnsureValue()
    {
        if (!_isDirty && (_hasValue || _hasComputeFailure))
        {
            return;
        }

        var oldValue = _value;
        var hadValue = _hasValue;
        var newValue = TryCompute();

        if (_hasValue &&
            hadValue &&
            !EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            Version++;
        }
    }

    private StateChangedEventArgs<T>? RecomputeForNotification()
    {
        var oldValue = _value;
        var hadValue = _hasValue;
        var newValue = TryCompute();

        if (!_hasValue)
        {
            return null;
        }

        if (hadValue && EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            return null;
        }

        Version++;

        return new StateChangedEventArgs<T>(oldValue!, newValue!, Version);
    }

    private T? TryCompute()
    {
        try
        {
            _value = _compute();
            _hasComputeFailure = false;
            _hasValue = true;
            _isDirty = false;
            LastError = null;

            return _value;
        }
        catch (Exception exception)
        {
            _hasComputeFailure = true;
            _isDirty = false;
            LastError = exception;
            _diagnostics?.Write(new HostDiagnosticRecord(
                StateDiagnosticIds.ComputedStateComputeFailed,
                $"Computed state failed to compute value type '{typeof(T).FullName}': {exception.Message}",
                HostDiagnosticSeverity.Error)
            {
                Context = StateDiagnosticContext.Create(
                    ("valueType", StateDiagnosticContext.TypeName(typeof(T))))
            });

            return _value;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(ComputedState<T>));
        }
    }

    private void DisposeSubscription(IDisposable subscription)
    {
        try
        {
            subscription.Dispose();
        }
        catch (Exception exception)
        {
            _diagnostics?.Write(new HostDiagnosticRecord(
                StateDiagnosticIds.ComputedStateDisposeFailed,
                $"Computed state failed to dispose value type '{typeof(T).FullName}' subscription: {exception.Message}",
                HostDiagnosticSeverity.Error)
            {
                Context = StateDiagnosticContext.Create(
                    ("valueType", StateDiagnosticContext.TypeName(typeof(T))))
            });
        }
    }

    private void Remove(StateSubscription subscription)
    {
        lock (_syncRoot)
        {
            _subscriptions.Remove(subscription);
        }
    }

    private sealed class RemovingStateSubscription : IStateSubscription
    {
        private readonly ComputedState<T> _state;
        private readonly StateSubscription _subscription;
        private bool _disposed;

        public RemovingStateSubscription(
            ComputedState<T> state,
            StateSubscription subscription)
        {
            _state = state;
            _subscription = subscription;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _subscription.Dispose();
            _state.Remove(_subscription);
        }
    }
}
