namespace AtomUI.City.Core.DependencyInjection;

/// <summary>
/// Defines the contract for iservice registrar.
/// </summary>
public interface IServiceRegistrar
{
    /// <summary>
    /// Executes the register operation.
    /// </summary>
    void Register(IServiceRegistrarContext context);
}
