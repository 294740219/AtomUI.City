namespace AtomUI.City.EventBus;

public readonly record struct EventContractId
{
    public EventContractId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value != value.Trim())
        {
            throw new ArgumentException("Event contract id cannot contain surrounding whitespace.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
