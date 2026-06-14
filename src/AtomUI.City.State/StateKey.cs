namespace AtomUI.City.State;

public readonly record struct StateKey<T>
{
    public StateKey(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
    }

    public string Name { get; }

    public override string ToString() => Name;

    internal static void ThrowIfDefault(StateKey<T> key, string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(key.Name))
        {
            throw new ArgumentException(
                "State key must be created with a non-empty name.",
                paramName ?? nameof(key));
        }
    }
}
