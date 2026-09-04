namespace AtomUI.City.EventBus;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class EventHandlerAttribute : Attribute
{
    private string _channelName = EventChannel<object>.DefaultName;
    private EventDispatchPolicy _dispatchPolicy = EventDispatchPolicy.Serialized;
    private EventDispatchMode _dispatchMode = EventDispatchMode.InlineIfAllowed;
    private EventErrorPolicy _errorPolicy = EventErrorPolicy.ContinueAndReport;
    private int _handlerTimeoutMilliseconds = 30_000;
    private int _disableSubscriptionAfterFailures = 3;

    public EventHandlerAttribute(Type ownerModuleType)
    {
        OwnerModuleType = EventAttributeValidation.ValidateOwner(ownerModuleType, nameof(ownerModuleType));
    }

    public Type OwnerModuleType { get; }

    public string ChannelName
    {
        get => _channelName;
        init => _channelName = EventAttributeValidation.ValidateName(value, nameof(value));
    }

    public EventDispatchPolicy DispatchPolicy
    {
        get => _dispatchPolicy;
        init => _dispatchPolicy = Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    public EventDispatchMode DispatchMode
    {
        get => _dispatchMode;
        init => _dispatchMode = Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    public EventErrorPolicy ErrorPolicy
    {
        get => _errorPolicy;
        init => _errorPolicy = Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    public int HandlerTimeoutMilliseconds
    {
        get => _handlerTimeoutMilliseconds;
        init => _handlerTimeoutMilliseconds = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    public int DisableSubscriptionAfterFailures
    {
        get => _disableSubscriptionAfterFailures;
        init => _disableSubscriptionAfterFailures = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
}
