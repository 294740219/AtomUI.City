namespace AtomUI.City.Data;

public sealed record DataCacheKey(
    string ClientId,
    string OperationName,
    DataTransportKind TransportKind,
    DataAccessMode AccessMode,
    string RequestFingerprint,
    string AuthenticationScheme,
    string PrincipalRevision,
    string PermissionRevision,
    string? PluginContributionId,
    string ClientVersion,
    string PolicyVersion)
{
    public string ClientId { get; init; } = Require(ClientId, nameof(ClientId));

    public string OperationName { get; init; } = Require(OperationName, nameof(OperationName));

    public DataTransportKind TransportKind { get; init; } = Validate(TransportKind, nameof(TransportKind));

    public DataAccessMode AccessMode { get; init; } = Validate(AccessMode, nameof(AccessMode));

    public string RequestFingerprint { get; init; } = Require(RequestFingerprint, nameof(RequestFingerprint));

    public string AuthenticationScheme { get; init; } = Require(AuthenticationScheme, nameof(AuthenticationScheme));

    public string PrincipalRevision { get; init; } = Require(PrincipalRevision, nameof(PrincipalRevision));

    public string PermissionRevision { get; init; } = Require(PermissionRevision, nameof(PermissionRevision));

    public string? PluginContributionId { get; init; } =
        RequireOptional(PluginContributionId, nameof(PluginContributionId));

    public string ClientVersion { get; init; } = Require(ClientVersion, nameof(ClientVersion));

    public string PolicyVersion { get; init; } = Require(PolicyVersion, nameof(PolicyVersion));

    public static DataCacheKey Create<TResponse>(
        DataRequest<TResponse> request,
        string authenticationScheme)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(authenticationScheme);

        var pluginContributionId = request.Origin.Kind == DataRequestOriginKind.Plugin
            ? request.Origin.ContributionId
            : request.Cache.PluginContributionId;

        return new DataCacheKey(
            request.ClientId,
            request.OperationName,
            request.TransportKind,
            request.AccessMode,
            request.Cache.RequestFingerprint,
            authenticationScheme,
            request.Cache.PrincipalRevision,
            request.Cache.PermissionRevision,
            pluginContributionId,
            request.Cache.ClientVersion,
            request.Cache.PolicyVersion);
    }

    private static string Require(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

        return value;
    }

    private static string? RequireOptional(string? value, string paramName)
    {
        if (value is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        }

        return value;
    }

    private static TEnum Validate<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        return Enum.IsDefined(value)
            ? value
            : throw new ArgumentOutOfRangeException(parameterName, value, "Data cache key enum value is not supported.");
    }
}
