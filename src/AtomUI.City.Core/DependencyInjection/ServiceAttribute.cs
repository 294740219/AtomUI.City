using Microsoft.Extensions.DependencyInjection;

namespace AtomUI.City.Core.DependencyInjection;

/// <summary>
/// Represents service attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ServiceAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the service attribute class.
    /// </summary>
    public ServiceAttribute(ServiceLifetime lifetime)
    {
        if (!Enum.IsDefined(lifetime))
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                lifetime,
                "Service lifetime must be a defined value.");
        }

        Lifetime = lifetime;
    }

    /// <summary>
    /// Gets the lifetime value.
    /// </summary>
    public ServiceLifetime Lifetime { get; }

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
