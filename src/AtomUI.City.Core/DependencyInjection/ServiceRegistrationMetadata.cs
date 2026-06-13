using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.DependencyInjection;

internal sealed class ServiceRegistrationMetadata
{
    private ServiceRegistrationMetadata(
        ServiceLifetime lifetime,
        IReadOnlyList<Type> exposedServiceTypes)
    {
        Lifetime = lifetime;
        ExposedServiceTypes = Array.AsReadOnly(exposedServiceTypes.ToArray());
    }

    public ServiceLifetime Lifetime { get; }

    public IReadOnlyList<Type> ExposedServiceTypes { get; }

    public static ServiceRegistrationMetadata Read(Type implementationType)
    {
        ArgumentNullException.ThrowIfNull(implementationType);

        var lifetime = ResolveLifetime(implementationType);
        var exposedServiceTypes = ResolveExposedServiceTypes(implementationType);

        foreach (var serviceType in exposedServiceTypes)
        {
            if (!serviceType.IsAssignableFrom(implementationType))
            {
                throw new InvalidOperationException(
                    $"Service '{implementationType.FullName}' cannot expose '{serviceType.FullName}' because it is not assignable from the implementation type.");
            }
        }

        return new ServiceRegistrationMetadata(lifetime, exposedServiceTypes);
    }

    private static ServiceLifetime ResolveLifetime(Type implementationType)
    {
        var markerLifetimes = new List<ServiceLifetime>();

        if (typeof(ISingletonDependency).IsAssignableFrom(implementationType))
        {
            markerLifetimes.Add(ServiceLifetime.Singleton);
        }

        if (typeof(IScopedDependency).IsAssignableFrom(implementationType))
        {
            markerLifetimes.Add(ServiceLifetime.Scoped);
        }

        if (typeof(ITransientDependency).IsAssignableFrom(implementationType))
        {
            markerLifetimes.Add(ServiceLifetime.Transient);
        }

        if (markerLifetimes.Distinct().Count() > 1)
        {
            throw new InvalidOperationException(
                $"Service '{implementationType.FullName}' declares conflicting dependency lifetime markers.");
        }

        var serviceAttribute = implementationType
            .GetCustomAttributes(typeof(ServiceAttribute), inherit: false)
            .OfType<ServiceAttribute>()
            .SingleOrDefault();
        var scopedServiceAttribute = implementationType
            .GetCustomAttributes(typeof(ScopedServiceAttribute), inherit: false)
            .OfType<ScopedServiceAttribute>()
            .SingleOrDefault();
        var attributeLifetimes = new List<ServiceLifetime>();

        if (serviceAttribute is not null)
        {
            attributeLifetimes.Add(serviceAttribute.Lifetime);
        }

        if (scopedServiceAttribute is not null)
        {
            attributeLifetimes.Add(scopedServiceAttribute.Lifetime);
        }

        if (attributeLifetimes.Distinct().Count() > 1)
        {
            throw new InvalidOperationException(
                $"Service '{implementationType.FullName}' declares conflicting service attribute lifetimes.");
        }

        if (markerLifetimes.Count > 0 &&
            attributeLifetimes.Count > 0 &&
            markerLifetimes[0] != attributeLifetimes[0])
        {
            throw new InvalidOperationException(
                $"Service '{implementationType.FullName}' declares conflicting attribute and marker lifetimes.");
        }

        if (attributeLifetimes.Count > 0)
        {
            return attributeLifetimes[0];
        }

        return markerLifetimes.Count > 0
            ? markerLifetimes[0]
            : ServiceLifetime.Transient;
    }

    private static IReadOnlyList<Type> ResolveExposedServiceTypes(Type implementationType)
    {
        var exposedAttribute = implementationType
            .GetCustomAttributes(typeof(ExposeServicesAttribute), inherit: false)
            .OfType<ExposeServicesAttribute>()
            .SingleOrDefault();

        if (exposedAttribute is not null)
        {
            return exposedAttribute.ServiceTypes;
        }

        var scopedServiceAttribute = implementationType
            .GetCustomAttributes(typeof(ScopedServiceAttribute), inherit: false)
            .OfType<ScopedServiceAttribute>()
            .SingleOrDefault();

        if (scopedServiceAttribute is not null)
        {
            return scopedServiceAttribute.ServiceTypes;
        }

        return [implementationType];
    }
}
