namespace AtomUI.City.EventBus;

public sealed class EventBusRuntimeOptions
{
    public const int DefaultMaximumChannelRuntimes = 256;
    public const int MaximumAllowedChannelRuntimes = 65_536;

    public static EventBusRuntimeOptions Default { get; } = new();

    public int MaximumChannelRuntimes { get; init; } = DefaultMaximumChannelRuntimes;

    internal void Validate()
    {
        if (MaximumChannelRuntimes is <= 0 or > MaximumAllowedChannelRuntimes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumChannelRuntimes),
                MaximumChannelRuntimes,
                $"EventBus maximum channel runtimes must be between 1 and {MaximumAllowedChannelRuntimes}.");
        }
    }
}
