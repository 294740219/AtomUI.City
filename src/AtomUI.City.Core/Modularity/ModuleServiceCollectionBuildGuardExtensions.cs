using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Modularity;

public static class ModuleServiceCollectionBuildGuardExtensions
{
    public static ServiceProvider BuildServiceProvider(this ModuleServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        throw new InvalidOperationException(
            "Modules must not build a temporary service provider during service configuration.");
    }
}
