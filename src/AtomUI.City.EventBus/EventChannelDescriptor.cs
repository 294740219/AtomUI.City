namespace AtomUI.City.EventBus;

public sealed class EventChannelDescriptor
{
    public EventChannelDescriptor(
        Type eventType,
        string channelName,
        EventChannelOptions options)
    {
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        ChannelName = new EventChannel<object>(channelName).Name;
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Options.Validate();
    }

    public Type EventType { get; }

    public string ChannelName { get; }

    public EventChannelOptions Options { get; }

    public static EventChannelDescriptor Create<TEvent>(
        EventChannel<TEvent> channel,
        EventChannelOptions options)
    {
        EventChannel<TEvent>.ThrowIfDefault(channel, nameof(channel));
        return new EventChannelDescriptor(typeof(TEvent), channel.Name, options);
    }
}
