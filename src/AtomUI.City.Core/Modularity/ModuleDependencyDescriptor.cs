namespace AtomUI.City.Core.Modularity;

public sealed class ModuleDependencyDescriptor
{
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

    public Type ModuleType { get; }

    public bool Optional { get; }
}
