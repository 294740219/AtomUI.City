namespace AtomUI.City.EventBus;

public static class EventDiagnosticIds
{
    public const string EventPublished = "EventBus.EventPublished";
    public const string EventAccepted = "EventBus.EventAccepted";
    public const string EventRejected = "EventBus.EventRejected";
    public const string EventDeliveryFailed = "EventBus.EventDeliveryFailed";
    public const string EventDeliveryCancelled = "EventBus.EventDeliveryCancelled";
    public const string EventSubscriptionAdded = "EventBus.EventSubscriptionAdded";
    public const string EventSubscriptionQuiescing = "EventBus.EventSubscriptionQuiescing";
    public const string EventSubscriptionDisposed = "EventBus.EventSubscriptionDisposed";
    public const string EventSubscriptionTerminationFailed = "EventBus.EventSubscriptionTerminationFailed";
}
