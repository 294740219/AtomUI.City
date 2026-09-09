namespace AtomUI.City.Data;

[Flags]
public enum DataCapability
{
    None = 0,
    UseDataClient = 1 << 0,
    UseHttpClient = 1 << 1,
    UseGrpcClient = 1 << 2,
    UseSignalRHub = 1 << 3,
    UseRealtimeConnection = 1 << 4,
    UseStreaming = 1 << 5,
}

public enum DataRequestOriginKind
{
    Host,
    Plugin,
}

public sealed class DataRequestOrigin
{
    private DataRequestOrigin(
        DataRequestOriginKind kind,
        string? pluginId,
        string? contributionId,
        DataCapability capabilities,
        object? token)
    {
        Kind = kind;
        PluginId = pluginId;
        ContributionId = contributionId;
        Capabilities = capabilities;
        Token = token;
    }

    public static DataRequestOrigin Host { get; } = new(
        DataRequestOriginKind.Host,
        pluginId: null,
        contributionId: null,
        DataCapabilityRules.All,
        token: null);

    public DataRequestOriginKind Kind { get; }

    public string? PluginId { get; }

    public string? ContributionId { get; }

    public DataCapability Capabilities { get; }

    internal object? Token { get; }

    internal static DataRequestOrigin Plugin(
        string pluginId,
        string contributionId,
        DataCapability capabilities,
        object token) =>
        new(DataRequestOriginKind.Plugin, pluginId, contributionId, capabilities, token);
}

public interface IDataCapabilityAuthorizer
{
    bool IsAuthorized(DataRequestOrigin origin, DataCapability capability);
}

public sealed class DefaultDataCapabilityAuthorizer : IDataCapabilityAuthorizer
{
    public bool IsAuthorized(DataRequestOrigin origin, DataCapability capability)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if ((capability & ~DataCapabilityRules.All) != 0)
        {
            return false;
        }

        return origin.Kind == DataRequestOriginKind.Host
            && (origin.Capabilities & capability) == capability;
    }
}

internal static class DataCapabilityRules
{
    public const DataCapability All = DataCapability.UseDataClient
        | DataCapability.UseHttpClient
        | DataCapability.UseGrpcClient
        | DataCapability.UseSignalRHub
        | DataCapability.UseRealtimeConnection
        | DataCapability.UseStreaming;

    public static DataCapability RequiredFor(DataTransportKind transportKind) =>
        DataCapability.UseDataClient | transportKind switch
        {
            DataTransportKind.Http => DataCapability.UseHttpClient,
            DataTransportKind.Grpc => DataCapability.UseGrpcClient,
            DataTransportKind.SignalR => DataCapability.UseSignalRHub,
            _ => DataCapability.None,
        };
}
