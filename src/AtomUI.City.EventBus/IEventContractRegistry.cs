namespace AtomUI.City.EventBus;

public interface IEventContractRegistry
{
    bool IsFrozen { get; }

    IReadOnlyList<EventContractDescriptor> Descriptors { get; }

    void Register(EventContractDescriptor descriptor);

    void Freeze();

    bool TryGet(EventContractId contractId, out EventContractDescriptor? descriptor);

    bool TryGet(Type eventType, out EventContractDescriptor? descriptor);

    EventContractDescriptor GetOrCreate<TEvent>();
}
