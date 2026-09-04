namespace AtomUI.City.EventBus;

public static class EventDiagnosticIds
{
    public const string EventPublished = "EventBus.EventPublished";
    public const string EventAccepted = "EventBus.EventAccepted";
    public const string EventRejected = "EventBus.EventRejected";
    public const string EventContractRejected = "EventBus.EventContractRejected";
    public const string EventPayloadProjectionFailed = "EventBus.EventPayloadProjectionFailed";
    public const string EventDropped = "EventBus.EventDropped";
    public const string EventChannelBackpressure = "EventBus.EventChannelBackpressure";
    public const string EventDeliveryStarted = "EventBus.EventDeliveryStarted";
    public const string EventDeliveryCompleted = "EventBus.EventDeliveryCompleted";
    public const string EventDeliveryFailed = "EventBus.EventDeliveryFailed";
    public const string EventDeliveryCancelled = "EventBus.EventDeliveryCancelled";
    public const string EventDeliveryTimedOut = "EventBus.EventDeliveryTimedOut";
    public const string EventSubscriptionDisabled = "EventBus.EventSubscriptionDisabled";
    public const string EventSubscriptionAdded = "EventBus.EventSubscriptionAdded";
    public const string EventSubscriptionQuiescing = "EventBus.EventSubscriptionQuiescing";
    public const string EventSubscriptionDisposed = "EventBus.EventSubscriptionDisposed";
    public const string EventSubscriptionTerminationFailed = "EventBus.EventSubscriptionTerminationFailed";
    public const string PluginContributionActivated = "EventBus.PluginContributionActivated";
    public const string PluginContributionRejected = "EventBus.PluginContributionRejected";
    public const string PluginContributionQuiescing = "EventBus.PluginContributionQuiescing";
    public const string EventPluginDrainTimedOut = "EventBus.EventPluginDrainTimedOut";
    public const string PluginContributionDisposed = "EventBus.PluginContributionDisposed";
}
