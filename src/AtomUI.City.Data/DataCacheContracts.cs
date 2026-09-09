namespace AtomUI.City.Data;

public sealed class DataCacheEntryOptions
{
    private TimeSpan? _timeToLive;

    public TimeSpan? TimeToLive
    {
        get => _timeToLive;
        init
        {
            if (value is { } duration && duration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(TimeToLive), value, "Cache time-to-live must be greater than zero.");
            }

            _timeToLive = value;
        }
    }

    public static DataCacheEntryOptions NoExpiration { get; } = new();
}

public enum DataCacheInvalidationReason
{
    Manual,
    Mutation,
    Subscription,
    PrincipalChanged,
    PermissionChanged,
    PluginRevoked,
    ClientVersionChanged,
    RouteLeft,
    Expired,
}

public sealed class DataCacheInvalidation
{
    private DataCacheInvalidation(
        DataCacheInvalidationReason reason,
        IReadOnlySet<DataCacheKey>? exactKeys = null,
        string? clientId = null,
        string? operationName = null,
        string? principalRevision = null,
        string? permissionRevision = null,
        string? pluginContributionId = null,
        string? clientVersion = null,
        string? policyVersion = null)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Cache invalidation reason is not supported.");
        }

        Reason = reason;
        ExactKeys = exactKeys;
        ClientId = ValidateOptional(clientId, nameof(clientId));
        OperationName = ValidateOptional(operationName, nameof(operationName));
        PrincipalRevision = ValidateOptional(principalRevision, nameof(principalRevision));
        PermissionRevision = ValidateOptional(permissionRevision, nameof(permissionRevision));
        PluginContributionId = ValidateOptional(pluginContributionId, nameof(pluginContributionId));
        ClientVersion = ValidateOptional(clientVersion, nameof(clientVersion));
        PolicyVersion = ValidateOptional(policyVersion, nameof(policyVersion));
    }

    public DataCacheInvalidationReason Reason { get; }

    public IReadOnlySet<DataCacheKey>? ExactKeys { get; }

    public string? ClientId { get; }

    public string? OperationName { get; }

    public string? PrincipalRevision { get; }

    public string? PermissionRevision { get; }

    public string? PluginContributionId { get; }

    public string? ClientVersion { get; }

    public string? PolicyVersion { get; }

    public static DataCacheInvalidation All(DataCacheInvalidationReason reason = DataCacheInvalidationReason.Manual) =>
        new(reason);

    public static DataCacheInvalidation Keys(
        IEnumerable<DataCacheKey> keys,
        DataCacheInvalidationReason reason = DataCacheInvalidationReason.Manual)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var snapshot = keys.ToHashSet();
        if (snapshot.Contains(null!))
        {
            throw new ArgumentException("Cache invalidation keys cannot contain null values.", nameof(keys));
        }

        if (snapshot.Count == 0)
        {
            throw new ArgumentException("At least one cache key is required.", nameof(keys));
        }

        return new DataCacheInvalidation(reason, snapshot);
    }

    public static DataCacheInvalidation ForClient(
        string clientId,
        DataCacheInvalidationReason reason = DataCacheInvalidationReason.Manual) =>
        new(reason, clientId: clientId);

    public static DataCacheInvalidation ForOperation(
        string clientId,
        string operationName,
        DataCacheInvalidationReason reason = DataCacheInvalidationReason.Manual) =>
        new(reason, clientId: clientId, operationName: operationName);

    public static DataCacheInvalidation ForPrincipal(
        string principalRevision,
        DataCacheInvalidationReason reason = DataCacheInvalidationReason.PrincipalChanged) =>
        new(reason, principalRevision: principalRevision);

    public static DataCacheInvalidation ForPermissionRevision(
        string permissionRevision,
        DataCacheInvalidationReason reason = DataCacheInvalidationReason.PermissionChanged) =>
        new(reason, permissionRevision: permissionRevision);

    public static DataCacheInvalidation ForPlugin(
        string pluginContributionId,
        DataCacheInvalidationReason reason = DataCacheInvalidationReason.PluginRevoked) =>
        new(reason, pluginContributionId: pluginContributionId);

    public static DataCacheInvalidation ForClientVersion(
        string clientId,
        string clientVersion,
        DataCacheInvalidationReason reason = DataCacheInvalidationReason.ClientVersionChanged) =>
        new(reason, clientId: clientId, clientVersion: clientVersion);

    public static DataCacheInvalidation ForPolicyVersion(
        string policyVersion,
        DataCacheInvalidationReason reason = DataCacheInvalidationReason.Manual) =>
        new(reason, policyVersion: policyVersion);

    public bool Matches(DataCacheKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (ExactKeys is not null && !ExactKeys.Contains(key))
        {
            return false;
        }

        return Matches(ClientId, key.ClientId)
            && Matches(OperationName, key.OperationName)
            && Matches(PrincipalRevision, key.PrincipalRevision)
            && Matches(PermissionRevision, key.PermissionRevision)
            && Matches(PluginContributionId, key.PluginContributionId)
            && Matches(ClientVersion, key.ClientVersion)
            && Matches(PolicyVersion, key.PolicyVersion);
    }

    private static bool Matches(string? expected, string? actual) =>
        expected is null || string.Equals(expected, actual, StringComparison.Ordinal);

    private static string? ValidateOptional(string? value, string parameterName)
    {
        if (value is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        }

        return value;
    }
}

public sealed record DataCacheInvalidationResult(int RemovedEntryCount)
{
    public int RemovedEntryCount { get; init; } = RemovedEntryCount >= 0
        ? RemovedEntryCount
        : throw new ArgumentOutOfRangeException(nameof(RemovedEntryCount));
}

public interface IDataCacheInvalidator
{
    ValueTask<DataCacheInvalidationResult> InvalidateAsync(
        DataCacheInvalidation invalidation,
        CancellationToken cancellationToken = default);
}

public interface IDataExpiringRequestCache : IDataRequestCache
{
    ValueTask SetAsync<TResponse>(
        DataCacheKey key,
        TResponse? value,
        DataCacheEntryOptions options,
        CancellationToken cancellationToken = default);
}

internal interface IDataCacheMutationGuard
{
    long CaptureMutationEpoch();

    ValueTask<bool> TrySetIfUnchangedAsync<TResponse>(
        DataCacheKey key,
        TResponse? value,
        DataCacheEntryOptions options,
        long expectedEpoch,
        CancellationToken cancellationToken = default);
}
