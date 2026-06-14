namespace AtomUI.City.State;

public sealed record StateCollectionSnapshotEntry<TKey, TItem>
    where TKey : notnull
{
    private TKey _key = default!;
    private long _itemVersion;

    public StateCollectionSnapshotEntry(TKey Key, TItem Item, long ItemVersion)
    {
        ArgumentNullException.ThrowIfNull(Key);

        if (ItemVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ItemVersion),
                ItemVersion,
                "State collection snapshot item version must be greater than or equal to 0.");
        }

        this.Key = Key;
        this.Item = Item;
        this.ItemVersion = ItemVersion;
    }

    public TKey Key
    {
        get => _key;
        init
        {
            ArgumentNullException.ThrowIfNull(value);

            _key = value;
        }
    }

    public TItem Item { get; init; }

    public long ItemVersion
    {
        get => _itemVersion;
        init
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "State collection snapshot item version must be greater than or equal to 0.");
            }

            _itemVersion = value;
        }
    }
}
