namespace AtomUI.City.Core.Modularity;

public sealed class ModuleDescriptor
{
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
        Dependencies = Array.AsReadOnly(dependencies.ToArray());
        Origin = origin;
        PluginId = pluginId;
    }

    public string Name { get; }

    public Type ModuleType { get; }

    public string? Version { get; }

    public string? Description { get; }

    public IReadOnlyList<ModuleDependencyDescriptor> Dependencies { get; }

    public ModuleOrigin Origin { get; }

    public string? PluginId { get; }
}
