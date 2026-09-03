using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.DependencyInjection;

/// <summary>
/// Represents scoped service attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ScopedServiceAttribute : Attribute
{
    private readonly Type[] _serviceTypes;

    /// <summary>
    /// Initializes a new instance of the scoped service attribute class.
    /// </summary>
    public ScopedServiceAttribute(params Type[] serviceTypes)
    {
        ArgumentNullException.ThrowIfNull(serviceTypes);

        if (serviceTypes.Any(static serviceType => serviceType is null))
        {
            throw new ArgumentException(
                "Scoped service types cannot contain null.",
                nameof(serviceTypes));
        }

        _serviceTypes = serviceTypes.ToArray();
    }

    /// <summary>
    /// Gets the lifetime value.
    /// </summary>
    public ServiceLifetime Lifetime => ServiceLifetime.Scoped;

    /// <summary>
    /// Gets the service types value.
    /// </summary>
    public Type[] ServiceTypes => _serviceTypes.ToArray();

    /// <summary>
    /// Gets or sets the replace value.
    /// </summary>
    public bool Replace { get; set; }

    /// <summary>
    /// Gets or sets the try add value.
    /// </summary>
    public bool TryAdd { get; set; }

    /// <summary>
    /// Gets or sets the key value.
    /// </summary>
    public string? Key { get; set; }
}
