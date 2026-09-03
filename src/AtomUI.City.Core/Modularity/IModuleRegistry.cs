namespace AtomUI.City.Core.Modularity;

/// <summary>
/// Provides a read-only view of the modules selected for the current application host.
/// </summary>
public interface IModuleRegistry
{
    /// <summary>
    /// Gets the immutable module descriptors in dependency order.
    /// </summary>
    IReadOnlyList<ModuleDescriptor> Modules { get; }
}
