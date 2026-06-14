namespace AtomUI.City.State;

public sealed record StateSnapshotEntry
{
    private string _stateName = null!;
    private Type _valueType = null!;

    public StateSnapshotEntry(
        string stateName,
        Type valueType,
        object? value,
        long version,
        int schemaVersion,
        string? ownerModule,
        string? pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        ArgumentNullException.ThrowIfNull(valueType);

        if (version < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(version),
                version,
                "State snapshot version must be greater than or equal to 0.");
        }

        if (schemaVersion < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                schemaVersion,
                "State snapshot schema version must be greater than or equal to 1.");
        }

        StateName = stateName;
        ValueType = valueType;
        Value = value;
        Version = version;
        SchemaVersion = schemaVersion;
        OwnerModule = ownerModule;
        PluginId = pluginId;
    }

    public string StateName
    {
        get => _stateName;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            _stateName = value;
        }
    }

    public Type ValueType
    {
        get => _valueType;
        init
        {
            ArgumentNullException.ThrowIfNull(value);

            _valueType = value;
        }
    }

    public object? Value { get; init; }

    public long Version { get; init; }

    public int SchemaVersion { get; init; }

    public string? OwnerModule { get; init; }

    public string? PluginId { get; init; }

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
