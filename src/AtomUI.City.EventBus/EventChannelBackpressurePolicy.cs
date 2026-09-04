namespace AtomUI.City.EventBus;

public enum EventChannelBackpressurePolicy
{
    Wait = 0,
    Reject = 1,
    DropOldest = 2,
    DropNewest = 3,
    CoalesceLatest = 4
}
