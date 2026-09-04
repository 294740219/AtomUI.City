namespace AtomUI.City.EventBus;

internal sealed class PluginPrivateEventContractRegistry : IEventContractRegistry
{
    private readonly IReadOnlyDictionary<EventContractId, EventContractDescriptor> _byId;
    private readonly IReadOnlyDictionary<Type, EventContractDescriptor> _byType;

    public PluginPrivateEventContractRegistry(IReadOnlyList<EventContractDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        if (descriptors.Any(descriptor => descriptor is null || descriptor.Plane != EventContractPlane.PluginPrivate))
            throw new ArgumentException("Private registry accepts only PluginPrivate descriptors.", nameof(descriptors));
        _byId = descriptors.ToDictionary(descriptor => descriptor.ContractId);
        _byType = descriptors.ToDictionary(descriptor => descriptor.EventType);
        Descriptors = Array.AsReadOnly(descriptors.ToArray());
    }

    public bool IsFrozen => true;
    public IReadOnlyList<EventContractDescriptor> Descriptors { get; }
    public void Register(EventContractDescriptor descriptor) => throw new InvalidOperationException("Plugin private event registry is frozen.");
    public void Freeze() { }

    public bool TryGet(EventContractId contractId, out EventContractDescriptor? descriptor)
    {
        EventContractId.ThrowIfDefault(contractId, nameof(contractId));
        return _byId.TryGetValue(contractId, out descriptor);
    }

    public bool TryGet(Type eventType, out EventContractDescriptor? descriptor)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        return _byType.TryGetValue(eventType, out descriptor);
    }

    public EventContractDescriptor GetOrCreate<TEvent>()
    {
        if (_byType.TryGetValue(typeof(TEvent), out var descriptor)) return descriptor;
        throw new InvalidOperationException($"Plugin private event registry does not contain event type '{typeof(TEvent).FullName}'.");
    }
}
