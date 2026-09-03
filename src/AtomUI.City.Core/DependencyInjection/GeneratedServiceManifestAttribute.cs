using System.Diagnostics.CodeAnalysis;

namespace AtomUI.City.Core.DependencyInjection;

/// <summary>
/// Represents generated service manifest attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class GeneratedServiceManifestAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the generated service manifest attribute class.
    /// </summary>
    public GeneratedServiceManifestAttribute(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        Type registrarType)
    {
        ArgumentNullException.ThrowIfNull(registrarType);
        RegistrarType = registrarType;
    }

    /// <summary>
    /// Gets the registrar type value.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type RegistrarType { get; }
}
