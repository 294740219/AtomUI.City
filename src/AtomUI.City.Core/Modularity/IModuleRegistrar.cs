namespace AtomUI.City.Core.Modularity;

/// <summary>
/// Defines the contract for imodule registrar.
/// </summary>
public interface IModuleRegistrar
{
    /// <summary>
    /// Executes the register operation.
    /// </summary>
    void Register(IModuleRegistrarContext context);
}
