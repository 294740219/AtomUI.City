using AtomUI.City.Core.Modularity;

namespace AtomUI.City.EventBus;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class EventContractAttribute : Attribute
{
    private int _schemaVersion = 1;

    public EventContractAttribute(string contractId, Type ownerModuleType)
    {
        ContractId = EventAttributeValidation.ValidateName(contractId, nameof(contractId));
        OwnerModuleType = EventAttributeValidation.ValidateOwner(ownerModuleType, nameof(ownerModuleType));
    }

    public string ContractId { get; }

    public Type OwnerModuleType { get; }

    public int SchemaVersion
    {
        get => _schemaVersion;
        init => _schemaVersion = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Schema version must be greater than zero.");
    }
}

internal static class EventAttributeValidation
{
    public static string ValidateName(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (string.IsNullOrWhiteSpace(value) ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
            value.Any(char.IsControl))
        {
            throw new ArgumentException("The value must be non-empty, trimmed, and contain no control characters.", parameterName);
        }

        return value;
    }

    public static Type ValidateOwner(Type ownerModuleType, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(ownerModuleType, parameterName);
        if (!typeof(IModule).IsAssignableFrom(ownerModuleType) ||
            ownerModuleType.IsAbstract || ownerModuleType.IsInterface || ownerModuleType.ContainsGenericParameters)
        {
            throw new ArgumentException("The owner type must be a closed, concrete IModule implementation.", parameterName);
        }

        return ownerModuleType;
    }
}
