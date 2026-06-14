namespace AtomUI.City.EventBus;

public sealed record EventPostResult(
    Guid EventId,
    EventContractId ContractId,
    bool Accepted,
    string? RejectionReason = null)
{
    public Guid EventId { get; init; } = ValidateEventId(EventId);

    private static Guid ValidateEventId(Guid eventId)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event post result id cannot be empty.", nameof(EventId));
        }

        return eventId;
    }
}
