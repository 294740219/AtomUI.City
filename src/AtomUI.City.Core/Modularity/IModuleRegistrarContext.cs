namespace AtomUI.City.Core.Modularity;

public interface IModuleRegistrarContext
{
    void Register(ModuleDescriptor descriptor, Func<IModule> factory);

    void AddApplicationRoot(Type moduleType);
}
