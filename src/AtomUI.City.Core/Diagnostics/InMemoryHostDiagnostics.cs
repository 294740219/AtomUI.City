namespace AtomUI.City.Core.Diagnostics;

public sealed class InMemoryHostDiagnostics : IHostDiagnostics
{
    private readonly object _syncRoot = new();
    private readonly Queue<HostDiagnosticRecord> _records = new();
    private readonly int? _capacity;
    private long _droppedCount;

    public InMemoryHostDiagnostics()
    {
    }

    public InMemoryHostDiagnostics(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _capacity = capacity;
    }

    public int? Capacity => _capacity;

    public long DroppedCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _droppedCount;
            }
        }
    }

    public IReadOnlyList<HostDiagnosticRecord> Records
    {
        get
        {
            lock (_syncRoot)
            {
                return Array.AsReadOnly(_records.ToArray());
            }
        }
    }

    public void Write(HostDiagnosticRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(record.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(record.Message);

        lock (_syncRoot)
        {
            if (_capacity is { } capacity && _records.Count == capacity)
            {
                _records.Dequeue();
                _droppedCount++;
            }

            _records.Enqueue(record);
        }
    }
}
