using AtomUI.City.Core.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Modularity;

public interface IModuleRegistry
{
    IReadOnlyList<ModuleDescriptor> Modules { get; }

    ValueTask ConfigureServicesAsync(
        IApplicationContext applicationContext,
        IServiceCollection services,
        CancellationToken cancellationToken = default);

    ValueTask ConfigureContributionsAsync(
        IApplicationContext applicationContext,
        IServiceProvider services,
        CancellationToken cancellationToken = default);

    ValueTask InitializeAsync(
        IApplicationContext applicationContext,
        IServiceProvider services,
        CancellationToken cancellationToken = default);

    ValueTask ShutdownAsync(
        IApplicationContext applicationContext,
        IServiceProvider services,
        CancellationToken cancellationToken = default);
}
