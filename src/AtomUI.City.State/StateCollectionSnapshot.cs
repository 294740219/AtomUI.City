namespace AtomUI.City.State;

public sealed class StateCollectionSnapshot<TKey, TItem>
    where TKey : notnull
{
    public StateCollectionSnapshot(
        long collectionVersion,
        IReadOnlyList<StateCollectionSnapshotEntry<TKey, TItem>> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (collectionVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(collectionVersion),
                collectionVersion,
                "State collection snapshot version must be greater than or equal to 0.");
        }

        var snapshotItems = items.ToArray();

        if (snapshotItems.Any(item => item is null))
        {
            throw new ArgumentException("State collection snapshot items must not contain null.", nameof(items));
        }

        CollectionVersion = collectionVersion;
        Items = Array.AsReadOnly(snapshotItems);
    }

    public long CollectionVersion { get; }

    public int ItemCount => Items.Count;

    public IReadOnlyList<StateCollectionSnapshotEntry<TKey, TItem>> Items { get; }
}
