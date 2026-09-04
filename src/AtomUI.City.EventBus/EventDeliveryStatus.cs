namespace AtomUI.City.EventBus;

public enum EventDeliveryStatus
{
    Succeeded = 0,
    Failed = 1,
    Canceled = 2,
    TimedOut = 3,
    Skipped = 4,
}
