using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Hosting;
using AtomUI.City.Core.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Modularity;

/// <summary>
/// Provides the Host-owned control plane for module lifecycle transactions.
/// </summary>
internal interface IModuleLifecycleController : IAsyncDisposable
{
    IReadOnlyList<ModuleDescriptor> Modules { get; }

    void ConfigureServices(
        IApplicationContext applicationContext,
        IServiceCollection services,
        IHostDiagnostics diagnostics,
        Action<IServiceCollection>? generatedServices = null,
        CancellationToken cancellationToken = default);

    ValueTask ConfigureContributionsAsync(
        IApplicationContext applicationContext,
        IServiceProvider services,
        CancellationToken cancellationToken = default);

    ValueTask InitializeAsync(
        IApplicationContext applicationContext,
        IServiceProvider services,
        LifecycleScope applicationScope,
        CancellationToken cancellationToken = default);

    ValueTask ShutdownAsync(
        IApplicationContext applicationContext,
        IServiceProvider services,
        CancellationToken cancellationToken = default);
}
