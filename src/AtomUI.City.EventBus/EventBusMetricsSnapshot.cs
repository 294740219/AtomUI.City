namespace AtomUI.City.EventBus;

public sealed record EventBusMetricsSnapshot
{
    public EventBusMetricsSnapshot(
        int activeSubscriptionCount,
        long publicationCount,
        long deliverySucceededCount,
        long deliveryFailedCount,
        long deliveryCanceledCount,
        long deliveryTimedOutCount,
        long deliverySkippedCount,
        TimeSpan totalHandlerDuration,
        long diagnosticWriteFailureCount)
    {
        ActiveSubscriptionCount = NonNegative(activeSubscriptionCount, nameof(activeSubscriptionCount));
        PublicationCount = NonNegative(publicationCount, nameof(publicationCount));
        DeliverySucceededCount = NonNegative(deliverySucceededCount, nameof(deliverySucceededCount));
        DeliveryFailedCount = NonNegative(deliveryFailedCount, nameof(deliveryFailedCount));
        DeliveryCanceledCount = NonNegative(deliveryCanceledCount, nameof(deliveryCanceledCount));
        DeliveryTimedOutCount = NonNegative(deliveryTimedOutCount, nameof(deliveryTimedOutCount));
        DeliverySkippedCount = NonNegative(deliverySkippedCount, nameof(deliverySkippedCount));
        TotalHandlerDuration = NonNegative(totalHandlerDuration, nameof(totalHandlerDuration));
        DiagnosticWriteFailureCount = NonNegative(diagnosticWriteFailureCount, nameof(diagnosticWriteFailureCount));
    }

    public int ActiveSubscriptionCount { get; }
    public long PublicationCount { get; }
    public long DeliverySucceededCount { get; }
    public long DeliveryFailedCount { get; }
    public long DeliveryCanceledCount { get; }
    public long DeliveryTimedOutCount { get; }
    public long DeliverySkippedCount { get; }
    public TimeSpan TotalHandlerDuration { get; }
    public long DiagnosticWriteFailureCount { get; }

    private static int NonNegative(int value, string parameterName) => value >= 0
        ? value
        : throw new ArgumentOutOfRangeException(parameterName, value, "Metric values cannot be negative.");

    private static long NonNegative(long value, string parameterName) => value >= 0
        ? value
        : throw new ArgumentOutOfRangeException(parameterName, value, "Metric values cannot be negative.");

    private static TimeSpan NonNegative(TimeSpan value, string parameterName) => value >= TimeSpan.Zero
        ? value
        : throw new ArgumentOutOfRangeException(parameterName, value, "Metric durations cannot be negative.");
}

public interface IEventBusMonitor
{
    EventBusMetricsSnapshot GetSnapshot();
}
