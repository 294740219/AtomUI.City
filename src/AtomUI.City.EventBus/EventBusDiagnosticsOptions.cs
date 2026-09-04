namespace AtomUI.City.EventBus;

public sealed class EventBusDiagnosticsOptions
{
    public const int DefaultMemoryBufferCapacity = 2048;
    public const int DefaultMaximumPayloadFieldCount = 16;
    public const int DefaultMaximumPayloadValueLength = 512;

    private double _traceSamplingRate = 1d;
    private int _maximumPayloadFieldCount = DefaultMaximumPayloadFieldCount;
    private int _maximumPayloadValueLength = DefaultMaximumPayloadValueLength;

    public static EventBusDiagnosticsOptions Default { get; } = new();

    public double TraceSamplingRate
    {
        get => _traceSamplingRate;
        init
        {
            if (double.IsNaN(value) || value is < 0d or > 1d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "EventBus diagnostic trace sampling rate must be between zero and one.");
            }

            _traceSamplingRate = value;
        }
    }

    public bool EnablePayloadProjection { get; init; }

    public int MaximumPayloadFieldCount
    {
        get => _maximumPayloadFieldCount;
        init
        {
            if (value is <= 0 or > 64)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "EventBus payload diagnostic field count must be between 1 and 64.");
            }

            _maximumPayloadFieldCount = value;
        }
    }

    public int MaximumPayloadValueLength
    {
        get => _maximumPayloadValueLength;
        init
        {
            if (value is <= 0 or > 4096)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "EventBus payload diagnostic value length must be between 1 and 4096.");
            }

            _maximumPayloadValueLength = value;
        }
    }
}
