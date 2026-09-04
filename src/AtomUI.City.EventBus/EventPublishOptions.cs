namespace AtomUI.City.EventBus;

public sealed class EventPublishOptions
{
    private string? _correlationId;
    private string? _causationId;
    private int _publishDepth;
    private string? _partitionKey;

    public static EventPublishOptions Default { get; } = new();

    public string? CorrelationId
    {
        get => _correlationId;
        init => _correlationId = EventCorrelationIds.ValidateOptional(value, nameof(CorrelationId));
    }

    public string? CausationId
    {
        get => _causationId;
        init => _causationId = EventCorrelationIds.ValidateOptional(value, nameof(CausationId));
    }

    public int PublishDepth
    {
        get => _publishDepth;
        init
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Event publish depth cannot be negative.");
            }

            _publishDepth = value;
        }
    }

    public string? PartitionKey
    {
        get => _partitionKey;
        init => _partitionKey = EventCorrelationIds.ValidateOptional(value, nameof(PartitionKey));
    }
}
