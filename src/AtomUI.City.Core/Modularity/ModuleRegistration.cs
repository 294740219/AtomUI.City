namespace AtomUI.City.Core.Modularity;

internal sealed class ModuleRegistration
{
    public ModuleRegistration(Type moduleType, Func<IModule> factory)
        : this(descriptor: null, moduleType, factory)
    {
    }

    public ModuleRegistration(ModuleDescriptor descriptor, Func<IModule> factory)
        : this(
            descriptor ?? throw new ArgumentNullException(nameof(descriptor)),
            descriptor.ModuleType,
            factory)
    {
    }

    private ModuleRegistration(
        ModuleDescriptor? descriptor,
        Type moduleType,
        Func<IModule> factory)
    {
        ArgumentNullException.ThrowIfNull(moduleType);
        ArgumentNullException.ThrowIfNull(factory);

        Descriptor = descriptor;
        ModuleType = moduleType;
        Factory = factory;
    }

    public ModuleDescriptor? Descriptor { get; }

    public Type ModuleType { get; }

    public Func<IModule> Factory { get; }
}
