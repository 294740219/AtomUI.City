namespace AtomUI.City.Data;

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = false)]
public sealed class DataClientAttribute : Attribute
{
    public DataClientAttribute(string clientId, DataTransportKind transportKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        if (!Enum.IsDefined(transportKind))
        {
            throw new ArgumentOutOfRangeException(nameof(transportKind), transportKind, "Data transport kind is not supported.");
        }

        ClientId = clientId;
        TransportKind = transportKind;
    }

    public string ClientId { get; }

    public DataTransportKind TransportKind { get; }

    public string Version { get; init; } = "1";
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class DataOperationAttribute : Attribute
{
    public DataOperationAttribute(
        string operationName,
        DataAccessMode accessMode = DataAccessMode.Query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        if (!Enum.IsDefined(accessMode))
        {
            throw new ArgumentOutOfRangeException(nameof(accessMode), accessMode, "Data access mode is not supported.");
        }

        OperationName = operationName;
        AccessMode = accessMode;
    }

    public string OperationName { get; }

    public DataAccessMode AccessMode { get; }

    public DataConcurrencyPolicy ConcurrencyPolicy { get; init; }

    public int TimeoutMilliseconds { get; init; }

    public int MaxRetryAttempts { get; init; }

    public bool CacheEnabled { get; init; }

    public string AuthenticationPolicy { get; init; } = "Anonymous";
}

public sealed class DataOperationDescriptor
{
    public DataOperationDescriptor(
        string operationName,
        Type requestType,
        Type responseType,
        DataAccessMode accessMode,
        DataConcurrencyPolicy concurrencyPolicy = DataConcurrencyPolicy.AllowConcurrent,
        TimeSpan? timeout = null,
        int maxRetryAttempts = 0,
        bool cacheEnabled = false,
        string authenticationPolicy = "Anonymous")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(requestType);
        ArgumentNullException.ThrowIfNull(responseType);
        if (!Enum.IsDefined(accessMode))
        {
            throw new ArgumentOutOfRangeException(nameof(accessMode), accessMode, "Data access mode is not supported.");
        }

        if (!Enum.IsDefined(concurrencyPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(concurrencyPolicy), concurrencyPolicy, "Data concurrency policy is not supported.");
        }

        if (timeout is { } duration && duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Operation timeout must be greater than zero.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(maxRetryAttempts);
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticationPolicy);
        if (cacheEnabled && accessMode != DataAccessMode.Query)
        {
            throw new ArgumentException(
                "Data operation caching can be enabled only for query access mode.",
                nameof(cacheEnabled));
        }

        OperationName = operationName;
        RequestType = requestType;
        ResponseType = responseType;
        AccessMode = accessMode;
        ConcurrencyPolicy = concurrencyPolicy;
        Timeout = timeout;
        MaxRetryAttempts = maxRetryAttempts;
        CacheEnabled = cacheEnabled;
        AuthenticationPolicy = authenticationPolicy;
    }

    public string OperationName { get; }

    public Type RequestType { get; }

    public Type ResponseType { get; }

    public DataAccessMode AccessMode { get; }

    public DataConcurrencyPolicy ConcurrencyPolicy { get; }

    public TimeSpan? Timeout { get; }

    public int MaxRetryAttempts { get; }

    public bool CacheEnabled { get; }

    public string AuthenticationPolicy { get; }
}

public sealed class DataClientDescriptor
{
    public DataClientDescriptor(
        string clientId,
        Type clientType,
        DataTransportKind transportKind,
        string version,
        IReadOnlyList<DataOperationDescriptor> operations,
        string? pluginContributionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(clientType);
        if (!Enum.IsDefined(transportKind))
        {
            throw new ArgumentOutOfRangeException(nameof(transportKind), transportKind, "Data transport kind is not supported.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(operations);
        if (operations.Any(static operation => operation is null))
        {
            throw new ArgumentException("Data operations cannot contain null values.", nameof(operations));
        }

        if (operations.GroupBy(static operation => operation.OperationName, StringComparer.Ordinal).Any(static group => group.Count() > 1))
        {
            throw new ArgumentException("Data operation names must be unique within a client descriptor.", nameof(operations));
        }

        if (pluginContributionId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginContributionId);
        }

        ClientId = clientId;
        ClientType = clientType;
        TransportKind = transportKind;
        Version = version;
        Operations = Array.AsReadOnly(operations.ToArray());
        PluginContributionId = pluginContributionId;
    }

    public string ClientId { get; }

    public Type ClientType { get; }

    public DataTransportKind TransportKind { get; }

    public string Version { get; }

    public IReadOnlyList<DataOperationDescriptor> Operations { get; }

    public string? PluginContributionId { get; }

    public DataClientDescriptor WithPluginContribution(string contributionId) => new(
        ClientId,
        ClientType,
        TransportKind,
        Version,
        Operations,
        contributionId);
}

public interface IDataClientDescriptorRegistrar
{
    void Register(DataClientDescriptorCatalog catalog);
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class GeneratedDataClientManifestAttribute : Attribute
{
    public const int CurrentVersion = 1;

    public GeneratedDataClientManifestAttribute(Type registrarType, int version = CurrentVersion)
    {
        RegistrarType = registrarType ?? throw new ArgumentNullException(nameof(registrarType));
        if (version != CurrentVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "Generated data client manifest version is not supported.");
        }

        Version = version;
    }

    public Type RegistrarType { get; }

    public int Version { get; }
}

public sealed class DataClientDescriptorCatalog
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, Registration> _descriptors = new(StringComparer.Ordinal);
    private readonly AsyncLocal<GeneratedRegistrationTransaction?> _currentGeneratedRegistration = new();

    public IReadOnlyList<DataClientDescriptor> Snapshot
    {
        get
        {
            lock (_syncRoot)
            {
                return Array.AsReadOnly(_descriptors.Values.Select(static value => value.Descriptor).ToArray());
            }
        }
    }

    public IDisposable Register(DataClientDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var token = new object();
        lock (_syncRoot)
        {
            if (_descriptors.ContainsKey(descriptor.ClientId))
            {
                throw new InvalidOperationException($"Data client descriptor '{descriptor.ClientId}' is already registered.");
            }

            _descriptors.Add(descriptor.ClientId, new Registration(descriptor, token));
        }

        var lease = new DescriptorLease(this, descriptor.ClientId, token);
        _currentGeneratedRegistration.Value?.Track(lease);
        return lease;
    }

    public void RegisterGenerated<TRegistrar>()
        where TRegistrar : IDataClientDescriptorRegistrar, new()
    {
        var parent = _currentGeneratedRegistration.Value;
        var transaction = new GeneratedRegistrationTransaction(parent);
        _currentGeneratedRegistration.Value = transaction;
        try
        {
            new TRegistrar().Register(this);
            transaction.Commit();
        }
        catch
        {
            transaction.RollBack();
            throw;
        }
        finally
        {
            _currentGeneratedRegistration.Value = parent;
        }
    }

    public bool TryGet(string clientId, out DataClientDescriptor? descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        lock (_syncRoot)
        {
            if (_descriptors.TryGetValue(clientId, out var registration))
            {
                descriptor = registration.Descriptor;
                return true;
            }
        }

        descriptor = null;
        return false;
    }

    private void Revoke(string clientId, object token)
    {
        lock (_syncRoot)
        {
            if (_descriptors.TryGetValue(clientId, out var registration)
                && ReferenceEquals(registration.Token, token))
            {
                _descriptors.Remove(clientId);
            }
        }
    }

    private sealed record Registration(DataClientDescriptor Descriptor, object Token);

    private sealed class GeneratedRegistrationTransaction(GeneratedRegistrationTransaction? parent)
    {
        private readonly List<DescriptorLease> _leases = [];

        public void Track(DescriptorLease lease) => _leases.Add(lease);

        public void Commit()
        {
            if (parent is not null)
            {
                foreach (var lease in _leases)
                {
                    parent.Track(lease);
                }
            }

            _leases.Clear();
        }

        public void RollBack()
        {
            for (var index = _leases.Count - 1; index >= 0; index--)
            {
                _leases[index].Dispose();
            }

            _leases.Clear();
        }
    }

    private sealed class DescriptorLease(
        DataClientDescriptorCatalog catalog,
        string clientId,
        object token) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                catalog.Revoke(clientId, token);
            }
        }
    }
}
