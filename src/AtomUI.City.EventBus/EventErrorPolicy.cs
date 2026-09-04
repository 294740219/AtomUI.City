namespace AtomUI.City.EventBus;

public enum EventErrorPolicy
{
    ContinueAndReport = 0,
    StopPublication = 1,
    FailPublisher = 2,
    DisableSubscription = 3,
}
