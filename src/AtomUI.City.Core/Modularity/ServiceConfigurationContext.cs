using AtomUI.City.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Modularity;

public sealed class ServiceConfigurationContext
{
    private readonly PreConfigureActionStore _preConfigureActions;

    public ServiceConfigurationContext(
        ApplicationContext applicationContext,
        IServiceCollection services)
        : this(applicationContext, services, new PreConfigureActionStore())
    {
    }

    internal ServiceConfigurationContext(
        ApplicationContext applicationContext,
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

    public ApplicationContext ApplicationContext { get; }

    public ModuleServiceCollection Services { get; }

    public void PreConfigure<TOptions>(Action<TOptions> configure)
        where TOptions : class
    {
        _preConfigureActions.Add(configure);
    }

    public void ExecutePreConfigure<TOptions>(TOptions options)
        where TOptions : class
    {
        _preConfigureActions.Apply(options);
    }
}
