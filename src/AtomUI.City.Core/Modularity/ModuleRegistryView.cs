namespace AtomUI.City.Core.Modularity;

/// <summary>
/// Exposes immutable module metadata without leaking the Host lifecycle controller.
/// </summary>
internal sealed class ModuleRegistryView : IModuleRegistry
{
    internal ModuleRegistryView(IReadOnlyList<ModuleDescriptor> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        Modules = Array.AsReadOnly(modules.ToArray());
    }

    public IReadOnlyList<ModuleDescriptor> Modules { get; }
}
