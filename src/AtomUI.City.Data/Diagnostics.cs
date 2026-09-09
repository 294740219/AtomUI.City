namespace AtomUI.City.Data;

public static class DataDiagnosticIds
{
    public const string RequestRetry = "AUCDATA001";
    public const string ConnectionRegistered = "AUCDATA002";
    public const string ConnectionStopped = "AUCDATA003";
    public const string RequestCompleted = "AUCDATA004";
    public const string RequestFailed = "AUCDATA005";
    public const string CacheReadFailed = "AUCDATA006";
    public const string CacheWriteFailed = "AUCDATA007";
    public const string CacheHit = "AUCDATA008";
    public const string CacheMiss = "AUCDATA009";
    public const string CacheInvalidated = "AUCDATA010";
    public const string ClientMissing = "AUCDATA011";
    public const string ConnectionStopFailed = "AUCDATA012";
    public const string ConnectionStartFailed = "AUCDATA013";
    public const string ConnectionStarted = "AUCDATA014";
    public const string ConnectionRegistrationRejected = "AUCDATA015";
    public const string ClientRegistered = "AUCDATA016";
    public const string ClientUnregistered = "AUCDATA017";
    public const string ClientUnregistrationMissing = "AUCDATA018";
    public const string RequestStaleSuppressed = "AUCDATA019";
    public const string RequestCancelled = "AUCDATA020";
    public const string CircuitOpened = "AUCDATA021";
    public const string CircuitRejected = "AUCDATA022";
    public const string RateLimitRejected = "AUCDATA023";
    public const string FallbackApplied = "AUCDATA024";
    public const string FallbackFailed = "AUCDATA025";
    public const string BackpressureDropped = "AUCDATA026";
    public const string StreamCompleted = "AUCDATA027";
    public const string StreamFailed = "AUCDATA028";
    public const string ContributionRegistered = "AUCDATA029";
    public const string ContributionRevoked = "AUCDATA030";
    public const string ContributionRejected = "AUCDATA031";
    public const string HandlerFailed = "AUCDATA032";
    public const string TransferProgressFailed = "AUCDATA033";
    public const string TransferCompleted = "AUCDATA034";
    public const string CacheInvalidationUnsupported = "AUCDATA035";
}

public sealed record DataDiagnosticRecord(
    string Code,
    string Message,
    DataDiagnosticSeverity Severity,
    Guid? OperationId = null,
    string? ClientId = null,
    string? OperationName = null,
    DataTransportKind? TransportKind = null,
    int? Attempt = null,
    DataErrorKind? ErrorKind = null)
{
    private Guid? _operationId = ValidateOperationId(OperationId);
    private string? _clientId = ValidateOptionalText(ClientId, nameof(ClientId));
    private string? _operationName = ValidateOptionalText(OperationName, nameof(OperationName));

    public string Code { get; init; } = Require(Code, nameof(Code));

    public string Message { get; init; } = Require(Message, nameof(Message));

    public DataDiagnosticSeverity Severity { get; init; } = Validate(Severity, nameof(Severity));

    public Guid? OperationId
    {
        get => _operationId;
        init => _operationId = ValidateOperationId(value);
    }

    public string? ClientId
    {
        get => _clientId;
        init => _clientId = ValidateOptionalText(value, nameof(ClientId));
    }

    public string? OperationName
    {
        get => _operationName;
        init => _operationName = ValidateOptionalText(value, nameof(OperationName));
    }

    public DataTransportKind? TransportKind { get; init; } = ValidateOptional(TransportKind, nameof(TransportKind));

    public int? Attempt { get; init; } = Attempt is null or >= 0
        ? Attempt
        : throw new ArgumentOutOfRangeException(nameof(Attempt), Attempt, "Diagnostic attempt cannot be negative.");

    public DataErrorKind? ErrorKind { get; init; } = ValidateOptional(ErrorKind, nameof(ErrorKind));

    private static string Require(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value;
    }

    private static string? ValidateOptionalText(string? value, string parameterName)
    {
        if (value is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        }

        return value;
    }

    private static Guid? ValidateOperationId(Guid? operationId)
    {
        return operationId is null || operationId != Guid.Empty
            ? operationId
            : throw new ArgumentException("Diagnostic operation id cannot be empty.", nameof(OperationId));
    }

    private static TEnum Validate<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        return Enum.IsDefined(value)
            ? value
            : throw new ArgumentOutOfRangeException(parameterName, value, "Diagnostic enum value is not supported.");
    }

    private static TEnum? ValidateOptional<TEnum>(TEnum? value, string parameterName)
        where TEnum : struct, Enum
    {
        return value is null ? null : Validate(value.Value, parameterName);
    }
}

public enum DataDiagnosticSeverity
{
    Trace,
    Info,
    Warning,
    Error,
}

public interface IDataDiagnostics
{
    IReadOnlyList<DataDiagnosticRecord> Records { get; }

    void Write(DataDiagnosticRecord record);
}

public sealed class InMemoryDataDiagnostics : IDataDiagnostics
{
    public const int DefaultCapacity = 4096;

    private readonly Queue<DataDiagnosticRecord> _records = [];
    private readonly object _syncRoot = new();
    private long _droppedCount;

    public InMemoryDataDiagnostics()
        : this(DefaultCapacity)
    {
    }

    public InMemoryDataDiagnostics(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
    }

    public int Capacity { get; }

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

    public IReadOnlyList<DataDiagnosticRecord> Records
    {
        get
        {
            lock (_syncRoot)
            {
                return Array.AsReadOnly(_records.ToArray());
            }
        }
    }

    public void Write(DataDiagnosticRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (_syncRoot)
        {
            if (_records.Count == Capacity)
            {
                _records.Dequeue();
                _droppedCount++;
            }

            _records.Enqueue(record);
        }
    }
}

internal static class DataDiagnosticWriter
{
    public static void TryWrite(IDataDiagnostics? diagnostics, DataDiagnosticRecord record)
    {
        if (diagnostics is null)
        {
            return;
        }

        try
        {
            diagnostics.Write(record);
        }
        catch (Exception)
        {
            // Diagnostics are observational and must not change the data operation outcome.
        }
    }
}
