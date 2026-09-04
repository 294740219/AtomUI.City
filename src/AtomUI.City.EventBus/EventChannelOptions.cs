namespace AtomUI.City.EventBus;

public sealed class EventChannelOptions
{
    public const int DefaultCapacity = 256;
    private static readonly TimeSpan MaximumQueueWaitTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

    public static EventChannelOptions Default { get; } = new();

    public int Capacity { get; init; } = DefaultCapacity;

    public EventChannelBackpressurePolicy BackpressurePolicy { get; init; } =
        EventChannelBackpressurePolicy.Wait;

    public EventChannelExecutionMode ExecutionMode { get; init; } =
        EventChannelExecutionMode.Serialized;

    public int MaximumConcurrency { get; init; } = 1;

    public TimeSpan? QueueWaitTimeout { get; init; }

    internal void Validate()
    {
        if (Capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Capacity),
                Capacity,
                "Event channel capacity must be greater than zero.");
        }

        if (!Enum.IsDefined(BackpressurePolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(BackpressurePolicy),
                BackpressurePolicy,
                "Event channel backpressure policy is not supported.");
        }

        if (!Enum.IsDefined(ExecutionMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(ExecutionMode),
                ExecutionMode,
                "Event channel execution mode is not supported.");
        }

        if (MaximumConcurrency <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumConcurrency),
                MaximumConcurrency,
                "Event channel maximum concurrency must be greater than zero.");
        }

        if (ExecutionMode == EventChannelExecutionMode.Serialized && MaximumConcurrency != 1)
        {
            throw new ArgumentException(
                "Serialized event channels require MaximumConcurrency to be exactly one.",
                nameof(MaximumConcurrency));
        }

        if (QueueWaitTimeout is { } timeout &&
            (timeout <= TimeSpan.Zero || timeout > MaximumQueueWaitTimeout))
        {
            throw new ArgumentOutOfRangeException(
                nameof(QueueWaitTimeout),
                timeout,
                $"Event channel queue wait timeout must be greater than zero and no greater than {MaximumQueueWaitTimeout} when specified.");
        }
    }
}
