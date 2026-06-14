namespace AtomUI.City.EventBus;

public sealed record EventPostResult(
    Guid EventId,
    EventContractId ContractId,
    bool Accepted,
    string? RejectionReason = null)
{
    public Guid EventId { get; init; } = ValidateEventId(EventId);

    public EventContractId ContractId { get; init; } = ValidateContractId(ContractId);

    private static Guid ValidateEventId(Guid eventId)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event post result id cannot be empty.", nameof(EventId));
        }

        return eventId;
    }

    private static EventContractId ValidateContractId(EventContractId contractId)
    {
        EventContractId.ThrowIfDefault(contractId, nameof(ContractId));

        return contractId;
    }
}
