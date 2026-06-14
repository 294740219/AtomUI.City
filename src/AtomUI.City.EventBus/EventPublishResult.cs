namespace AtomUI.City.EventBus;

public sealed class EventPublishResult
{
    public EventPublishResult(
        Guid eventId,
        EventContractId contractId,
        IReadOnlyList<EventDeliveryResult> deliveries)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Event publish result id cannot be empty.", nameof(eventId));
        }

        EventContractId.ThrowIfDefault(contractId, nameof(contractId));
        ArgumentNullException.ThrowIfNull(deliveries);
        if (deliveries.Any(delivery => delivery is null))
        {
            throw new ArgumentException("Event publish result deliveries cannot contain null entries.", nameof(deliveries));
        }

        EventId = eventId;
        ContractId = contractId;
        Deliveries = Array.AsReadOnly(deliveries.ToArray());
    }

    public Guid EventId { get; }

    public EventContractId ContractId { get; }

    public IReadOnlyList<EventDeliveryResult> Deliveries { get; }

    public int DeliveredCount => Deliveries.Count;

    public int FailedCount => Deliveries.Count(delivery => !delivery.Succeeded && !delivery.Canceled);

    public int CanceledCount => Deliveries.Count(delivery => delivery.Canceled);

    public bool Succeeded => FailedCount == 0 && CanceledCount == 0;
}

public sealed record EventDeliveryResult(
    EventSubscriptionId SubscriptionId,
    EventDispatchPolicy DispatchPolicy,
    bool Succeeded,
    string? ErrorMessage = null,
    bool Canceled = false)
{
    public EventSubscriptionId SubscriptionId { get; init; } = ValidateSubscriptionId(SubscriptionId);

    public EventDispatchPolicy DispatchPolicy { get; init; } = ValidateDispatchPolicy(DispatchPolicy);

    private static EventSubscriptionId ValidateSubscriptionId(EventSubscriptionId subscriptionId)
    {
        EventSubscriptionId.ThrowIfDefault(subscriptionId, nameof(SubscriptionId));

        return subscriptionId;
    }

    private static EventDispatchPolicy ValidateDispatchPolicy(EventDispatchPolicy dispatchPolicy)
    {
        if (!Enum.IsDefined(dispatchPolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(DispatchPolicy),
                dispatchPolicy,
                "Event delivery dispatch policy is not supported.");
        }

        return dispatchPolicy;
    }
}
