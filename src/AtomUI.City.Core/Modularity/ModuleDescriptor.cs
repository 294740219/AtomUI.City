namespace AtomUI.City.Modularity;

public sealed class ModuleDescriptor
{
    public ModuleDescriptor(
        string name,
        Type moduleType,
        string? version,
        string? description,
        IReadOnlyList<ModuleDependencyDescriptor> dependencies)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(moduleType);
        ArgumentNullException.ThrowIfNull(dependencies);

        if (!typeof(IModule).IsAssignableFrom(moduleType))
        {
            throw new ArgumentException(
                $"Module type '{moduleType.FullName}' must implement {nameof(IModule)}.",
                nameof(moduleType));
        }

        Name = name;
        ModuleType = moduleType;
        Version = version;
        Description = description;
        Dependencies = Array.AsReadOnly(dependencies.ToArray());
    }

    public string Name { get; }

    public Type ModuleType { get; }

    public string? Version { get; }

    public string? Description { get; }

    public IReadOnlyList<ModuleDependencyDescriptor> Dependencies { get; }
}
