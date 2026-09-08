using AtomUI.City.Core.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AtomUI.City.State;

public static class StateServiceCollectionExtensions
{
    public static IServiceCollection AddState(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IStateScopeAccessor, StateScopeAccessor>();
        services.TryAddSingleton<StateFactory>();
        services.TryAddSingleton<IStateFactory>(
            provider => provider.GetRequiredService<StateFactory>());
        services.TryAddSingleton<IStateRegistry>(
            provider => new ApplicationStateRegistry(provider.GetService<IHostDiagnostics>()));
        services.TryAddSingleton<IApplicationState>(
            provider => (IApplicationState)provider.GetRequiredService<IStateRegistry>());
        services.TryAddSingleton<IApplicationStateWriter>(
            provider => (IApplicationStateWriter)provider.GetRequiredService<IStateRegistry>());

        return services;
    }
}
