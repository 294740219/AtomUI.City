using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.State;

public sealed class StateScope : IStateScope
{
    private readonly IHostDiagnostics? _diagnostics;
    private readonly List<IDisposable> _subscriptions = [];
    private readonly object _syncRoot = new();
    private bool _disposed;
    private StateScopeState _state = StateScopeState.Active;

    public StateScope(string id, IHostDiagnostics? diagnostics = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Id = id;
        _diagnostics = diagnostics;
    }

    public string Id { get; }

    public StateScopeState State
    {
        get
        {
            lock (_syncRoot)
            {
                return _state;
            }
        }
    }

    public void Add(IDisposable subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        lock (_syncRoot)
        {
            if (!_disposed)
            {
                _subscriptions.Add(subscription);
                return;
            }
        }

        DisposeSubscription(subscription);
    }

    public void Dispose()
    {
        IDisposable[] subscriptions;

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _state = StateScopeState.Disposing;
            subscriptions = _subscriptions.ToArray();
            _subscriptions.Clear();
        }

        for (var i = subscriptions.Length - 1; i >= 0; i--)
        {
            DisposeSubscription(subscriptions[i]);
        }

        lock (_syncRoot)
        {
            _state = StateScopeState.Disposed;
        }
    }

    private void WriteDisposeFailedDiagnostic(Exception exception)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            StateDiagnosticIds.StateScopeDisposeFailed,
            $"State scope '{Id}' subscription disposal failed: {exception.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = StateDiagnosticContext.Create(
                ("scopeId", Id))
        });
    }

    private void DisposeSubscription(IDisposable subscription)
    {
        try
        {
            subscription.Dispose();
        }
        catch (Exception exception)
        {
            WriteDisposeFailedDiagnostic(exception);
        }
    }
}
