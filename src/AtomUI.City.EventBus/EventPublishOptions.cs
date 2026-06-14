namespace AtomUI.City.EventBus;

public sealed class EventPublishOptions
{
    private int _publishDepth;

    public static EventPublishOptions Default { get; } = new();

    public string? CorrelationId { get; init; }

    public string? CausationId { get; init; }

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
}
