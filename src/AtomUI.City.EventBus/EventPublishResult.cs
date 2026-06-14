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
    private EventSubscriptionId _subscriptionId = ValidateSubscriptionId(SubscriptionId);

    public EventSubscriptionId SubscriptionId
    {
        get => _subscriptionId;
        init => _subscriptionId = ValidateSubscriptionId(value);
    }

    private EventDispatchPolicy _dispatchPolicy = ValidateDispatchPolicy(DispatchPolicy);

    public EventDispatchPolicy DispatchPolicy
    {
        get => _dispatchPolicy;
        init => _dispatchPolicy = ValidateDispatchPolicy(value);
    }

    private bool _succeeded = ValidateSucceeded(Succeeded, Canceled, ErrorMessage);

    public bool Succeeded
    {
        get => _succeeded;
        init => _succeeded = ValidateSucceeded(value, Canceled, ErrorMessage);
    }

    private string? _errorMessage = ValidateErrorMessage(Succeeded, ErrorMessage);

    public string? ErrorMessage
    {
        get => _errorMessage;
        init => _errorMessage = ValidateErrorMessage(Succeeded, value);
    }

    private bool _canceled = ValidateCanceled(Succeeded, Canceled);

    public bool Canceled
    {
        get => _canceled;
        init => _canceled = ValidateCanceled(Succeeded, value);
    }

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

    private static string? ValidateErrorMessage(bool succeeded, string? errorMessage)
    {
        if (succeeded && errorMessage is not null)
        {
            throw new ArgumentException("Successful event delivery result cannot include an error message.", nameof(ErrorMessage));
        }

        return errorMessage;
    }

    private static bool ValidateCanceled(bool succeeded, bool canceled)
    {
        if (succeeded && canceled)
        {
            throw new ArgumentException("Event delivery result cannot be both succeeded and canceled.", nameof(Canceled));
        }

        return canceled;
    }

    private static bool ValidateSucceeded(
        bool succeeded,
        bool canceled,
        string? errorMessage)
    {
        if (succeeded && canceled)
        {
            throw new ArgumentException("Event delivery result cannot be both succeeded and canceled.", nameof(Succeeded));
        }

        if (succeeded && errorMessage is not null)
        {
            throw new ArgumentException("Successful event delivery result cannot include an error message.", nameof(ErrorMessage));
        }

        return succeeded;
    }
}
