namespace AtomUI.City.EventBus;

public sealed record EventPostResult(
    Guid EventId,
    EventContractId ContractId,
    bool Accepted,
    string? RejectionReason = null)
{
    private Guid _eventId = ValidateEventId(EventId);

    public Guid EventId
    {
        get => _eventId;
        init => _eventId = ValidateEventId(value);
    }

    public EventContractId ContractId { get; init; } = ValidateContractId(ContractId);

    public string? RejectionReason { get; init; } = ValidateRejectionReason(Accepted, RejectionReason);

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

    private static string? ValidateRejectionReason(
        bool accepted,
        string? rejectionReason)
    {
        if (accepted && !string.IsNullOrWhiteSpace(rejectionReason))
        {
            throw new ArgumentException("Accepted event post result cannot include a rejection reason.", nameof(RejectionReason));
        }

        if (!accepted && string.IsNullOrWhiteSpace(rejectionReason))
        {
            throw new ArgumentException("Rejected event post result must include a rejection reason.", nameof(RejectionReason));
        }

        return rejectionReason;
    }
}
