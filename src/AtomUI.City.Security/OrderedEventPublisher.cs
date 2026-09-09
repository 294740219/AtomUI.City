using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Security;

internal sealed class OrderedEventPublisher<TEventArgs>
    where TEventArgs : EventArgs
{
    private readonly Queue<Notification> _notifications = new();
    private readonly object _syncRoot = new();
    private readonly IHostDiagnostics? _diagnostics;
    private readonly string _failureCode;
    private bool _isDraining;
    private bool _isCompleted;

    public OrderedEventPublisher(IHostDiagnostics? diagnostics, string failureCode)
    {
        _diagnostics = diagnostics;
        _failureCode = failureCode;
    }

    public bool Enqueue(EventHandler<TEventArgs>? handlers, TEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        lock (_syncRoot)
        {
            if (_isCompleted || handlers is null)
            {
                return false;
            }

            _notifications.Enqueue(new Notification(handlers, args));
            if (_isDraining)
            {
                return false;
            }

            _isDraining = true;
            return true;
        }
    }

    public void Drain(object sender)
    {
        while (true)
        {
            Notification notification;

            lock (_syncRoot)
            {
                if (_isCompleted || _notifications.Count == 0)
                {
                    _notifications.Clear();
                    _isDraining = false;
                    return;
                }

                notification = _notifications.Dequeue();
            }

            foreach (EventHandler<TEventArgs> handler in notification.Handlers.GetInvocationList())
            {
                try
                {
                    handler(sender, notification.Args);
                }
                catch (Exception exception)
                {
                    SecurityDiagnostics.Write(
                        _diagnostics,
                        _failureCode,
                        "A Security event observer failed.",
                        HostDiagnosticSeverity.Error,
                        new Dictionary<string, string?>(StringComparer.Ordinal)
                        {
                            ["eventType"] = typeof(TEventArgs).FullName,
                            ["observerType"] = handler.Method.DeclaringType?.FullName,
                            ["exceptionType"] = exception.GetType().FullName,
                        });
                }
            }
        }
    }

    public void Complete()
    {
        lock (_syncRoot)
        {
            _isCompleted = true;
            _notifications.Clear();
        }
    }

    private sealed record Notification(
        EventHandler<TEventArgs> Handlers,
        TEventArgs Args);
}
