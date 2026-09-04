namespace AtomUI.City.EventBus;

public sealed record EventChannelMetricsSnapshot
{
    private TimeSpan _totalQueueWaitDuration;
    private TimeSpan _maximumQueueWaitDuration;

    public EventChannelMetricsSnapshot(
        EventContractId contractId,
        string channelName,
        EventChannelExecutionMode executionMode,
        int capacity,
        int pendingCount,
        int inFlightCount,
        long acceptedCount,
        long rejectedCount,
        long droppedCount,
        long completedCount,
        long failedCount)
    {
        EventContractId.ThrowIfDefault(contractId, nameof(contractId));
        ContractId = contractId;
        ChannelName = EventAttributeValidation.ValidateName(channelName, nameof(channelName));
        ExecutionMode = Enum.IsDefined(executionMode)
            ? executionMode
            : throw new ArgumentOutOfRangeException(nameof(executionMode), executionMode, "Unknown channel execution mode.");
        Capacity = capacity > 0
            ? capacity
            : throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Channel capacity must be positive.");
        PendingCount = NonNegative(pendingCount, nameof(pendingCount));
        InFlightCount = NonNegative(inFlightCount, nameof(inFlightCount));
        AcceptedCount = NonNegative(acceptedCount, nameof(acceptedCount));
        RejectedCount = NonNegative(rejectedCount, nameof(rejectedCount));
        DroppedCount = NonNegative(droppedCount, nameof(droppedCount));
        CompletedCount = NonNegative(completedCount, nameof(completedCount));
        FailedCount = NonNegative(failedCount, nameof(failedCount));
    }

    public EventContractId ContractId { get; }
    public string ChannelName { get; }
    public EventChannelExecutionMode ExecutionMode { get; }
    public int Capacity { get; }
    public int PendingCount { get; }
    public int InFlightCount { get; }
    public long AcceptedCount { get; }
    public long RejectedCount { get; }
    public long DroppedCount { get; }
    public long CompletedCount { get; }
    public long FailedCount { get; }

    public TimeSpan TotalQueueWaitDuration
    {
        get => _totalQueueWaitDuration;
        init => _totalQueueWaitDuration = NonNegative(value, nameof(TotalQueueWaitDuration));
    }

    public TimeSpan MaximumQueueWaitDuration
    {
        get => _maximumQueueWaitDuration;
        init => _maximumQueueWaitDuration = NonNegative(value, nameof(MaximumQueueWaitDuration));
    }

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

public interface IEventChannelMonitor
{
    IReadOnlyList<EventChannelMetricsSnapshot> GetChannelSnapshots();
}
