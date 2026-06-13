using AtomUI.City.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.Tests;

public sealed class ServiceRegistrationAttributeTests
{
    [Fact]
    public void ServiceAttributeStoresLifetimeAndRegistrationOptions()
    {
        var attribute = new ServiceAttribute(ServiceLifetime.Singleton)
        {
            Replace = true,
            TryAdd = true,
            Key = "system-clock",
        };

        Assert.Equal(ServiceLifetime.Singleton, attribute.Lifetime);
        Assert.True(attribute.Replace);
        Assert.True(attribute.TryAdd);
        Assert.Equal("system-clock", attribute.Key);
    }

    [Fact]
    public void ScopedServiceAttributeStoresExposedServiceTypes()
    {
        var attribute = new ScopedServiceAttribute(typeof(IClock), typeof(ISystemClock));

        Assert.Equal(ServiceLifetime.Scoped, attribute.Lifetime);
        Assert.Equal([typeof(IClock), typeof(ISystemClock)], attribute.ServiceTypes);
    }

    [Fact]
    public void ExposeServicesAttributeStoresExposedServiceTypes()
    {
        var attribute = new ExposeServicesAttribute(typeof(IClock));

        Assert.Equal([typeof(IClock)], attribute.ServiceTypes);
    }

    [Fact]
    public void ServiceTypeAttributesRejectExternalArrayMutation()
    {
        Type[] scopedTypes = [typeof(IClock)];
        Type[] exposedTypes = [typeof(IClock)];
        var scoped = new ScopedServiceAttribute(scopedTypes);
        var exposed = new ExposeServicesAttribute(exposedTypes);
        var scopedServiceTypes = scoped.ServiceTypes;
        var exposedServiceTypes = exposed.ServiceTypes;

        scopedTypes[0] = typeof(ISystemClock);
        exposedTypes[0] = typeof(ISystemClock);
        scopedServiceTypes[0] = typeof(ISystemClock);
        exposedServiceTypes[0] = typeof(ISystemClock);

        Assert.Equal(typeof(IClock), scoped.ServiceTypes[0]);
        Assert.Equal(typeof(IClock), exposed.ServiceTypes[0]);
    }

    [Fact]
    public void DependencyMarkerInterfacesAreEmptyContracts()
    {
        Assert.True(typeof(ISingletonDependency).IsInterface);
        Assert.Empty(typeof(ISingletonDependency).GetInterfaces());
        Assert.True(typeof(IScopedDependency).IsInterface);
        Assert.Empty(typeof(IScopedDependency).GetInterfaces());
        Assert.True(typeof(ITransientDependency).IsInterface);
        Assert.Empty(typeof(ITransientDependency).GetInterfaces());
    }

    [Fact]
    public void ServiceRegistrationMetadataReadsLifetimeAndExposedServices()
    {
        var metadata = ServiceRegistrationMetadata.Read(typeof(SampleSingletonService));

        Assert.Equal(ServiceLifetime.Singleton, metadata.Lifetime);
        Assert.Equal([typeof(IClock)], metadata.ExposedServiceTypes);
    }

    [Fact]
    public void ConflictingLifetimeMarkersAreRejectedByMetadataReader()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ServiceRegistrationMetadata.Read(typeof(ConflictingLifetimeService)));

        Assert.Contains(nameof(ConflictingLifetimeService), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidExposedServiceTypeIsRejectedByMetadataReader()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ServiceRegistrationMetadata.Read(typeof(InvalidExposedService)));

        Assert.Contains(nameof(InvalidExposedService), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ISystemClock), exception.Message, StringComparison.Ordinal);
    }

    private interface IClock;

    private interface ISystemClock;

    [Service(ServiceLifetime.Singleton)]
    [ExposeServices(typeof(IClock))]
    private sealed class SampleSingletonService : IClock;

    private sealed class ConflictingLifetimeService : ISingletonDependency, IScopedDependency;

    [ExposeServices(typeof(ISystemClock))]
    private sealed class InvalidExposedService : IClock;
}
