using AtomUI.City.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Modularity;

public sealed class ServiceConfigurationContext
{
    public ServiceConfigurationContext(
        ApplicationContext applicationContext,
        IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(applicationContext);
        ArgumentNullException.ThrowIfNull(services);

        ApplicationContext = applicationContext;
        Services = services as ModuleServiceCollection ?? new ModuleServiceCollection(services);
    }

    public ApplicationContext ApplicationContext { get; }

    public ModuleServiceCollection Services { get; }
}
