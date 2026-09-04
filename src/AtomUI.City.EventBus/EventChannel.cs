namespace AtomUI.City.EventBus;

public readonly record struct EventChannel<TEvent>
{
    public const string DefaultName = "default";

    public static EventChannel<TEvent> Default { get; } = new(DefaultName);

    public EventChannel(string name)
    {
        Name = ValidateName(name);
    }

    public string Name { get; }

    internal static void ThrowIfDefault(EventChannel<TEvent> channel, string paramName)
    {
        if (channel.Name is null)
        {
            throw new ArgumentException("Event channel must be created before use.", paramName);
        }
    }

    private static string ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!string.Equals(name, name.Trim(), StringComparison.Ordinal) || name.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Event channel name cannot contain leading/trailing whitespace or control characters.",
                nameof(name));
        }

        return name;
    }
}
