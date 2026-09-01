using System.Diagnostics.CodeAnalysis;

namespace AtomUI.City.Core.Modularity;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class GeneratedModuleManifestAttribute : Attribute
{
    public GeneratedModuleManifestAttribute(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        Type registrarType)
    {
        ArgumentNullException.ThrowIfNull(registrarType);

        RegistrarType = registrarType;
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type RegistrarType { get; }
}
