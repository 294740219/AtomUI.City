using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.State;

public sealed class WritableState<T> : IWritableState<T>, IDisposable
{
    private readonly IEqualityComparer<T> _comparer;
    private readonly IHostDiagnostics? _diagnostics;
    private readonly string _stateName;
    private readonly StateAccessPolicy _access;
    private readonly List<StateSubscription> _subscriptions = [];
    private readonly object _syncRoot = new();
    private T _value;
    private bool _disposed;
    private long _version;

    public WritableState(
        T initialValue,
        IEqualityComparer<T>? comparer = null,
        IHostDiagnostics? diagnostics = null,
        string? stateName = null,
        StateAccessPolicy access = StateAccessPolicy.HostWrite)
    {
        if (!Enum.IsDefined(access))
        {
            throw new ArgumentOutOfRangeException(nameof(access), access, "State access policy is not supported.");
        }

        _value = initialValue;
        _comparer = comparer ?? EqualityComparer<T>.Default;
        _diagnostics = diagnostics;
        _stateName = string.IsNullOrWhiteSpace(stateName)
            ? typeof(T).FullName ?? typeof(T).Name
            : stateName;
        _access = access;
    }

    public event EventHandler<StateChangedEventArgs<T>>? Changed;

    public T Value
    {
        get
        {
            lock (_syncRoot)
            {
                return _value;
            }
        }
    }

    object? IReadOnlyState.Value => Value;

    public long Version
    {
        get
        {
            lock (_syncRoot)
            {
                return _version;
            }
        }
    }

    public Type ValueType => typeof(T);

    public void Set(T value)
    {
        SetValue(value);
    }

    public bool SetValue(T value)
    {
        StateChangedEventArgs<T>? args;
        StateSubscription[] subscriptions;

        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ThrowIfWriteDenied();

            if (_comparer.Equals(_value, value))
            {
                return false;
            }

            var oldValue = _value;
            _value = value;
            _version++;
            args = new StateChangedEventArgs<T>(oldValue, value, _version);
            subscriptions = _subscriptions.ToArray();
        }

        Notify(args, subscriptions);

        return true;
    }

    public bool Update(Func<T, T> updater)
    {
        ArgumentNullException.ThrowIfNull(updater);

        StateChangedEventArgs<T>? args;
        StateSubscription[] subscriptions;

        lock (_syncRoot)
        {
            ThrowIfDisposed();
            ThrowIfWriteDenied();

            T nextValue;

            try
            {
                nextValue = updater(_value);
            }
            catch (Exception exception)
            {
                WriteUpdateFailedDiagnostic(exception);
                throw;
            }

            if (_comparer.Equals(_value, nextValue))
            {
                return false;
            }

            var oldValue = _value;
            _value = nextValue;
            _version++;
            args = new StateChangedEventArgs<T>(oldValue, nextValue, _version);
            subscriptions = _subscriptions.ToArray();
        }

        Notify(args, subscriptions);

        return true;
    }

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

        var subscription = new StateSubscription(
            args => handler((StateChangedEventArgs<T>)args),
            options,
            _diagnostics);

        lock (_syncRoot)
        {
            ThrowIfDisposed();

            _subscriptions.Add(subscription);
        }

        return new RemovingStateSubscription(this, subscription);
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
        StateSubscription[] subscriptions;

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            subscriptions = _subscriptions.ToArray();
            _subscriptions.Clear();
        }

        foreach (var subscription in subscriptions)
        {
            subscription.Dispose();
        }
    }

    internal void Restore(T value, long version)
    {
        StateChangedEventArgs<T>? args;
        StateSubscription[] subscriptions;

        lock (_syncRoot)
        {
            ThrowIfDisposed();

            if (_comparer.Equals(_value, value) && _version == version)
            {
                return;
            }

            var oldValue = _value;
            _value = value;
            _version = version;
            args = new StateChangedEventArgs<T>(oldValue, value, _version);
            subscriptions = _subscriptions.ToArray();
        }

        Notify(args, subscriptions);
    }

    internal (T Value, long Version) CaptureSnapshot()
    {
        lock (_syncRoot)
        {
            return (_value, _version);
        }
    }

    private void Notify(
        StateChangedEventArgs<T> args,
        StateSubscription[] subscriptions)
    {
        NotifyChangedEvent(args);

        foreach (var subscription in subscriptions)
        {
            subscription.Notify(args);
        }
    }

    private void NotifyChangedEvent(StateChangedEventArgs<T> args)
    {
        var changed = Changed;

        if (changed is null)
        {
            return;
        }

        foreach (var handler in changed.GetInvocationList().Cast<EventHandler<StateChangedEventArgs<T>>>())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception exception)
            {
                _diagnostics?.Write(new HostDiagnosticRecord(
                    StateDiagnosticIds.ChangedEventHandlerFailed,
                    $"Writable state changed event handler failed for value type '{typeof(T).FullName}' at version {args.Version}: {exception.Message}",
                    HostDiagnosticSeverity.Error)
                {
                    Context = StateDiagnosticContext.Create(
                        ("stateKey", _stateName),
                        ("valueType", StateDiagnosticContext.TypeName(typeof(T))),
                        ("version", StateDiagnosticContext.Version(args.Version)))
                });
            }
        }
    }

    private void WriteUpdateFailedDiagnostic(Exception exception)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            StateDiagnosticIds.WritableStateUpdateFailed,
            $"Writable state failed to update value type '{typeof(T).FullName}' at version {Version}: {exception.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = StateDiagnosticContext.Create(
                ("stateKey", _stateName),
                ("valueType", StateDiagnosticContext.TypeName(typeof(T))),
                ("version", StateDiagnosticContext.Version(Version)))
        });
    }

    private void WriteWriteDeniedDiagnostic()
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            StateDiagnosticIds.ApplicationStateWriteDenied,
            $"Writable state '{_stateName}' with value type '{typeof(T).FullName}' rejected write because access policy is '{_access}'.",
            HostDiagnosticSeverity.Warning)
        {
            Context = StateDiagnosticContext.Create(
                ("accessPolicy", _access.ToString()),
                ("stateKey", _stateName),
                ("valueType", StateDiagnosticContext.TypeName(typeof(T))))
        });
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }

    private void ThrowIfWriteDenied()
    {
        if (_access == StateAccessPolicy.ReadOnly)
        {
            WriteWriteDeniedDiagnostic();
            throw new StateAccessDeniedException(_stateName);
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
        private readonly WritableState<T> _state;
        private readonly StateSubscription _subscription;
        private int _disposed;

        public RemovingStateSubscription(
            WritableState<T> state,
            StateSubscription subscription)
        {
            _state = state;
            _subscription = subscription;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _subscription.Dispose();
            _state.Remove(_subscription);
        }
    }
}
