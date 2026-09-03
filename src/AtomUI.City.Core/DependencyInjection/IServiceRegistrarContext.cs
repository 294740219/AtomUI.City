using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.DependencyInjection;

/// <summary>
/// Defines the contract for iservice registrar context.
/// </summary>
public interface IServiceRegistrarContext
{
    /// <summary>
    /// Executes a referenced registrar at most once in the current registrar graph.
    /// </summary>
    void RegisterRegistrar(Type registrarType, Func<IServiceRegistrar> registrarFactory);

    /// <summary>
    /// Registers services owned by the current registrar's local module.
    /// </summary>
    void Register(Type ownerModuleType, Action<IServiceCollection> registration);
}
