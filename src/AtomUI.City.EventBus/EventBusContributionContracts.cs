namespace AtomUI.City.EventBus;

public enum EventPluginPlane
{
    Shared = 0,
    Private = 1,
}

[Flags]
public enum EventPluginAccess
{
    None = 0,
    Publish = 1,
    Subscribe = 2,
}

public enum EventBusContributionState
{
    Activating = 0,
    Active = 1,
    Quiescing = 2,
    Draining = 3,
    Disposed = 4,
    Faulted = 5,
}

public sealed record EventPluginAccessRule
{
    public EventPluginAccessRule(EventContractId contractId, string channelName, EventPluginAccess access,
        int minimumSchemaVersion = 1, int maximumSchemaVersion = int.MaxValue)
    {
        EventContractId.ThrowIfDefault(contractId, nameof(contractId));
        ContractId = contractId;
        ChannelName = EventAttributeValidation.ValidateName(channelName, nameof(channelName));
        if (access == EventPluginAccess.None || (access & ~(EventPluginAccess.Publish | EventPluginAccess.Subscribe)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(access), access, "Plugin event access must declare Publish, Subscribe, or both.");
        }
        Access = access;
        if (minimumSchemaVersion <= 0 || maximumSchemaVersion < minimumSchemaVersion)
            throw new ArgumentOutOfRangeException(nameof(minimumSchemaVersion), "Plugin schema version range is invalid.");
        MinimumSchemaVersion = minimumSchemaVersion;
        MaximumSchemaVersion = maximumSchemaVersion;
    }

    public EventContractId ContractId { get; }
    public string ChannelName { get; }
    public EventPluginAccess Access { get; }
    public int MinimumSchemaVersion { get; }
    public int MaximumSchemaVersion { get; }
}

public sealed class EventPluginQuotas
{
    private static readonly TimeSpan MaximumDrainTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

    public static EventPluginQuotas Default { get; } = new();

    public int MaximumSharedAccessRules { get; init; } = 128;
    public int MaximumPrivateContracts { get; init; } = 128;
    public int MaximumSubscriptions { get; init; } = 256;
    public int MaximumPrivateChannelRuntimes { get; init; } = 64;
    public TimeSpan DrainTimeout { get; init; } = TimeSpan.FromSeconds(30);

    internal void Validate()
    {
        ValidatePositive(MaximumSharedAccessRules, nameof(MaximumSharedAccessRules));
        ValidatePositive(MaximumPrivateContracts, nameof(MaximumPrivateContracts));
        ValidatePositive(MaximumSubscriptions, nameof(MaximumSubscriptions));
        if (MaximumPrivateChannelRuntimes is <= 0 or > 65536)
            throw new ArgumentOutOfRangeException(nameof(MaximumPrivateChannelRuntimes));
        if (DrainTimeout <= TimeSpan.Zero || DrainTimeout > MaximumDrainTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DrainTimeout),
                DrainTimeout,
                $"Plugin EventBus drain timeout must be greater than zero and no greater than {MaximumDrainTimeout}.");
        }
    }

    private static void ValidatePositive(int value, string name)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(name, value, "Plugin event quota must be greater than zero.");
    }
}

public sealed class EventBusContributionRequest
{
    public EventBusContributionRequest(
        string pluginId,
        IReadOnlyList<EventPluginAccessRule>? sharedAccess = null,
        IReadOnlyList<EventContractDescriptor>? privateContracts = null,
        EventPluginQuotas? quotas = null)
    {
        PluginId = EventAttributeValidation.ValidateName(pluginId, nameof(pluginId));
        SharedAccess = Array.AsReadOnly((sharedAccess ?? [])
            .Select(rule => rule ?? throw new ArgumentException("Shared access rules cannot contain null.", nameof(sharedAccess)))
            .ToArray());
        PrivateContracts = Array.AsReadOnly((privateContracts ?? [])
            .Select(descriptor => descriptor ?? throw new ArgumentException("Private contracts cannot contain null.", nameof(privateContracts)))
            .ToArray());
        Quotas = quotas ?? EventPluginQuotas.Default;
        Quotas.Validate();

        if (SharedAccess.Count > Quotas.MaximumSharedAccessRules)
            throw new ArgumentException("Shared access rule count exceeds the plugin quota.", nameof(sharedAccess));
        if (PrivateContracts.Count > Quotas.MaximumPrivateContracts)
            throw new ArgumentException("Private contract count exceeds the plugin quota.", nameof(privateContracts));
        if (SharedAccess.GroupBy(rule => (rule.ContractId, rule.ChannelName)).Any(group => group.Count() > 1))
            throw new ArgumentException("Shared access rules cannot contain duplicate contract/channel identities.", nameof(sharedAccess));
        if (PrivateContracts.Any(descriptor => descriptor.Plane != EventContractPlane.PluginPrivate))
            throw new ArgumentException("Private contract collection can contain only PluginPrivate descriptors.", nameof(privateContracts));
        if (PrivateContracts.GroupBy(descriptor => descriptor.ContractId).Any(group => group.Count() > 1) ||
            PrivateContracts.GroupBy(descriptor => descriptor.EventType).Any(group => group.Count() > 1))
            throw new ArgumentException("Private contract ids and event types must be unique.", nameof(privateContracts));
        var privateLoadContexts = PrivateContracts
            .Select(descriptor => System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(descriptor.Assembly))
            .Distinct(ReferenceEqualityComparer.Instance)
            .ToArray();
        if (privateLoadContexts.Length > 1)
            throw new ArgumentException("All private event contracts in one contribution must come from the same plugin AssemblyLoadContext.", nameof(privateContracts));
    }

    public string PluginId { get; }
    public IReadOnlyList<EventPluginAccessRule> SharedAccess { get; }
    public IReadOnlyList<EventContractDescriptor> PrivateContracts { get; }
    public EventPluginQuotas Quotas { get; }
}

public interface IEventBusContributionController
{
    ValueTask<IEventBusContributionLease> CreateAsync(
        EventBusContributionRequest request,
        CancellationToken cancellationToken = default);
}

public interface IEventBusContributionLease : IDisposable, IAsyncDisposable
{
    string PluginId { get; }
    EventBusContributionState State { get; }
    IPluginEventPublisher Publisher { get; }
    IPluginEventSubscriber Subscriber { get; }
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}

public interface IPluginEventPublisher
{
    ValueTask<EventPublishResult> PublishAsync<TEvent>(EventPluginPlane plane, TEvent eventData,
        EventPublishOptions? options = null, CancellationToken cancellationToken = default);
    ValueTask<EventPublishResult> PublishAsync<TEvent>(EventPluginPlane plane, EventChannel<TEvent> channel,
        TEvent eventData, EventPublishOptions? options = null, CancellationToken cancellationToken = default);
    ValueTask<EventPostResult> PostAsync<TEvent>(EventPluginPlane plane, TEvent eventData,
        EventPublishOptions? options = null, CancellationToken cancellationToken = default);
    ValueTask<EventPostResult> PostAsync<TEvent>(EventPluginPlane plane, EventChannel<TEvent> channel,
        TEvent eventData, EventPublishOptions? options = null, CancellationToken cancellationToken = default);
}

public interface IPluginEventSubscriber
{
    IEventSubscription Subscribe<TEvent>(EventPluginPlane plane,
        Func<EventContext<TEvent>, ValueTask> handler, EventSubscriptionOptions? options = null);
    IEventSubscription Subscribe<TEvent>(EventPluginPlane plane, EventChannel<TEvent> channel,
        Func<EventContext<TEvent>, ValueTask> handler, EventSubscriptionOptions? options = null);
}
