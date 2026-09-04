using AtomUI.City.Core.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AtomUI.City.EventBus;

[Module("AtomUI.City.EventBus", Version = "1.0.0", Description = "Provides the City application event bus.")]
public sealed class EventBusModule : ModuleBase
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Services.TryAddSingleton<EventBusHostManagedMarker>();
        context.Services.AddEventBus();
        context.Services.TryAddSingleton<IEventBusLifecycleController>(
            serviceProvider => serviceProvider.GetRequiredService<InMemoryEventBus>());
    }

    public override void PostConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        GeneratedEventCatalogValidator.ValidateSelectedContributions(context.Services);
    }

    public override ValueTask OnPreApplicationInitializationAsync(
        ApplicationInitializationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Services
            .GetRequiredService<IEventBusLifecycleController>()
            .StartAsync(context.ApplicationScope, cancellationToken);
    }

    public override ValueTask OnApplicationShutdownAsync(
        ApplicationShutdownContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Services
            .GetRequiredService<IEventBusLifecycleController>()
            .StopAsync(cancellationToken);
    }
}
