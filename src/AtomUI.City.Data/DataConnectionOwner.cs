namespace AtomUI.City.Data;

public readonly record struct DataConnectionOwner
{
    public DataConnectionOwner(DataConnectionOwnerKind kind, string id)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Data connection owner kind is not supported.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Kind = kind;
        Id = id;
    }

    public DataConnectionOwnerKind Kind { get; }

    public string? Id { get; }

    public static DataConnectionOwner None { get; } = new();
}
