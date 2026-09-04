namespace AtomUI.City.EventBus;

public sealed record EventChannelMetricsSnapshot(
    EventContractId ContractId,
    string ChannelName,
    EventChannelExecutionMode ExecutionMode,
    int Capacity,
    int PendingCount,
    int InFlightCount,
    long AcceptedCount,
    long RejectedCount,
    long DroppedCount,
    long CompletedCount,
    long FailedCount)
{
    public TimeSpan TotalQueueWaitDuration { get; init; }

    public TimeSpan MaximumQueueWaitDuration { get; init; }
}

public interface IEventChannelMonitor
{
    IReadOnlyList<EventChannelMetricsSnapshot> GetChannelSnapshots();
}
