using AtomUI.City.Core.Hosting;

namespace AtomUI.City.Core.Modularity;

public sealed class ApplicationInitializationContext
{
    public ApplicationInitializationContext(
        IApplicationContext applicationContext,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(applicationContext);
        ArgumentNullException.ThrowIfNull(services);

        ApplicationContext = applicationContext;
        Services = services;
    }

    public IApplicationContext ApplicationContext { get; }

    public IServiceProvider Services { get; }
}
