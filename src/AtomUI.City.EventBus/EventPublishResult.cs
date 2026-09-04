namespace AtomUI.City.EventBus;

public sealed class EventPublishResult
{
    public EventPublishResult(
        Guid eventId,
        EventContractId contractId,
        IReadOnlyList<EventDeliveryResult> deliveries)
        : this(eventId, contractId, deliveries, TimeSpan.Zero)
    {
    }

    public EventPublishResult(
        Guid eventId,
        EventContractId contractId,
        IReadOnlyList<EventDeliveryResult> deliveries,
        TimeSpan duration)
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

        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Event publication duration cannot be negative.");
        }

        EventId = eventId;
        ContractId = contractId;
        Deliveries = Array.AsReadOnly(deliveries.ToArray());
        Duration = duration;
    }

    public Guid EventId { get; }

    public EventContractId ContractId { get; }

    public IReadOnlyList<EventDeliveryResult> Deliveries { get; }

    public TimeSpan Duration { get; }

    public int SubscriptionCount => Deliveries.Count;

    public int DeliveredCount => Deliveries.Count(delivery => !delivery.Skipped);

    public int FailedCount => Deliveries.Count(delivery =>
        !delivery.Succeeded && !delivery.Canceled && !delivery.Skipped && !delivery.TimedOut);

    public int CanceledCount => Deliveries.Count(delivery =>
        delivery.Canceled && !delivery.Skipped && !delivery.TimedOut);

    public int TimedOutCount => Deliveries.Count(delivery => delivery.TimedOut);

    public int SkippedCount => Deliveries.Count(delivery => delivery.Skipped);

    public bool Succeeded => FailedCount == 0 && CanceledCount == 0 && TimedOutCount == 0 && SkippedCount == 0;
}

public sealed record EventDeliveryResult(
    EventSubscriptionId SubscriptionId,
    EventDispatchPolicy DispatchPolicy,
    bool Succeeded,
    string? ErrorMessage = null,
    bool Canceled = false)
{
    private TimeSpan _duration;

    public TimeSpan Duration
    {
        get => _duration;
        init
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(Duration), value, "Event delivery duration cannot be negative.");
            }

            _duration = value;
        }
    }

    private bool _timedOut;

    public bool TimedOut
    {
        get => _timedOut;
        init
        {
            if (value && Succeeded)
            {
                throw new ArgumentException("Successful event delivery result cannot be timed out.", nameof(TimedOut));
            }

            if (value && Skipped)
            {
                throw new ArgumentException("Event delivery result cannot be both timed out and skipped.", nameof(TimedOut));
            }

            _timedOut = value;
        }
    }

    private bool _skipped;

    public bool Skipped
    {
        get => _skipped;
        init
        {
            if (value && Succeeded)
            {
                throw new ArgumentException("Successful event delivery result cannot be skipped.", nameof(Skipped));
            }

            if (value && TimedOut)
            {
                throw new ArgumentException("Event delivery result cannot be both skipped and timed out.", nameof(Skipped));
            }

            _skipped = value;
        }
    }

    public EventDeliveryStatus Status => Skipped
        ? EventDeliveryStatus.Skipped
        : TimedOut
            ? EventDeliveryStatus.TimedOut
            : Succeeded
                ? EventDeliveryStatus.Succeeded
                : Canceled
                    ? EventDeliveryStatus.Canceled
                    : EventDeliveryStatus.Failed;

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

    private bool _succeeded = ValidateSucceeded(Succeeded, Canceled, ErrorMessage, timedOut: false, skipped: false);

    public bool Succeeded
    {
        get => _succeeded;
        init => _succeeded = ValidateSucceeded(value, Canceled, ErrorMessage, TimedOut, Skipped);
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
        string? errorMessage,
        bool timedOut,
        bool skipped)
    {
        if (succeeded && canceled)
        {
            throw new ArgumentException("Event delivery result cannot be both succeeded and canceled.", nameof(Succeeded));
        }

        if (succeeded && errorMessage is not null)
        {
            throw new ArgumentException("Successful event delivery result cannot include an error message.", nameof(ErrorMessage));
        }

        if (succeeded && timedOut)
        {
            throw new ArgumentException("Successful event delivery result cannot be timed out.", nameof(Succeeded));
        }

        if (succeeded && skipped)
        {
            throw new ArgumentException("Successful event delivery result cannot be skipped.", nameof(Succeeded));
        }

        return succeeded;
    }
}
