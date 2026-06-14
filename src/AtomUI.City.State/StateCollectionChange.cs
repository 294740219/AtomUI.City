namespace AtomUI.City.State;

public sealed record StateCollectionChange<TKey, TItem>
    where TKey : notnull
{
    public StateCollectionChange(
        StateCollectionChangeKind Kind,
        TKey Key,
        bool HasOldItem,
        TItem? OldItem,
        bool HasNewItem,
        TItem? NewItem,
        long CollectionVersion,
        long ItemVersion)
    {
        ArgumentNullException.ThrowIfNull(Key);

        if (Kind < StateCollectionChangeKind.Added || Kind > StateCollectionChangeKind.Reset)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Kind),
                Kind,
                "State collection change kind is not supported.");
        }

        if (CollectionVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CollectionVersion),
                CollectionVersion,
                "State collection version must be greater than or equal to 0.");
        }

        if (ItemVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ItemVersion),
                ItemVersion,
                "State collection item version must be greater than or equal to 0.");
        }

        this.Kind = Kind;
        this.Key = Key;
        this.HasOldItem = HasOldItem;
        this.OldItem = OldItem;
        this.HasNewItem = HasNewItem;
        this.NewItem = NewItem;
        this.CollectionVersion = CollectionVersion;
        this.ItemVersion = ItemVersion;
    }

    public StateCollectionChangeKind Kind { get; init; }

    public TKey Key { get; init; }

    public bool HasOldItem { get; init; }

    public TItem? OldItem { get; init; }

    public bool HasNewItem { get; init; }

    public TItem? NewItem { get; init; }

    public long CollectionVersion { get; init; }

    public long ItemVersion { get; init; }
}
