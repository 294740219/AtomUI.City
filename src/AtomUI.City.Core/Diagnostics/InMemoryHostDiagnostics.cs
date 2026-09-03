namespace AtomUI.City.Core.Diagnostics;

/// <summary>
/// Represents in memory host diagnostics.
/// </summary>
public sealed class InMemoryHostDiagnostics : IHostDiagnostics, IDisposable
{
    private readonly object _syncRoot = new();
    private readonly Queue<HostDiagnosticRecord> _records = new();
    private readonly int? _capacity;
    private long _droppedCount;
    private bool _isCompleted;

    /// <summary>
    /// Initializes a new instance of the in memory host diagnostics class.
    /// </summary>
    public InMemoryHostDiagnostics()
    {
    }

    /// <summary>
    /// Initializes a new instance of the in memory host diagnostics class.
    /// </summary>
    public InMemoryHostDiagnostics(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _capacity = capacity;
    }

    /// <summary>
    /// Gets the capacity value.
    /// </summary>
    public int? Capacity => _capacity;

    /// <summary>
    /// Gets the dropped count value.
    /// </summary>
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

    /// <summary>
    /// Gets the records value.
    /// </summary>
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

    /// <summary>
    /// Executes the write operation.
    /// </summary>
    public void Write(HostDiagnosticRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(record.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(record.Message);

        if (!Enum.IsDefined(record.Severity))
        {
            throw new ArgumentOutOfRangeException(
                nameof(record),
                record.Severity,
                "Diagnostic severity must be a defined value.");
        }

        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_isCompleted, this);

            if (_capacity is { } capacity && _records.Count == capacity)
            {
                _records.Dequeue();
                _droppedCount++;
            }

            _records.Enqueue(record);
        }
    }

    /// <summary>
    /// Executes the complete operation.
    /// </summary>
    public void Complete()
    {
        lock (_syncRoot)
        {
            _isCompleted = true;
        }
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        Complete();
    }
}
