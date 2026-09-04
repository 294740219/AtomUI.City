namespace AtomUI.City.EventBus;

public enum EventDispatchPolicy
{
    Current = 0,
    UiThread = 1,
    Background = 2,
    Serialized = 3,
}
