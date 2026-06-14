namespace AtomUI.City.State;

public sealed class StateCollectionChangedEventArgs<TKey, TItem> : StateChangedEventArgs
    where TKey : notnull
{
    public StateCollectionChangedEventArgs(StateCollectionChange<TKey, TItem> change)
        : this(CreateChangeList(change))
    {
    }

    public StateCollectionChangedEventArgs(IReadOnlyList<StateCollectionChange<TKey, TItem>> changes)
        : base(oldValue: null, newValue: null, GetCollectionVersion(changes))
    {
        var snapshotChanges = changes.ToArray();

        if (snapshotChanges.Any(change => change is null))
        {
            throw new ArgumentException("State collection changes must not contain null.", nameof(changes));
        }

        Changes = Array.AsReadOnly(snapshotChanges);
    }

    public StateCollectionChange<TKey, TItem> Change => Changes[0];

    public IReadOnlyList<StateCollectionChange<TKey, TItem>> Changes { get; }

    private static long GetCollectionVersion(IReadOnlyList<StateCollectionChange<TKey, TItem>> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        if (changes.Count == 0)
        {
            throw new ArgumentException("State collection change list cannot be empty.", nameof(changes));
        }

        var lastChange = changes[^1]
            ?? throw new ArgumentException("State collection changes must not contain null.", nameof(changes));

        return lastChange.CollectionVersion;
    }

    private static StateCollectionChange<TKey, TItem>[] CreateChangeList(
        StateCollectionChange<TKey, TItem> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        return [change];
    }
}
