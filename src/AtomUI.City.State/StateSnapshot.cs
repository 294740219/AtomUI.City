namespace AtomUI.City.State;

public sealed class StateSnapshot
{
    public StateSnapshot(IReadOnlyList<StateSnapshotEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var snapshotEntries = entries.ToArray();

        if (snapshotEntries.Any(entry => entry is null))
        {
            throw new ArgumentException("State snapshot entries must not contain null.", nameof(entries));
        }

        Entries = Array.AsReadOnly(snapshotEntries);
    }

    public IReadOnlyList<StateSnapshotEntry> Entries { get; }
}
