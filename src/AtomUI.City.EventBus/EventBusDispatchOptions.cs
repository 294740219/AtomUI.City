namespace AtomUI.City.EventBus;

public sealed class EventBusDispatchOptions
{
    public const int DefaultMaximumConcurrentDeliveriesPerPublication = 16;

    public static EventBusDispatchOptions Default { get; } = new();

    public int MaximumConcurrentDeliveriesPerPublication { get; init; } =
        DefaultMaximumConcurrentDeliveriesPerPublication;

    internal void Validate()
    {
        if (MaximumConcurrentDeliveriesPerPublication is <= 0 or > 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumConcurrentDeliveriesPerPublication),
                MaximumConcurrentDeliveriesPerPublication,
                "Maximum concurrent deliveries per publication must be between 1 and 1024.");
        }
    }
}
