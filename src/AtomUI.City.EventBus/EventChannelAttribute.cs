namespace AtomUI.City.EventBus;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class EventChannelAttribute : Attribute
{
    private int _capacity = EventChannelOptions.DefaultCapacity;
    private EventChannelBackpressurePolicy _backpressurePolicy = EventChannelBackpressurePolicy.Wait;
    private EventChannelExecutionMode _executionMode = EventChannelExecutionMode.Serialized;
    private int _maximumConcurrency = 1;
    private int _queueWaitTimeoutMilliseconds;

    public EventChannelAttribute(string name)
    {
        Name = EventAttributeValidation.ValidateName(name, nameof(name));
    }

    public string Name { get; }

    public int Capacity { get => _capacity; init => _capacity = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value)); }

    public EventChannelBackpressurePolicy BackpressurePolicy
    {
        get => _backpressurePolicy;
        init => _backpressurePolicy = Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    public EventChannelExecutionMode ExecutionMode
    {
        get => _executionMode;
        init => _executionMode = Enum.IsDefined(value) ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    public int MaximumConcurrency { get => _maximumConcurrency; init => _maximumConcurrency = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value)); }

    public int QueueWaitTimeoutMilliseconds
    {
        get => _queueWaitTimeoutMilliseconds;
        init => _queueWaitTimeoutMilliseconds = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
}
