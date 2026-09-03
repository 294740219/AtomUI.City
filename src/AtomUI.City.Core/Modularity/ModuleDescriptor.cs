namespace AtomUI.City.Core.Modularity;

/// <summary>
/// Represents module descriptor.
/// </summary>
public sealed class ModuleDescriptor
{
    /// <summary>
    /// Initializes a new instance of the module descriptor class.
    /// </summary>
    public ModuleDescriptor(
        string name,
        Type moduleType,
        string? version,
        string? description,
        IReadOnlyList<ModuleDependencyDescriptor> dependencies)
        : this(
            name,
            moduleType,
            version,
            description,
            dependencies,
            ModuleOrigin.Application,
            pluginId: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the module descriptor class.
    /// </summary>
    public ModuleDescriptor(
        string name,
        Type moduleType,
        string? version,
        string? description,
        IReadOnlyList<ModuleDependencyDescriptor> dependencies,
        ModuleOrigin origin,
        string? pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(moduleType);
        ArgumentNullException.ThrowIfNull(dependencies);

        var dependencySnapshot = dependencies.ToArray();
        if (dependencySnapshot.Any(static dependency => dependency is null))
        {
            throw new ArgumentException(
                "Module dependencies cannot contain null.",
                nameof(dependencies));
        }

        if (!Enum.IsDefined(origin))
        {
            throw new ArgumentOutOfRangeException(nameof(origin), origin, "Module origin is not supported.");
        }

        if (!typeof(IModule).IsAssignableFrom(moduleType))
        {
            throw new ArgumentException(
                $"Module type '{moduleType.FullName}' must implement {nameof(IModule)}.",
                nameof(moduleType));
        }

        if (origin == ModuleOrigin.Plugin)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        }
        else if (pluginId is not null)
        {
            throw new ArgumentException(
                "Application module descriptors must not declare a plugin id.",
                nameof(pluginId));
        }

        Name = name;
        ModuleType = moduleType;
        Version = version;
        Description = description;
        Dependencies = Array.AsReadOnly(dependencySnapshot);
        Origin = origin;
        PluginId = pluginId;
    }

    /// <summary>
    /// Gets the name value.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the module type value.
    /// </summary>
    public Type ModuleType { get; }

    /// <summary>
    /// Gets the version value.
    /// </summary>
    public string? Version { get; }

    /// <summary>
    /// Gets the description value.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the dependencies value.
    /// </summary>
    public IReadOnlyList<ModuleDependencyDescriptor> Dependencies { get; }

    /// <summary>
    /// Gets the origin value.
    /// </summary>
    public ModuleOrigin Origin { get; }

    /// <summary>
    /// Gets the plugin id value.
    /// </summary>
    public string? PluginId { get; }
}
