namespace AtomUI.City.Core.Modularity;

/// <summary>
/// Represents module dependency descriptor.
/// </summary>
public sealed class ModuleDependencyDescriptor
{
    /// <summary>
    /// Initializes a new instance of the module dependency descriptor class.
    /// </summary>
    public ModuleDependencyDescriptor(Type moduleType, bool optional)
    {
        ArgumentNullException.ThrowIfNull(moduleType);

        if (!typeof(IModule).IsAssignableFrom(moduleType))
        {
            throw new ArgumentException(
                $"Module dependency type '{moduleType.FullName}' must implement {nameof(IModule)}.",
                nameof(moduleType));
        }

        ModuleType = moduleType;
        Optional = optional;
    }

    /// <summary>
    /// Gets the module type value.
    /// </summary>
    public Type ModuleType { get; }

    /// <summary>
    /// Gets the optional value.
    /// </summary>
    public bool Optional { get; }
}
