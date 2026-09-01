namespace AtomUI.City.Core.Modularity;

internal static class ModuleDescriptorFactory
{
    public static ModuleDescriptor CreateFromAttributes(Type moduleType)
    {
        ArgumentNullException.ThrowIfNull(moduleType);

        var attribute = moduleType
            .GetCustomAttributes(typeof(ModuleAttribute), inherit: false)
            .OfType<ModuleAttribute>()
            .SingleOrDefault();
        var dependencies = moduleType
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .OfType<DependsOnAttribute>()
            .Select(dependency => new ModuleDependencyDescriptor(
                dependency.ModuleType,
                dependency.Optional))
            .ToArray();

        return new ModuleDescriptor(
            attribute?.Name ?? moduleType.FullName ?? moduleType.Name,
            moduleType,
            attribute?.Version,
            attribute?.Description,
            dependencies);
    }
}
