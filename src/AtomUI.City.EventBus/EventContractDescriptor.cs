using System.Reflection;
using System.Runtime.Loader;

namespace AtomUI.City.EventBus;

public sealed class EventContractDescriptor
{
    private EventContractDescriptor(
        EventContractId contractId,
        Type eventType,
        EventContractPlane plane,
        Assembly assembly,
        int schemaVersion,
        string schemaFingerprint,
        bool isGeneratedObjectGraphValidated)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(assembly);

        ContractId = contractId;
        EventType = eventType;
        Plane = plane;
        Assembly = assembly;
        SchemaVersion = schemaVersion > 0
            ? schemaVersion
            : throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "Schema version must be greater than zero.");
        SchemaFingerprint = EventAttributeValidation.ValidateName(schemaFingerprint, nameof(schemaFingerprint));
        IsGeneratedObjectGraphValidated = isGeneratedObjectGraphValidated;
    }

    public EventContractId ContractId { get; }

    public Type EventType { get; }

    public EventContractPlane Plane { get; }

    public Assembly Assembly { get; }

    public int SchemaVersion { get; }

    public string SchemaFingerprint { get; }

    internal bool IsGeneratedObjectGraphValidated { get; }

    public static EventContractDescriptor Shared<TEvent>(
        EventContractId contractId,
        Assembly sharedAssembly)
    {
        var eventType = typeof(TEvent);
        return CreateShared<TEvent>(
            contractId,
            sharedAssembly,
            1,
            eventType.FullName ?? eventType.Name,
            isGeneratedObjectGraphValidated: false);
    }

    public static EventContractDescriptor Shared<TEvent>(
        EventContractId contractId,
        Assembly sharedAssembly,
        int schemaVersion,
        string schemaFingerprint)
    {
        return CreateShared<TEvent>(
            contractId,
            sharedAssembly,
            schemaVersion,
            schemaFingerprint,
            isGeneratedObjectGraphValidated: false);
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static EventContractDescriptor GeneratedShared<TEvent>(
        EventContractId contractId,
        Assembly sharedAssembly,
        int schemaVersion,
        string schemaFingerprint)
    {
        return CreateShared<TEvent>(
            contractId,
            sharedAssembly,
            schemaVersion,
            schemaFingerprint,
            isGeneratedObjectGraphValidated: true);
    }

    private static EventContractDescriptor CreateShared<TEvent>(
        EventContractId contractId,
        Assembly sharedAssembly,
        int schemaVersion,
        string schemaFingerprint,
        bool isGeneratedObjectGraphValidated)
    {
        EventContractId.ThrowIfDefault(contractId, nameof(contractId));
        ArgumentNullException.ThrowIfNull(sharedAssembly);

        var eventType = typeof(TEvent);
        if (!ReferenceEquals(eventType.Assembly, sharedAssembly))
        {
            throw new InvalidOperationException(
                $"Shared event contract '{eventType.FullName}' must be defined by shared assembly '{sharedAssembly.GetName().Name}'.");
        }

        var loadContext = AssemblyLoadContext.GetLoadContext(sharedAssembly);
        if (!ReferenceEquals(loadContext, AssemblyLoadContext.Default))
        {
            throw new InvalidOperationException(
                $"Shared event contract '{eventType.FullName}' must be loaded by the default AssemblyLoadContext.");
        }

        return new EventContractDescriptor(
            contractId,
            eventType,
            EventContractPlane.Shared,
            sharedAssembly,
            schemaVersion,
            schemaFingerprint,
            isGeneratedObjectGraphValidated);
    }

    public static EventContractDescriptor PluginPrivate<TEvent>(EventContractId contractId)
    {
        EventContractId.ThrowIfDefault(contractId, nameof(contractId));

        var eventType = typeof(TEvent);
        var loadContext = AssemblyLoadContext.GetLoadContext(eventType.Assembly);
        if (loadContext is null ||
            ReferenceEquals(loadContext, AssemblyLoadContext.Default) ||
            !loadContext.IsCollectible)
        {
            throw new InvalidOperationException(
                $"Plugin-private event contract '{eventType.FullName}' must be loaded by a collectible non-default AssemblyLoadContext.");
        }

        return new EventContractDescriptor(
            contractId,
            eventType,
            EventContractPlane.PluginPrivate,
            eventType.Assembly,
            1,
            eventType.FullName ?? eventType.Name,
            isGeneratedObjectGraphValidated: false);
    }

    internal static EventContractDescriptor DefaultShared<TEvent>()
    {
        var eventType = typeof(TEvent);
        var contractName = eventType.FullName ?? eventType.Name;

        return Shared<TEvent>(
            new EventContractId(contractName),
            eventType.Assembly);
    }
}
