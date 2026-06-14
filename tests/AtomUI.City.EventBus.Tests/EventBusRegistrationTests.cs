using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.EventBus.Tests;

public sealed class EventBusRegistrationTests
{
    [Fact]
    public async Task ServiceCollectionRegistersEventBusContracts()
    {
        var services = new ServiceCollection();

        services.AddEventBus();

        await using var serviceProvider = services.BuildServiceProvider();
        var eventBus = serviceProvider.GetRequiredService<IEventBus>();
        var publisher = serviceProvider.GetRequiredService<IEventPublisher>();
        var subscriber = serviceProvider.GetRequiredService<IEventSubscriber>();
        var registry = serviceProvider.GetRequiredService<IEventContractRegistry>();

        Assert.Same(eventBus, publisher);
        Assert.Same(eventBus, subscriber);
        Assert.IsType<InMemoryEventBus>(eventBus);
        Assert.IsType<InMemoryEventContractRegistry>(registry);
    }

    [Fact]
    public async Task ServiceCollectionRegistersEventContractDescriptor()
    {
        var services = new ServiceCollection();
        var contractId = new EventContractId("atomui.city.tests.registration.v1");

        services.AddEventContract<RegisteredEvent>(contractId);

        await using var serviceProvider = services.BuildServiceProvider();
        var registry = serviceProvider.GetRequiredService<IEventContractRegistry>();

        var descriptor = registry.GetOrCreate<RegisteredEvent>();

        Assert.Equal(contractId, descriptor.ContractId);
        Assert.Equal(typeof(RegisteredEvent), descriptor.EventType);
    }

    [Fact]
    public async Task ServiceProviderDisposesEventBusSingleton()
    {
        var services = new ServiceCollection();
        services.AddEventBus();
        var serviceProvider = services.BuildServiceProvider();
        var eventBus = (InMemoryEventBus)serviceProvider.GetRequiredService<IEventBus>();

        await serviceProvider.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await eventBus.PublishAsync(new RegisteredEvent("disposed")));
    }

    [Fact]
    public void EventBusInterfaceExposesDisposeContract()
    {
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(IEventBus)));
    }

    private sealed record RegisteredEvent(string Value);
}
