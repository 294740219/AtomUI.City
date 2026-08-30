namespace AtomUI.City.Core.Lifecycle;

public enum LifecycleScopeState
{
    Created,
    Starting,
    Running,
    CancelRequested,
    Stopping,
    Stopped,
    Faulted,
    UnloadPending,
    Disposing,
    Disposed,
}
