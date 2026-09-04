namespace AtomUI.City.EventBus;

public sealed record EventBusMetricsSnapshot(
    int ActiveSubscriptionCount,
    long PublicationCount,
    long DeliverySucceededCount,
    long DeliveryFailedCount,
    long DeliveryCanceledCount,
    long DeliveryTimedOutCount,
    long DeliverySkippedCount,
    TimeSpan TotalHandlerDuration,
    long DiagnosticWriteFailureCount);

public interface IEventBusMonitor
{
    EventBusMetricsSnapshot GetSnapshot();
}
