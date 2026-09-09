using System.Collections.Concurrent;
using AtomUI.City.Core.Lifecycle;

namespace AtomUI.City.Data;

public class DataRequest<TResponse>
{
    private DataAuthenticationOptions _authentication = DataAuthenticationOptions.Anonymous;
    private DataCacheOptions _cache = DataCacheOptions.Disabled;
    private DataResilienceOptions _resilience = DataResilienceOptions.None;
    private DataConcurrencyOptions _concurrency = DataConcurrencyOptions.AllowConcurrent;
    private DataConsistencyOptions _consistency = DataConsistencyOptions.None;
    private DataRequestOrigin _origin = DataRequestOrigin.Host;
    private string? _idempotencyKey;

    public DataRequest(
        string clientId,
        string operationName,
        DataTransportKind transportKind,
        DataAccessMode accessMode = DataAccessMode.Query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        if (!Enum.IsDefined(transportKind))
        {
            throw new ArgumentOutOfRangeException(nameof(transportKind), transportKind, "Data transport kind is not supported.");
        }

        if (!Enum.IsDefined(accessMode))
        {
            throw new ArgumentOutOfRangeException(nameof(accessMode), accessMode, "Data access mode is not supported.");
        }

        ClientId = clientId;
        OperationName = operationName;
        TransportKind = transportKind;
        AccessMode = accessMode;
    }

    public string ClientId { get; }

    public string OperationName { get; }

    public DataTransportKind TransportKind { get; }

    public DataAccessMode AccessMode { get; }

    public DataAuthenticationOptions Authentication
    {
        get => _authentication;
        init => _authentication = value ?? throw new ArgumentNullException(nameof(Authentication));
    }

    public DataCacheOptions Cache
    {
        get => _cache;
        init => _cache = value ?? throw new ArgumentNullException(nameof(Cache));
    }

    public DataResilienceOptions Resilience
    {
        get => _resilience;
        init => _resilience = value ?? throw new ArgumentNullException(nameof(Resilience));
    }

    public DataConcurrencyOptions Concurrency
    {
        get => _concurrency;
        init => _concurrency = value ?? throw new ArgumentNullException(nameof(Concurrency));
    }

    public DataConsistencyOptions Consistency
    {
        get => _consistency;
        init => _consistency = value ?? throw new ArgumentNullException(nameof(Consistency));
    }

    public DataRequestOrigin Origin
    {
        get => _origin;
        init => _origin = value ?? throw new ArgumentNullException(nameof(Origin));
    }

    public LifecycleScope? ParentScope { get; init; }

    public string? IdempotencyKey
    {
        get => _idempotencyKey;
        init
        {
            if (value is not null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(IdempotencyKey));
            }

            _idempotencyKey = value;
        }
    }

    public IDictionary<string, object?> Items { get; } =
        new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);
}
