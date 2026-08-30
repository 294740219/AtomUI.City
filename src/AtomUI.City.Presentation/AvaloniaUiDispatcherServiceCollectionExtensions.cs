using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Core.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AtomUI.City.Presentation;

public static class AvaloniaUiDispatcherServiceCollectionExtensions
{
    public static IServiceCollection AddAvaloniaUiDispatcher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IUiDispatcher>(
            serviceProvider => new AvaloniaUiDispatcher(
                Avalonia.Threading.Dispatcher.UIThread,
                serviceProvider.GetService<IPresentationRuntime>(),
                serviceProvider.GetService<IHostDiagnostics>()));

        return services;
    }
}
