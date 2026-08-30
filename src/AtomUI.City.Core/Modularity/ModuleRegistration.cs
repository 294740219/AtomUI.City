namespace AtomUI.City.Core.Modularity;

internal sealed class ModuleRegistration
{
    public ModuleRegistration(Type moduleType, Func<IModule> factory)
    {
        ArgumentNullException.ThrowIfNull(moduleType);
        ArgumentNullException.ThrowIfNull(factory);

        ModuleType = moduleType;
        Factory = factory;
    }

    public Type ModuleType { get; }

    public Func<IModule> Factory { get; }
}
