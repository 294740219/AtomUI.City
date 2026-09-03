namespace AtomUI.City.Core.DependencyInjection;

/// <summary>
/// Represents expose services attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ExposeServicesAttribute : Attribute
{
    private readonly Type[] _serviceTypes;

    /// <summary>
    /// Initializes a new instance of the expose services attribute class.
    /// </summary>
    public ExposeServicesAttribute(params Type[] serviceTypes)
    {
        ArgumentNullException.ThrowIfNull(serviceTypes);

        if (serviceTypes.Any(static serviceType => serviceType is null))
        {
            throw new ArgumentException(
                "Exposed service types cannot contain null.",
                nameof(serviceTypes));
        }

        _serviceTypes = serviceTypes.ToArray();
    }

    /// <summary>
    /// Gets the service types value.
    /// </summary>
    public Type[] ServiceTypes => _serviceTypes.ToArray();
}
