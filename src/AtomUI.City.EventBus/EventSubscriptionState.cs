namespace AtomUI.City.EventBus;

public enum EventSubscriptionState
{
    Created = 0,
    Active = 1,
    Quiescing = 2,
    Draining = 3,
    Disposed = 4,
    Faulted = 5,
}
