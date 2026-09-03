using AtomUI.City.Core.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Modularity;

/// <summary>
/// Represents service configuration context.
/// </summary>
public sealed class ServiceConfigurationContext
{
    private readonly PreConfigureActionStore _preConfigureActions;

    /// <summary>
    /// Initializes a new instance of the service configuration context class.
    /// </summary>
    public ServiceConfigurationContext(
        IApplicationContext applicationContext,
        IServiceCollection services)
        : this(applicationContext, services, new PreConfigureActionStore())
    {
    }

    internal ServiceConfigurationContext(
        IApplicationContext applicationContext,
        IServiceCollection services,
        PreConfigureActionStore preConfigureActions)
    {
        ArgumentNullException.ThrowIfNull(applicationContext);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(preConfigureActions);

        ApplicationContext = applicationContext;
        Services = services as ModuleServiceCollection ?? new ModuleServiceCollection(services);
        _preConfigureActions = preConfigureActions;
    }

    /// <summary>
    /// Gets the application context value.
    /// </summary>
    public IApplicationContext ApplicationContext { get; }

    /// <summary>
    /// Gets the services value.
    /// </summary>
    public ModuleServiceCollection Services { get; }

    /// <summary>
    /// Executes the pre configure operation.
    /// </summary>
    public void PreConfigure<TOptions>(Action<TOptions> configure)
        where TOptions : class
    {
        _preConfigureActions.Add(configure);
    }

    /// <summary>
    /// Executes the execute pre configure operation.
    /// </summary>
    public void ExecutePreConfigure<TOptions>(TOptions options)
        where TOptions : class
    {
        _preConfigureActions.Apply(options);
    }
}
