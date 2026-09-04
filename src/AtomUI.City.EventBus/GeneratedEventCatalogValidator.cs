using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.EventBus;

internal static class GeneratedEventCatalogValidator
{
    public static void ValidateSelectedContributions(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var contractTypes = services
            .Where(descriptor => !descriptor.IsKeyedService &&
                descriptor.ServiceType == typeof(EventContractDescriptor) &&
                descriptor.ImplementationInstance is EventContractDescriptor contract &&
                contract.Plane == EventContractPlane.Shared &&
                contract.IsGeneratedObjectGraphValidated)
            .Select(descriptor => ((EventContractDescriptor)descriptor.ImplementationInstance!).EventType)
            .ToHashSet();

        foreach (var handler in services
                     .Where(descriptor => !descriptor.IsKeyedService &&
                         descriptor.ServiceType == typeof(GeneratedEventHandlerDescriptor) &&
                         descriptor.ImplementationInstance is GeneratedEventHandlerDescriptor)
                     .Select(descriptor => (GeneratedEventHandlerDescriptor)descriptor.ImplementationInstance!))
        {
            if (!contractTypes.Contains(handler.EventType))
            {
                throw new InvalidOperationException(
                    $"Generated event handler '{handler.HandlerType.FullName}' owned by " +
                    $"'{handler.OwnerModuleType.FullName}' targets event type '{handler.EventType.FullName}', " +
                    "but the selected Module contributions do not contain its generated Shared event contract.");
            }
        }
    }
}
