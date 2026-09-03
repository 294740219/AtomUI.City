namespace AtomUI.City.Core.Modularity;

/// <summary>
/// Defines the contract for imodule registrar context.
/// </summary>
public interface IModuleRegistrarContext
{
    /// <summary>
    /// Executes the register operation.
    /// </summary>
    void Register(ModuleDescriptor descriptor, Func<IModule> factory);

    /// <summary>
    /// Executes the add application root operation.
    /// </summary>
    void AddApplicationRoot(Type moduleType);
}
