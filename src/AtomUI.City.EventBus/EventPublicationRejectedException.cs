namespace AtomUI.City.EventBus;

public sealed class EventPublicationRejectedException : InvalidOperationException
{
    public EventPublicationRejectedException(
        Guid eventId,
        EventContractId contractId,
        string reason)
        : base(reason)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Rejected publication event id cannot be empty.", nameof(eventId));
        }

        EventContractId.ThrowIfDefault(contractId, nameof(contractId));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        EventId = eventId;
        ContractId = contractId;
    }

    public Guid EventId { get; }

    public EventContractId ContractId { get; }
}
